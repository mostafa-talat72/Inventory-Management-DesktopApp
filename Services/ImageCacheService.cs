using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ProductApp.Services;

/// <summary>
/// ذاكرة تخزين مؤقت للصور المصغرة — فك الترميز مرة واحدة في الخلفية
/// ثم إعادة استخدام الصورة المجمدة لكل البطاقات.
/// </summary>
public static class ImageCacheService
{
    private static readonly Dictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> MissingPaths = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// فحص وجود الملف على القرص مع تخزين مؤقت للنتائج السلبية
    /// حتى لا نكرر عمليات فحص القرص مع كل إعادة بناء للبطاقات.
    /// </summary>
    public static bool ExistsOnDisk(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;
        if (MissingPaths.Contains(path)) return false;
        if (File.Exists(path)) return true;
        lock (MissingPaths)
        {
            MissingPaths.Add(path);
        }
        return false;
    }

    public static bool HasCached(string? path)
        => !string.IsNullOrWhiteSpace(path) && Cache.ContainsKey(path);

    public static BitmapImage? Get(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return Cache.TryGetValue(path, out var cached) ? cached : null;
    }

    public static Task<BitmapImage?> LoadAsync(string path, int decodeWidth)
    {
        return Task.Run(() =>
        {
            if (Cache.TryGetValue(path, out var cached)) return cached;
            try
            {
                if (!ExistsOnDisk(path)) return null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(path);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.DecodePixelWidth = decodeWidth;
                bmp.EndInit();
                bmp.Freeze();
                Cache[path] = bmp;
                return bmp;
            }
            catch
            {
                return null;
            }
        });
    }

    public static void Clear() => Cache.Clear();
}
