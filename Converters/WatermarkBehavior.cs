using System.Windows;
using System.Windows.Controls;

namespace ProductApp.Converters;

public static class WatermarkBehavior
{
    public static readonly DependencyProperty WatermarkProperty =
        DependencyProperty.RegisterAttached("Watermark", typeof(string), typeof(WatermarkBehavior),
            new PropertyMetadata(null, OnChanged));

    public static void SetWatermark(TextBox box, string val) => box.SetValue(WatermarkProperty, val);
    public static string GetWatermark(TextBox box) => (string)box.GetValue(WatermarkProperty);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TextBox box)
        {
            box.GotFocus -= OnGotFocus;
            box.LostFocus -= OnLostFocus;
            box.GotFocus += OnGotFocus;
            box.LostFocus += OnLostFocus;
            // Use SetCurrentValue to avoid triggering TextChanged during XAML load
            if (string.IsNullOrEmpty(box.Text))
                box.SetCurrentValue(TextBox.TextProperty, e.NewValue?.ToString());
        }
    }

    private static void OnGotFocus(object s, RoutedEventArgs e)
    {
        if (s is TextBox box && box.Text == GetWatermark(box))
            box.Clear();
    }

    private static void OnLostFocus(object s, RoutedEventArgs e)
    {
        if (s is TextBox box && string.IsNullOrWhiteSpace(box.Text))
            box.SetCurrentValue(TextBox.TextProperty, GetWatermark(box));
    }
}
