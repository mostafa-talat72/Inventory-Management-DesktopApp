namespace ProductApp.Services;

/// <summary>
/// Global toggle for hiding/showing money amounts across Dashboard and Reports pages.
/// </summary>
public static class AmountsVisibilityService
{
    public static bool IsHidden { get; private set; } = true;

    /// <summary>Fired whenever the visibility state changes.</summary>
    public static event Action? VisibilityChanged;

    public static void Initialize(bool hidden)
    {
        IsHidden = hidden;
        // Don't fire event on startup — pages read the initial state themselves.
    }

    public static void Toggle()
    {
        IsHidden = !IsHidden;

        // Persist preference
        var cfg = AppConfig.Load();
        cfg.HideAmounts = IsHidden;
        cfg.Save();

        VisibilityChanged?.Invoke();
    }
}
