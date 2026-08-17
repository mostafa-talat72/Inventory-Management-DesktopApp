using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class InvoicesPage : Page
{
    private readonly AppDbContext _db;
    private readonly DispatcherTimer _searchTimer = new();
    private string _filterMode = "Unpaid";
    private bool _sortAscending;
    private int _pageSize = 20;
    private int _displayCount;
    private readonly HashSet<int> _selectedIds = new();
    private bool _showAll;
    private int _totalFiltered;

    // Supplier tab state
    private string _sFilterMode = "Unpaid";
    private bool _sShowAll;
    private int _sPageSize = 20;
    private bool _sSortAscending;
    private int _sTotalFiltered;
    private bool _supplierTabActive;
    private bool _tabsReady;
    private decimal _sTotal, _sPaid, _sRemaining;

    // Cached summary values for masking
    private decimal _cTotal, _cPaid, _cDiscount, _cRemaining;

    public InvoicesPage()
    {
        _db = new AppDbContext();
        InitializeComponent();
        _searchTimer.Interval = TimeSpan.FromMilliseconds(300);
        _searchTimer.Tick += (_, _) =>
        {
            _searchTimer.Stop();
            if (_supplierTabActive) ApplySupplierFilter();
            else ApplyFilter();
        };

        App.DataChanged += OnAppDataChanged;
        Loaded   += (_, _) =>
        {
            _tabsReady = true;
            AmountsVisibilityService.VisibilityChanged += OnVisibilityChanged;
        };
        Unloaded += (_, _) =>
        {
            App.DataChanged -= OnAppDataChanged;
            AmountsVisibilityService.VisibilityChanged -= OnVisibilityChanged;
            _db.Dispose();
        };

        LoadData();
    }

    // Theme-aware badge brush: looks up the hex key in the active theme (dark/light)
    // and falls back to the literal hex if the key is missing.
    private static Brush Res(string hex)
        => Application.Current.TryFindResource(hex) as Brush
           ?? (Brush)new BrushConverter().ConvertFrom(hex)!;

    private void OnAppDataChanged()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!_tabsReady) return;
            if (_supplierTabActive) ApplySupplierFilter();
            else ApplyFilter();
        }));
    }

    private void OnVisibilityChanged()
    {
        ApplySummaryMask();
        ApplySupplierSummaryMask();
    }

    private void ApplySupplierSummaryMask()
    {
        const string mask = "••••••";
        bool hidden = AmountsVisibilityService.IsHidden;
        TxtSupplierTotal.Text     = hidden ? mask : $"{_sTotal:0.##} ج.م";
        TxtSupplierPaid.Text      = hidden ? mask : $"{_sPaid:0.##} ج.م";
        TxtSupplierRemaining.Text = hidden ? mask : $"{_sRemaining:0.##} ج.م";
    }

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_tabsReady) return;
        _supplierTabActive = MainTabs.SelectedIndex == 1;
        if (_supplierTabActive) ApplySupplierFilter();
        else ApplyFilter();
    }

    private void ApplySummaryMask()
    {
        const string mask = "••••••";
        bool hidden = AmountsVisibilityService.IsHidden;
        TxtTotalAmount.Text     = hidden ? mask : $"{_cTotal:0.##} ج.م";
        TxtPaidAmount.Text      = hidden ? mask : $"{_cPaid:0.##} ج.م";
        TxtDiscountAmount.Text  = hidden ? mask : $"{_cDiscount:0.##} ج.م";
        TxtRemainingAmount.Text = hidden ? mask : $"{_cRemaining:0.##} ج.م";
    }

    private void LoadData()
    {
        SetFilter("Unpaid");
    }

    private IQueryable<Invoice> GetBaseQuery()
    {
        if (_filterMode == "Trash")
            return _db.Invoices.AsNoTracking().IgnoreQueryFilters()
                .Where(i => i.IsDeleted)
                .OrderByDescending(i => i.DeletedAt ?? i.CreatedAt);

        var q = _db.Invoices.AsNoTracking();

        q = _filterMode switch
        {
            "PartiallyPaid" => q.Where(i => i.Status == InvoiceStatus.PartiallyPaid),
            "Cancelled" => q.Where(i => i.Status == InvoiceStatus.Cancelled),
            "Paid" => q.Where(i => i.Status == InvoiceStatus.Paid),
            "All" => q,
            _ => q.Where(i => i.Status != InvoiceStatus.Paid)
        };

        var searchText = TxtSearch.Text.Trim();
        if (int.TryParse(searchText, out var searchId))
            q = q.Where(i => i.Id == searchId);
        else if (!string.IsNullOrEmpty(searchText))
            q = q.Where(i => i.Id.ToString().Contains(searchText)
                || (i.CustomerName != null && i.CustomerName.Contains(searchText))
                || (i.Customer != null && i.Customer.Name.Contains(searchText)));

        if (DpFromDate.SelectedDate is DateTime fromDate)
            q = q.Where(i => i.CreatedAt >= fromDate);
        if (DpToDate.SelectedDate is DateTime toDate)
            q = q.Where(i => i.CreatedAt < toDate.Date.AddDays(1));

        q = _sortAscending ? q.OrderBy(i => i.CreatedAt) : q.OrderByDescending(i => i.CreatedAt);

        return q;
    }

    private void ApplyFilter()
    {
        var query = GetBaseQuery();
        _totalFiltered = query.Count();

        var showCount = _showAll ? _totalFiltered : Math.Min(_pageSize, _totalFiltered);
        var invoices = query.Take(showCount).ToList();

        TxtInvoiceCount.Text = _totalFiltered.ToString();
        _cTotal     = invoices.Sum(i => i.TotalAmount);
        _cPaid      = invoices.Sum(i => i.TotalPaid);
        _cDiscount  = invoices.Sum(i => i.Discount);
        _cRemaining = invoices.Sum(i => i.Remaining);
        ApplySummaryMask();

        InvoicesPanel.Children.Clear();

        if (_totalFiltered == 0)
        {
            var filterLabel = _filterMode switch
            {
                "PartiallyPaid" => "مدفوعة جزئياً",
                "Cancelled" => "ملغاة",
                "Paid" => "مدفوعة",
                "All" => "",
                "Trash" => "محذوفة",
                _ => "غير مدفوعة"
            };
            var subtitle = _filterMode == "Trash"
                ? "سلة المحذوفات فارغة"
                : _filterMode == "All"
                    ? "لم يتم العثور على أي فواتير"
                    : $"لم يتم العثور على فواتير {filterLabel}";

            var emptyCardBg = Application.Current.TryFindResource("CardBackground") as Brush ?? Brushes.White;
            var emptyBorder = Application.Current.TryFindResource("BorderBrushLight") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E8E8E8")!;
            var emptyIconFg = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;
            var emptyTitleFg= Application.Current.TryFindResource("BodyTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;
            var emptySubFg  = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? Res("#90A4AE");
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
                            Data = Geometry.Parse("M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 3c1.93 0 3.5 1.57 3.5 3.5S13.93 13 12 13s-3.5-1.57-3.5-3.5S10.07 6 12 6zm7 13H5v-.23c0-.62.28-1.2.76-1.58C7.47 15.82 9.64 15 12 15s4.53.82 6.24 2.19c.48.38.76.97.76 1.58V19z")
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

        _displayCount = invoices.Count;

        foreach (var invoice in invoices)
            InvoicesPanel.Children.Add(CreateInvoiceCard(invoice));

        ShowMoreBar.Visibility = _displayCount < _totalFiltered ? Visibility.Visible : Visibility.Collapsed;
        TxtShowMore.Text = $"عرض المزيد ({_totalFiltered - _displayCount} متبقي)";
    }

    private Border CreateInvoiceCard(Invoice invoice)
    {
        var isSelected = _selectedIds.Contains(invoice.Id);
        var isTrash = _filterMode == "Trash";
        var (statusText, statusBg, statusFg) = isTrash ? ("محذوفة", "#F5F5F5", "#9E9E9E") : invoice.Status switch
        {
            InvoiceStatus.Paid => ("مدفوعة", "#E8F5E9", "#2E7D32"),
            InvoiceStatus.PartiallyPaid => ("مدفوعة جزئياً", "#FFF8E1", "#F57F17"),
            InvoiceStatus.Cancelled => ("ملغاة", "#F5F5F5", "#9E9E9E"),
            _ => ("غير مدفوعة", "#FFEBEE", "#C62828")
        };
        var statusBgBrush = Res(statusBg);
        var statusFgBrush = Res(statusFg);

        var customerLabel = invoice.CustomerName ?? "نقدي";
        var customerBadge = invoice.CustomerId == null ? Res("#FFF3E0") : Res("#E8EAF6");
        var customerFg = invoice.CustomerId == null ? Res("#E65100") : Res("#1A237E");

        // Theme-aware brushes
        var cardBg      = Application.Current.TryFindResource("CardBackground")     as Brush ?? Brushes.White;
        var cardBorder  = Application.Current.TryFindResource("BorderBrushLight")   as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;
        var surfaceBg   = Application.Current.TryFindResource("SurfaceBackground")  as Brush ?? (Brush)new BrushConverter().ConvertFrom("#F5F5F5")!;
        var headingFg   = Application.Current.TryFindResource("HeadingTextBrush")   as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
        var primaryFg   = Application.Current.TryFindResource("PrimaryTextBrush")   as Brush ?? Res("#1A237E");
        var subtleFg    = Application.Current.TryFindResource("SubtleTextBrush")    as Brush ?? (Brush)new BrushConverter().ConvertFrom("#78909C")!;
        var mutedFg     = Application.Current.TryFindResource("MutedTextBrush")     as Brush ?? Res("#90A4AE");
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

        var checkBorder = new Border
        {
            Width = 22, Height = 22,
            CornerRadius = new CornerRadius(5),
            Background = isSelected ? (Brush)new BrushConverter().ConvertFrom("#7B1FA2")! : Brushes.Transparent,
            BorderBrush = Res("#BDBDBD"),
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
        if (isTrash)
            checkBorder.Visibility = Visibility.Collapsed;
        mainGrid.Children.Add(checkBorder);
        Grid.SetColumn(checkBorder, 0);
        Grid.SetRowSpan(checkBorder, 2);

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
                Data = Geometry.Parse("M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2z")
            }
        };
        mainGrid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 1);
        Grid.SetRowSpan(iconBorder, 2);

        var topRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 4) };
        topRight.Children.Add(new TextBlock { Text = $"فاتورة #{invoice.Id}", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = primaryFg });
        topRight.Children.Add(new Border { CornerRadius = new CornerRadius(5), Background = statusBgBrush, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = statusText, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = statusFgBrush } });
        topRight.Children.Add(new Border { CornerRadius = new CornerRadius(5), Background = customerBadge, Padding = new Thickness(8, 2, 8, 2), Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = customerLabel, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = customerFg } });
        mainGrid.Children.Add(topRight);
        Grid.SetColumn(topRight, 2);
        Grid.SetRow(topRight, 0);

        var remaining = invoice.Remaining;
        var remainingBg = remaining > 0 ? "#FFEBEE" : "#E8F5E9";
        var remainingFg = remaining > 0 ? "#C62828" : "#2E7D32";
        var amtCol = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Res(remainingBg),
            Padding = new Thickness(12, 6, 12, 6),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = "المتبقي:", FontSize = 11, Foreground = Res(remainingFg), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = $" {remaining:0.##} ج.م", FontSize = 18, FontWeight = FontWeights.ExtraBold, Foreground = Res(remainingFg), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) }
            }}
        };
        mainGrid.Children.Add(amtCol);
        Grid.SetColumn(amtCol, 3);
        Grid.SetRow(amtCol, 0);

        var bottomRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };

        bottomRow.Children.Add(new TextBlock { Text = invoice.CreatedAt.ToString("yyyy/MM/dd"), FontSize = 11, Foreground = mutedFg, VerticalAlignment = VerticalAlignment.Center });

        var sep = new Rectangle { Width = 1, Height = 18, Fill = dividerBrush, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        bottomRow.Children.Add(sep);

        var totalBadge = new Border { CornerRadius = new CornerRadius(5), Background = Res("#F0F0FF"), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = "الإجمالي", FontSize = 9, Foreground = Res("#5C6BC0"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = $" {invoice.TotalAmount:0.##}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Res("#283593"), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        bottomRow.Children.Add(totalBadge);

        if (invoice.Discount > 0)
        {
            var discBadge = new Border { CornerRadius = new CornerRadius(5), Background = Res("#FFF8E1"), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
                {
                    new TextBlock { Text = "خصم", FontSize = 9, Foreground = Res("#F57F17"), VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = $" {invoice.Discount:0.##}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Res("#E65100"), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
                }}
            };
            bottomRow.Children.Add(discBadge);
        }

        var paidBadge = new Border { CornerRadius = new CornerRadius(5), Background = Res("#E8F5E9"), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = "مدفوع", FontSize = 9, Foreground = Res("#2E7D32"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = $" {invoice.TotalPaid:0.##}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = invoice.TotalPaid > 0 ? Res("#1B5E20") : Res("#90A4AE"), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        bottomRow.Children.Add(paidBadge);

        mainGrid.Children.Add(bottomRow);
        Grid.SetColumn(bottomRow, 2);
        Grid.SetRow(bottomRow, 1);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

        if (isTrash)
        {
            actions.Children.Add(CreateRestoreBtn(() => RestoreInvoice(invoice)));
            actions.Children.Add(CreatePermanentDeleteBtn(() => PermanentlyDeleteInvoice(invoice)));
        }
        else
        {
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

        // View orders of THIS invoice
        var ordersBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#00897B")!,
            Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 4, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M3 13h2v-2H3v2zm0 4h2v-2H3v2zm0-8h2V7H3v2zm4 4h14v-2H7v2zm0 4h14v-2H7v2zM7 7v2h14V7H7z") },
                new TextBlock { Text = "الطلبات", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
            }}
        };
        ordersBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; ShowOrders(invoice); };
        actions.Children.Add(ordersBtn);

        if (invoice.Status != InvoiceStatus.Paid && invoice.Status != InvoiceStatus.Cancelled)
        {
            // Add order to THIS invoice
            var addBtn = new Border
            {
                CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#1565C0")!,
                Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 4, 0),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
                {
                    new Path { Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z") },
                    new TextBlock { Text = "إضافة طلب", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
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
            CornerRadius = new CornerRadius(6), Background = Res("#FFEBEE"),
            Cursor = Cursors.Hand, Padding = new Thickness(8, 5, 8, 5),
            Child = new Path { Width = 14, Height = 14, Fill = Res("#C62828"), Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z") }
        };
        deleteBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; DeleteInvoice(invoice); };
        actions.Children.Add(deleteBtn);
        }

        mainGrid.Children.Add(actions);
        Grid.SetColumn(actions, 3);
        Grid.SetRow(actions, 1);

        card.Child = new Grid { Children = { accentBar, mainGrid } };

        if (!isTrash)
        {
            checkBorder.MouseLeftButtonDown += (_, e) => { e.Handled = true; ToggleSelection(invoice.Id); };
            card.MouseLeftButtonDown += (_, e) => OpenInvoice(invoice);
        }
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

    private void DeleteInvoice(Invoice invoice)
    {
        ConfirmDialog.Show("نقل إلى سلة المحذوفات",
            $"هل أنت متأكد من حذف الفاتورة #{invoice.Id}؟\nيمكنك استعادتها لاحقاً من سلة المحذوفات.",
            result =>
            {
                if (result != true) return;
                var full = _db.Invoices.First(i => i.Id == invoice.Id);
                full.IsDeleted = true;
                full.DeletedAt = DateTime.Now;
                _db.SaveChanges();
                _selectedIds.Remove(invoice.Id);
                App.NotifyDataChanged();
                LoadData();
                NotificationManager.ShowSuccess($"تم نقل الفاتورة #{invoice.Id} إلى سلة المحذوفات");
            },
            ConfirmDialog.DialogType.Warning);
    }

    private void RestoreInvoice(Invoice invoice)
    {
        var full = _db.Invoices.IgnoreQueryFilters().First(i => i.Id == invoice.Id);
        full.IsDeleted = false;
        full.DeletedAt = null;
        _db.SaveChanges();
        App.NotifyDataChanged();
        LoadData();
        NotificationManager.ShowSuccess($"تمت استعادة الفاتورة #{invoice.Id}");
    }

    private void PermanentlyDeleteInvoice(Invoice invoice)
    {
        ConfirmDialog.Show("حذف نهائي",
            $"هل أنت متأكد من الحذف النهائي للفاتورة #{invoice.Id}؟\nسيتم ترجيع الكميات للمخزن ولا يمكن التراجع.",
            result =>
            {
                if (result != true) return;
                var full = _db.Invoices.IgnoreQueryFilters().Include(i => i.Orders).ThenInclude(o => o.Items)
                    .Include(i => i.Payments).First(i => i.Id == invoice.Id);

                var inv = new InventoryService(_db);
                foreach (var order in full.Orders)
                {
                    foreach (var item in order.Items)
                    {
                        _db.Entry(item).Reference(oi => oi.Product).Load();
                        _db.Entry(item).Reference(oi => oi.ProductUnit).Load();
                        if (item.ProductUnit == null) continue;
                        int totalPieces = inv.CalculatePieceEquivalent(item.Product, item.CartonQuantity, item.BoxQuantity, item.PieceQuantity);
                        if (totalPieces <= 0) continue;
                        var (unitCost, totalCost) = inv.ReturnToBatches(item.ProductId, totalPieces);
                        _db.InventoryMovements.Add(new InventoryMovement
                        {
                            ProductId = item.ProductId,
                            MovementType = MovementType.Return,
                            Quantity = totalPieces,
                            CostPrice = unitCost,
                            SellingPrice = totalCost,
                            ReferenceType = ReferenceType.Return,
                            ReferenceId = full.Id,
                            Notes = $"مرتجعات بيع - فاتورة #{full.Id}"
                        });
                    }
                    _db.OrderItems.RemoveRange(order.Items);
                }
                _db.Payments.RemoveRange(full.Payments);
                _db.Orders.RemoveRange(full.Orders);
                _db.Invoices.Remove(full);
                _db.SaveChanges();
                _selectedIds.Remove(invoice.Id);
                App.NotifyDataChanged();
                LoadData();
                NotificationManager.ShowSuccess("تم الحذف النهائي للفاتورة وترجيع الكميات للمخزن");
            },
            ConfirmDialog.DialogType.Danger);
    }

    private Border CreateRestoreBtn(Action action)
    {
        var btn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = Res("#2E7D32"),
            Cursor = Cursors.Hand, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 4, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { FlowDirection = System.Windows.FlowDirection.LeftToRight, Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center,
                    Data = Geometry.Parse("M12 5V1L7 6l5 5V7c3.31 0 6 2.69 6 6s-2.69 6-6 6-6-2.69-6-6H4c0 4.42 3.58 8 8 8s8-3.58 8-8-3.58-8-8-8z") },
                new TextBlock { Text = "  استعادة", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        btn.MouseLeftButtonDown += (_, e) => { e.Handled = true; action(); };
        return btn;
    }

    private Border CreatePermanentDeleteBtn(Action action)
    {
        var btn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = Res("#C62828"),
            Cursor = Cursors.Hand, Padding = new Thickness(12, 5, 12, 5),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { FlowDirection = System.Windows.FlowDirection.LeftToRight, Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center,
                    Data = Geometry.Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z") },
                new TextBlock { Text = "  حذف نهائي", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        btn.MouseLeftButtonDown += (_, e) => { e.Handled = true; action(); };
        return btn;
    }

    private void BatchPrint_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedIds.Count == 0) return;
        var printer = new ReceiptPrinter(_db);
        foreach (var id in _selectedIds.ToList())
        {
            var inv = _db.Invoices.FirstOrDefault(i => i.Id == id);
            if (inv != null) printer.Print(inv);
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
        _showAll = true;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void DateFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        _showAll = true;
        ApplyFilter();
    }

    private void SupplierDateFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        _sShowAll = true;
        ApplySupplierFilter();
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        var invoices = GetBaseQuery().ToList();
        if (invoices.Count == 0)
        {
            MessageBox.Show("لا توجد فواتير للتصدير.", "تصدير", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"الفواتير_{DateTime.Now:yyyyMMdd}.xlsx"
        };
        if (saveDialog.ShowDialog() != true) return;

        try
        {
            string[] headers = { "رقم الفاتورة", "التاريخ", "العميل", "الحالة", "الإجمالي", "الخصم", "المدفوع", "المتبقي" };
            var rows = invoices.Select(i => new object?[]
            {
                i.Id,
                i.CreatedAt.ToString("yyyy/MM/dd HH:mm"),
                i.CustomerName ?? "نقدي",
                i.Status switch
                {
                    InvoiceStatus.Paid => "مدفوعة",
                    InvoiceStatus.PartiallyPaid => "مدفوعة جزئياً",
                    InvoiceStatus.Cancelled => "ملغاة",
                    _ => "غير مدفوعة"
                },
                i.TotalAmount, i.Discount, i.TotalPaid, i.Remaining
            }).ToList();

            ExcelExportService.Export(saveDialog.FileName, headers, rows);
            NotificationManager.ShowSuccess("تم تصدير الفواتير إلى Excel بنجاح");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"حدث خطأ أثناء التصدير:\n{ex.Message}", "تصدير", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenInvoice(Invoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new InvoiceDetailsDialog(_db, invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            LoadData();
        };
    }

    private void OpenNewInvoice(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        if (mainWindow == null) return;

        var db = new AppDbContext();

        if (!db.Customers.Any())
        {
            HandleCustomerSelected(db, mainWindow, null);
            return;
        }

        var dialog = new SelectCustomerDialog(db);
        mainWindow.ShowOverlay(dialog);
        dialog.CustomerSelected += (_, customer) =>
        {
            HandleCustomerSelected(db, mainWindow, customer);
        };
    }

    private void HandleCustomerSelected(AppDbContext db, MainWindow mainWindow, Customer? customer)
    {
        var unpaidInvoices = db.Invoices
            .Where(i => (customer == null ? i.CustomerId == null : i.CustomerId == customer.Id)
                && (i.Status == InvoiceStatus.Open || i.Status == InvoiceStatus.PartiallyPaid))
            .OrderByDescending(i => i.Id)
            .ToList();

        if (unpaidInvoices.Count > 0)
        {
            var dialog = new SelectInvoiceDialog(db, customer);
            mainWindow.ShowOverlay(dialog);
            dialog.InvoiceSelected += (invoice) =>
            {
                OpenAddOrder(db, mainWindow, customer, invoice);
            };
        }
        else
        {
            OpenAddOrder(db, mainWindow, customer, null);
        }
    }

    private void OpenAddOrder(AppDbContext db, MainWindow mainWindow, Customer? customer, Invoice? invoice)
    {
        var isNew = false;
        if (invoice == null)
        {
            invoice = new Invoice
            {
                CustomerId = customer?.Id,
                CustomerName = customer?.Name ?? "نقدي",
                CreatedAt = DateTime.Now,
                Status = InvoiceStatus.Open
            };
            db.Invoices.Add(invoice);
            db.SaveChanges();
            isNew = true;
        }
        var addOrder = new AddOrderDialog(db, invoice);
        mainWindow.ShowOverlay(addOrder);
        addOrder.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (isNew && r != true)
            {
                db.Entry(invoice).Collection(i => i.Orders).Load();
                if (!invoice.Orders.Any())
                {
                    db.Invoices.Remove(invoice);
                    db.SaveChanges();
                }
            }
            db.Dispose();
            LoadData();
        };
    }

    private void ShowOrders(Invoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ManageOrdersDialog(_db, invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void AddOrderToInvoice(Invoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new AddOrderDialog(_db, invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            LoadData();
        };
    }

    private void PrintInvoice(Invoice invoice)
    {
        var printer = new ReceiptPrinter(_db);
        printer.Print(invoice);
    }

    private void PayInvoice(Invoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var fullInvoice = _db.Invoices.First(i => i.Id == invoice.Id);
        var dialog = new ConfirmPaymentDialog(_db, fullInvoice);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            LoadData();
        };
        mainWindow.ShowOverlay(dialog);
    }

    private static readonly SolidColorBrush BlueBrush = new(Color.FromRgb(21, 101, 192));
    private static readonly SolidColorBrush GrayBrush = new(Color.FromRgb(84, 110, 122));

    private void SetFilter(string mode)
    {
        _filterMode = mode;

        foreach (var btn in new[] { BtnUnpaid, BtnPartiallyPaid, BtnCancelled, BtnPaid, BtnAll, BtnTrash })
            btn.Background = Brushes.Transparent;
        foreach (var txt in new[] { TxtUnpaid, TxtPartiallyPaid, TxtCancelled, TxtPaid, TxtAll, TxtTrash })
        { txt.Foreground = GrayBrush; txt.FontWeight = FontWeights.SemiBold; }

        var activeBtn = mode switch
        {
            "PartiallyPaid" => (BtnPartiallyPaid, (TextBlock)TxtPartiallyPaid),
            "Cancelled" => (BtnCancelled, (TextBlock)TxtCancelled),
            "Paid" => (BtnPaid, (TextBlock)TxtPaid),
            "All" => (BtnAll, (TextBlock)TxtAll),
            "Trash" => (BtnTrash, (TextBlock)TxtTrash),
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
    private void BtnTrash_Click(object sender, MouseButtonEventArgs e) => SetFilter("Trash");

    // ══════════ فواتير الموردين ══════════

    private IQueryable<SupplierInvoice> GetBaseSupplierQuery()
    {
        if (_sFilterMode == "Trash")
            return _db.SupplierInvoices.AsNoTracking().IgnoreQueryFilters()
                .Where(i => i.IsDeleted)
                .OrderByDescending(i => i.DeletedAt ?? i.CreatedAt);

        var q = _db.SupplierInvoices.AsNoTracking();

        q = _sFilterMode switch
        {
            "PartiallyPaid" => q.Where(i => i.Status == InvoiceStatus.PartiallyPaid),
            "Cancelled" => q.Where(i => i.Status == InvoiceStatus.Cancelled),
            "Paid" => q.Where(i => i.Status == InvoiceStatus.Paid),
            "All" => q,
            _ => q.Where(i => i.Status != InvoiceStatus.Paid)
        };

        var searchText = TxtSupplierSearch.Text.Trim();
        if (int.TryParse(searchText, out var searchId))
            q = q.Where(i => i.Id == searchId);
        else if (!string.IsNullOrEmpty(searchText))
            q = q.Where(i => i.SupplierName != null && i.SupplierName.Contains(searchText));

        if (SDpFromDate.SelectedDate is DateTime fromDate)
            q = q.Where(i => i.CreatedAt >= fromDate);
        if (SDpToDate.SelectedDate is DateTime toDate)
            q = q.Where(i => i.CreatedAt < toDate.Date.AddDays(1));

        q = _sSortAscending
            ? q.OrderBy(i => i.CreatedAt)
            : q.OrderByDescending(i => i.CreatedAt);

        return q;
    }

    private void ApplySupplierFilter()
    {
        var query = GetBaseSupplierQuery();
        _sTotalFiltered = query.Count();

        var showCount = _sShowAll ? _sTotalFiltered : Math.Min(_sPageSize, _sTotalFiltered);
        var invoices = query.Take(showCount).ToList();

        TxtSupplierCount.Text = _sTotalFiltered.ToString();
        _sTotal     = invoices.Sum(i => i.TotalAmount);
        _sPaid      = invoices.Sum(i => i.TotalPaid);
        _sRemaining = invoices.Sum(i => i.Remaining);
        ApplySupplierSummaryMask();

        SupplierInvoicesPanel.Children.Clear();

        if (_sTotalFiltered == 0)
        {
            var cardBg   = Application.Current.TryFindResource("CardBackground") as Brush ?? Brushes.White;
            var borderB  = Application.Current.TryFindResource("BorderBrushLight") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E8E8E8")!;
            var iconFg   = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;
            var titleFg  = Application.Current.TryFindResource("BodyTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;
            SupplierInvoicesPanel.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = cardBg,
                BorderBrush = borderB,
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
                            Fill = iconFg,
                            Stretch = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Data = Geometry.Parse("M20,8H4V6H20M20,18H4V16H20M9,10H4V12H9V10M8,11H6V16H8V11M15,9.7C15,11 13.6,12 11.8,12C9.9,12 8.5,11 8.5,9.7C8.5,9.5 8.6,8.8 8.7,8.2C7.6,8 6.7,7.9 6.7,7.9C8.7,5.3 12.1,5 12.6,6.1C13.1,7.2 11.5,8.1 11.5,8.1C10.7,8.6 9.2,9.4 9.2,10C9.2,10.4 10.2,10.9 11.8,10.9C13.7,10.9 15,10.2 15,9.7M10.5,9C10.8,9.1 11,8.9 10.9,8.6C10.7,8.3 10.3,8.1 10,8.1C9.8,8.1 9.6,8.2 9.6,8.4C9.7,8.7 10.2,8.9 10.5,9M3,4L3,22C3,22.55 3.45,23 4,23H13C13.55,23 14,22.55 14,22V20H20C20.55,20 21,19.55 21,19V4C21,3.45 20.55,3 20,3H4C3.45,3 3,3.45 3,4M12,21H5V4H19V18H13C12.6,18 12.3,18.4 12.3,18.9L12,21Z")
                        },
                        new TextBlock
                        {
                            Text = "لا توجد فواتير موردين",
                            FontSize = 18,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = titleFg,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 16, 0, 4)
                        },
                        new TextBlock
                        {
                            Text = _sFilterMode == "Trash" ? "سلة المحذوفات فارغة" : "لم يتم العثور على فواتير مطابقة",
                            FontSize = 13,
                            Foreground = iconFg,
                            HorizontalAlignment = HorizontalAlignment.Center
                        }
                    }
                }
            });
            SupplierShowMoreBar.Visibility = Visibility.Collapsed;
            return;
        }

        foreach (var invoice in invoices)
            SupplierInvoicesPanel.Children.Add(CreateSupplierCard(invoice));

        var displayCount = invoices.Count;
        SupplierShowMoreBar.Visibility = displayCount < _sTotalFiltered ? Visibility.Visible : Visibility.Collapsed;
        TxtSupplierShowMore.Text = $"عرض المزيد ({_sTotalFiltered - displayCount} متبقي)";
    }

    private Border CreateSupplierCard(SupplierInvoice invoice)
    {
        var isTrash = _sFilterMode == "Trash";
        var (statusText, statusBg, statusFg) = isTrash ? ("محذوفة", "#F5F5F5", "#9E9E9E") : invoice.Status switch
        {
            InvoiceStatus.Paid => ("مدفوعة", "#E8F5E9", "#2E7D32"),
            InvoiceStatus.PartiallyPaid => ("مدفوعة جزئياً", "#FFF8E1", "#F57F17"),
            InvoiceStatus.Cancelled => ("ملغاة", "#F5F5F5", "#9E9E9E"),
            _ => ("غير مدفوعة", "#FFEBEE", "#C62828")
        };
        var statusBgBrush = Res(statusBg);
        var statusFgBrush = Res(statusFg);

        var cardBg      = Application.Current.TryFindResource("CardBackground")     as Brush ?? Brushes.White;
        var cardBorder  = Application.Current.TryFindResource("BorderBrushLight")   as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;
        var primaryFg   = Application.Current.TryFindResource("PrimaryTextBrush")   as Brush ?? (Brush)new BrushConverter().ConvertFrom("#004D40")!;
        var mutedFg     = Application.Current.TryFindResource("MutedTextBrush")     as Brush ?? Res("#90A4AE");
        var dividerBrush= Application.Current.TryFindResource("DividerBrush")       as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!;

        var card = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = cardBg,
            BorderBrush = cardBorder,
            BorderThickness = new Thickness(1),
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
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Margin = new Thickness(14, 12, 14, 12)
        };

        var iconBorder = new Border
        {
            Width = 44, Height = 44,
            CornerRadius = new CornerRadius(12),
            Background = statusBgBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Width = 22, Height = 22, Fill = statusFgBrush, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M20,8H4V6H20M20,18H4V16H20M9,10H4V12H9V10M8,11H6V16H8V11M15,9.7C15,11 13.6,12 11.8,12C9.9,12 8.5,11 8.5,9.7C8.5,9.5 8.6,8.8 8.7,8.2C7.6,8 6.7,7.9 6.7,7.9C8.7,5.3 12.1,5 12.6,6.1C13.1,7.2 11.5,8.1 11.5,8.1C10.7,8.6 9.2,9.4 9.2,10C9.2,10.4 10.2,10.9 11.8,10.9C13.7,10.9 15,10.2 15,9.7M10.5,9C10.8,9.1 11,8.9 10.9,8.6C10.7,8.3 10.3,8.1 10,8.1C9.8,8.1 9.6,8.2 9.6,8.4C9.7,8.7 10.2,8.9 10.5,9M3,4L3,22C3,22.55 3.45,23 4,23H13C13.55,23 14,22.55 14,22V20H20C20.55,20 21,19.55 21,19V4C21,3.45 20.55,3 20,3H4C3.45,3 3,3.45 3,4M12,21H5V4H19V18H13C12.6,18 12.3,18.4 12.3,18.9L12,21Z")
            }
        };
        mainGrid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 0);
        Grid.SetRowSpan(iconBorder, 2);

        var topRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 4) };
        topRight.Children.Add(new TextBlock { Text = $"فاتورة مورد #{invoice.Id}", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = primaryFg });
        topRight.Children.Add(new Border { CornerRadius = new CornerRadius(5), Background = statusBgBrush, Padding = new Thickness(10, 2, 10, 2), Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = statusText, FontSize = 10, FontWeight = FontWeights.Bold, Foreground = statusFgBrush } });
        mainGrid.Children.Add(topRight);
        Grid.SetColumn(topRight, 1);
        Grid.SetRow(topRight, 0);

        var remaining = invoice.Remaining;
        var remainingBg = remaining > 0 ? "#FFEBEE" : "#E8F5E9";
        var remainingFg = remaining > 0 ? "#C62828" : "#2E7D32";
        var amtCol = new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = Res(remainingBg),
            Padding = new Thickness(12, 6, 12, 6),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = "المتبقي:", FontSize = 11, Foreground = Res(remainingFg), VerticalAlignment = VerticalAlignment.Center, FontWeight = FontWeights.SemiBold },
                new TextBlock { Text = $" {remaining:0.##} ج.م", FontSize = 18, FontWeight = FontWeights.ExtraBold, Foreground = Res(remainingFg), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) }
            }}
        };
        mainGrid.Children.Add(amtCol);
        Grid.SetColumn(amtCol, 2);
        Grid.SetRow(amtCol, 0);

        var bottomRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };

        bottomRow.Children.Add(new TextBlock { Text = invoice.CreatedAt.ToString("yyyy/MM/dd"), FontSize = 11, Foreground = mutedFg, VerticalAlignment = VerticalAlignment.Center });

        var sep = new Rectangle { Width = 1, Height = 18, Fill = dividerBrush, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
        bottomRow.Children.Add(sep);

        var supplierLabel = new Border { CornerRadius = new CornerRadius(5), Background = Res("#E0F2F1"), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
            Child = new TextBlock { Text = invoice.SupplierName ?? "بدون مورد", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Res("#00695C") } };
        bottomRow.Children.Add(supplierLabel);

        var totalBadge = new Border { CornerRadius = new CornerRadius(5), Background = Res("#F0F0FF"), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = "الإجمالي", FontSize = 9, Foreground = Res("#5C6BC0"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = $" {invoice.TotalAmount:0.##}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Res("#283593"), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        bottomRow.Children.Add(totalBadge);

        var paidBadge = new Border { CornerRadius = new CornerRadius(5), Background = Res("#E8F5E9"), Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center,
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new TextBlock { Text = "مدفوع", FontSize = 9, Foreground = Res("#2E7D32"), VerticalAlignment = VerticalAlignment.Center },
                new TextBlock { Text = $" {invoice.TotalPaid:0.##}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = invoice.TotalPaid > 0 ? Res("#1B5E20") : Res("#90A4AE"), Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
            }}
        };
        bottomRow.Children.Add(paidBadge);

        mainGrid.Children.Add(bottomRow);
        Grid.SetColumn(bottomRow, 1);
        Grid.SetRow(bottomRow, 1);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };

        if (isTrash)
        {
            actions.Children.Add(CreateRestoreBtn(() => RestoreSupplierInvoice(invoice)));
            actions.Children.Add(CreatePermanentDeleteBtn(() => PermanentlyDeleteSupplierInvoice(invoice)));
        }
        else
        {
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
        ordersBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; OpenSupplierInvoice(invoice); };
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
            addBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; AddSupplierOrder(invoice); };
            actions.Children.Add(addBtn);
        }

        var printBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#546E7A")!,
            Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 4, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { Width = 14, Height = 14, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M19 8H5c-1.66 0-3 1.34-3 3v6h4v4h12v-4h4v-6c0-1.66-1.34-3-3-3zm-3 11H8v-5h8v5zm3-7c-.55 0-1-.45-1-1s.45-1 1-1 1 .45 1 1-.45 1-1 1zm-1-9H6v4h12V3z") },
                new TextBlock { Text = "طباعة", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
            }}
        };
        printBtn.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            var printer = new ReceiptPrinter(_db);
            printer.PrintSupplierInvoice(invoice);
        };
        actions.Children.Add(printBtn);

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
            payBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; PaySupplierInvoice(invoice); };
            actions.Children.Add(payBtn);
        }

        var deleteBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = Res("#FFEBEE"),
            Cursor = Cursors.Hand, Padding = new Thickness(8, 5, 8, 5),
            Child = new Path { Width = 14, Height = 14, Fill = Res("#C62828"), Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z") }
        };
        deleteBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; DeleteSupplierInvoice(invoice); };
        actions.Children.Add(deleteBtn);
        }

        mainGrid.Children.Add(actions);
        Grid.SetColumn(actions, 2);
        Grid.SetRow(actions, 1);

        card.Child = new Grid { Children = { accentBar, mainGrid } };

        if (!isTrash)
            card.MouseLeftButtonDown += (_, e) => OpenSupplierInvoice(invoice);
        return card;
    }

    private void OpenSupplierInvoice(SupplierInvoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new SupplierInvoiceDetailsDialog(_db, invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            ApplySupplierFilter();
        };
    }

    private void AddSupplierOrder(SupplierInvoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockInDialog(invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            ApplySupplierFilter();
        };
    }

    private void PaySupplierInvoice(SupplierInvoice invoice)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new SupplierPaymentDialog(_db, invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            ApplySupplierFilter();
        };
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
                NotificationManager.ShowSuccess($"تم نقل فاتورة المورد #{invoice.Id} إلى سلة المحذوفات");
                ApplySupplierFilter();
            },
            ConfirmDialog.DialogType.Warning);
    }

    private void RestoreSupplierInvoice(SupplierInvoice invoice)
    {
        var full = _db.SupplierInvoices.IgnoreQueryFilters().First(i => i.Id == invoice.Id);
        full.IsDeleted = false;
        full.DeletedAt = null;
        _db.SaveChanges();
        App.NotifyDataChanged();
        NotificationManager.ShowSuccess($"تمت استعادة فاتورة المورد #{invoice.Id}");
        ApplySupplierFilter();
    }

    private void PermanentlyDeleteSupplierInvoice(SupplierInvoice invoice)
    {
        ConfirmDialog.Show("حذف نهائي",
            $"هل أنت متأكد من الحذف النهائي لفاتورة المورد #{invoice.Id}؟\nسيتم خصم الكميات من المخزون ولا يمكن التراجع.",
            result =>
            {
                if (result != true) return;

                var full = _db.SupplierInvoices.IgnoreQueryFilters()
                    .Include(i => i.Items).ThenInclude(i => i.Product)
                    .Include(i => i.Payments)
                    .First(i => i.Id == invoice.Id);

                var inv = new InventoryService(_db);
                foreach (var item in full.Items)
                {
                    int totalPieces = inv.CalculatePieceEquivalent(item.Product, item.CartonQuantity, item.BoxQuantity, item.PieceQuantity);
                    if (totalPieces <= 0) continue;

                    var (fifoCost, consumed) = inv.CalculateFifoCost(item.Product, totalPieces);
                    _db.InventoryMovements.Add(new InventoryMovement
                    {
                        ProductId = item.ProductId,
                        MovementType = MovementType.StockOut,
                        Quantity = totalPieces,
                        CostPrice = totalPieces > 0 ? fifoCost / totalPieces : 0,
                        ReferenceType = ReferenceType.Adjustment,
                        ReferenceId = full.Id,
                        Notes = $"حذف فاتورة مورد #{full.Id}"
                    });

                    foreach (var batch in consumed)
                        _db.Entry(batch).State = EntityState.Modified;
                }

                _db.SupplierInvoiceItems.RemoveRange(full.Items);
                _db.SupplierPayments.RemoveRange(full.Payments);
                _db.SupplierInvoices.Remove(full);
                _db.SaveChanges();
                App.NotifyDataChanged();

                App.AppBackup?.BackupIfOnOperation();
                NotificationManager.ShowSuccess("تم الحذف النهائي للفاتورة وخصم الكميات من المخزون");
                ApplySupplierFilter();
            },
            ConfirmDialog.DialogType.Danger);
    }

    private void SetSupplierFilter(string mode)
    {
        _sFilterMode = mode;

        foreach (var btn in new[] { SBtnUnpaid, SBtnPartiallyPaid, SBtnCancelled, SBtnPaid, SBtnAll, SBtnTrash })
            btn.Background = Brushes.Transparent;
        foreach (var txt in new[] { STxtUnpaid, STxtPartiallyPaid, STxtCancelled, STxtPaid, STxtAll, STxtTrash })
        { txt.Foreground = GrayBrush; txt.FontWeight = FontWeights.SemiBold; }

        var activeBtn = mode switch
        {
            "PartiallyPaid" => (SBtnPartiallyPaid, (TextBlock)STxtPartiallyPaid),
            "Cancelled" => (SBtnCancelled, (TextBlock)STxtCancelled),
            "Paid" => (SBtnPaid, (TextBlock)STxtPaid),
            "All" => (SBtnAll, (TextBlock)STxtAll),
            "Trash" => (SBtnTrash, (TextBlock)STxtTrash),
            _ => (SBtnUnpaid, (TextBlock)STxtUnpaid)
        };
        activeBtn.Item1.Background = BlueBrush;
        activeBtn.Item2.Foreground = Brushes.White;
        activeBtn.Item2.FontWeight = FontWeights.Bold;

        _sShowAll = false;
        ApplySupplierFilter();
    }

    private void SBtnUnpaid_Click(object sender, MouseButtonEventArgs e) => SetSupplierFilter("Unpaid");
    private void SBtnPartiallyPaid_Click(object sender, MouseButtonEventArgs e) => SetSupplierFilter("PartiallyPaid");
    private void SBtnCancelled_Click(object sender, MouseButtonEventArgs e) => SetSupplierFilter("Cancelled");
    private void SBtnPaid_Click(object sender, MouseButtonEventArgs e) => SetSupplierFilter("Paid");
    private void SBtnAll_Click(object sender, MouseButtonEventArgs e) => SetSupplierFilter("All");
    private void SBtnTrash_Click(object sender, MouseButtonEventArgs e) => SetSupplierFilter("Trash");

    private void SBtnSort_Click(object sender, MouseButtonEventArgs e)
    {
        _sSortAscending = !_sSortAscending;
        STxtSort.Text = _sSortAscending ? "الأقدم" : "الأحدث";
        ApplySupplierFilter();
    }

    private void SupplierShowMore_Click(object sender, MouseButtonEventArgs e)
    {
        _sShowAll = true;
        ApplySupplierFilter();
    }

    private void TxtSupplierSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        _sShowAll = true;
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private void OpenSupplierOrder(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockInDialog();
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            ApplySupplierFilter();
        };
    }
}
