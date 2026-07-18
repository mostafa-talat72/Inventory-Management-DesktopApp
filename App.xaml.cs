using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Markup;
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
        Shutdown();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        // Force Arabic culture on all threads
        var arCulture = new CultureInfo("ar-SA");
        arCulture.DateTimeFormat.Calendar = new GregorianCalendar();
        arCulture.NumberFormat.NumberGroupSeparator = "";
        arCulture.NumberFormat.NumberDecimalDigits = 2;
        arCulture.NumberFormat.CurrencyGroupSeparator = "";
        arCulture.NumberFormat.CurrencyDecimalSeparator = ".";
        arCulture.NumberFormat.PercentGroupSeparator = "";
        CultureInfo.DefaultThreadCurrentCulture = arCulture;
        CultureInfo.DefaultThreadCurrentUICulture = arCulture;
        Thread.CurrentThread.CurrentCulture = arCulture;
        Thread.CurrentThread.CurrentUICulture = arCulture;

        // Force Arabic Language on every FrameworkElement when it loads
        EventManager.RegisterClassHandler(typeof(FrameworkElement),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((s, _) =>
            {
                if (s is FrameworkElement fe)
                    fe.Language = XmlLanguage.GetLanguage("ar-SA");
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
            DbSeeder.Seed(db);
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
    }
}
