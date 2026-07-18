using System.IO;
using System.Linq;
using System.Windows.Threading;

namespace ProductApp.Services;

public class BackupService
{
    private readonly AppConfig _config;
    private readonly string _dbPath;
    private DispatcherTimer? _timer;

    public const int MaxBackups = 5;

    public BackupService(AppConfig config)
    {
        _config = config;
        _dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MTE Stock", "inventory.db");
    }

    public string CreateBackup()
    {
        if (string.IsNullOrWhiteSpace(_config.BackupFolder))
            throw new InvalidOperationException("لم يتم تحديد مجلد النسخ الاحتياطي");

        if (!Directory.Exists(_config.BackupFolder))
            Directory.CreateDirectory(_config.BackupFolder);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFile = Path.Combine(_config.BackupFolder, $"backup_{timestamp}.db");

        File.Copy(_dbPath, backupFile, overwrite: true);

        CleanupOldBackups();

        return backupFile;
    }

    public void CleanupOldBackups()
    {
        if (string.IsNullOrWhiteSpace(_config.BackupFolder) || !Directory.Exists(_config.BackupFolder))
            return;

        var backups = Directory.GetFiles(_config.BackupFolder, "backup_*.db")
            .OrderByDescending(f => f)
            .ToList();

        while (backups.Count > MaxBackups)
        {
            var oldest = backups[^1];
            try { File.Delete(oldest); }
            catch { }
            backups.RemoveAt(backups.Count - 1);
        }
    }

    public int GetBackupCount()
    {
        if (string.IsNullOrWhiteSpace(_config.BackupFolder) || !Directory.Exists(_config.BackupFolder))
            return 0;
        return Directory.GetFiles(_config.BackupFolder, "backup_*.db").Length;
    }

    public long GetBackupFolderSize()
    {
        if (string.IsNullOrWhiteSpace(_config.BackupFolder) || !Directory.Exists(_config.BackupFolder))
            return 0;
        return Directory.GetFiles(_config.BackupFolder, "backup_*.db")
            .Sum(f => new FileInfo(f).Length);
    }

    public void StartAutoBackup()
    {
        StopAutoBackup();

        if (_config.BackupIntervalMinutes <= 0) return;

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(_config.BackupIntervalMinutes)
        };
        _timer.Tick += (_, _) =>
        {
            try { CreateBackup(); }
            catch { }
        };
        _timer.Start();
    }

    public void StopAutoBackup()
    {
        _timer?.Stop();
        _timer = null;
    }

    public void BackupIfOnOperation()
    {
        if (_config.BackupOnOperation)
        {
            try { CreateBackup(); }
            catch { }
        }
    }

    public void BackupIfOnStartup()
    {
        if (_config.BackupOnStartup)
        {
            try { CreateBackup(); }
            catch { }
        }
    }
}
