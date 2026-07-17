using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using ProductApp.Models;

namespace ProductApp.Converters;

public class InvoiceStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is InvoiceStatus status)
        {
            return status switch
            {
                InvoiceStatus.Paid => new SolidColorBrush(Color.FromRgb(46, 125, 50)),
                InvoiceStatus.PartiallyPaid => new SolidColorBrush(Color.FromRgb(245, 127, 23)),
                InvoiceStatus.Open => new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                InvoiceStatus.Cancelled => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CustomerStatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Good" => new SolidColorBrush(Color.FromRgb(232, 245, 233)),
                "HasUnpaid" => new SolidColorBrush(Color.FromRgb(255, 243, 224)),
                "Overdue" => new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                _ => new SolidColorBrush(Color.FromRgb(245, 245, 245))
            };
        }
        return Brushes.White;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class CustomerStatusToBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Good" => new SolidColorBrush(Color.FromRgb(46, 125, 50)),
                "HasUnpaid" => new SolidColorBrush(Color.FromRgb(245, 127, 23)),
                "Overdue" => new SolidColorBrush(Color.FromRgb(198, 40, 40)),
                _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
            };
        }
        return Brushes.Gray;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class UnitTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is UnitType type)
        {
            return type switch
            {
                UnitType.Carton => "كرتونة",
                UnitType.Box => "علبة",
                UnitType.Piece => "قطعة",
                _ => type.ToString()
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class PriceTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is PriceType type)
        {
            return type switch
            {
                PriceType.Retail => "قطاعي",
                PriceType.Wholesale => "جملة",
                _ => type.ToString()
            };
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b)
            return b ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
}
