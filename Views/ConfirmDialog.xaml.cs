using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProductApp.Views;

public partial class ConfirmDialog : UserControl
{
    public event EventHandler<bool>? DialogClosed;

    public enum DialogType { Danger, Warning, Info }

    private static readonly (Brush color, string icon)[] TypeData =
    {
        (Brushes.Transparent, ""),
    };

    private ConfirmDialog(string title, string message, DialogType type,
        string confirmText, string cancelText)
    {
        InitializeComponent();

        var (iconColor, icon, confirmBg) = type switch
        {
            DialogType.Danger => ((Brush)new BrushConverter().ConvertFrom("#C62828")!,
                "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z",
                (Brush)new BrushConverter().ConvertFrom("#C62828")!),
            DialogType.Warning => ((Brush)new BrushConverter().ConvertFrom("#F57F17")!,
                "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z",
                (Brush)new BrushConverter().ConvertFrom("#E65100")!),
            _ => ((Brush)new BrushConverter().ConvertFrom("#1565C0")!,
                "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z",
                (Brush)new BrushConverter().ConvertFrom("#1565C0")!)
        };

        TxtTitle.Text = title;
        TxtMessage.Text = message;
        IconPath.Fill = iconColor;
        IconPath.Data = Geometry.Parse(icon);
        BtnConfirm.Background = confirmBg;
        TxtConfirm.Text = confirmText;
    }

    public static void Show(string title, string message, Action<bool> callback,
        DialogType type = DialogType.Info,
        string confirmText = "نعم، تأكيد", string cancelText = "إلغاء")
    {
        var mainWindow = Application.Current.MainWindow as MainWindow;
        if (mainWindow == null) return;

        var dialog = new ConfirmDialog(title, message, type, confirmText, cancelText);
        dialog.DialogClosed += (_, result) =>
        {
            mainWindow.HideOverlay();
            callback(result);
        };
        mainWindow.ShowOverlay(dialog);
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}
