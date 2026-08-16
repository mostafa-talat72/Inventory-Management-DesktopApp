using System;
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

public partial class AccountDebtsDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly int _accountId;
    private List<Debt> _allDebts = new();
    private string _filterMode = "All";
    private bool _sortAscending;

    public AccountDebtsDialog(AppDbContext db, int accountId)
    {
        InitializeComponent();
        _db = db;
        _accountId = accountId;
        LoadData();
    }

    private void LoadData()
    {
        var account = _db.DebtAccounts.First(a => a.Id == _accountId);

        TxtTitle.Text = $"ديون {account.Name}";
        TxtSubtitle.Text = $"تسجيل ومتابعة ديون {account.Name}";

        _allDebts = _db.Debts.Include(d => d.Payments).Where(d => d.DebtAccountId == _accountId).ToList();
        SetFilter(_filterMode);
    }

    private List<Debt> GetFiltered()
    {
        var q = _allDebts.AsEnumerable();

        q = _filterMode switch
        {
            "Partially" => q.Where(d => d.Status == InvoiceStatus.PartiallyPaid),
            "Paid" => q.Where(d => d.Status == InvoiceStatus.Paid),
            "Open" => q.Where(d => d.Status == InvoiceStatus.Open),
            _ => q
        };

        q = _sortAscending ? q.OrderBy(d => d.CreatedAt) : q.OrderByDescending(d => d.CreatedAt);
        return q.ToList();
    }

    private void SetFilter(string mode)
    {
        _filterMode = mode;

        var bodyBrush = Application.Current.TryFindResource("BodyTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;
        foreach (var btn in new[] { BtnAll, BtnOpen, BtnPartial, BtnPaid })
            btn.Background = Brushes.Transparent;
        foreach (var txt in new[] { TxtAll, TxtOpen, TxtPartial, TxtPaid })
        {
            txt.Foreground = bodyBrush;
            txt.FontWeight = FontWeights.SemiBold;
        }

        (Border btn, TextBlock txt) selected = mode switch
        {
            "Open" => (BtnOpen, TxtOpen),
            "Partially" => (BtnPartial, TxtPartial),
            "Paid" => (BtnPaid, TxtPaid),
            _ => (BtnAll, TxtAll)
        };
        selected.btn.Background = (Brush)new BrushConverter().ConvertFrom("#00695C")!;
        selected.txt.Foreground = Brushes.White;
        selected.txt.FontWeight = FontWeights.Bold;

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var debts = GetFiltered();

        decimal onMe = debts.Where(d => d.Direction == DebtDirection.OnMe).Sum(d => d.Remaining);
        decimal forMe = debts.Where(d => d.Direction == DebtDirection.ForMe).Sum(d => d.Remaining);
        decimal net = forMe - onMe;

        bool hidden = AmountsVisibilityService.IsHidden;
        const string mask = "••••••";
        TxtDebtCount.Text = debts.Count.ToString();
        TxtOnMe.Text = hidden ? mask : $"{onMe:0.##} ج.م";
        TxtForMe.Text = hidden ? mask : $"{forMe:0.##} ج.م";

        if (hidden)
        {
            TxtRemaining.Text = mask;
        }
        else if (net > 0)
        {
            TxtRemaining.Text = $"ديون لي {net:0.##} ج.م";
            TxtRemaining.Foreground = (Brush)new BrushConverter().ConvertFrom("#2E7D32")!;
        }
        else if (net < 0)
        {
            TxtRemaining.Text = $"ديون عليا {Math.Abs(net):0.##} ج.م";
            TxtRemaining.Foreground = (Brush)new BrushConverter().ConvertFrom("#C62828")!;
        }
        else
        {
            TxtRemaining.Text = "0 ج.م";
            TxtRemaining.Foreground = (Brush)new BrushConverter().ConvertFrom("#E65100")!;
        }

        DebtsPanel.Children.Clear();
        if (debts.Count == 0)
        {
            DebtsPanel.Children.Add(new TextBlock
            {
                Text = "لا توجد ديون لهذا الحساب — اضغط «إضافة دين»",
                FontSize = 13,
                Foreground = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 24, 0, 24)
            });
            return;
        }

        foreach (var debt in debts)
            DebtsPanel.Children.Add(CreateDebtCard(debt));
    }

    private Border CreateDebtCard(Debt debt)
    {
        var surfaceBg = Application.Current.TryFindResource("SurfaceBackground") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!;
        var headingFg = Application.Current.TryFindResource("HeadingTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
        var mutedFg = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!;

        var onMe = debt.Direction == DebtDirection.OnMe;
        var accent = onMe ? "#C62828" : "#2E7D32";

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBorder = new Border
        {
            Width = 34, Height = 34,
            CornerRadius = new CornerRadius(9),
            Background = (Brush)new BrushConverter().ConvertFrom(onMe ? "#FFEBEE" : "#E8F5E9")!,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Width = 16, Height = 16,
                Fill = (Brush)new BrushConverter().ConvertFrom(accent)!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M12 3L1 9l4 2.18v6L12 21l7-3.82v-6l1-0.54V17h2V9L12 3zM8 17.15v-4.44l4 2.22 4-2.22v4.44L12 19.4l-4-2.25zM6 9.83V7.36l6-3.26 6 3.26v2.47l-6 3.26-6-3.26z")
            }
        };
        grid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 0);

        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        string statusText = debt.Status switch
        {
            InvoiceStatus.Paid => "مسدد",
            InvoiceStatus.PartiallyPaid => "مسدد جزئياً",
            _ => "مفتوح"
        };
        infoStack.Children.Add(new TextBlock
        {
            Text = $"{debt.TotalAmount:0.##} ج.م  •  {statusText}",
            FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = headingFg
        });
        infoStack.Children.Add(new TextBlock
        {
            Text = $"{debt.CreatedAt:yyyy/MM/dd}  •  {debt.TotalPaid:0.##} مدفوع  •  {debt.Remaining:0.##} متبقي  {(string.IsNullOrWhiteSpace(debt.Notes) ? "" : $" • {debt.Notes}")}",
            FontSize = 11, Foreground = mutedFg, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis
        });
        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);

        var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };

        if (debt.Status != InvoiceStatus.Paid)
        {
            var payBtn = new Button
            {
                Height = 26, Cursor = Cursors.Hand, Background = (Brush)new BrushConverter().ConvertFrom("#2E7D32")!,
                Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(10, 0, 10, 0),
                FontSize = 11, FontWeight = FontWeights.Bold, Content = "دفع",
                VerticalAlignment = VerticalAlignment.Center
            };
            payBtn.Click += (_, _) => AddPayment(debt.Id);
            actionStack.Children.Add(payBtn);
        }

        var historyBtn = new Button
        {
            Height = 26, Cursor = Cursors.Hand, Background = (Brush)new BrushConverter().ConvertFrom("#1565C0")!,
            Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(10, 0, 10, 0),
            FontSize = 11, FontWeight = FontWeights.Bold, Content = "دفعات", Margin = new Thickness(4, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        historyBtn.Click += (_, _) => OpenPayments(debt.Id);
        actionStack.Children.Add(historyBtn);

        var editBtn = new Button
        {
            Width = 30, Height = 30, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, ToolTip = "تعديل", Margin = new Thickness(4, 0, 0, 0),
            Content = new Path
            {
                Width = 14, Height = 14, Fill = (Brush)new BrushConverter().ConvertFrom("#546E7A")!,
                Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z")
            }
        };
        editBtn.Click += (_, _) => EditDebt(debt.Id);
        actionStack.Children.Add(editBtn);

        var deleteBtn = new Button
        {
            Width = 30, Height = 30, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand, ToolTip = "حذف", Margin = new Thickness(4, 0, 0, 0),
            Content = new Path
            {
                Width = 14, Height = 14, Fill = (Brush)new BrushConverter().ConvertFrom("#E53935")!,
                Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z")
            }
        };
        deleteBtn.Click += (_, _) => DeleteDebt(debt.Id);
        actionStack.Children.Add(deleteBtn);

        grid.Children.Add(actionStack);
        Grid.SetColumn(actionStack, 2);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = surfaceBg,
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(12, 10, 12, 10),
            Child = grid
        };
    }

    private void BtnAddDebt_Click(object sender, RoutedEventArgs e)
    {
        var account = _db.DebtAccounts.First(a => a.Id == _accountId);
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new DebtDialog(_db, account);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void EditDebt(int debtId)
    {
        var account = _db.DebtAccounts.First(a => a.Id == _accountId);
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new DebtDialog(_db, account, debtId);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void DeleteDebt(int debtId)
    {
        var debt = _db.Debts.FirstOrDefault(d => d.Id == debtId);
        if (debt == null) return;

        ConfirmDialog.Show("حذف الدين",
            $"هل أنت متأكد من حذف هذا الدين بقيمة {debt.TotalAmount:0.##} ج.م؟\nسيتم حذف كل دفعاته ولا يمكن التراجع.",
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

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }

    private void BtnAll_Click(object sender, MouseButtonEventArgs e) => SetFilter("All");
    private void BtnOpen_Click(object sender, MouseButtonEventArgs e) => SetFilter("Open");
    private void BtnPartial_Click(object sender, MouseButtonEventArgs e) => SetFilter("Partially");
    private void BtnPaid_Click(object sender, MouseButtonEventArgs e) => SetFilter("Paid");

    private void BtnSort_Click(object sender, MouseButtonEventArgs e)
    {
        _sortAscending = !_sortAscending;
        TxtSort.Text = _sortAscending ? "الأقدم" : "الأحدث";
        ApplyFilter();
    }
}