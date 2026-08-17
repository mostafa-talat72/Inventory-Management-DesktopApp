using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProductApp.Services;

public class AppConfig
{
    private static readonly string ConfigDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MTE Stock");
    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    public string PasswordHash { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string BackupFolder { get; set; } = "";
    public bool BackupOnStartup { get; set; }
    public bool BackupOnOperation { get; set; }
    public int BackupIntervalMinutes { get; set; }
    public string LocationAddress { get; set; } = "";
    public string LocationPhone { get; set; } = "";
    public string LocationDescription { get; set; } = "";
    public bool PrintLocationName { get; set; } = true;
    public bool PrintLocationAddress { get; set; }
    public bool PrintLocationPhone { get; set; }
    public bool PrintLocationDescription { get; set; }
    public string PrinterName { get; set; } = "";
    public bool AutoPrintAfterSave { get; set; } = true;
    public bool IsDarkMode { get; set; } = false;
    public bool HideAmounts { get; set; } = true;

    // تفضيلات شاشة المنتجات
    public string ProductsSortMode { get; set; } = "name";
    public bool ProductsLowStockOnly { get; set; }

    private static readonly string DefaultPassword = "123456";

    public static AppConfig Load()
    {
        if (File.Exists(ConfigPath))
        {
            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
            return config;
        }
        var newConfig = new AppConfig
        {
            PasswordHash = HashPassword(DefaultPassword)
        };
        newConfig.Save();
        return newConfig;
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }

    public bool VerifyPassword(string password)
    {
        if (string.IsNullOrEmpty(PasswordHash)) return false;

        // PBKDF2 format: "$PBKDF2$<iterations>$<salt_b64>$<hash_b64>"
        if (PasswordHash.StartsWith("$PBKDF2$"))
        {
            var parts = PasswordHash.Split('$');
            if (parts.Length < 5) return false;
            if (!int.TryParse(parts[2], out var iterations)) return false;
            var salt = Convert.FromBase64String(parts[3]);
            var storedHash = Convert.FromBase64String(parts[4]);
            var computedHash = PBKDF2Hash(password, salt, iterations);
            return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
        }

        // Fallback: old SHA256 format (migration support)
        return PasswordHash == LegacySha256Hash(password);
    }

    public void ChangePassword(string newPassword)
    {
        PasswordHash = HashPassword(newPassword);
        Save();
    }

    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = PBKDF2Hash(password, salt, 100_000);
        return $"$PBKDF2$100000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static byte[] PBKDF2Hash(string password, byte[] salt, int iterations)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            32);
    }

    private static string LegacySha256Hash(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes).ToLower();
    }

    public static void ResetToDefault()
    {
        var cfg = new AppConfig { PasswordHash = HashPassword(DefaultPassword) };
        cfg.Save();
    }
}
