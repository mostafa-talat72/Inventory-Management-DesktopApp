using System;
using System.Collections.Generic;
using System.IO;
using System.Printing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class PrintPreviewDialog : UserControl
{
    private readonly string _html;
    private readonly string _tempFilePath;
    private Invoice?        _invoice;
    private List<OrderItem>? _items;
    private AppConfig?      _config;

    public event EventHandler<bool>? DialogClosed;

    private PrintPreviewDialog(string html, string title,
        Invoice? invoice = null, List<OrderItem>? items = null, AppConfig? config = null)
    {
        InitializeComponent();
        _html    = html;
        _invoice = invoice;
        _items   = items;
        _config  = config;

        TxtPreviewInfo.Text = title;

        _tempFilePath = Path.Combine(Path.GetTempPath(), $"receipt_{Guid.NewGuid():N}.html");
        File.WriteAllText(_tempFilePath, html, System.Text.Encoding.UTF8);

        ReceiptBrowser.NavigateToString(html);
    }

    public static void Show(string html, string title,
        Invoice? invoice = null, List<OrderItem>? items = null, AppConfig? config = null)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow == null) return;

        var dialog = new PrintPreviewDialog(html, title, invoice, items, config);
        dialog.DialogClosed += (_, _) => mainWindow.HideOverlay();
        mainWindow.ShowOverlay(dialog);
    }

    private void DoPrint()
    {
        var config = _config ?? AppConfig.Load();

        try
        {
            if (!string.IsNullOrWhiteSpace(config.PrinterName) && _invoice != null && _items != null)
            {
                // طباعة WPF مباشرة — بدون browser بدون dialog
                var printer = new ReceiptPrinter(null!);
                printer.PrintDirect(_invoice, _items, config);
            }
            else if (!string.IsNullOrWhiteSpace(config.PrinterName))
            {
                // طباعة عبر OLE بدون dialog
                PrintToReceiptPrinter(config.PrinterName);
            }
            else
            {
                // لا توجد طابعة محددة — اعرض dialog الطباعة
                PrintViaOle(showDialog: true);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"تعذرت الطباعة:\n{ex.Message}", "خطأ في الطباعة",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PrintToReceiptPrinter(string printerName)
    {
        string? originalDefault = GetDefaultPrinterName();
        bool changed = false;
        try
        {
            SetPrinterPaperSize(printerName);
            if (originalDefault != printerName) { SetDefaultPrinter(printerName); changed = true; }
            ReceiptBrowser.Dispatcher.Invoke(() => PrintViaOle(false), DispatcherPriority.Background);
        }
        catch { PrintViaOle(showDialog: true); }
        finally { if (changed && originalDefault != null) SetDefaultPrinter(originalDefault); }
    }

    private static void SetPrinterPaperSize(string printerName)
    {
        try
        {
            var defaults = new PRINTER_DEFAULTS { DesiredAccess = PRINTER_ACCESS_ADMINISTER };
            if (!OpenPrinter(printerName, out IntPtr hPrinter, ref defaults)) return;
            try
            {
                int sz = DocumentProperties(IntPtr.Zero, hPrinter, printerName, IntPtr.Zero, IntPtr.Zero, 0);
                if (sz <= 0) return;
                IntPtr pDev = Marshal.AllocHGlobal(sz);
                try
                {
                    DocumentProperties(IntPtr.Zero, hPrinter, printerName, pDev, IntPtr.Zero, DM_OUT_BUFFER);
                    var dm = Marshal.PtrToStructure<DEVMODE>(pDev);
                    dm.dmFields     |= DM_PAPERSIZE | DM_PAPERLENGTH | DM_PAPERWIDTH;
                    dm.dmPaperSize   = DMPAPER_USER;
                    dm.dmPaperWidth  = 800;
                    dm.dmPaperLength = 2970;
                    Marshal.StructureToPtr(dm, pDev, true);
                    DocumentProperties(IntPtr.Zero, hPrinter, printerName, pDev, pDev, DM_IN_BUFFER | DM_OUT_BUFFER);
                }
                finally { Marshal.FreeHGlobal(pDev); }
            }
            finally { ClosePrinter(hPrinter); }
        }
        catch { }
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct PRINTER_DEFAULTS { public IntPtr pDatatype; public IntPtr pDevMode; public int DesiredAccess; }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 1)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int   dmFields;
        public short dmOrientation, dmPaperSize, dmPaperLength, dmPaperWidth;
        public short dmScale, dmCopies, dmDefaultSource, dmPrintQuality;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int   dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int   dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
        public int   dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    private const int   PRINTER_ACCESS_ADMINISTER = 0x4;
    private const int   DM_IN_BUFFER  = 8;
    private const int   DM_OUT_BUFFER = 2;
    private const int   DM_PAPERSIZE  = 0x0002;
    private const int   DM_PAPERLENGTH= 0x0004;
    private const int   DM_PAPERWIDTH = 0x0008;
    private const short DMPAPER_USER  = 256;

    [DllImport("winspool.drv", CharSet = CharSet.Auto)] private static extern bool OpenPrinter(string n, out IntPtr h, ref PRINTER_DEFAULTS d);
    [DllImport("winspool.drv")] private static extern bool ClosePrinter(IntPtr h);
    [DllImport("winspool.drv", CharSet = CharSet.Auto)] private static extern int DocumentProperties(IntPtr hwnd, IntPtr hPrinter, string dev, IntPtr o, IntPtr i, int f);
    [DllImport("winspool.drv", CharSet = CharSet.Auto)] private static extern bool SetDefaultPrinter(string name);

    private static string? GetDefaultPrinterName()
    {
        try { using var s = new LocalPrintServer(); return s.DefaultPrintQueue?.FullName; }
        catch { return null; }
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