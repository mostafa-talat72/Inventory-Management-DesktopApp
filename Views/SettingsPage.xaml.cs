using System.Printing;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class SettingsPage : UserControl
{
    private readonly AppConfig _config;
    private readonly BackupService _backup;

    public SettingsPage()
    {
        InitializeComponent();
        _config = AppConfig.Load();
        _backup = new BackupService(_config);

        LoadSettings();
    }

    private void LoadSettings()
    {
        TxtLocationName.Text = _config.LocationName;
        TxtBackupFolder.Text = _config.BackupFolder;
        ChkBackupOnStartup.IsChecked = _config.BackupOnStartup;
        ChkBackupOnOperation.IsChecked = _config.BackupOnOperation;

        foreach (ComboBoxItem item in CmbBackupInterval.Items)
        {
            if (int.TryParse(item.Tag?.ToString(), out var val) && val == _config.BackupIntervalMinutes)
            {
                CmbBackupInterval.SelectedItem = item;
                break;
            }
        }

        UpdateBackupInfo();
        LoadPrinters();
    }

    private void LoadPrinters()
    {
        var server = new LocalPrintServer();
        var printers = server.GetPrintQueues().OrderBy(p => p.FullName).ToList();
        CmbPrinter.Items.Clear();

        // Add "default" option
        CmbPrinter.Items.Add(new { FullName = "اختيار الطابعة عند الطباعة (افتراضي)" });

        foreach (var p in printers)
            CmbPrinter.Items.Add(new { FullName = p.FullName });

        CmbPrinter.DisplayMemberPath = "FullName";
        CmbPrinter.SelectedIndex = 0;

        if (!string.IsNullOrWhiteSpace(_config.PrinterName))
        {
            for (int i = 0; i < CmbPrinter.Items.Count; i++)
            {
                var item = CmbPrinter.Items[i];
                var name = item.GetType().GetProperty("FullName")?.GetValue(item)?.ToString();
                if (name == _config.PrinterName)
                {
                    CmbPrinter.SelectedIndex = i;
                    break;
                }
            }
        }

        UpdatePrinterInfo();
    }

    private void UpdatePrinterInfo()
    {
        if (string.IsNullOrWhiteSpace(_config.PrinterName))
            TxtPrinterInfo.Text = "لم يتم تحديد طابعة - سيتم فتح مربع حوار اختيار الطابعة عند الطباعة";
        else
            TxtPrinterInfo.Text = $"الطابعة المحددة: {_config.PrinterName}";
    }

    private void UpdateBackupInfo()
    {
        var count = _backup.GetBackupCount();
        var size = _backup.GetBackupFolderSize();
        var sizeText = size >= 1_000_000
            ? $"{size / 1_000_000.0:F1} MB"
            : $"{size / 1_000.0:F1} KB";
        TxtBackupInfo.Text = $"إجمالي حجم النسخ: {sizeText}";
        TxtBackupCount.Text = count.ToString();
    }

    private void BtnSaveLocation_Click(object sender, RoutedEventArgs e)
    {
        _config.LocationName = TxtLocationName.Text.Trim();
        _config.Save();
        NotificationManager.ShowSuccess("تم حفظ اسم المكان بنجاح");

        // Update the title bar if MainWindow is accessible
        if (Window.GetWindow(this) is MainWindow mainWin)
            mainWin.UpdateLocationName(_config.LocationName);
    }

    private void BtnChangePassword_Click(object sender, RoutedEventArgs e)
    {
        TxtPasswordError.Visibility = Visibility.Collapsed;

        var current = TxtCurrentPassword.Password;
        var newPass = TxtNewPassword.Password;
        var confirm = TxtConfirmPassword.Password;

        if (!_config.VerifyPassword(current))
        {
            TxtPasswordError.Text = "الرقم السري الحالي غير صحيح";
            TxtPasswordError.Visibility = Visibility.Visible;
            return;
        }

        if (string.IsNullOrWhiteSpace(newPass))
        {
            TxtPasswordError.Text = "الرجاء إدخال الرقم السري الجديد";
            TxtPasswordError.Visibility = Visibility.Visible;
            return;
        }

        if (newPass != confirm)
        {
            TxtPasswordError.Text = "الرقم السري الجديد غير متطابق مع التأكيد";
            TxtPasswordError.Visibility = Visibility.Visible;
            return;
        }

        _config.ChangePassword(newPass);
        TxtCurrentPassword.Password = "";
        TxtNewPassword.Password = "";
        TxtConfirmPassword.Password = "";
        NotificationManager.ShowSuccess("تم تغيير الرقم السري بنجاح");
    }

    private void BtnBrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "اختر مجلد النسخ الاحتياطي"
        };

        if (dialog.ShowDialog() == true)
        {
            TxtBackupFolder.Text = dialog.FolderName;
        }
    }

    private void BtnBackupNow_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtBackupFolder.Text))
        {
            NotificationManager.ShowWarning("الرجاء تحديد مجلد النسخ الاحتياطي أولاً");
            return;
        }

        _config.BackupFolder = TxtBackupFolder.Text.Trim();
        _config.Save();

        try
        {
            var file = _backup.CreateBackup();
            UpdateBackupInfo();
            NotificationManager.ShowSuccess($"تم إنشاء نسخة احتياطية بنجاح");
        }
        catch (Exception ex)
        {
            NotificationManager.ShowError($"فشل إنشاء النسخة الاحتياطية: {ex.Message}");
        }
    }

    private void OnAutoBackupChanged(object sender, RoutedEventArgs e)
    {
        // Preview values without saving yet
    }

    private void BtnSaveBackupSettings_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtBackupFolder.Text))
        {
            NotificationManager.ShowWarning("الرجاء تحديد مجلد النسخ الاحتياطي أولاً");
            return;
        }

        _config.BackupFolder = TxtBackupFolder.Text.Trim();
        _config.BackupOnStartup = ChkBackupOnStartup.IsChecked == true;
        _config.BackupOnOperation = ChkBackupOnOperation.IsChecked == true;

        if (CmbBackupInterval.SelectedItem is ComboBoxItem item && int.TryParse(item.Tag?.ToString(), out var minutes))
            _config.BackupIntervalMinutes = minutes;

        _config.Save();

        // Restart auto-backup timer
        if (Window.GetWindow(this) is MainWindow mainWin)
            mainWin.RestartAutoBackup();

        UpdateBackupInfo();
        NotificationManager.ShowSuccess("تم حفظ إعدادات النسخ الاحتياطي");
    }

    private void BtnSavePrinter_Click(object sender, RoutedEventArgs e)
    {
        if (CmbPrinter.SelectedItem == null)
        {
            _config.PrinterName = "";
        }
        else
        {
            var name = CmbPrinter.SelectedItem.GetType().GetProperty("FullName")?.GetValue(CmbPrinter.SelectedItem)?.ToString();
            _config.PrinterName = name ?? "";
        }

        _config.Save();
        UpdatePrinterInfo();
        NotificationManager.ShowSuccess("تم حفظ الطابعة الافتراضية");
    }

    private void BtnTestPrint_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var printer = new ReceiptPrinter(null!);
            if (!string.IsNullOrWhiteSpace(_config.PrinterName))
            {
                var server = new LocalPrintServer();
                var queue = new PrintQueue(server, _config.PrinterName);
                printer.PrintTestPage(queue);
            }
            else
            {
                printer.PrintTestPage(null);
            }
        }
        catch (Exception ex)
        {
            NotificationManager.ShowError($"فشلت الطباعة التجريبية: {ex.Message}");
        }
    }
}
