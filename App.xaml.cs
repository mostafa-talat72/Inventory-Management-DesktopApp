using System.IO;
using System.Windows;
using ProductApp.Data;

namespace ProductApp;

public partial class App : Application
{
    private void App_OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        LogError(e.Exception);
        e.Handled = true;
        Shutdown();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is System.Exception ex)
                LogError(ex);
        };

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
        base.OnStartup(e);
    }

    private void LogError(System.Exception ex)
    {
        var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
        var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{ex}\n";
        if (ex.InnerException != null)
            msg += $"INNER:\n{ex.InnerException}\n";
        File.AppendAllText(logPath, msg);
    }
}
