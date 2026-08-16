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

public partial class DebtsPage : Page
{
    private readonly AppDbContext _db;
    private string _filterMode = "All";

    public DebtsPage()
    {
        InitializeComponent();
        _db = new AppDbContext();

        App.DataChanged += OnAppDataChanged;
        AmountsVisibilityService.VisibilityChanged += OnVisibilityChanged;

        LoadData();
    }

    private void OnAppDataChanged() => LoadData();

    private void OnVisibilityChanged() => ApplyMasks();

    private void LoadData()
    {
        var debts = _db.Debts.AsNoTracking().ToList();

        decimal onMeRemaining = debts.Where(d => d.Direction == DebtDirection.OnMe).Sum(d => d.Remaining);
        decimal forMeRemaining = debts.Where(d => d.Direction == DebtDirection.ForMe).Sum(d => d.Remaining);

        TxtOnMeCount.Text = $"{debts.Count(d => d.Direction == DebtDirection.OnMe)} دين";
        TxtForMeCount.Text = $"{debts.Count(d => d.Direction == DebtDirection.ForMe)} دين";

        _onMeRemaining = onMeRemaining;
        _forMeRemaining = forMeRemaining;

        // ديوني للموردين = المتبقي غير المدفوع على فواتير الموردين
        var supplierInvoices = _db.SupplierInvoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled)
            .ToList();
        _supplierDebt = supplierInvoices.Sum(i => i.Remaining);
        TxtSupplierDebtDesc2.Text = $"{supplierInvoices.Count} فاتورة مورد غير مدفوعة";

        // دين العملاء لي = المتبقي غير المدفوع على فواتير العملاء
        var customerInvoices = _db.Invoices.AsNoTracking()
            .Where(i => i.Status != InvoiceStatus.Paid && i.Status != InvoiceStatus.Cancelled)
            .ToList();
        _customerDebt = customerInvoices.Sum(i => i.Remaining);
        TxtCustomerDebtDesc2.Text = $"{customerInvoices.Count} فاتورة عميل غير مدفوعة";

        // المتبقي الكلي = (لي من الديون + العملاء) − (عليا من الديون + الموردين)
        _netRemaining = (forMeRemaining + _customerDebt) - (onMeRemaining + _supplierDebt);

        BuildAccountCards();

        ApplyMasks();
        SetFilter(_filterMode);
    }

    private decimal _onMeRemaining, _forMeRemaining, _netRemaining, _supplierDebt, _customerDebt;

    private void ApplyMasks()
    {
        const string mask = "••••••";
        bool hidden = AmountsVisibilityService.IsHidden;

        TxtOnMeRemaining.Text = hidden ? mask : $"{_onMeRemaining:0.##} ج.م";
        TxtForMeRemaining.Text = hidden ? mask : $"{_forMeRemaining:0.##} ج.م";
        TxtSupplierDebt.Text = hidden ? mask : $"{_supplierDebt:0.##} ج.م";
        TxtSupplierDebtAmount.Text = hidden ? mask : $"{_supplierDebt:0.##} ج.م";
        TxtCustomerDebt.Text = hidden ? mask : $"{_customerDebt:0.##} ج.م";
        TxtCustomerDebtAmount.Text = hidden ? mask : $"{_customerDebt:0.##} ج.م";

        if (hidden)
        {
            TxtTotalRemaining.Text = mask;
        }
        else if (_netRemaining > 0)
        {
            TxtTotalRemaining.Text = $"ديون لي {_netRemaining:0.##} ج.م";
            TxtTotalRemaining.Foreground = (Brush)new BrushConverter().ConvertFrom("#2E7D32")!;
        }
        else if (_netRemaining < 0)
        {
            TxtTotalRemaining.Text = $"ديون عليا {Math.Abs(_netRemaining):0.##} ج.م";
            TxtTotalRemaining.Foreground = (Brush)new BrushConverter().ConvertFrom("#C62828")!;
        }
        else
        {
            TxtTotalRemaining.Text = "0 ج.م";
            TxtTotalRemaining.Foreground = (Brush)new BrushConverter().ConvertFrom("#E65100")!;
        }
    }

    private void BuildAccountCards()
    {
        AccountsPanel.Children.Clear();

        var accounts = _db.DebtAccounts.AsNoTracking().ToList();
        TxtAccountsCount.Text = $"{accounts.Count} حساب";

        if (accounts.Count == 0)
        {
            AccountsPanel.Children.Add(new TextBlock
            {
                Text = "لا توجد حسابات دائمة بعد — أضف حساباً لتسجيل الديون عليه من داخل بطاقته",
                FontSize = 11,
                Foreground = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 4)
            });
            return;
        }

        foreach (var account in accounts)
            AccountsPanel.Children.Add(CreateAccountCard(account));
    }

    private Border CreateAccountCard(DebtAccount account)
    {
        var headingFg = Application.Current.TryFindResource("HeadingTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
        var mutedFg = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!;

        var debts = _db.Debts.Where(d => d.DebtAccountId == account.Id).ToList();
        int debtCount = debts.Count;
        decimal onMe = debts.Where(d => d.Direction == DebtDirection.OnMe).Sum(d => d.Remaining);
        decimal forMe = debts.Where(d => d.Direction == DebtDirection.ForMe).Sum(d => d.Remaining);

        var card = new Border
        {
            Width = 330,
            CornerRadius = new CornerRadius(12),
            Background = Application.Current.TryFindResource("CardBackground") as Brush ?? Brushes.White,
            BorderBrush = Application.Current.TryFindResource("BorderBrushLight") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 14, 12),
            Padding = new Thickness(16)
        };
        card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.08, Color = Colors.Black };

        var inner = new Grid();
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header: name + phone
        var nameGrid = new Grid();
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        nameGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameStack = new StackPanel();
        nameStack.Children.Add(new TextBlock
        {
            Text = account.Name,
            FontSize = 15, FontWeight = FontWeights.Bold,
            Foreground = headingFg,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var sub = $"{debtCount} دين";
        if (!string.IsNullOrWhiteSpace(account.Phone)) sub += $"  •  {account.Phone}";
        nameStack.Children.Add(new TextBlock { Text = sub, FontSize = 10, Foreground = mutedFg, Margin = new Thickness(0, 2, 0, 0) });
        Grid.SetColumn(nameStack, 0);
        nameGrid.Children.Add(nameStack);

        var iconBorder = new Border
        {
            Width = 36, Height = 36,
            CornerRadius = new CornerRadius(10),
            Background = (Brush)new BrushConverter().ConvertFrom("#FCE4EC")!,
            VerticalAlignment = VerticalAlignment.Top,
            Child = new Path
            {
                Width = 16, Height = 16,
                Fill = (Brush)new BrushConverter().ConvertFrom("#C62828")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z")
            }
        };
        Grid.SetColumn(iconBorder, 1);
        nameGrid.Children.Add(iconBorder);

        inner.Children.Add(nameGrid);
        Grid.SetRow(nameGrid, 0);

        // amount badges
        var badgeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        if (onMe > 0)
            badgeRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = (Brush)new BrushConverter().ConvertFrom("#FFEBEE")!,
                Padding = new Thickness(7, 2, 7, 2),
                Child = new TextBlock { Text = $"عليا {onMe:0.##}", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#C62828")! }
            });
        if (forMe > 0)
            badgeRow.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = (Brush)new BrushConverter().ConvertFrom("#E8F5E9")!,
                Padding = new Thickness(7, 2, 7, 2),
                Margin = new Thickness(4, 0, 0, 0),
                Child = new TextBlock { Text = $"لي {forMe:0.##}", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#2E7D32")! }
            });
        if (badgeRow.Children.Count == 0)
            badgeRow.Children.Add(new TextBlock { Text = "لا توجد ديون", FontSize = 10, Foreground = mutedFg, VerticalAlignment = VerticalAlignment.Center });
        inner.Children.Add(badgeRow);
        Grid.SetRow(badgeRow, 1);

        // divider
        var divider = new Border { Height = 1, Background = Application.Current.TryFindResource("DividerBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#F0F0F0")!, Margin = new Thickness(0, 10, 0, 0) };
        inner.Children.Add(divider);
        Grid.SetRow(divider, 2);

        // actions
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };

        var debtsBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#00695C")!,
            Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 4, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { Width = 13, Height = 13, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M3 13h2v-2H3v2zm0 4h2v-2H3v2zm0-8h2V7H3v2zm4 4h14v-2H7v2zm0 4h14v-2H7v2zM7 7v2h14V7H7z") },
                new TextBlock { Text = "الديون", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
            }}
        };
        debtsBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; OpenAccountDebts(account.Id); };
        actions.Children.Add(debtsBtn);

        var addDebtBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#C62828")!,
            Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 4, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { Width = 13, Height = 13, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z") },
                new TextBlock { Text = "إضافة دين", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
            }}
        };
        addDebtBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; AddDebtForAccount(account.Id); };
        actions.Children.Add(addDebtBtn);

        var editBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#546E7A")!,
            Cursor = Cursors.Hand, Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(4, 0, 4, 0),
            Child = new Path { Width = 13, Height = 13, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z") }
        };
        editBtn.ToolTip = "تعديل الحساب";
        editBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; EditAccount(account.Id); };
        actions.Children.Add(editBtn);

        var deleteBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#FFEBEE")!,
            Cursor = Cursors.Hand, Padding = new Thickness(8, 5, 8, 5),
            Child = new Path { Width = 13, Height = 13, Fill = (Brush)new BrushConverter().ConvertFrom("#C62828")!, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z") }
        };
        deleteBtn.ToolTip = "حذف الحساب";
        deleteBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; DeleteAccount(account.Id); };
        actions.Children.Add(deleteBtn);

        inner.Children.Add(actions);
        Grid.SetRow(actions, 3);

        card.Child = inner;
        card.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            OpenAccountDebts(account.Id);
        };
        return card;
    }

    private void OpenAccountDebts(int accountId)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new AccountDebtsDialog(_db, accountId);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void AddDebtForAccount(int accountId)
    {
        var account = _db.DebtAccounts.FirstOrDefault(a => a.Id == accountId);
        if (account == null) return;
        AddDebtDialog(account);
    }

    private void EditAccount(int accountId)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new DebtAccountDialog(_db, accountId);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void DeleteAccount(int accountId)
    {
        var account = _db.DebtAccounts.FirstOrDefault(a => a.Id == accountId);
        if (account == null) return;

        ConfirmDialog.Show("حذف الحساب",
            $"هل أنت متأكد من حذف حساب «{account.Name}»؟\nستبقى ديونه مسجلة باسمه بدون حسابه.",
            result =>
            {
                if (result != true) return;
                var debts = _db.Debts.Where(d => d.DebtAccountId == account.Id).ToList();
                foreach (var debt in debts)
                    debt.DebtAccountId = null;
                _db.DebtAccounts.Remove(account);
                _db.SaveChanges();
                App.NotifyDataChanged();
                App.AppBackup?.BackupIfOnOperation();
                NotificationManager.ShowSuccess("تم حذف الحساب وتبقّت ديونه مسجلة باسمه");
                LoadData();
            },
            ConfirmDialog.DialogType.Danger);
    }

    private void SetFilter(string mode)
    {
        _filterMode = mode;

        var bodyBrush = Application.Current.TryFindResource("BodyTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;
        foreach (var btn in new[] { BtnAll, BtnOnMe, BtnForMe })
            btn.Background = Brushes.Transparent;
        foreach (var txt in new[] { TxtAll, TxtOnMe, TxtForMe })
        {
            txt.Foreground = bodyBrush;
            txt.FontWeight = FontWeights.SemiBold;
        }

        (Border btn, TextBlock txt) selected = mode switch
        {
            "OnMe" => (BtnOnMe, TxtOnMe),
            "ForMe" => (BtnForMe, TxtForMe),
            _ => (BtnAll, TxtAll)
        };
        selected.btn.Background = (Brush)new BrushConverter().ConvertFrom("#C62828")!;
        selected.txt.Foreground = Brushes.White;
        selected.txt.FontWeight = FontWeights.Bold;

        SupplierDebtCard.Visibility = mode is "All" or "OnMe" ? Visibility.Visible : Visibility.Collapsed;
        CustomerDebtCard.Visibility = SupplierDebtCard.Visibility;

        BuildCards();
    }

    private void BuildCards()
    {
        DebtsPanel.Children.Clear();

        // الديون المسجلة على الحسابات لا تظهر ككروت هنا — تظهر داخل «ديون الحساب» فقط.
        // تبقى الكروت للديون القديمة غير المرتبطة بأي حساب حتى لا تضيع.
        var debts = _db.Debts.AsNoTracking()
            .Where(d => d.DebtAccountId == null)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();

        if (_filterMode == "OnMe") debts = debts.Where(d => d.Direction == DebtDirection.OnMe).ToList();
        if (_filterMode == "ForMe") debts = debts.Where(d => d.Direction == DebtDirection.ForMe).ToList();

        DebtsPanel.Visibility = debts.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        foreach (var debt in debts)
            DebtsPanel.Children.Add(CreateDebtCard(debt));
    }

    private Border CreateDebtCard(Debt debt)
    {
        var headingFg = Application.Current.TryFindResource("HeadingTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
        var mutedFg = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!;

        var onMe = debt.Direction == DebtDirection.OnMe;
        var accent = onMe ? "#C62828" : "#2E7D32";
        var displayName = string.IsNullOrWhiteSpace(debt.AccountName)
            ? debt.DebtAccount?.Name ?? "بدون شخص"
            : debt.AccountName;

        var card = new Border
        {
            Width = 330,
            CornerRadius = new CornerRadius(12),
            Background = Application.Current.TryFindResource("CardBackground") as Brush ?? Brushes.White,
            BorderBrush = Application.Current.TryFindResource("BorderBrushLight") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 14, 14),
            Padding = new Thickness(16)
        };
        card.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.08, Color = Colors.Black };

        var inner = new Grid();
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        inner.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header: name + badges
        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var nameStack = new StackPanel();
        nameStack.Children.Add(new TextBlock
        {
            Text = displayName,
            FontSize = 15, FontWeight = FontWeights.Bold,
            Foreground = headingFg,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        nameStack.Children.Add(new TextBlock
        {
            Text = $"{debt.CreatedAt:yyyy/MM/dd}",
            FontSize = 10, Foreground = mutedFg, Margin = new Thickness(0, 2, 0, 0)
        });
        Grid.SetColumn(nameStack, 0);
        header.Children.Add(nameStack);

        var badgeStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        badgeStack.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = (Brush)new BrushConverter().ConvertFrom(onMe ? "#FFEBEE" : "#E8F5E9")!,
            Padding = new Thickness(6, 2, 6, 2),
            Child = new TextBlock
            {
                Text = onMe ? "عليا" : "لي",
                FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = (Brush)new BrushConverter().ConvertFrom(accent)!
            }
        });
        var statusText = debt.Status switch
        {
            InvoiceStatus.Paid => ("مسدد", "#2E7D32", "#E8F5E9"),
            InvoiceStatus.PartiallyPaid => ("مسدد جزئياً", "#E65100", "#FFF3E0"),
            _ => ("مفتوح", "#C62828", "#FFEBEE")
        };
        badgeStack.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(4),
            Background = (Brush)new BrushConverter().ConvertFrom(statusText.Item3)!,
            Padding = new Thickness(6, 2, 6, 2), Margin = new Thickness(4, 0, 0, 0),
            Child = new TextBlock
            {
                Text = statusText.Item1, FontSize = 10, FontWeight = FontWeights.Bold,
                Foreground = (Brush)new BrushConverter().ConvertFrom(statusText.Item2)!
            }
        });
        Grid.SetColumn(badgeStack, 1);
        header.Children.Add(badgeStack);

        inner.Children.Add(header);
        Grid.SetRow(header, 0);

        // Notes
        if (!string.IsNullOrWhiteSpace(debt.Notes))
        {
            var notes = new TextBlock
            {
                Text = debt.Notes,
                FontSize = 11, Foreground = mutedFg,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
                MaxHeight = 40
            };
            inner.Children.Add(notes);
            Grid.SetRow(notes, 1);
        }

        // Amounts
        var amounts = new Grid { Margin = new Thickness(0, 10, 0, 0) };
        amounts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        amounts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        amounts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        const string mask = "••••••";
        bool hidden = AmountsVisibilityService.IsHidden;

        void AddAmount(int col, string label, string value, string color)
        {
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = label, FontSize = 9, Foreground = mutedFg });
            stack.Children.Add(new TextBlock
            {
                Text = value, FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = (Brush)new BrushConverter().ConvertFrom(color)!,
                Margin = new Thickness(0, 1, 0, 0)
            });
            amounts.Children.Add(stack);
            Grid.SetColumn(stack, col);
        }

        AddAmount(0, "الإجمالي", hidden ? mask : $"{debt.TotalAmount:0.##} ج.م", onMe ? "#C62828" : "#2E7D32");
        AddAmount(1, "المدفوع", hidden ? mask : $"{debt.TotalPaid:0.##} ج.م", "#1565C0");
        AddAmount(2, "المتبقي", hidden ? mask : $"{debt.Remaining:0.##} ج.م", "#E65100");

        inner.Children.Add(amounts);
        Grid.SetRow(amounts, 2);

        // Actions
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };

        if (debt.Status != InvoiceStatus.Paid)
        {
            var payBtn = new Border
            {
                CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#2E7D32")!,
                Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 4, 0),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
                {
                    new Path { Width = 13, Height = 13, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z") },
                    new TextBlock { Text = "دفع", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
                }}
            };
            payBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; AddPayment(debt.Id); };
            actions.Children.Add(payBtn);
        }

        var historyBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#1565C0")!,
            Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 4, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { Width = 13, Height = 13, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M6 2c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6H6zm6 7h5.5L12 4.5V9zM8 12h8v2H8v-2zm0 4h8v2H8v-2z") },
                new TextBlock { Text = "دفعات", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
            }}
        };
        historyBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; OpenPayments(debt.Id); };
        actions.Children.Add(historyBtn);

        var editBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#546E7A")!,
            Cursor = Cursors.Hand, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(4, 0, 4, 0),
            Child = new StackPanel { Orientation = Orientation.Horizontal, Children =
            {
                new Path { Width = 13, Height = 13, Fill = Brushes.White, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z") },
                new TextBlock { Text = "تعديل", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(5, 0, 0, 0) }
            }}
        };
        editBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; EditDebt(debt.Id); };
        actions.Children.Add(editBtn);

        var deleteBtn = new Border
        {
            CornerRadius = new CornerRadius(6), Background = (Brush)new BrushConverter().ConvertFrom("#FFEBEE")!,
            Cursor = Cursors.Hand, Padding = new Thickness(8, 5, 8, 5),
            Child = new Path { Width = 13, Height = 13, Fill = (Brush)new BrushConverter().ConvertFrom("#C62828")!, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center, Data = Geometry.Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z") }
        };
        deleteBtn.MouseLeftButtonDown += (_, e) => { e.Handled = true; DeleteDebt(debt.Id); };
        actions.Children.Add(deleteBtn);

        inner.Children.Add(actions);
        Grid.SetRow(actions, 3);

        card.Child = inner;
        return card;
    }

    private void AddPayment(int debtId)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new DebtPaymentDialog(_db, debtId);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void OpenPayments(int debtId)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new DebtPaymentsDialog(_db, debtId);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void EditDebt(int debtId)
    {
        var debt = _db.Debts.FirstOrDefault(d => d.Id == debtId);
        if (debt == null) return;

        var account = debt.DebtAccountId != null
            ? _db.DebtAccounts.FirstOrDefault(a => a.Id == debt.DebtAccountId.Value)
            : null;

        if (account == null)
        {
            // ديون قديمة بدون شخص — اطلب اختيار شخص أولاً
            var mainWindow = (MainWindow)Window.GetWindow(this);
            var select = new SelectDebtAccountDialog(_db);
            mainWindow.ShowOverlay(select);
            select.AccountSelected += (s, picked) =>
            {
                mainWindow.HideOverlay();
                if (picked == null) return;
                var dialog = new DebtDialog(_db, picked, debtId);
                mainWindow.ShowOverlay(dialog);
                dialog.DialogClosed += (s2, r2) =>
                {
                    mainWindow.HideOverlay();
                    if (r2 == true) LoadData();
                };
            };
            return;
        }

        var dlg = new DebtDialog(_db, account, debtId);
        var mw = (MainWindow)Window.GetWindow(this);
        mw.ShowOverlay(dlg);
        dlg.DialogClosed += (s, r) =>
        {
            mw.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void DeleteDebt(int debtId)
    {
        var debt = _db.Debts.FirstOrDefault(d => d.Id == debtId);
        if (debt == null) return;
        var displayName = string.IsNullOrWhiteSpace(debt.AccountName) ? "غير معروف" : debt.AccountName;

        ConfirmDialog.Show("حذف الدين",
            $"هل أنت متأكد من حذف دين «{displayName}» بقيمة {debt.TotalAmount:0.##} ج.م؟\nسيتم حذف كل دفعاته ولا يمكن التراجع.",
            result =>
            {
                if (result != true) return;
                _db.DebtPayments.RemoveRange(_db.DebtPayments.Where(p => p.DebtId == debt.Id));
                _db.Debts.Remove(debt);
                _db.SaveChanges();
                App.NotifyDataChanged();
                App.AppBackup?.BackupIfOnOperation();
                NotificationManager.ShowSuccess("تم حذف الدين ودفعاته");
                LoadData();
            },
            ConfirmDialog.DialogType.Danger);
    }

    private void AddDebtDialog(DebtAccount account, int? debtId = null)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new DebtDialog(_db, account, debtId);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void BtnAddAccount_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new DebtAccountDialog(_db);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void SupplierDebtCard_Click(object sender, MouseButtonEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new SupplierInvoicesDialog(_db, showAll: true);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            LoadData();
        };
    }

    private void CustomerDebtCard_Click(object sender, MouseButtonEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new CustomerInvoicesDialog(_db, showAll: true,
            title: "دين العملاء لي",
            subtitle: "فواتير العملاء والنقدي غير المدفوعة — المتبقي عليها لك");
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            LoadData();
        };
    }

    private void BtnAll_Click(object sender, MouseButtonEventArgs e) => SetFilter("All");
    private void BtnOnMe_Click(object sender, MouseButtonEventArgs e) => SetFilter("OnMe");
    private void BtnForMe_Click(object sender, MouseButtonEventArgs e) => SetFilter("ForMe");
}