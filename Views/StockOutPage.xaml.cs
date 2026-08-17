using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class StockOutPage : Page
{
    private readonly AppDbContext _db;
    private readonly List<StockOutItem> _rows = [];
    private readonly List<TypeChip> _chips = [];
    private List<StockOutItem> _shown = [];
    private string? _typeFilter;
    private decimal _totalValue;
    private int _totalCount;
    private int _productsCount;

    public StockOutPage()
    {
        InitializeComponent();
        _db = new AppDbContext();

        App.DataChanged += OnAppDataChanged;
        AmountsVisibilityService.VisibilityChanged += OnVisibilityChanged;

        LoadData();
    }

    private void OnAppDataChanged() => LoadData();

    private void OnVisibilityChanged() => ApplyMasks();

    private static readonly (string Name, string Color, string Bg)[] Types =
    [
        ("مرتجع للمورد", "#E65100", "#FFF3E0"),
        ("تالف",          "#C62828", "#FFEBEE"),
        ("عجز",           "#C62828", "#FFEBEE"),
        ("فقد",           "#C62828", "#FFEBEE"),
        ("استخدام داخلي", "#F57F17", "#FFF8E1"),
        ("عينة",          "#1565C0", "#E3F2FD"),
        ("تبرع",          "#2E7D32", "#E8F5E9"),
        ("شطب",           "#546E7A", "#ECEFF1"),
        ("تعديل",         "#455A64", "#ECEFF1"),
    ];

    private static (string Name, string Color, string Bg) GetTypeOf(InventoryMovement m)
    {
        var notes = m.Notes ?? "";
        foreach (var t in Types)
        {
            if (notes.StartsWith(t.Name, StringComparison.Ordinal))
                return t;
        }
        return m.MovementType switch
        {
            MovementType.ReturnToSupplier => Types[0],
            MovementType.Shortage => Types[2],
            _ => Types[^1],
        };
    }

    /// <summary>يجلب الفرشاة الموافقة للمفتاح (hex-Key) حسب الثيم الحالي — عند غيابه يستخدم اللون الخام</summary>
    private static Brush ThemeBrush(string hexKey) =>
        Application.Current.TryFindResource(hexKey) as Brush
        ?? new SolidColorBrush(ParseHex(hexKey));

    private static Color ThemeColor(string hexKey) =>
        (ThemeBrush(hexKey) as SolidColorBrush)?.Color ?? ParseHex(hexKey);

    private static Color ParseHex(string hex)
    {
        hex = hex.TrimStart('#');
        return Color.FromRgb(
            Convert.ToByte(hex[0..2], 16),
            Convert.ToByte(hex[2..4], 16),
            Convert.ToByte(hex[4..6], 16));
    }

    private void LoadData()
    {
        var movements = _db.InventoryMovements
            .AsNoTracking()
            .Where(m => m.Quantity > 0
                && (m.MovementType == MovementType.Adjustment
                    || m.MovementType == MovementType.Shortage
                    || (m.MovementType == MovementType.ReturnToSupplier && !m.IsCostRecovered)))
            .OrderByDescending(m => m.CreatedAt)
            .ToList();

        var names = _db.Products.AsNoTracking().Where(p => !p.IsDeleted).ToDictionary(p => p.Id, p => p.Name);

        _rows.Clear();
        foreach (var m in movements)
        {
            var (typeName, color, bgHex) = GetTypeOf(m);
            _rows.Add(new StockOutItem
            {
                ProductId       = m.ProductId,
                MovementId      = m.Id,
                DateDisplay     = $"{m.CreatedAt:yyyy/MM/dd}",
                TimeDisplay     = $"{m.CreatedAt:hh:mm} {(m.CreatedAt.Hour < 12 ? "ص" : "م")}",
                ProductName     = names.TryGetValue(m.ProductId, out var n) ? n : "منتج محذوف",
                TypeName        = typeName,
                TypeDotColor    = ThemeColor(color),
                TypeFgColor     = ThemeColor(color),
                TypeBgColor     = ThemeColor(bgHex),
                ReasonDisplay   = string.IsNullOrWhiteSpace(m.Notes) ? "-" : m.Notes,
                Value           = m.Quantity * m.CostPrice
            });
        }

        _totalCount    = _rows.Count;
        _totalValue    = _rows.Sum(r => r.Value);
        _productsCount = _rows.Select(r => r.ProductId).Distinct().Count();

        TxtSubtitle.Text = "لا يشمل مرتجع المورد المسدد قيمته";
        TxtBadgeCount.Text = _totalCount.ToString();

        BuildChips();
        SetFilter(_typeFilter);
        ApplyMasks();
    }

    private void BuildChips()
    {
        ChipsPanel.Children.Clear();
        _chips.Clear();

        // «الكل» — لون ثابت من عائلة الأحمر لتمييزه
        AddChip(new TypeChip
        {
            Type = null,
            ColorKey = "#C62828",
            BgKey = "#FFEBEE",
            Count = _rows.Count,
            Value = _rows.Sum(r => r.Value)
        });

        var present = _rows.Select(r => r.TypeName).Distinct().ToHashSet();
        foreach (var t in Types)
        {
            if (present.Contains(t.Name))
            {
                var same = _rows.Where(r => r.TypeName == t.Name).ToList();
                AddChip(new TypeChip
                {
                    Type = t.Name,
                    ColorKey = t.Color,
                    BgKey = t.Bg,
                    Count = same.Count,
                    Value = same.Sum(r => r.Value)
                });
            }
        }

        UpdateChipSelection();
    }

    private void AddChip(TypeChip chip)
    {
        var txtName = new TextBlock
        {
            Text = chip.Type ?? "الكل",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(6, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        chip.TxtName = txtName;

        var line1 = new StackPanel { Orientation = Orientation.Horizontal };
        line1.Children.Add(new Border
        {
            Width = 7,
            Height = 7,
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(ThemeColor(chip.ColorKey)),
            VerticalAlignment = VerticalAlignment.Center
        });
        line1.Children.Add(txtName);

        var txtValue = new TextBlock { FontSize = 10, Margin = new Thickness(0, 2, 0, 0) };
        chip.TxtValue = txtValue;

        var inner = new StackPanel { Margin = new Thickness(12, 7, 12, 7) };
        inner.Children.Add(line1);
        inner.Children.Add(txtValue);

        var border = new Border
        {
            CornerRadius = new CornerRadius(9),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 7, 0),
            Tag = chip.Type
        };
        border.Child = inner;
        border.MouseLeftButtonDown += TypeChip_Click;
        border.MouseEnter += (_, _) => { chip.Hovered = true; UpdateChipSelection(); };
        border.MouseLeave += (_, _) => { chip.Hovered = false; UpdateChipSelection(); };

        chip.Border = border;
        _chips.Add(chip);
        ChipsPanel.Children.Add(border);
    }

    private void TypeChip_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border b) return;
        var tag = b.Tag as string;
        _typeFilter = string.IsNullOrEmpty(tag) ? null : tag;
        SetFilter(_typeFilter);
        ApplyMasks();
    }

    private void SetFilter(string? type)
    {
        _typeFilter = type;
        _shown = type == null ? _rows : _rows.Where(r => r.TypeName == type).ToList();

        Grid.ItemsSource = _shown;
        EmptyState.Visibility = _shown.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        var shownValue = _shown.Sum(r => r.Value);
        TxtFooterCount.Text = $"إجمالي {CountLabel(_shown.Count)}";
        TxtFooterValue.Text = $"{shownValue:0.##} ج.م";

        UpdateChipSelection();
    }

    private void UpdateChipSelection()
    {
        foreach (var chip in _chips)
        {
            bool selected = (chip.Type == null && _typeFilter == null)
                || (chip.Type != null && chip.Type == _typeFilter);

            if (selected)
            {
                chip.Border.Background = ThemeBrush(chip.BgKey);
                chip.Border.BorderBrush = ThemeBrush(chip.ColorKey);
                chip.TxtName.Foreground = ThemeBrush(chip.ColorKey);
                chip.TxtValue.Foreground = ThemeBrush(chip.ColorKey);
            }
            else if (chip.Hovered)
            {
                chip.Border.SetResourceReference(Border.BackgroundProperty, "SurfaceBackground");
                chip.Border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                chip.TxtName.Foreground = ThemeBrush("BodyTextBrush");
                chip.TxtValue.Foreground = ThemeBrush("BodyTextBrush");
            }
            else
            {
                chip.Border.SetResourceReference(Border.BackgroundProperty, "CardBackgroundAlt");
                chip.Border.SetResourceReference(Border.BorderBrushProperty, "BorderBrushLight");
                chip.TxtName.Foreground = ThemeBrush("BodyTextBrush");
                chip.TxtValue.Foreground = ThemeBrush("MutedTextBrush");
            }
        }
    }

    private void ApplyMasks()
    {
        const string mask = "••••••";
        bool hidden = AmountsVisibilityService.IsHidden;

        TxtTotalValue.Text = hidden ? mask : $"{_totalValue:0.##} ج.م";
        TxtTotalCount.Text = _totalCount.ToString();
        TxtProductsCount.Text = _productsCount.ToString();

        foreach (var chip in _chips)
        {
            chip.TxtValue.Text = hidden
                ? $"{CountLabel(chip.Count)} · {mask}"
                : $"{CountLabel(chip.Count)} · {chip.Value:0.##} ج.م";
        }

        foreach (var r in _rows)
            r.ValueDisplay = hidden ? mask : $"{r.Value:0.##} ج.م";

        var shownValue = _shown.Sum(r => r.Value);
        TxtFooterValue.Text = hidden ? mask : $"{shownValue:0.##} ج.م";

        Grid.ItemsSource = null;
        Grid.ItemsSource = _shown;
    }

    private static string CountLabel(int count) =>
        count == 1 ? "حركة واحدة" : $"{count} حركات";

    private void ViewProductLog_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not StockOutItem item) return;
        var product = _db.Products.Find(item.ProductId);
        if (product == null) return;

        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockMovementDialog(_db, product);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (_, _) =>
        {
            mainWindow.HideOverlay();
            LoadData();
        };
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        if (_shown.Count == 0)
        {
            NotificationManager.ShowInfo("لا توجد حركات منصرف للتصدير");
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"منصرف_المخزون_{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (saveDialog.ShowDialog() != true) return;

        try
        {
            string[] headers = { "التاريخ", "المنتج", "النوع", "السبب", "القيمة" };
            var rows = _shown.Select(i => new object?[]
            {
                i.DateDisplay, i.ProductName, i.TypeName, i.ReasonDisplay, $"{i.Value:0.##}"
            }).ToList();

            ExcelExportService.Export(saveDialog.FileName, headers, rows);
            NotificationManager.ShowSuccess("تم تصدير منصرف المخزون إلى Excel بنجاح");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء التصدير:\n{ex.Message}", "تصدير", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}

public class StockOutItem
{
    public int ProductId { get; set; }
    public int MovementId { get; set; }
    public required string DateDisplay { get; set; }
    public required string TimeDisplay { get; set; }
    public required string ProductName { get; set; }
    public required string TypeName { get; set; }
    public Color TypeDotColor { get; set; }
    public Color TypeFgColor { get; set; }
    public Color TypeBgColor { get; set; }
    public required string ReasonDisplay { get; set; }
    public decimal Value { get; set; }
    public string ValueDisplay { get; set; } = "";
}

public class TypeChip
{
    public Border? Border { get; set; }
    public TextBlock? TxtName { get; set; }
    public TextBlock? TxtValue { get; set; }
    public string? Type { get; set; }
    public required string ColorKey { get; set; }
    public required string BgKey { get; set; }
    public int Count { get; set; }
    public decimal Value { get; set; }
    public bool Hovered { get; set; }
}