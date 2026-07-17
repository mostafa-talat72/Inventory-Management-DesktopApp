using System.Windows;
using System.Windows.Controls;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class UnitLevelsDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly InventoryService _inv;
    private readonly Product _product;

    public UnitLevelsDialog(AppDbContext db, Product product)
    {
        _db = db;
        _inv = new InventoryService(db);
        _product = product;
        InitializeComponent();
        LoadUnits();
    }

    private void LoadUnits()
    {
        TxtHeader.Text = $"مستويات التعبئة";
        TxtSubHeader.Text = _product.Name;

        var units = _db.ProductUnits.Where(u => u.ProductId == _product.Id)
            .OrderBy(u => u.UnitType).ToList();

        var cardData = units.Select(u =>
        {
            var (icon, color) = u.UnitType switch
            {
                UnitType.Carton => ("📦", "#1A237E"),
                UnitType.Box => ("📋", "#00897B"),
                UnitType.Piece => ("⚪", "#546E7A"),
                _ => ("📦", "#78909C")
            };

            string contains = "";
            if (u.UnitType == UnitType.Carton)
            {
                var children = units.FirstOrDefault(x => x.ParentUnitId == u.Id);
                if (children != null)
                    contains = $"يحتوي على {u.QuantityPerParent} {children.Name}";
            }
            else if (u.UnitType == UnitType.Box)
            {
                var piece = units.FirstOrDefault(x => x.UnitType == UnitType.Piece && x.ParentUnitId == u.Id);
                if (piece != null)
                    contains = $"يحتوي على {u.QuantityPerParent} قطعة";
            }
            else if (u.UnitType == UnitType.Piece)
                contains = "الوحدة الأساسية";

            return new
            {
                u.Name,
                UnitTypeDisplay = u.UnitType switch
                {
                    UnitType.Carton => "كرتونة",
                    UnitType.Box => "علبة",
                    UnitType.Piece => "قطعة",
                    _ => ""
                },
                ContainsDisplay = contains,
                RetailDisplay = $"{u.RetailPrice:0.##} ج.م",
                WholesaleDisplay = $"{u.WholesalePrice:0.##} ج.م",
                LevelIcon = icon,
                LevelColor = color
            };
        }).ToList();

        UnitsCards.ItemsSource = cardData;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, null);
    }

    private void StockDeduction_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockDeductionDialog(_db, _product);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
                LoadUnits();
        };
    }

    private void StockMovement_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new StockMovementDialog(_db, _product);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            LoadUnits();
        };
    }

    private void EditProduct_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new ProductDialog(_db, _product);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                LoadUnits();
                DialogClosed?.Invoke(this, true);
            }
        };
    }
}
