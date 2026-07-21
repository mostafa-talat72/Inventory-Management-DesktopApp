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

public partial class CustomerInvoicesDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Customer? _customer;
    private readonly bool _isCashMode;
    private List<Invoice> _allInvoices = new();
    private string _filterMode = "Unpaid";
    private bool _sortAscending;
    private string _sortMode = "date";
    private int _pageSize = 20;
    private int _displayCount;
    private readonly HashSet<int> _selectedIds = new();
    private bool _showAll;

    public CustomerInvoicesDialog(AppDbContext db, Customer customer) : this(db, customer, false) { }

    // All-invoices mode: shows every invoice (all customers + cash)
    public CustomerInvoicesDialog(AppDbContext db, bool showAll)
    {
        InitializeComponent();
        _db = db;
        _customer = null;
        _isCashMode = false;
        _showAllInvoices = true;
        TxtTitle.Text = "كل الفواتير";
        TxtSubtitle.Text = "جميع فواتير العملاء والنقدي";
        TxtSearch.Focus();
        BtnAddOrder.Visibility = Visibility.Collapsed;
        BtnManageOrders.Visibility = Visibility.Collapsed;
        BtnMerge.Visibility = Visibility.Collapsed;
        LoadData();
    }

    public CustomerInvoicesDialog(AppDbContext db)
    {
        InitializeComponent();
        _db = db;
        _customer = null;
        _isCashMode = true;
        TxtTitle.Text = "فواتير نقدي";
        TxtSubtitle.Text = "الفواتير النقدية (بدون عميل)";
        TxtSearch.Focus();
        LoadData();
    }

    private CustomerInvoicesDialog(AppDbContext db, Customer customer, bool _)
    {
        InitializeComponent();
        _db = db;
        _customer = customer;
        _isCashMode = false;
        TxtTitle.Text = $"فواتير - {customer.Name}";
        TxtSubtitle.Text = customer.Phone ?? "لا يوجد رقم هاتف";
        LoadData();
    }

    private readonly bool _showAllInvoices;

    private void LoadData()
    {
        if (_showAllInvoices)
            _allInvoices = _db.Invoices.OrderByDescending(i => i.CreatedAt).ToList();
        else
            _allInvoices = _db.Invoices
                .Where(i => _isCashMode ? i.CustomerId == null : i.CustomerId == _customer!.Id)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();

        SetFilter("Unpaid");
        UpdateManageOrdersVisibility();
    }

    private List<Invoice> GetFiltered()
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

        // Search by invoice ID
        var searchText = TxtSearch.Text.Trim();
        if (int.TryParse(searchText, out var searchId))
            q = q.Where(i => i.Id == searchId);
        else if (!string.IsNullOrEmpty(searchText))
            q = q.Where(i => i.Id.ToString().Contains(searchText));

        // Sort
        if (_sortMode == "amount")
            q = _sortAscending ? q.OrderBy(i => i.TotalAmount) : q.OrderByDescending(i => i.TotalAmount);
        else
            q = _sortAscending ? q.OrderBy(i => i.CreatedAt) : q.OrderByDescending(i => i.CreatedAt);

        return q.ToList();
    }

    private void ApplyFilter()
    {
        var filtered = GetFiltered();
        var totalFiltered = filtered.Count;

        TxtInvoiceCount.Text = totalFiltered.ToString();
        TxtTotalAmount.Text = $"{filtered.Sum(i => i.TotalAmount):0.##} ج.م";
        TxtPaidAmount.Text = $"{filtered.Sum(i => i.TotalPaid):0.##} ج.م";
        TxtRemainingAmount.Text = $"{filtered.Sum(i => i.Remaining):0.##} ج.م";

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
                ? "لم يتم العثور على أي فواتير"
                : $"لم يتم العثور على فواتير {filterLabel}";

            InvoicesPanel.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = Brushes.White,
                BorderBrush = (Brush)new BrushConverter().ConvertFrom("#E8E8E8")!,
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
                            Fill = (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!,
                            Stretch = Stretch.Uniform,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Data = Geometry.Parse("M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm-7 3c1.93 0 3.5 1.57 3.5 3.5S13.93 13 12 13s-3.5-1.57-3.5-3.5S10.07 6 12 6zm7 13H5v-.23c0-.62.28-1.2.76-1.58C7.47 15.82 9.64 15 12 15s4.53.82 6.24 2.19c.48.38.76.97.76 1.58V19z")
                        },
                        new TextBlock
                        {
                            Text = "لا توجد فواتير",
                            FontSize = 18,
                            FontWeight = FontWeights.SemiBold,
                            Foreground = (Brush)new BrushConverter().ConvertFrom("#546E7A")!,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 16, 0, 4)
                        },
                        new TextBlock
                        {
                            Text = subtitle,
                            FontSize = 13,
                            Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
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
            InvoicesPanel.Children.Add(CreateInvoiceCard(invoice));

        ShowMoreBar.Visibility = _displayCount < totalFiltered ? Visibility.Visible : Visibility.Collapsed;
        TxtShowMore.Text = $"عرض المزيد ({totalFiltered - _displayCount} متبقي)";
    }

    private Border CreateInvoiceCard(Invoice invoice)
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

        var card = new Border
        {
            CornerRadius = new CornerRadius(14),
            Background = isSelected ? (Brush)new BrushConverter().ConvertFrom("#F3E5F5")! : Brushes.White,
            BorderBrush = isSelected ? (Brush)new BrushConverter().ConvertFrom("#CE93D8")! : (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!,
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
                Data = Geometry.Parse("M19 3H5c-1.1 0-2 .9-2 2v14c0 1.1.9 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2z")
            }
        };
        mainGrid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 1);
        Grid.SetRowSpan(iconBorder, 2);

        // -- Row 0: Title + Status | Remaining --
        var topRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 4) };
        topRight.Children.Add(new TextBlock { Text = $"فاتورة #{invoice.Id}", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#1A237E")! });
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

        // Date
        bottomRow.Children.Add(new TextBlock { Text = invoice.CreatedAt.ToString("yyyy/MM/dd"), FontSize = 11, Foreground = (Brush)new BrushConverter().ConvertFrom("#90A4AE")!, VerticalAlignment = VerticalAlignment.Center });

        // Separator
        var sep = new Rectangle { Width = 1, Height = 18, Fill = (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!, Margin = new Thickness(10, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
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

        // Discount badge
        if (invoice.Discount > 0)
        {
            var discBadge = new Border { CornerRadius = new CornerRadius(5), Background = (Brush)new BrushConverter().ConvertFrom("#FFF8E1")!, Padding = new Thickness(8, 3, 8, 3), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
                {
                    new TextBlock { Text = "خصم", FontSize = 9, Foreground = (Brush)new BrushConverter().ConvertFrom("#F57F17")!, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = $" {invoice.Discount:0.##}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#E65100")!, Margin = new Thickness(4, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }
                }}
            };
            bottomRow.Children.Add(discBadge);
        }

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
        deleteBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; DeleteInvoice(invoice); };
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

    private void DeleteInvoice(Invoice invoice)
    {
        ConfirmDialog.Show("حذف الفاتورة",
            $"هل أنت متأكد من حذف الفاتورة #{invoice.Id}؟\nلا يمكن التراجع عن هذا الإجراء.",
            result =>
            {
                if (result != true) return;
                var full = _db.Invoices.Include(i => i.Orders).ThenInclude(o => o.Items)
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
                        var batch = _db.InventoryBatches
                            .Where(b => b.ProductId == item.ProductId && b.RemainingQuantity > 0)
                            .OrderByDescending(b => b.PurchaseDate)
                            .FirstOrDefault();
                        if (batch != null)
                            batch.RemainingQuantity += totalPieces;
                        else
                        {
                            _db.InventoryBatches.Add(new InventoryBatch
                            {
                                ProductId = item.ProductId,
                                CostPricePerPiece = item.CostPrice / totalPieces,
                                InitialQuantity = totalPieces,
                                RemainingQuantity = totalPieces,
                                PurchaseDate = DateTime.Now
                            });
                        }
                        _db.InventoryMovements.Add(new InventoryMovement
                        {
                            ProductId = item.ProductId,
                            MovementType = MovementType.Return,
                            Quantity = totalPieces,
                            CostPrice = item.CostPrice / totalPieces,
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
                LoadData();
                NotificationManager.ShowSuccess("تم حذف الفاتورة وترجيع الكميات للمخزن");
            },
            ConfirmDialog.DialogType.Warning);
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
        _showAll = true; // show all matching results
        ApplyFilter();
    }

    // ----- Existing methods below (unchanged) -----

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
            if (r == true) LoadData();
        };
        mainWindow.ShowOverlay(dialog);
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

    private Invoice? GetOpenInvoice()
    {
        if (_showAllInvoices) return null;
        if (_customer == null && !_isCashMode) return null;
        return _db.Invoices
            .Where(i => (_isCashMode ? i.CustomerId == null : i.CustomerId == _customer!.Id)
                && i.Status != InvoiceStatus.Paid
                && i.Status != InvoiceStatus.Cancelled)
            .OrderByDescending(i => i.CreatedAt)
            .FirstOrDefault();
    }

    private void UpdateManageOrdersVisibility()
    {
        if (_showAllInvoices) { BtnManageOrders.Visibility = Visibility.Collapsed; return; }
        BtnManageOrders.Visibility = GetOpenInvoice() != null ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnAddOrder_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new AddOrderDialog(_db, _customer);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void BtnManageOrders_Click(object sender, RoutedEventArgs e)
    {
        var invoice = GetOpenInvoice();
        if (invoice == null) return;
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ManageOrdersDialog(_db, invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void BtnMerge_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new MergeInvoicesDialog(_db, _customer, _isCashMode);
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
