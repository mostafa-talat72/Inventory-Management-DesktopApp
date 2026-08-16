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

public partial class SelectDebtAccountDialog : UserControl
{
    public event EventHandler<DebtAccount?>? AccountSelected;

    private readonly AppDbContext _db;

    public SelectDebtAccountDialog(AppDbContext db)
    {
        InitializeComponent();
        _db = db;
        LoadAccounts("");
    }

    private void LoadAccounts(string search)
    {
        if (_db == null) return;

        AccountsPanel.Children.Clear();

        var accounts = _db.DebtAccounts.AsNoTracking().ToList();
        if (!string.IsNullOrWhiteSpace(search))
            accounts = accounts.Where(a => a.Name.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase)
                || (a.Phone ?? "").Contains(search.Trim())).ToList();

        if (accounts.Count == 0)
        {
            AccountsPanel.Children.Add(new TextBlock
            {
                Text = "لا يوجد أشخاص — اضغط «إضافة شخص» أولاً",
                FontSize = 13,
                Foreground = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 30, 0, 30)
            });
            return;
        }

        foreach (var account in accounts)
            AccountsPanel.Children.Add(CreateAccountRow(account));
    }

    private Border CreateAccountRow(DebtAccount account)
    {
        var headingFg = Application.Current.TryFindResource("HeadingTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#37474F")!;
        var mutedFg = Application.Current.TryFindResource("MutedTextBrush") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#90A4AE")!;

        int debtCount = _db.Debts.Count(d => d.DebtAccountId == account.Id);
        decimal onMe = _db.Debts.Where(d => d.DebtAccountId == account.Id && d.Direction == DebtDirection.OnMe)
            .Sum(d => (decimal?)d.Remaining) ?? 0;
        decimal forMe = _db.Debts.Where(d => d.DebtAccountId == account.Id && d.Direction == DebtDirection.ForMe)
            .Sum(d => (decimal?)d.Remaining) ?? 0;

        var card = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = Application.Current.TryFindResource("SurfaceBackground") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#F8F9FA")!,
            BorderBrush = Application.Current.TryFindResource("BorderBrushLight") as Brush ?? (Brush)new BrushConverter().ConvertFrom("#E0E0E0")!,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(14, 12, 14, 12),
            Cursor = Cursors.Hand
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var iconBorder = new Border
        {
            Width = 38, Height = 38,
            CornerRadius = new CornerRadius(10),
            Background = (Brush)new BrushConverter().ConvertFrom("#FCE4EC")!,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new Path
            {
                Width = 18, Height = 18,
                Fill = (Brush)new BrushConverter().ConvertFrom("#C62828")!,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Data = Geometry.Parse("M16 11c1.66 0 2.99-1.34 2.99-3S17.66 5 16 5c-1.66 0-3 1.34-3 3s1.34 3 3 3zm-8 0c1.66 0 2.99-1.34 2.99-3S9.66 5 8 5C6.34 5 5 6.34 5 8s1.34 3 3 3zm0 2c-2.33 0-7 1.17-7 3.5V19h14v-2.5c0-2.33-4.67-3.5-7-3.5zm8 0c-.29 0-.62.02-.97.05 1.16.84 1.97 1.97 1.97 3.45V19h6v-2.5c0-2.33-4.67-3.5-7-3.5z")
            }
        };
        grid.Children.Add(iconBorder);
        Grid.SetColumn(iconBorder, 0);

        var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        nameStack.Children.Add(new TextBlock
        {
            Text = account.Name,
            FontSize = 14, FontWeight = FontWeights.SemiBold,
            Foreground = headingFg,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        var sub = $"{debtCount} دين";
        if (!string.IsNullOrWhiteSpace(account.Phone)) sub += $"  •  {account.Phone}";
        nameStack.Children.Add(new TextBlock { Text = sub, FontSize = 11, Foreground = mutedFg, Margin = new Thickness(0, 2, 0, 0) });
        grid.Children.Add(nameStack);
        Grid.SetColumn(nameStack, 1);

        var totals = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        if (onMe > 0)
            totals.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = (Brush)new BrushConverter().ConvertFrom("#FFEBEE")!,
                Padding = new Thickness(6, 2, 6, 2),
                Child = new TextBlock { Text = $"عليا {onMe:0.##}", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#C62828")! }
            });
        if (forMe > 0)
            totals.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = (Brush)new BrushConverter().ConvertFrom("#E8F5E9")!,
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(4, 0, 0, 0),
                Child = new TextBlock { Text = $"لي {forMe:0.##}", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = (Brush)new BrushConverter().ConvertFrom("#2E7D32")! }
            });
        grid.Children.Add(totals);
        Grid.SetColumn(totals, 2);

        card.Child = grid;
        card.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            AccountSelected?.Invoke(this, account);
        };
        return card;
    }

    private void BtnAddAccount_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new DebtAccountDialog(_db);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            LoadAccounts(TxtSearch.Text.Trim());
        };
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e) => LoadAccounts(TxtSearch.Text.Trim());

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        AccountSelected?.Invoke(this, null);
    }
}