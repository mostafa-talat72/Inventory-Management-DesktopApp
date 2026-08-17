using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class SupplierInvoicesDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Supplier? _supplier;
    private readonly bool _isNoSupplierMode;
    private readonly bool _showAllInvoices;
    private List<SupplierInvoice> _allInvoices = new();
    private string _filterMode = "Unpaid";
    private bool _sortAscending;
    private string _sortMode = "date";
    private int _pageSize = 20;
    private int _displayCount;
    private readonly HashSet<int> _selectedIds = new();
    private bool _showAll;

    public SupplierInvoicesDialog(AppDbContext db, Supplier supplier) : this(db, supplier, false) { }

    // Without-supplier mode: invoices recorded with no supplier
    public SupplierInvoicesDialog(AppDbContext db)
    {
        InitializeComponent();
        _db = db;
        _isNoSupplierMode = true;
        TxtTitle.Text = "فواتير بدون مورد";
        TxtSubtitle.Text = "فواتير التوريد المسجلة بدون مورد";
        TxtSearch.Focus();
        BtnPaySupplier.Visibility = Visibility.Collapsed;
        RegisterVisibilityEvents();
        LoadData();
    }

    // All-invoices mode: shows invoices of every supplier (with and without supplier)
    public SupplierInvoicesDialog(AppDbContext db, bool showAll)
    {
        InitializeComponent();
        _db = db;
        _showAllInvoices = true;
        TxtTitle.Text = "كل فواتير الموردين";
        TxtSubtitle.Text = "فواتير التوريد من جميع الموردين";
        TxtSearch.Focus();
        BtnAddOrder.Visibility = Visibility.Collapsed;
        BtnPaySupplier.Visibility = Visibility.Collapsed;
        RegisterVisibilityEvents();
        LoadData();
    }

    private SupplierInvoicesDialog(AppDbContext db, Supplier supplier, bool _)
    {
        InitializeComponent();
        _db = db;
        _supplier = supplier;
        TxtTitle.Text = $"فواتير - {supplier.Name}";
        TxtSubtitle.Text = supplier.Phone ?? "لا يوجد رقم هاتف";
        RegisterVisibilityEvents();
        LoadData();
    }

    private void RegisterVisibilityEvents()
    {
        Loaded   += (_, _) =>
        {
            AmountsVisibilityService.VisibilityChanged += OnVisibilityChanged;
            App.DataChanged += OnAppDataChanged;
        };
        Unloaded += (_, _) =>
        {
            AmountsVisibilityService.VisibilityChanged -= OnVisibilityChanged;
            App.DataChanged -= OnAppDataChanged;
        };
    }

    private void OnAppDataChanged()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (IsLoaded)
            {
                ReloadAllInvoices();
                ApplyFilter();
            }
        }));
    }

    private void OnVisibilityChanged() => ApplySummaryMask();

    private void ApplySummaryMask()
    {
        const string mask = "••••••";
        bool hidden = AmountsVisibilityService.IsHidden;
        TxtTotalAmount.Text     = hidden ? mask : $"{_cTotal:0.##} ج.م";
        TxtPaidAmount.Text      = hidden ? mask : $"{_cPaid:0.##} ج.م";
        TxtRemainingAmount.Text = hidden ? mask : $"{_cRemaining:0.##} ج.م";
    }

    private void LoadData()
    {
        ReloadAllInvoices();
        SetFilter("Unpaid");
    }

    private void ReloadAllInvoices()
    {
        IQueryable<SupplierInvoice> q = _showAllInvoices
            ? _db.SupplierInvoices.AsQueryable()
            : _db.SupplierInvoices.Where(i => _isNoSupplierMode ? i.SupplierId == null : i.SupplierId == _supplier!.Id);

        _allInvoices = q.Include(i => i.Items)
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .ToList();
    }

    private List<SupplierInvoice> GetFiltered()
    {
        var q = _allInvoices.AsEnumerable();

        q = _filterMode switch
        {
            "PartiallyPaid" => q.Where(i => i.Status == InvoiceStatus.PartiallyPaid),
            "Cancelled" => q.Where(i => i.Status == InvoiceStatus.Cancelled),
            "Paid" => q.Where(i => i.Status == InvoiceStatus.Paid),
            "All" => q,
            _ => q.Where(i => i.Status != InvoiceStatus.Paid)
        };

        // Search by invoice ID or supplier name
        var searchText = TxtSearch.Text.Trim();
        if (int.TryParse(searchText, out var searchId))
            q = q.Where(i => i.Id == searchId);
        else if (!string.IsNullOrEmpty(searchText))
            q = q.Where(i => i.Id.ToString().Contains(searchText) || (i.SupplierName ?? "").Contains(searchText));

        // Sort
        if (_sortMode == "amount")
            q = _sortAscending ? q.OrderBy(i => i.TotalAmount) : q.OrderByDescending(i => i.TotalAmount);
        else
            q = _sortAscending ? q.OrderBy(i => i.InvoiceDate) : q.OrderByDescending(i => i.InvoiceDate);

        return q.ToList();
    }

    // Cached summary values for masking
    private decimal _cTotal, _cPaid, _cRemaining;
    private int _cItems;

    private void ApplyFilter()
    {
        var filtered = GetFiltered();
        var totalFiltered = filtered.Count;

        _cTotal     = filtered.Sum(i => i.TotalAmount);
        _cPaid      = filtered.Sum(i => i.TotalPaid);
        _cRemaining = filtered.Sum(i => i.Remaining);
        _cItems     = filtered.Sum(i => i.Items.Count);

        TxtInvoiceCount.Text = totalFiltered.ToString();
        TxtItemsCount.Text = _cItems.ToString();
        ApplySummaryMask();

        InvoicesPanel.Children.Clear();

        if (totalFiltered == 0)
        {
            var filterLabel = _filterMode switch
            {
                "PartiallyPaid" => "مدفوعة جزئياً",
                "Cancelled" => "ملغاة",
                "Paid" => "مدفوعة",
                "All" => "",
                _ => "غير مدفوعة"
            };
            var subtitle = _filterMode == "All"
                ? "لم يتم العثور على أي فواتير توريد"
                : $"لم يتم العثور على فواتير توريد {filterLabel}";

            var emptyCardBg = Application.Current.TryFindResource("CardBackground") as Brush ?? Brushes.White;
            var emptyBorder = Application.Current.TryFindResource("BorderBrushLight") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E8E8E8")!;
            var emptyIconFg = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;
            var emptyTitleFg= Application.Current.TryFindResource("BodyTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;
            var emptySubFg  = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!;
            InvoicesPanel.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = emptyCardBg,
                BorderBrush = emptyBorder,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(40, 48, 40, 48),
                Margin = new Thickness(0, 0, 0, 10),
                Child = new StackPanel
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Children =
                    {
                        new Path
                        {
                            Width = 64, Height = 64,
                            Fill = emptyIconFg,
                            Stretch = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Data = Geometry.Parse("M20,8H4V6H20M20,18H4V16H20M9,10H4V12H9V10M8,11H6V16H8V11M15,9.7C15,11 13.6,12 11.8,12C9.9,12 8.5,11 8.5,9.7C8.5,9.5 8.6,8.8 8.7,8.2C7.6,8 6.7,7.9 6.7,7.9C8.7,5.3 12.1,5 12.6,6.1C13.1,7.2 11.5,8.1 11.5,8.1C10.7,8.6 9.2,9.4 9.2,10C9.2,10.4 10.2,10.9 11.8,10.9C13.7,10.9 15,10.2 15,9.7M10.5,9C10.8,9.1 11,8.9 10.9,8.6C10.7,8.3 10.3,8.1 10,8.1C9.8,8.1 9.6,8.2 9.6,8.4C9.7,8.7 10.2,8.9 10.5,9M3,4L3,22C3,22.55 3.45,23 4,23H13C13.55,23 14,22.55 14,22V20H20C20.55,20 21,19.55 21,19V4C21,3.45 20.55,3 20,3H4C3.45,3 3,3.45 3,4M12,21H5V4H19V18H13C12.6,18 12.3,18.4 12.3,18.9L12,21Z")
                        },
                        new TextBlock
                        {
                            Text = "لا توجد فواتير",
                            FontSize = 18,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = emptyTitleFg,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 16, 0, 4)
                        },
                        new TextBlock
                        {
                            Text = subtitle,
                            FontSize = 13,
                            Foreground = emptySubFg,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            });
            ShowMoreBar.Visibility = Visibility.Collapsed;
            return;
        }

        // Pagination
        _displayCount = _showAll ? totalFiltered : Math.Min(_pageSize, totalFiltered);
        var toShow = filtered.Take(_displayCount).ToList();

        foreach (var invoice in toShow)
            InvoicesPanel.Children.Add(CreateSupplierInvoiceCard(invoice));

        ShowMoreBar.Visibility = _displayCount < totalFiltered ? Visibility.Visible : Visibility.Collapsed;
        TxtShowMore.Text = $"عرض المزيد ({totalFiltered - _displayCount} متبقي)";
    }

    private Border CreateSupplierInvoiceCard(SupplierInvoice invoice)
    {
        var isSelected = _selectedIds.Contains(invoice.Id);
        var (statusText, statusBg, statusFg) = invoice.Status switch
        {
            InvoiceStatus.Paid => ("مدفوعة", "#E8F5E9", "#2E7D32"),
            InvoiceStatus.PartiallyPaid => ("مدفوعة جزئياً", "#FFF8E1", "#F57F17"),
            InvoiceStatus.Cancelled => ("ملغاة", "#F5F5F5", "#9E9E9E"),
            _ => ("غير مدفوعة", "#FFEBEE", "#C62828")
        };
        var statusBgBrush = (Brush)new BrushConverter().ConvertFrom(statusBg)!;
        var statusFgBrush = (Brush)new BrushConverter().ConvertFrom(statusFg)!;

        // Theme-aware brushes
        var cardBg      = Application.Current.TryFindResource("CardBackground")     as Brush ?? Brushes.White;
        var cardBorder  = Application.Current.TryFindResource("BorderBrushLight")   as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;
        var primaryFg   = Application.Current.TryFindResource("PrimaryTextBrush")   as Brush ?? (Brush)new BrushConverter().ConvertFrom("#1A237E")!;
        var mutedFg     = Application.Current.TryFindResource("MutedTextBrush")     as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!;
        var dividerBrush= Application.Current.TryFindResource("DividerBrush")       as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;

        var card = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = isSelected ? (Brush)new BrushConverter().ConvertFrom("#30CE93D8")! : cardBg,
            BorderBrush = isSelected ? (Brush)new BrushConverter().ConvertFrom("#CE93D8")! : cardBorder,
            BorderThickness = new Thickness(isSelected ? 1.5 : 1),
            Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 0, 10),
        };

        var accentBar = new Rectangle
        {
            Width = 5, Fill = statusFgBrush,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Stretch,
            RadiusX = 3, RadiusY = 3
        };

        var mainGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Auto },
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Margin = new Thickness(14, 12, 14, 12)
        };

        // Checkbox
        var checkBorder = new Border
        {
            Width = 22, Height = 22,
            CornerRadius = new CornerRadius(5),
            Background = isSelected ? (Brush)new BrushConverter().ConvertFrom("#7B1FA2")! : Brushes.Transparent,
            BorderBrush = (Brush)new BrushConverter().ConvertFrom("#BDBDBD")!,
            BorderThickness = new Thickness(1.5),
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = Cursors.Hand,
        };
        if (isSelected)
            checkBorder.Child = new Path
            {
                Width = 12, Height = 12, Fill = Brushes.White, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M9 16.17L4.83 12l-1.42 1.41L9 19 21 7l-1.41-1.41z")
            };
        mainGrid.Children.Add(checkBorder);
        Grid.SetColumn(checkBorder, 0);
        Grid.SetRowSpan(checkBorder, 2);

        // Icon
        var iconBorder = new Border
        {
            Width = 44, Height = 44,
            CornerRadius = new CornerRadius(12),
            Background = statusBgBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            Child = new Path
            {
                Width = 22, Height = 22, Fill = statusFgBrush, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M20,8H4V6H20M20,18H4V16H20M9,10H4V12H9V10M8,11H6V16H8V11M3,4L3,22C3,22.55 3.45,23 4,23H13C13.55,23 14,22.55 14,22V20H20C20.55,20 21,19.55 21,19V4C21,3.45 20.55,3 20,3H4C3.45,3 3,3.45 3,4M12,21H5V4H19V18H13C12.6,18 12.3,18.4 12.3,18.9L12,21Z")
            }
        };
        mainGrid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 1);
        Grid.SetRowSpan(iconBorder, 2);

        // -- Row 0: Title + Status | Remaining --
        var topRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 4) };
        topRight.Children.Add(new TextBlock { Text = $"فاتورة مورد #{invoice.Id}", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = primaryFg });
        topRight.Children.Add(new Border { CornerRadius = new CornerRadius(5), Background = statusBgBrush, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = statusText, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = statusFgBrush } });
        mainGrid.Children.Add(topRight);
        Grid.SetColumn(topRight, 2);
        Grid.SetRow(topRight, 0);

        // Remaining badge (top-right)
        var remaining = invoice.Remaining;
        var remainingBg = remaining > 0 ? "#FFEBEE" : "#E8F5E9";
        var remainingFg = remaining > 0 ? "#C62828" : "#2E7D32";
        var amtCol = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = (Brush)new BrushConverter().ConvertFrom(remainingBg)!,
            Padding = new Thickness(12, 6, 12, 6),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = "المتبقي:", FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom(remainingFg)!, VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = $" {remaining:0.##} ج.م", FontSize = 18, FontWeight = FontWeights.ExtraBold, Foreground = (Brush)new BrushConverter().ConvertFrom(remainingFg)!, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) }
            }}
        };
        mainGrid.Children.Add(amtCol);
        Grid.SetColumn(amtCol, 3);
        Grid.SetRow(amtCol, 0);

        // -- Row 1: Amounts breakdown + Actions --
        var bottomRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };

        // Date + supplier name
        bottomRow.Children.Add(new TextBlock { Text = $"{invoice.InvoiceDate:yyyy/MM/dd} • {invoice.SupplierName ?? "بدون مورد"}", FontSize = 11, Foreground = mutedFg, VerticalAlignment = VerticalAlignment.Center });

        // Separator
        var sep = new Rectangle { Width = 1, Height = 18, Fill = dividerBrush, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        bottomRow.Children.Add(sep);

        // Total badge
        var totalBadge = new Border { CornerRadius = new CornerRadius(5), Background = (Brush)new BrushConverter().ConvertFrom("#F0F0FF")!, Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = "الإجمالي", FontSize = 9, Foreground = (Brush)new BrushConverter().ConvertFrom("#5C6BC0")!, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = $" {invoice.TotalAmount:0.##}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#283593")!, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        bottomRow.Children.Add(totalBadge);

        // Items count badge
        var itemsBadge = new Border { CornerRadius = new CornerRadius(5), Background = (Brush)new BrushConverter().ConvertFrom("#EDE7F6")!, Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = "منتجات", FontSize = 9, Foreground = (Brush)new BrushConverter().ConvertFrom("#6A1B9A")!, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = $" {invoice.Items.Count}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#4A148C")!, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        bottomRow.Children.Add(itemsBadge);

        // Paid badge
        var paidBadge = new Border { CornerRadius = new CornerRadius(5), Background = (Brush)new BrushConverter().ConvertFrom("#E8F5E9")!, Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = "مدفوع", FontSize = 9, Foreground = (Brush)new BrushConverter().ConvertFrom("#2E7D32")!, VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = $" {invoice.TotalPaid:0.##}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = invoice.TotalPaid > 0 ? (Brush)new BrushConverter().ConvertFrom("#1B5E20")! : (Brush)new BrushConverter().ConvertFrom("#90A4AE")!, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        bottomRow.Children.Add(paidBadge);

        mainGrid.Children.Add(bottomRow);
        Grid.SetColumn(bottomRow, 2);
        Grid.SetRow(bottomRow, 1);

        // Action buttons (row 1, col 3)
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

        var printBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#546E7A")!,
            Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 4, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z") },
                new TextBlock { Text = "طباعة", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
            }}
        };
        printBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; PrintInvoice(invoice); };
        actions.Children.Add(printBtn);

        // View the invoice's orders (same as details window)
        var ordersBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#00897B")!,
            Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 4, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M3 13h2v-2H3v2zm0 4h2v-2H3v2zm0-8h2V7H3v2zm4 4h14v-2H7v2zm0 4h14v-2H7v2zM7 7v2h14V7H7z") },
                new TextBlock { Text = "الطلبيات", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
            }}
        };
        ordersBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; OpenInvoice(invoice); };
        actions.Children.Add(ordersBtn);

        if (invoice.Status != InvoiceStatus.Paid && invoice.Status != InvoiceStatus.Cancelled)
        {
            // Add an order to THIS supplier invoice
            var addBtn = new Border
            {
                CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#1565C0")!,
                Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 4, 0),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
                {
                    new Path { Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z") },
                    new TextBlock { Text = "إضافة طلبية", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
                }}
            };
            addBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; AddOrderToInvoice(invoice); };
            actions.Children.Add(addBtn);
        }

        if (invoice.Status != InvoiceStatus.Paid && invoice.Status != InvoiceStatus.Cancelled)
        {
            var payBtn = new Border
            {
                CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#2E7D32")!,
                Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 4, 0),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
                {
                    new Path { Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z") },
                    new TextBlock { Text = "دفع", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
                }}
            };
            payBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; PayInvoice(invoice); };
            actions.Children.Add(payBtn);
        }

        var deleteBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#FFEBEE")!,
            Cursor = Cursors.Hand, Padding = new Thickness(8, 5, 8, 5),
            Child = new Path { Width = 14, Height = 14, Fill = (Brush)new BrushConverter().ConvertFrom("#C62828")!, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z") }
        };
        deleteBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; DeleteSupplierInvoice(invoice); };
        actions.Children.Add(deleteBtn);

        mainGrid.Children.Add(actions);
        Grid.SetColumn(actions, 3);
        Grid.SetRow(actions, 1);

        card.Child = new Grid { Children = { accentBar, mainGrid } };

        checkBorder.MouseLeftButtonDown += (_, e) => { e.Handled = true; ToggleSelection(invoice.Id); };
        card.MouseLeftButtonDown += (_, e) => OpenInvoice(invoice);
        return card;
    }

    private void ToggleSelection(int invoiceId)
    {
        if (!_selectedIds.Remove(invoiceId))
            _selectedIds.Add(invoiceId);

        UpdateBatchBar();
        ApplyFilter();
    }

    private void UpdateBatchBar()
    {
        var count = _selectedIds.Count;
        BatchBar.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtBatchCount.Text = count.ToString();
    }

    private void ShowMore_Click(object sender, MouseButtonEventArgs e)
    {
        _showAll = true;
        ApplyFilter();
    }

    private void DeleteSupplierInvoice(SupplierInvoice invoice)
    {
        ConfirmDialog.Show("نقل إلى سلة المحذوفات",
            $"هل أنت متأكد من حذف فاتورة المورد #{invoice.Id}؟\nيمكنك استعادتها لاحقاً من سلة المحذوفات.",
            result =>
            {
                if (result != true) return;

                var full = _db.SupplierInvoices.First(i => i.Id == invoice.Id);
                full.IsDeleted = true;
                full.DeletedAt = DateTime.Now;
                _db.SaveChanges();
                App.NotifyDataChanged();

                App.AppBackup?.BackupIfOnOperation();
                _selectedIds.Remove(invoice.Id);
                LoadData();
                NotificationManager.ShowSuccess($"تم نقل فاتورة المورد #{invoice.Id} إلى سلة المحذوفات");
            },
            ConfirmDialog.DialogType.Danger);
    }

    private void BatchPrint_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedIds.Count == 0) return;
        var printer = new ReceiptPrinter(_db);
        foreach (var id in _selectedIds.ToList())
        {
            var inv = _db.SupplierInvoices.FirstOrDefault(i => i.Id == id);
            if (inv != null) printer.PrintSupplierInvoice(inv);
        }
        NotificationManager.ShowSuccess($"تم طباعة {_selectedIds.Count} فاتورة");
    }

    private void BatchClear_Click(object sender, MouseButtonEventArgs e)
    {
        _selectedIds.Clear();
        UpdateBatchBar();
        ApplyFilter();
    }

    private void BtnSort_Click(object sender, MouseButtonEventArgs e)
    {
        _sortAscending = !_sortAscending;
        TxtSort.Text = _sortAscending ? "الأقدم" : "الأحدث";
        ApplyFilter();
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        SearchWatermark.Visibility = string.IsNullOrEmpty(TxtSearch.Text) ? Visibility.Visible : Visibility.Collapsed;
        _showAll = true; // show all matching results
        ApplyFilter();
    }

    private void OpenInvoice(SupplierInvoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new SupplierInvoiceDetailsDialog(_db, invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            LoadData();
        };
    }

    private void BtnPaySupplier_Click(object sender, RoutedEventArgs e)
    {
        if (_supplier == null || _isNoSupplierMode || _showAllInvoices) return;
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new PaySupplierDialog(_db, _supplier);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void BtnPrintAll_Click(object sender, RoutedEventArgs e)
    {
        var invoices = GetFiltered();
        if (invoices.Count == 0)
        {
            NotificationManager.ShowWarning("لا توجد فواتير للطباعة.");
            return;
        }
        var printer = new ReceiptPrinter(_db);
        foreach (var inv in invoices)
            printer.PrintSupplierInvoice(inv);
    }

    private void PrintInvoice(SupplierInvoice invoice)
    {
        var printer = new ReceiptPrinter(_db);
        printer.PrintSupplierInvoice(invoice);
    }

    private void PayInvoice(SupplierInvoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var fullInvoice = _db.SupplierInvoices.First(i => i.Id == invoice.Id);
        var dialog = new SupplierPaymentDialog(_db, fullInvoice);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
        mainWindow.ShowOverlay(dialog);
    }

    private void AddOrderToInvoice(SupplierInvoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var fullInvoice = _db.SupplierInvoices.First(i => i.Id == invoice.Id);
        var dialog = new StockInDialog(fullInvoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            ApplyFilter();
        };
    }

    private static readonly SolidColorBrush BlueBrush = new(Color.FromRgb(21, 101, 192));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(84, 110, 122));

    private void SetFilter(string mode)
    {
        _filterMode = mode;

        foreach (var btn in new[] { BtnUnpaid, BtnPartiallyPaid, BtnCancelled, BtnPaid, BtnAll })
            btn.Background = Brushes.Transparent;
        foreach (var txt in new[] { TxtUnpaid, TxtPartiallyPaid, TxtCancelled, TxtPaid, TxtAll })
        { txt.Foreground = GrayBrush; txt.FontWeight = FontWeights.SemiBold; }

        var activeBtn = mode switch
        {
            "PartiallyPaid" => (BtnPartiallyPaid, (TextBlock)TxtPartiallyPaid),
            "Cancelled" => (BtnCancelled, (TextBlock)TxtCancelled),
            "Paid" => (BtnPaid, (TextBlock)TxtPaid),
            "All" => (BtnAll, (TextBlock)TxtAll),
            _ => (BtnUnpaid, (TextBlock)TxtUnpaid)
        };
        activeBtn.Item1.Background = BlueBrush;
        activeBtn.Item2.Foreground = Brushes.White;
        activeBtn.Item2.FontWeight = FontWeights.Bold;

        _showAll = false;
        ApplyFilter();
    }

    private void BtnUnpaid_Click(object sender, MouseButtonEventArgs e) => SetFilter("Unpaid");
    private void BtnPartiallyPaid_Click(object sender, MouseButtonEventArgs e) => SetFilter("PartiallyPaid");
    private void BtnCancelled_Click(object sender, MouseButtonEventArgs e) => SetFilter("Cancelled");
    private void BtnPaid_Click(object sender, MouseButtonEventArgs e) => SetFilter("Paid");
    private void BtnAll_Click(object sender, MouseButtonEventArgs e) => SetFilter("All");

    private void BtnAddOrder_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockInDialog();
        if (_supplier != null && !_showAllInvoices)
            dialog.Loaded += (s2, e2) => dialog.SetSupplier(_supplier);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }
}