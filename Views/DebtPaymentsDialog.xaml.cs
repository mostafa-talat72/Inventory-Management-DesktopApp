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

public partial class DebtPaymentsDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly int _debtId;

    public DebtPaymentsDialog(AppDbContext db, int debtId)
    {
        InitializeComponent();
        _db = db;
        _debtId = debtId;
        LoadData();
    }

    private void LoadData()
    {
        var debt = _db.Debts.Include(d => d.Payments).First(d => d.Id == _debtId);

        var displayName = string.IsNullOrWhiteSpace(debt.AccountName) ? "بدون شخص" : debt.AccountName;
        TxtTitle.Text = debt.Direction == DebtDirection.OnMe ? $"دفعات عليا — {displayName}" : $"دفعات لي — {displayName}";
        TxtSubtitle.Text = $"{debt.Payments.Count} دفعة";

        bool hidden = AmountsVisibilityService.IsHidden;
        const string mask = "••••••";
        TxtTotal.Text = hidden ? mask : $"{debt.TotalAmount:0.##} ج.م";
        TxtPaid.Text = hidden ? mask : $"{debt.TotalPaid:0.##} ج.م";
        TxtRemaining.Text = hidden ? mask : $"{debt.Remaining:0.##} ج.م";

        BtnAddPayment.Visibility = debt.Status == InvoiceStatus.Paid ? Visibility.Collapsed : Visibility.Visible;

        PaymentsPanel.Children.Clear();
        var payments = debt.Payments.OrderByDescending(p => p.PaymentDate).ToList();
        if (payments.Count == 0)
        {
            PaymentsPanel.Children.Add(new TextBlock
            {
                Text = "لا توجد دفعات بعد",
                FontSize = 13,
                Foreground = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 24, 0, 24)
            });
            return;
        }

        foreach (var p in payments)
            PaymentsPanel.Children.Add(CreatePaymentCard(p));
    }

    private Border CreatePaymentCard(DebtPayment p)
    {
        var surfaceBg = Application.Current.TryFindResource("SurfaceBackground") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!;
        var headingFg = Application.Current.TryFindResource("HeadingTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
        var mutedFg = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!;
        var bodyFg = Application.Current.TryFindResource("BodyTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#546E7A")!;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBorder = new Border
        {
            Width = 34, Height = 34,
            CornerRadius = new CornerRadius(9),
            Background = (Brush)new BrushConverter().ConvertFrom("#E8F5E9")!,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Width = 16, Height = 16,
                Fill = (Brush)new BrushConverter().ConvertFrom("#2E7D32")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z")
            }
        };
        grid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 0);

        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
        infoStack.Children.Add(new TextBlock { Text = $"{p.Amount:0.##} ج.م", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = headingFg });
        var detail = $"{p.PaymentDate:yyyy/MM/dd HH:mm}";
        if (!string.IsNullOrWhiteSpace(p.PaymentMethod)) detail += $"  •  {p.PaymentMethod}";
        if (!string.IsNullOrWhiteSpace(p.Notes)) detail += $"  •  {p.Notes}";
        infoStack.Children.Add(new TextBlock { Text = detail, FontSize = 11, Foreground = mutedFg, Margin = new Thickness(0, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        grid.Children.Add(infoStack);
        Grid.SetColumn(infoStack, 1);

        var actionStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };

        var editBtn = new Button
        {
            Width = 30, Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "تعديل",
            Content = new Path
            {
                Width = 14, Height = 14,
                Fill = bodyFg,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M3 17.25V21h3.75L17.81 9.94l-3.75-3.75L3 17.25zM20.71 7.04c.39-.39.39-1.02 0-1.41l-2.34-2.34c-.39-.39-1.02-.39-1.41 0l-1.83 1.83 3.75 3.75 1.83-1.83z")
            }
        };
        var pCopy = p;
        editBtn.Click += (_, _) => EditPayment(pCopy.Id);

        var deleteBtn = new Button
        {
            Width = 30, Height = 30,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            ToolTip = "حذف",
            Margin = new Thickness(4, 0, 0, 0),
            Content = new Path
            {
                Width = 14, Height = 14,
                Fill = (Brush)new BrushConverter().ConvertFrom("#E53935")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M6 19c0 1.1.9 2 2 2h8c1.1 0 2-.9 2-2V7H6v12zM19 4h-3.5l-1-1h-5l-1 1H5v2h14V4z")
            }
        };
        deleteBtn.Click += (_, _) => DeletePayment(pCopy.Id);

        actionStack.Children.Add(editBtn);
        actionStack.Children.Add(deleteBtn);
        grid.Children.Add(actionStack);
        Grid.SetColumn(actionStack, 2);

        return new Border
        {
            CornerRadius = new CornerRadius(8),
            Background = surfaceBg,
            Margin = new Thickness(0, 0, 0, 6),
            Padding = new Thickness(12, 8, 12, 8),
            Child = grid
        };
    }

    private void BtnAddPayment_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new DebtPaymentDialog(_db, _debtId);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void EditPayment(int paymentId)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new DebtPaymentDialog(_db, _debtId, paymentId);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true) LoadData();
        };
    }

    private void DeletePayment(int paymentId)
    {
        var payment = _db.DebtPayments.FirstOrDefault(p => p.Id == paymentId);
        if (payment == null) return;

        ConfirmDialog.Show("حذف الدفعة",
            $"هل أنت متأكد من حذف دفعة بقيمة {payment.Amount:0.##} ج.م؟",
            result =>
            {
                if (result != true) return;

                _db.DebtPayments.Remove(payment);
                _db.SaveChanges();

                var debt = _db.Debts.Include(d => d.Payments).First(d => d.Id == _debtId);
                debt.TotalPaid = debt.Payments.Sum(p => p.Amount);
                if (debt.TotalPaid > debt.TotalAmount) debt.TotalPaid = debt.TotalAmount;
                debt.Status = debt.Remaining <= 0
                    ? (debt.TotalPaid > 0 ? InvoiceStatus.Paid : InvoiceStatus.Open)
                    : (debt.TotalPaid > 0 ? InvoiceStatus.PartiallyPaid : InvoiceStatus.Open);
                _db.SaveChanges();

                App.NotifyDataChanged();
                App.AppBackup?.BackupIfOnOperation();
                NotificationManager.ShowSuccess("تم حذف الدفعة");
                LoadData();
            },
            ConfirmDialog.DialogType.Danger);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }
}