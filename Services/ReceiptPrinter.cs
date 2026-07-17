using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;

namespace ProductApp.Services;

public class ReceiptPrinter
{
    private readonly AppDbContext _db;

    public ReceiptPrinter(AppDbContext db)
    {
        _db = db;
    }

    public void Print(Invoice invoice)
    {
        _db.Entry(invoice).Reference(i => i.Customer).Load();
        _db.Entry(invoice).Collection(i => i.Orders).Load();

        var items = _db.OrderItems
            .Include(oi => oi.Product)
            .Where(oi => oi.Order.InvoiceId == invoice.Id)
            .ToList();

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;

        var receipt = BuildReceiptVisual(invoice, items);
        printDialog.PrintVisual(receipt, $"فاتورة #{invoice.Id}");
    }

    private Visual BuildReceiptVisual(Invoice invoice, List<OrderItem> items)
    {
        double width = 280;
        double margin = 12;
        double innerWidth = width - margin * 2;

        var root = new StackPanel
        {
            Width = width,
            Background = Brushes.White,
            FlowDirection = FlowDirection.RightToLeft
        };

        double y = 0;
        Action<UIElement> add = el => { root.Children.Add(el); y += el is FrameworkElement fe ? fe.Height : 30; };

        // --- Header ---
        add(MakeText("نظام إدارة المخزون", 18, FontWeights.Black, horizontal: HorizontalAlignment.Center));
        add(MakeText($"فاتورة رقم {invoice.Id}", 14, FontWeights.Bold, horizontal: HorizontalAlignment.Center));
        add(MakeText(invoice.CreatedAt.ToString("yyyy/MM/dd - hh:mm tt"), 10, FontWeights.Normal, foreground: Brushes.Gray, horizontal: HorizontalAlignment.Center));

        if (invoice.CustomerId != null)
        {
            add(MakeSeparator(4));
            var customerBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)), Padding = new Thickness(8), Margin = new Thickness(0, 4, 0, 4) };
            var customerStack = new StackPanel();
            customerStack.Children.Add(MakeText(invoice.CustomerName, 12, FontWeights.Bold));
            if (invoice.Customer?.Phone != null)
                customerStack.Children.Add(MakeText(invoice.Customer.Phone, 10, FontWeights.Normal, foreground: Brushes.Gray));
            customerBorder.Child = customerStack;
            add(customerBorder);
        }

        add(MakeSeparator(8));

        // --- Barcode ---
        var barcodeImage = GenerateBarcodeImage(invoice.Id.ToString("D4"), 240, 60);
        if (barcodeImage != null)
        {
            var barcodeContainer = new Border { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 4) };
            barcodeContainer.Child = barcodeImage;
            add(barcodeContainer);
        }

        add(MakeSeparator(8));

        // --- Items Table ---
        add(MakeText("المنتجات", 12, FontWeights.Bold, horizontal: HorizontalAlignment.Center, background: new SolidColorBrush(Color.FromRgb(224, 224, 224))));

        var table = new Grid { Width = innerWidth, Margin = new Thickness(0, 4, 0, 0) };
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Star) });
        table.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Star) });

        // Header row
        var headerBorder = new Border { Background = new SolidColorBrush(Color.FromRgb(224, 224, 224)), BorderThickness = new Thickness(1), BorderBrush = Brushes.Black };
        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Star) });
        AddCell(headerGrid, "المنتج", 0, true);
        AddCell(headerGrid, "الكمية", 1, true);
        AddCell(headerGrid, "السعر", 2, true);
        AddCell(headerGrid, "الإجمالي", 3, true);
        headerBorder.Child = headerGrid;
        table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(headerBorder, 0);
        table.Children.Add(headerBorder);

        // Data rows
        int row = 1;
        foreach (var item in items)
        {
            var qtyParts = new List<string>();
            if (item.CartonQuantity > 0) qtyParts.Add($"{item.CartonQuantity}كر");
            if (item.BoxQuantity > 0) qtyParts.Add($"{item.BoxQuantity}ع");
            if (item.PieceQuantity > 0) qtyParts.Add($"{item.PieceQuantity}ق");
            var qtyText = string.Join("+", qtyParts);

            var rowBorder = new Border { BorderThickness = new Thickness(1), BorderBrush = Brushes.Black };
            var rowGrid = new Grid();
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(25, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Star) });
            rowGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20, GridUnitType.Star) });
            AddCell(rowGrid, item.Product.Name, 0, false);
            AddCell(rowGrid, qtyText, 1, false);
            AddCell(rowGrid, item.UnitPrice.ToString("N2"), 2, false);
            AddCell(rowGrid, item.Total.ToString("N2"), 3, false);
            rowBorder.Child = rowGrid;
            table.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(rowBorder, row);
            table.Children.Add(rowBorder);
            row++;
        }

        // Set row position for all children
        foreach (var child in table.Children)
        {
            if (child is FrameworkElement fe && Grid.GetRow(fe) == 0 && fe != headerBorder)
            {
                // these are data rows that didn't get row set yet
            }
        }

        add(table);

        add(MakeSeparator(8));

        // --- Totals ---
        var discount = invoice.Discount;
        var remaining = invoice.Remaining;

        add(MakeTotalRow("الإجمالي", invoice.TotalAmount, 16, FontWeights.Black, Brushes.White, new SolidColorBrush(Color.FromRgb(0, 0, 0))));
        add(MakeTotalRow("المدفوع", invoice.TotalPaid, 14, FontWeights.Bold, Brushes.White, new SolidColorBrush(Color.FromRgb(46, 125, 50))));
        if (remaining > 0)
            add(MakeTotalRow("المتبقي", remaining, 14, FontWeights.Bold, Brushes.White, new SolidColorBrush(Color.FromRgb(245, 127, 23))));

        add(MakeSeparator(8));

        // --- Thank you & Footer ---
        add(MakeText("شكراً لتعاملكم معنا", 12, FontWeights.Bold, horizontal: HorizontalAlignment.Center));
        add(MakeSeparator(8));
        add(MakeText("تم بواسطة نظام إدارة المخزون", 9, FontWeights.Normal, foreground: Brushes.Gray, horizontal: HorizontalAlignment.Center));

        // Measure to set final size
        root.Measure(new Size(width, double.PositiveInfinity));
        root.Arrange(new Rect(new Size(width, root.DesiredSize.Height)));

        return root;
    }

    private static TextBlock MakeText(string text, double fontSize, FontWeight weight, Brush? foreground = null, HorizontalAlignment horizontal = HorizontalAlignment.Right, Brush? background = null)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = foreground ?? Brushes.Black,
            HorizontalAlignment = horizontal,
            TextAlignment = horizontal == HorizontalAlignment.Center ? TextAlignment.Center : TextAlignment.Right,
            Margin = new Thickness(0, 2, 0, 2),
            Background = background
        };
    }

    private static Border MakeSeparator(double height)
    {
        return new Border
        {
            Height = height,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200))
        };
    }

    private static Border MakeTotalRow(string label, decimal amount, double fontSize, FontWeight weight, Brush foreground, Brush background)
    {
        var border = new Border
        {
            Background = background,
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(0, 3, 0, 3),
            Padding = new Thickness(8, 6, 8, 6)
        };
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        grid.Children.Add(new TextBlock { Text = label, FontSize = fontSize, FontWeight = weight, Foreground = foreground, VerticalAlignment = VerticalAlignment.Center });
        grid.Children.Add(new TextBlock { Text = $"{amount:N2} ج.م", FontSize = fontSize, FontWeight = weight, Foreground = foreground, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center });
        Grid.SetColumn(grid.Children[^1], 2);

        border.Child = grid;
        return border;
    }

    private static void AddCell(Grid grid, string text, int col, bool isHeader)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontSize = isHeader ? 10 : 9,
            FontWeight = isHeader ? FontWeights.Bold : FontWeights.Normal,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(3, 4, 3, 4)
        };
        Grid.SetColumn(tb, col);
        grid.Children.Add(tb);
    }

    private Image? GenerateBarcodeImage(string text, int width, int height)
    {
        try
        {
            var writer = new BarcodeWriter<WriteableBitmap>
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Width = width,
                    Height = height,
                    Margin = 2,
                    PureBarcode = true
                }
            };

            var bitmap = writer.Write(text);
            if (bitmap == null) return null;

            return new Image
            {
                Source = bitmap,
                Width = width,
                Height = height,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }
        catch
        {
            return null;
        }
    }
}
