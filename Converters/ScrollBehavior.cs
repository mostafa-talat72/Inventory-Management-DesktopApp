using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ProductApp.Converters;

public static class ScrollBehavior
{
    public static readonly DependencyProperty ForceMouseWheelProperty =
        DependencyProperty.RegisterAttached("ForceMouseWheel", typeof(bool), typeof(ScrollBehavior),
            new PropertyMetadata(false, OnForceMouseWheelChanged));

    public static void SetForceMouseWheel(DependencyObject element, bool value)
        => element.SetValue(ForceMouseWheelProperty, value);

    public static bool GetForceMouseWheel(DependencyObject element)
        => (bool)element.GetValue(ForceMouseWheelProperty);

    private static void OnForceMouseWheelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer sv || !(bool)e.NewValue) return;
        sv.PreviewMouseWheel += OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv) return;
        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta);
        e.Handled = true;
    }
}
