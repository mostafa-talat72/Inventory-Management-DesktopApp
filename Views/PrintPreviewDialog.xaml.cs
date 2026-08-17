using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Printing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class PrintPreviewDialog : UserControl
{
    private readonly string _html;
    private readonly string _tempFilePath;
    private readonly Func<double, UIElement>? _visualFactory;
    private readonly Func<double, double, List<UIElement>>? _pageFactory;
    private Invoice?        _invoice;

    public event EventHandler<bool>? DialogClosed;

    private PrintPreviewDialog(string html, string title,
        Func<double, UIElement>? visualFactory = null, Invoice? invoice = null,
        Func<double, double, List<UIElement>>? pageFactory = null)
    {
        InitializeComponent();
        _html          = html;
        _invoice       = invoice;
        _visualFactory = visualFactory;
        _pageFactory   = pageFactory;

        TxtPreviewInfo.Text = title;

        _tempFilePath = Path.Combine(Path.GetTempPath(), $"receipt_{Guid.NewGuid():N}.html");
        File.WriteAllText(_tempFilePath, html, System.Text.Encoding.UTF8);

        if (visualFactory != null)
        {
            // المعاينة = نفس عنصر WPF الذي سيُطبع بالضبط (نفس العرض، نفس البكسلات الكيميائية)
            try
            {
                var config = AppConfig.Load();
                double w = GetQueuePaperWidth(config.PrinterName);
                var visual = visualFactory(w);
                visual.Measure(new Size(w, double.PositiveInfinity));
                visual.Arrange(new Rect(0, 0, w, visual.DesiredSize.Height));
                visual.UpdateLayout();

                PreviewScroll.Content = new Border
                {
                    Width = w,
                    Background = Brushes.White,
                    Child = visual
                };
            }
            catch { ReceiptBrowser.NavigateToString(html); }
        }
        else if (pageFactory != null)
        {
            // التقرير: نفس صفحات WPF التي ستُطبع بالضبط (مقسمة على صفحات)
            try
            {
                var config = AppConfig.Load();
                double w = GetQueuePaperWidth(config.PrinterName);
                double h = GetQueuePaperHeight(config.PrinterName);
                var pages = pageFactory(w, h);

                var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(12, 12, 12, 4) };
                foreach (var page in pages)
                {
                    page.Measure(new Size(w, double.PositiveInfinity));
                    page.Arrange(new Rect(0, 0, w, page.DesiredSize.Height));
                    page.UpdateLayout();
                    stack.Children.Add(new Border
                    {
                        Width = w,
                        Background = Brushes.White,
                        BorderBrush = new SolidColorBrush(Color.FromRgb(180, 180, 180)),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(0, 0, 0, 14),
                        Child = page
                    });
                }
                PreviewScroll.Content = stack;
            }
            catch { ReceiptBrowser.NavigateToString(html); }
        }
        else
        {
            ReceiptBrowser.NavigateToString(html);
        }
    }

    public static void Show(string html, string title,
        Func<double, UIElement>? visualFactory = null, Invoice? invoice = null)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow == null) return;

        var dialog = new PrintPreviewDialog(html, title, visualFactory, invoice);
        dialog.DialogClosed += (_, _) => mainWindow.HideOverlay();
        mainWindow.ShowOverlay(dialog);
    }

    /// <summary>Shows a print preview for non-invoice documents (e.g. inventory report).</summary>
    public static void ShowInventory(string html, string title)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow == null) return;

        var dialog = new PrintPreviewDialog(html, title);
        dialog.DialogClosed += (_, _) => mainWindow.HideOverlay();
        mainWindow.ShowOverlay(dialog);
    }

    /// <summary>
    /// معاينة وطباعة مستند متعدد الصفحات (تقارير) بنفس مسار الفواتير:
    /// عناصر WPF تُبنى لكل صفحة وتُطبع مباشرة على FixedDocument بدون WebBrowser.
    /// pageFactory يبني الصفحات عند عرض معيّن وارتفاع ورقة معيّن.
    /// </summary>
    public static void ShowDocument(string html, string title,
        Func<double, double, List<UIElement>> pageFactory)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow == null) return;

        var dialog = new PrintPreviewDialog(html, title, pageFactory: pageFactory);
        dialog.DialogClosed += (_, _) => mainWindow.HideOverlay();
        mainWindow.ShowOverlay(dialog);
    }

    private void DoPrint()
    {
        try
        {
            if (_pageFactory != null)
            {
                // التقارير: صفحات WPF مقسمة تُطبع مباشرة على FixedDocument
                PrintPages(_pageFactory, AppConfig.Load());
                NotificationManager.ShowSuccess("تمت الطباعة بنفس شكل المعاينة بالضبط");
                return;
            }

            if (_visualFactory != null)
            {
                // الفاتورة أو فاتورة المورد: نطبع نفس عنصر WPF المعروض في المعاينة
                // بعرض ورقة الطابعة الفعلي من النقطة (0,0) — بدون أي هوامش نهائياً
                PrintVisual(_visualFactory, AppConfig.Load());
                NotificationManager.ShowSuccess("تمت الطباعة بنفس شكل المعاينة بالضبط");
                return;
            }

            // المستندات HTML فقط (تقارير...): مسار IE مع هوامش معادلة على صفر
            var savedSetup = SaveIePageSetup();
            SetIePageSetupZero();
            try
            {
                var config = AppConfig.Load();
                PrintViaOle(showDialog: string.IsNullOrWhiteSpace(config.PrinterName));
            }
            finally { RestoreIePageSetup(savedSetup); }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"تعذرت الطباعة:\n{ex.Message}", "خطأ في الطباعة",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static readonly string[] IeMarginValues = ["margin_bottom", "margin_left", "margin_right", "margin_top"];

    /// <summary>يقرأ إعدادات الطباعة الحالية لمحرك IE (هوامش + رأس وتذييل) ليعيدها بعد الطباعة</summary>
    private static Dictionary<string, string?>? SaveIePageSetup()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser
                .OpenSubKey(@"Software\Microsoft\Internet Explorer\PageSetup", writable: false);
            if (key == null) return null;

            var saved = new Dictionary<string, string?>();
            foreach (var name in new[] { "header", "footer" }.Concat(IeMarginValues))
                saved[name] = key.GetValue(name) as string;
            return saved;
        }
        catch { return null; }
    }

    /// <summary>يجعل الطباعة بدون هامش أو رأس/تذييل — تطبع المعاينة بعرض الورقة بالكامل من كل الجهات</summary>
    private static void SetIePageSetupZero()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser
                .CreateSubKey(@"Software\Microsoft\Internet Explorer\PageSetup");
            if (key == null) return;
            key.SetValue("header", "", Microsoft.Win32.RegistryValueKind.String);
            key.SetValue("footer", "", Microsoft.Win32.RegistryValueKind.String);
            foreach (var m in IeMarginValues)
                key.SetValue(m, "0", Microsoft.Win32.RegistryValueKind.String);
        }
        catch { }
    }

    private static void RestoreIePageSetup(Dictionary<string, string?>? saved)
    {
        try
        {
            if (saved == null) return;
            using var key = Microsoft.Win32.Registry.CurrentUser
                .OpenSubKey(@"Software\Microsoft\Internet Explorer\PageSetup", writable: true);
            if (key == null) return;
            foreach (var kv in saved)
            {
                if (kv.Value == null)
                    key.DeleteValue(kv.Key, throwOnMissingValue: false);
                else
                    key.SetValue(kv.Key, kv.Value, Microsoft.Win32.RegistryValueKind.String);
            }
        }
        catch { }
    }

    /// <summary>
    /// يطبع نفس عنصر WPF المعروض في المعاينة مباشرة على صفحة FixedPage بعرض ورق
    /// الطابعة الفعلي من النقطة (0,0) — لا WebBrowser ولا التقط حاجة من الشاشة،
    /// لذلك لا يمكن أن يضيف IE أو الـ driver أي هامش على الإطلاق.
    /// </summary>
    private void PrintVisual(Func<double, UIElement> visualFactory, AppConfig config)
    {
        PrintQueue? queue = null;
        try
        {
            if (string.IsNullOrWhiteSpace(config.PrinterName))
            {
                // لا توجد طابعة محددة — اختر الطابعة ثم اطبع بنفس الطريقة تماماً
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() != true) return;
                queue = printDialog.PrintQueue;
            }
            else
            {
                queue = new PrintQueue(new LocalPrintServer(), config.PrinterName);
            }

            using (queue)
            {
                double width = queue.DefaultPrintTicket.PageMediaSize?.Width is double pw && pw > 0
                    ? Math.Clamp(pw, 100, 900)
                    : 302;

                var visual = visualFactory(width);
                visual.Measure(new Size(width, double.PositiveInfinity));
                double height = visual.DesiredSize.Height;
                visual.Arrange(new Rect(0, 0, width, height));
                visual.UpdateLayout();

                // طابعة عادية (ورق مقصوص): إن كانت الفاتورة أطول من الورقة تُصغَّر بنفس النسبة
                if (!IsNarrowPaperPrinter(queue))
                {
                    double mediaHeight = queue.DefaultPrintTicket.PageMediaSize?.Height is double mh && mh > 0
                        ? mh : 1123;
                    double maxHeight = Math.Max(700, mediaHeight - 10);
                    if (height > maxHeight)
                    {
                        double scaledWidth = width * maxHeight / height;
                        visual = visualFactory(scaledWidth);
                        visual.Measure(new Size(scaledWidth, double.PositiveInfinity));
                        height = visual.DesiredSize.Height;
                        visual.Arrange(new Rect(0, 0, scaledWidth, height));
                        visual.UpdateLayout();
                        width = scaledWidth;
                    }
                }

                var fixedPage = new FixedPage
                {
                    Width = width,
                    Height = height,
                    Background = Brushes.White
                };
                FixedPage.SetLeft(visual, 0);
                FixedPage.SetTop(visual, 0);
                fixedPage.Children.Add(visual);

                var pageContent = new PageContent { Child = fixedPage };
                var doc = new FixedDocument();
                doc.Pages.Add(pageContent);

                fixedPage.Measure(new Size(width, height));
                fixedPage.Arrange(new Rect(new Size(width, height)));
                fixedPage.UpdateLayout();

                var ticket = queue.DefaultPrintTicket.Clone();
                ticket.PageOrientation = PageOrientation.Portrait;
                ticket.CopyCount = 1;

                PrintQueue.CreateXpsDocumentWriter(queue).Write(doc, ticket);
            }
        }
        finally
        {
            queue?.Dispose();
        }
    }

    /// <summary>
    /// يطبع صفحات تقرير (عناصر WPF مقسمة) مباشرة على FixedDocument —
    /// كل صفحة بعرض ورق الطابعة الفعلي من النقطة (0,0) بدون هوامش.
    /// </summary>
    private void PrintPages(Func<double, double, List<UIElement>> pageFactory, AppConfig config)
    {
        PrintQueue? queue = null;
        try
        {
            if (string.IsNullOrWhiteSpace(config.PrinterName))
            {
                var printDialog = new PrintDialog();
                if (printDialog.ShowDialog() != true) return;
                queue = printDialog.PrintQueue;
            }
            else
            {
                queue = new PrintQueue(new LocalPrintServer(), config.PrinterName);
            }

            using (queue)
            {
                double width = queue.DefaultPrintTicket.PageMediaSize?.Width is double pw && pw > 0
                    ? Math.Clamp(pw, 100, 900)
                    : 302;
                double mediaHeight = queue.DefaultPrintTicket.PageMediaSize?.Height is double mh && mh > 0
                    ? mh : 1123;

                var pages = pageFactory(width, mediaHeight);
                var doc = new FixedDocument();
                foreach (var page in pages)
                {
                    page.Measure(new Size(width, double.PositiveInfinity));
                    double height = page.DesiredSize.Height;
                    page.Arrange(new Rect(0, 0, width, height));
                    page.UpdateLayout();

                    var fixedPage = new FixedPage
                    {
                        Width = width,
                        Height = height,
                        Background = Brushes.White
                    };
                    FixedPage.SetLeft(page, 0);
                    FixedPage.SetTop(page, 0);
                    fixedPage.Children.Add(page);

                    var pageContent = new PageContent { Child = fixedPage };
                    doc.Pages.Add(pageContent);

                    fixedPage.Measure(new Size(width, height));
                    fixedPage.Arrange(new Rect(new Size(width, height)));
                    fixedPage.UpdateLayout();
                }

                var ticket = queue.DefaultPrintTicket.Clone();
                ticket.PageOrientation = PageOrientation.Portrait;
                ticket.CopyCount = 1;

                PrintQueue.CreateXpsDocumentWriter(queue).Write(doc, ticket);
            }
        }
        finally
        {
            queue?.Dispose();
        }
    }

    /// <summary>عرض ورق الطابعة الفعلي (بوحدة 1/96 بوصة) من الـ driver، وافتراض 80mm ≈ 302 عند الغياب</summary>
    private static double GetQueuePaperWidth(string? printerName)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(printerName))
            {
                var queue = new PrintQueue(new LocalPrintServer(), printerName);
                using (queue)
                {
                    if (queue.DefaultPrintTicket.PageMediaSize?.Width is double w && w > 0)
                        return Math.Clamp(w, 100, 900);
                }
            }
        }
        catch { }
        return 302;
    }

    /// <summary>ارتفاع ورق الطابعة الفعلي (بوحدة 1/96 بوصة)، وافتراض A4 ≈ 1123 عند الغياب</summary>
    private static double GetQueuePaperHeight(string? printerName)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(printerName))
            {
                var queue = new PrintQueue(new LocalPrintServer(), printerName);
                using (queue)
                {
                    if (queue.DefaultPrintTicket.PageMediaSize?.Height is double h && h > 0)
                        return Math.Clamp(h, 400, 2000);
                }
            }
        }
        catch { }
        return 1123;
    }

    /// <summary>
    /// يحدد إن كانت الطابعة حرارية ضيقة (رول 58/80mm) أم طابعة عادية (A4/A5).
    /// عرض الوسائط في System.Printing بوحدة 1/96 بوصة: 80mm ≈ 302، 58mm ≈ 219، A5 ≈ 559، A4 ≈ 794
    /// </summary>
    private static bool IsNarrowPaperPrinter(PrintQueue queue)
    {
        try
        {
            var caps = queue.GetPrintCapabilities();
            return caps.PageMediaSizeCapability
                .Where(m => m.Width.HasValue)
                .Any(m => m.Width!.Value < 400); // أقل من ~106mm
        }
        catch { return false; }
    }

    private void PrintViaOle(bool showDialog = false)
    {
        var doc = ReceiptBrowser.Document;
        if (doc == null) return;
        var oleCmd = doc as IOleCommandTarget;
        if (oleCmd == null) return;
        oleCmd.Exec(IntPtr.Zero, 6, showDialog ? 1u : 2u, IntPtr.Zero, IntPtr.Zero);
    }

    // ===== COM / Win32 =====

    [ComImport, Guid("B722BCCB-4E68-101B-A2BC-00AA00404770"),
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleCommandTarget
    {
        [PreserveSig] int QueryStatus(IntPtr pguidCmdGroup, uint cCmds, IntPtr prgCmds, IntPtr pCmdText);
        [PreserveSig] int Exec(IntPtr pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut);
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        DoPrint();
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        try { if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath); } catch { }
        DialogClosed?.Invoke(this, false);
    }

    private void BtnPdf_Click(object sender, RoutedEventArgs e)
    {
        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = $"فاتورة_{(_invoice?.Id.ToString() ?? "export")}.pdf",
            DefaultExt = ".pdf"
        };

        if (saveDialog.ShowDialog() != true) return;

        var success = PdfExportService.ExportHtmlToPdf(_html, saveDialog.FileName);
        if (success)
        {
            NotificationManager.ShowSuccess("تم تصدير الفاتورة بنجاح بصيغة PDF");
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true }); } catch { }
        }
        else
        {
            var result = MessageBox.Show(
                "تعذر تصدير PDF باستخدام المتصفح.\nهل تريد فتح الفاتورة في المتصفح لطباعتها PDF يدوياً؟",
                "تصدير PDF", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (result == MessageBoxResult.Yes)
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_tempFilePath) { UseShellExecute = true }); } catch { }
            }
        }
    }
}