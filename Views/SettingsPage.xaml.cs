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
        TxtLocationAddress.Text = _config.LocationAddress;
        TxtLocationPhone.Text = _config.LocationPhone;
        TxtLocationDescription.Text = _config.LocationDescription;
        ChkPrintLocationName.IsChecked = _config.PrintLocationName;
        ChkPrintLocationAddress.IsChecked = _config.PrintLocationAddress;
        ChkPrintLocationPhone.IsChecked = _config.PrintLocationPhone;
        ChkPrintLocationDescription.IsChecked = _config.PrintLocationDescription;
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
        _config.LocationAddress = TxtLocationAddress.Text.Trim();
        _config.LocationPhone = TxtLocationPhone.Text.Trim();
        _config.LocationDescription = TxtLocationDescription.Text.Trim();
        _config.PrintLocationName = ChkPrintLocationName.IsChecked == true;
        _config.PrintLocationAddress = ChkPrintLocationAddress.IsChecked == true;
        _config.PrintLocationPhone = ChkPrintLocationPhone.IsChecked == true;
        _config.PrintLocationDescription = ChkPrintLocationDescription.IsChecked == true;
        _config.Save();
        NotificationManager.ShowSuccess("تم حفظ بيانات المكان بنجاح");

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

    private void BtnRestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        TxtRestoreError.Visibility = Visibility.Collapsed;

        var password = TxtRestorePassword.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            TxtRestoreError.Text = "الرجاء إدخال كلمة المرور";
            TxtRestoreError.Visibility = Visibility.Visible;
            return;
        }

        if (!_config.VerifyPassword(password))
        {
            TxtRestoreError.Text = "كلمة المرور غير صحيحة";
            TxtRestoreError.Visibility = Visibility.Visible;
            TxtRestorePassword.Password = "";
            return;
        }

        TxtRestorePassword.Password = "";
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "اختر ملف النسخة الاحتياطية",
            Filter = "Database Files (*.db)|*.db|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() != true) return;

        var sourceFile = dialog.FileName;

        ConfirmDialog.Show(
            "تأكيد استيراد النسخة الاحتياطية",
            $"هل أنت متأكد من استيراد النسخة الاحتياطية؟\nسيتم استبدال قاعدة البيانات الحالية بالكامل.\n\nالملف المحدد: {System.IO.Path.GetFileName(sourceFile)}",
            result =>
            {
                if (!result) return;
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                try
                {
                    var dbPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MTE Stock", "inventory.db");

                    // نسخ احتياطي تلقائي للداتابيز الحالية قبل الاستيراد
                    if (!string.IsNullOrWhiteSpace(_config.BackupFolder) && System.IO.Directory.Exists(_config.BackupFolder))
                    {
                        var autoBackup = System.IO.Path.Combine(
                            _config.BackupFolder,
                            $"before_restore_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                        System.IO.File.Copy(dbPath, autoBackup, true);
                    }

                    // احذف كل ملفات الداتابيز القديمة
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    foreach (var f in new[] { dbPath, dbPath + "-wal", dbPath + "-shm" })
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            try { if (System.IO.File.Exists(f)) System.IO.File.Delete(f); break; }
                            catch { if (i == 4) throw; System.Threading.Thread.Sleep(300); }
                        }
                    }

                    System.IO.File.Copy(sourceFile, dbPath, overwrite: true);

                    NotificationManager.ShowSuccess("تم استيراد النسخة الاحتياطية — سيتم إعادة تشغيل البرنامج الآن");

                    var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                        ?? Environment.ProcessPath;
                    if (exe != null)
                    {
                        System.Diagnostics.Process.Start(exe);
                        Application.Current.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    NotificationManager.ShowError($"فشل استيراد النسخة الاحتياطية: {ex.Message}");
                }
            },
            ConfirmDialog.DialogType.Warning);
    }

    private void BtnDeleteDatabase_Click(object sender, RoutedEventArgs e)
    {
        TxtDeleteError.Visibility = Visibility.Collapsed;

        var password = TxtDeletePassword.Password;
        if (string.IsNullOrWhiteSpace(password))
        {
            TxtDeleteError.Text = "الرجاء إدخال كلمة المرور";
            TxtDeleteError.Visibility = Visibility.Visible;
            return;
        }

        if (!_config.VerifyPassword(password))
        {
            TxtDeleteError.Text = "كلمة المرور غير صحيحة";
            TxtDeleteError.Visibility = Visibility.Visible;
            TxtDeletePassword.Password = "";
            return;
        }

        TxtDeletePassword.Password = "";
        // Clear connection pools so the file can be deleted
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        ConfirmDialog.Show(
            "تأكيد حذف قاعدة البيانات",
            "تحذير: سيتم حذف جميع البيانات نهائياً\n(الفواتير، المنتجات، العملاء، المخزون، الحركات)\n\nهذا الإجراء لا يمكن التراجع عنه. هل أنت متأكد تماماً؟",
            result =>
            {
                if (!result) return;
                try
                {
                    var dbPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "MTE Stock", "inventory.db");

                    // نسخ احتياطي تلقائي قبل الحذف لو فيه مجلد محدد
                    if (!string.IsNullOrWhiteSpace(_config.BackupFolder) && System.IO.Directory.Exists(_config.BackupFolder))
                    {
                        var autoBackup = System.IO.Path.Combine(
                            _config.BackupFolder,
                            $"before_delete_{DateTime.Now:yyyyMMdd_HHmmss}.db");
                        System.IO.File.Copy(dbPath, autoBackup, true);
                    }

                    // حذف الداتابيز مع إعادة المحاولة
                    bool deleted = false;
                    for (int attempt = 0; attempt < 5 && !deleted; attempt++)
                    {
                        try
                        {
                            if (attempt > 0) System.Threading.Thread.Sleep(500);
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            if (System.IO.File.Exists(dbPath))
                                System.IO.File.Delete(dbPath);
                            if (System.IO.File.Exists(dbPath + "-wal"))
                                System.IO.File.Delete(dbPath + "-wal");
                            if (System.IO.File.Exists(dbPath + "-shm"))
                                System.IO.File.Delete(dbPath + "-shm");
                            deleted = true;
                        }
                        catch { }
                    }
                    if (!deleted) throw new System.IO.IOException("لم يتم حذف قاعدة البيانات بعد 5 محاولات");

                    NotificationManager.ShowSuccess("تم حذف قاعدة البيانات — سيتم إعادة تشغيل البرنامج الآن");

                    // إعادة تشغيل البرنامج لإنشاء داتابيز جديدة فارغة
                    var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName
                        ?? Environment.ProcessPath;
                    if (exe != null)
                    {
                        System.Diagnostics.Process.Start(exe);
                        Application.Current.Shutdown();
                    }
                }
                catch (Exception ex)
                {
                    NotificationManager.ShowError($"فشل حذف قاعدة البيانات: {ex.Message}");
                }
            },
            ConfirmDialog.DialogType.Danger);
    }
}
