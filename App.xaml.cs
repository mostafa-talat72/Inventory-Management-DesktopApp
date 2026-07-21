using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using ProductApp.Data;
using ProductApp.Services;
using ProductApp.Views;

namespace ProductApp;

public partial class App : Application
{
    public static BackupService? AppBackup { get; private set; }
    public static AppConfig? AppConfiguration { get; private set; }

    private void App_OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogError(e.Exception);
        e.Handled = true;
        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MTE Stock", "error.log");
        MessageBox.Show(
            $"حدث خطأ غير متوقع:\n{e.Exception.Message}\n\nتم تسجيل الخطأ في:\n{logPath}",
            "خطأ في البرنامج",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        Shutdown();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Force Arabic culture on all threads
        var arCulture = new CultureInfo("ar-SA");
        arCulture.DateTimeFormat.Calendar = new GregorianCalendar();
        arCulture.NumberFormat.NumberGroupSeparator = "";
        arCulture.NumberFormat.NumberDecimalSeparator = ".";
        arCulture.NumberFormat.NumberDecimalDigits = 2;
        arCulture.NumberFormat.CurrencyGroupSeparator = "";
        arCulture.NumberFormat.CurrencyDecimalSeparator = ".";
        arCulture.NumberFormat.PercentGroupSeparator = "";
        CultureInfo.DefaultThreadCurrentCulture = arCulture;
        CultureInfo.DefaultThreadCurrentUICulture = arCulture;
        Thread.CurrentThread.CurrentCulture = arCulture;
        Thread.CurrentThread.CurrentUICulture = arCulture;

        // Fix all icons direction (RTL reverses Path geometry)
        EventManager.RegisterClassHandler(typeof(System.Windows.Shapes.Path),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) =>
            {
                if (s is System.Windows.Shapes.Path p)
                    p.FlowDirection = FlowDirection.LeftToRight;
            }));

        // Convert Arabic-Indic digits (٠-٩) to Western digits (0-9) in all TextBoxes on input
        bool _convertingDigit = false;
        EventManager.RegisterClassHandler(typeof(TextBox),
            TextBox.TextChangedEvent,
            new RoutedEventHandler((s, _) =>
            {
                if (_convertingDigit) return;
                if (s is TextBox tb && !string.IsNullOrEmpty(tb.Text))
                {
                    bool changed = false;
                    var chars = tb.Text.ToCharArray();
                    for (int i = 0; i < chars.Length; i++)
                    {
                        if (chars[i] >= 0x660 && chars[i] <= 0x669)
                        {
                            chars[i] = (char)('0' + (chars[i] - 0x660));
                            changed = true;
                        }
                    }
                    if (changed)
                    {
                        _convertingDigit = true;
                        int pos = tb.CaretIndex;
                        tb.SetCurrentValue(TextBox.TextProperty, new string(chars));
                        tb.CaretIndex = pos;
                        _convertingDigit = false;
                    }
                }
            }));

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is System.Exception ex)
                LogError(ex);
        };

        // Load config
        var config = AppConfig.Load();
        AppConfiguration = config;

        // Initialize database
        try
        {
            using var db = new AppDbContext();
            db.Database.EnsureCreated();
        }
        catch (System.Exception ex)
        {
            LogError(ex);
            Shutdown();
            return;
        }

        // Show login dialog
        var login = new LoginDialog(config);
        if (login.ShowDialog() != true)
        {
            Shutdown();
            return;
        }

        // Initialize backup service
        var backup = new BackupService(config);
        AppBackup = backup;
        backup.StartAutoBackup();
        backup.BackupIfOnStartup();

        // Show main window
        var mainWin = new MainWindow();
        mainWin.Closed += (_, _) => Shutdown();
        mainWin.Show();
    }

    private void LogError(System.Exception ex)
    {
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MTE Stock");
        if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, "error.log");
        var msg = $"[{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}]\n{ex}\n";
        if (ex.InnerException != null)
            msg += $"INNER:\n{ex.InnerException}\n";
        File.AppendAllText(logPath, msg);

        try
        {
            var localPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            File.AppendAllText(localPath, msg);
        }
        catch { }
    }
}
