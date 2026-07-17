using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ProductApp.Services;

public static class NotificationManager
{
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(3);

    public static void ShowSuccess(string message)
    {
        ShowToast(message, "#2E7D32", "#E8F5E9", "M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z");
    }

    public static void ShowError(string message)
    {
        ShowToast(message, "#C62828", "#FFEBEE", "M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z");
    }

    public static void ShowInfo(string message)
    {
        ShowToast(message, "#1565C0", "#E3F2FD", "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z");
    }

    public static void ShowWarning(string message)
    {
        ShowToast(message, "#E65100", "#FFF3E0", "M1 21h22L12 2 1 21zm12-3h-2v-2h2v2zm0-4h-2v-4h2v4z");
    }

    private static void ShowToast(string message, string accentColor, string bgColor, string iconData)
    {
        var mainWindow = Application.Current.MainWindow;
        if (mainWindow == null) return;

        var toastContainer = mainWindow.FindName("ToastContainer") as StackPanel;
        if (toastContainer == null) return;

        var accentBrush = (Brush)new BrushConverter().ConvertFrom(accentColor)!;

        var toast = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = (Brush)new BrushConverter().ConvertFrom(bgColor)!,
            BorderBrush = (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 10),
            Padding = new Thickness(0),
            MaxWidth = 380,
            Opacity = 0,
            RenderTransform = new TranslateTransform(0, 20)
        };

        var innerGrid = new Grid
        {
            Margin = new Thickness(0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            }
        };

        // Accent bar
        var accentBar = new Rectangle
        {
            Width = 5,
            Fill = accentBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
            RadiusX = 6,
            RadiusY = 6
        };
        Grid.SetColumn(accentBar, 0);
        innerGrid.Children.Add(accentBar);

        // Icon
        var iconPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 14, 4, 14)
        };
        iconPanel.Children.Add(new Path
        {
            Width = 22,
            Height = 22,
            Fill = accentBrush,
            Stretch = Stretch.Uniform,
            Data = Geometry.Parse(iconData)
        });
        Grid.SetColumn(iconPanel, 1);
        innerGrid.Children.Add(iconPanel);

        // Text
        var textBlock = new TextBlock
        {
            Text = message,
            FontSize = 14,
            Foreground = (Brush)new BrushConverter().ConvertFrom("#37474F")!,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 14, 16, 14),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetColumn(textBlock, 2);
        innerGrid.Children.Add(textBlock);

        toast.Child = innerGrid;
        toastContainer.Children.Add(toast);

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var slideIn = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        toast.RenderTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);

        var timer = new DispatcherTimer { Interval = Duration };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            fadeOut.Completed += (_, _) =>
            {
                if (toastContainer.Children.Contains(toast))
                    toastContainer.Children.Remove(toast);
            };
            toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        };
        timer.Start();
    }
}
