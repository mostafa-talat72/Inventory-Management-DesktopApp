using System.Windows;
using System.Windows.Controls;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class ProductDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly Product? _product;
    private readonly HashSet<string> _dirtyFields = [];
    private bool _loaded;
    private bool _isUpdating;

    public ProductDialog(AppDbContext db, Product? product = null)
    {
        InitializeComponent();
        _db = db;
        _product = product;

        if (product != null)
        {
            LoadProductData();
            TxtHeader.Text = "تعديل المنتج";
        }

        _loaded = true;
    }

    private void LoadProductData()
    {
        _loaded = false;

        TxtName.Text = _product!.Name;
        TxtDescription.Text = _product.Description;
        BtnSave.Content = "حفظ التعديلات";

        var units = _db.ProductUnits.Where(u => u.ProductId == _product.Id).ToList();
        var piece = units.FirstOrDefault(u => u.UnitType == UnitType.Piece);
        var box = units.FirstOrDefault(u => u.UnitType == UnitType.Box);
        var carton = units.FirstOrDefault(u => u.UnitType == UnitType.Carton);

        if (piece != null)
        {
            TxtPieceName.Text = piece.Name;
            TxtPieceRetail.Text = piece.RetailPrice.ToString();
            TxtPieceWholesale.Text = piece.WholesalePrice == piece.RetailPrice ? "" : piece.WholesalePrice.ToString("F2");
        }

        if (box != null)
        {
            ChkHasBox.IsChecked = true;
            TxtBoxName.Text = box.Name;
            TxtBoxQty.Text = box.QuantityPerParent.ToString();
            TxtBoxRetail.Text = box.RetailPrice.ToString("F2");
            TxtBoxWholesale.Text = box.WholesalePrice == box.RetailPrice ? "" : box.WholesalePrice.ToString("F2");
        }

        if (carton != null)
        {
            ChkHasCarton.IsChecked = true;
            TxtCartonName.Text = carton.Name;
            TxtCartonQty.Text = carton.QuantityPerParent.ToString();
            TxtCartonRetail.Text = carton.RetailPrice.ToString("F2");
            TxtCartonWholesale.Text = carton.WholesalePrice == carton.RetailPrice ? "" : carton.WholesalePrice.ToString("F2");

            bool hasBox = units.Any(u => u.UnitType == UnitType.Box && u.ParentUnitId == carton.Id);
            UpdateCartonUnitLabel(hasBox);
        }

        _loaded = true;
    }

    private void ChkHasBox_Checked(object sender, RoutedEventArgs e)
    {
        BoxPanel.IsEnabled = true;
        BoxPanel.Opacity = 1;
        _dirtyFields.Clear();
        UpdateCartonUnitLabel(ChkHasCarton.IsChecked == true);
        UpdateAutoPrices();
    }

    private void ChkHasBox_Unchecked(object sender, RoutedEventArgs e)
    {
        BoxPanel.IsEnabled = false;
        BoxPanel.Opacity = 0.5;
        _dirtyFields.Clear();
        UpdateCartonUnitLabel(ChkHasCarton.IsChecked == true);
        UpdateAutoPrices();
    }

    private void ChkHasCarton_Checked(object sender, RoutedEventArgs e)
    {
        CartonPanel.IsEnabled = true;
        CartonPanel.Opacity = 1;
        _dirtyFields.Clear();
        UpdateCartonUnitLabel(ChkHasBox.IsChecked == true);
        UpdateAutoPrices();
    }

    private void ChkHasCarton_Unchecked(object sender, RoutedEventArgs e)
    {
        CartonPanel.IsEnabled = false;
        CartonPanel.Opacity = 0.5;
        _dirtyFields.Clear();
        UpdateAutoPrices();
    }

    private void UpdateCartonUnitLabel(bool cartonHasBoxes)
    {
        if (ChkHasCarton.IsChecked != true) return;
        if (cartonHasBoxes)
        {
            TxtCartonUnitLabel.Text = "الكرتونة تحتوي على: علب";
            TxtCartonHint.Text = "* السعر يُحتسب تلقائياً من سعر العلبة × عدد العلب";
        }
        else
        {
            TxtCartonUnitLabel.Text = "الكرتونة تحتوي على: قطع مباشرة";
            TxtCartonHint.Text = "* السعر يُحتسب تلقائياً من سعر القطعة × عدد القطع";
        }
    }

    private void Qty_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded || _isUpdating) return;
        UpdateAutoPrices();
    }

    private void Price_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded || _isUpdating) return;

        var tb = (TextBox)sender;
        if (tb == TxtBoxRetail) _dirtyFields.Add("BoxRetail");
        else if (tb == TxtBoxWholesale) _dirtyFields.Add("BoxWholesale");
        else if (tb == TxtCartonRetail) _dirtyFields.Add("CartonRetail");
        else if (tb == TxtCartonWholesale) _dirtyFields.Add("CartonWholesale");

        UpdateAutoPrices();
    }

    private void UpdateAutoPrices()
    {
        if (_isUpdating) return;
        _isUpdating = true;

        if (!decimal.TryParse(TxtPieceRetail.Text, out decimal pieceRetail))
        {
            _isUpdating = false;
            return;
        }

        decimal pieceWholesale = decimal.TryParse(TxtPieceWholesale.Text, out decimal pw) ? pw : pieceRetail;

        bool hasBox = ChkHasBox.IsChecked == true;
        bool hasCarton = ChkHasCarton.IsChecked == true;
        bool boxQtyValid = int.TryParse(TxtBoxQty.Text, out int boxQty) && boxQty > 0;
        bool cartonQtyValid = int.TryParse(TxtCartonQty.Text, out int cartonQty) && cartonQty > 0;

        // Box prices
        if (hasBox && boxQtyValid)
        {
            if (!_dirtyFields.Contains("BoxRetail"))
                TxtBoxRetail.Text = (pieceRetail * boxQty).ToString("F2");
            if (!_dirtyFields.Contains("BoxWholesale"))
                TxtBoxWholesale.Text = (pieceWholesale * boxQty).ToString("F2");
        }

        // Carton prices
        if (hasCarton && cartonQtyValid)
        {
            bool cartonHasBoxes = hasBox && boxQtyValid;

            if (cartonHasBoxes)
            {
                decimal boxRetail = decimal.TryParse(TxtBoxRetail.Text, out decimal br) ? br : pieceRetail * boxQty;
                decimal boxWholesale = decimal.TryParse(TxtBoxWholesale.Text, out decimal bw) ? bw : pieceWholesale * boxQty;

                if (!_dirtyFields.Contains("CartonRetail"))
                    TxtCartonRetail.Text = (boxRetail * cartonQty).ToString("F2");
                if (!_dirtyFields.Contains("CartonWholesale"))
                    TxtCartonWholesale.Text = (boxWholesale * cartonQty).ToString("F2");
            }
            else
            {
                if (!_dirtyFields.Contains("CartonRetail"))
                    TxtCartonRetail.Text = (pieceRetail * cartonQty).ToString("F2");
                if (!_dirtyFields.Contains("CartonWholesale"))
                    TxtCartonWholesale.Text = (pieceWholesale * cartonQty).ToString("F2");
            }
        }

        _isUpdating = false;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            NotificationManager.ShowError("الرجاء إدخال اسم المنتج");
            return;
        }

        if (string.IsNullOrWhiteSpace(TxtPieceRetail.Text) || !decimal.TryParse(TxtPieceRetail.Text, out _))
        {
            NotificationManager.ShowError("الرجاء إدخال سعر القطاعي للقطعة");
            return;
        }

        Product product;
        if (_product != null)
        {
            product = _product;
            _db.ProductUnits.RemoveRange(_db.ProductUnits.Where(u => u.ProductId == product.Id));
        }
        else
        {
            product = new Product();
            _db.Products.Add(product);
        }

        product.Name = TxtName.Text.Trim();
        product.Description = TxtDescription.Text?.Trim();
        _db.SaveChanges();

        decimal pieceRetail = decimal.Parse(TxtPieceRetail.Text); // validated earlier
        decimal pieceWholesale = decimal.TryParse(TxtPieceWholesale.Text, out decimal pw) ? pw : pieceRetail;

        var pieceUnit = new ProductUnit
        {
            ProductId = product.Id,
            Name = string.IsNullOrWhiteSpace(TxtPieceName.Text) ? "قطعة" : TxtPieceName.Text.Trim(),
            UnitType = UnitType.Piece,
            RetailPrice = pieceRetail,
            WholesalePrice = pieceWholesale,
            IsBaseUnit = true,
            QuantityPerParent = 1
        };
        _db.ProductUnits.Add(pieceUnit);
        _db.SaveChanges();

        ProductUnit? boxUnit = null;
        if (ChkHasBox.IsChecked == true && int.TryParse(TxtBoxQty.Text, out int boxQty) && boxQty > 0)
        {
            decimal boxRetail = decimal.TryParse(TxtBoxRetail.Text, out decimal br) ? br : 0;
            decimal boxWholesale = decimal.TryParse(TxtBoxWholesale.Text, out decimal bw) ? bw : boxRetail;
            string boxName = string.IsNullOrWhiteSpace(TxtBoxName.Text) ? "علبة" : TxtBoxName.Text.Trim();

            boxUnit = new ProductUnit
            {
                ProductId = product.Id,
                Name = boxName,
                UnitType = UnitType.Box,
                RetailPrice = boxRetail,
                WholesalePrice = boxWholesale,
                QuantityPerParent = boxQty
            };
            _db.ProductUnits.Add(boxUnit);
            _db.SaveChanges();
        }

        if (ChkHasCarton.IsChecked == true && int.TryParse(TxtCartonQty.Text, out int cartonQty) && cartonQty > 0)
        {
            decimal cartonRetail = decimal.TryParse(TxtCartonRetail.Text, out decimal cr) ? cr : 0;
            decimal cartonWholesale = decimal.TryParse(TxtCartonWholesale.Text, out decimal cw) ? cw : cartonRetail;
            string cartonName = string.IsNullOrWhiteSpace(TxtCartonName.Text) ? "كرتونة" : TxtCartonName.Text.Trim();

            var cartonUnit = new ProductUnit
            {
                ProductId = product.Id,
                Name = cartonName,
                UnitType = UnitType.Carton,
                RetailPrice = cartonRetail,
                WholesalePrice = cartonWholesale,
                QuantityPerParent = cartonQty
            };
            _db.ProductUnits.Add(cartonUnit);
            _db.SaveChanges();

            bool cartonHasBoxes = boxUnit != null;
            if (cartonHasBoxes)
            {
                boxUnit!.ParentUnitId = cartonUnit.Id;
                pieceUnit.ParentUnitId = boxUnit.Id;
            }
            else
            {
                pieceUnit.ParentUnitId = cartonUnit.Id;
            }
        }
        else if (boxUnit != null)
        {
            pieceUnit.ParentUnitId = boxUnit.Id;
        }

        _db.SaveChanges();
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}
