using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ProductApp.Views;

public partial class PrintPreviewDialog : UserControl
{
    private readonly string _html;
    private readonly string _tempFilePath;
    public event EventHandler<bool>? DialogClosed;

    private PrintPreviewDialog(string html, string title)
    {
        InitializeComponent();
        _html = html;
        TxtPreviewInfo.Text = title;

        // Save HTML to temp file for printing
        _tempFilePath = Path.Combine(Path.GetTempPath(), $"receipt_{Guid.NewGuid():N}.html");
        File.WriteAllText(_tempFilePath, html);

        // Load HTML into WebBrowser
        ReceiptBrowser.NavigateToString(html);
    }

    public static void Show(string html, string title)
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow == null) return;

        var dialog = new PrintPreviewDialog(html, title);
        dialog.DialogClosed += (_, _) =>
        {
            mainWindow.HideOverlay();
        };
        mainWindow.ShowOverlay(dialog);
    }

    private void DoPrint()
    {
        try
        {
            // Open the temp HTML file in default browser with print
            var psi = new System.Diagnostics.ProcessStartInfo(_tempFilePath)
            {
                UseShellExecute = true,
                Verb = "open"
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch
        {
            MessageBox.Show("تعذر فتح ملف الطباعة", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        DoPrint();
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        // Clean up temp file
        try { if (File.Exists(_tempFilePath)) File.Delete(_tempFilePath); } catch { }
        DialogClosed?.Invoke(this, false);
    }
}
