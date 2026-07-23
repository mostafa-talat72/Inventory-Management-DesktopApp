using System.Windows;

namespace ProductApp.Services;

/// <summary>
/// Manages application-wide light/dark theme switching.
/// </summary>
public static class ThemeService
{
    private const string LightThemeSource = "Styles/LightTheme.xaml";
    private const string DarkThemeSource  = "Styles/DarkTheme.xaml";

    public static bool IsDarkMode { get; private set; }

    /// <summary>
    /// Loads the saved theme preference (called once on startup).
    /// </summary>
    public static void Initialize(bool isDark)
    {
        IsDarkMode = isDark;
        ApplyTheme(isDark, persist: false);
    }

    /// <summary>
    /// Toggles between light and dark mode and persists the setting.
    /// </summary>
    public static void Toggle()
    {
        ApplyTheme(!IsDarkMode, persist: true);
    }

    private static void ApplyTheme(bool isDark, bool persist)
    {
        IsDarkMode = isDark;

        var dicts = Application.Current.Resources.MergedDictionaries;

        // Remove existing theme dictionaries
        var toRemove = dicts
            .Where(d => d.Source != null &&
                        (d.Source.OriginalString.Contains("LightTheme") ||
                         d.Source.OriginalString.Contains("DarkTheme") ||
                         d.Source.OriginalString.Contains("Colors.xaml")))
            .ToList();

        foreach (var d in toRemove)
            dicts.Remove(d);

        // Add the correct theme
        var themeSrc = isDark ? DarkThemeSource : LightThemeSource;
        var theme = new ResourceDictionary
        {
            Source = new Uri(themeSrc, UriKind.Relative)
        };
        dicts.Insert(0, theme);

        if (persist)
        {
            var config = AppConfig.Load();
            config.IsDarkMode = isDark;
            config.Save();
        }
    }
}
