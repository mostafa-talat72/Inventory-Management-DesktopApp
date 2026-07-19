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
        else
        {
            ChkHasBox.IsChecked = true;
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

        ChkHasPiece.IsChecked = piece != null;
        if (piece != null)
        {
            TxtPieceName.Text = piece.Name;
            TxtPieceRetail.Text = piece.RetailPrice.ToString();
            TxtPieceWholesale.Text = piece.WholesalePrice == piece.RetailPrice ? "" : piece.WholesalePrice.ToString("0.##");
        }

        ChkHasBox.IsChecked = box != null;
        if (box != null)
        {
            TxtBoxName.Text = box.Name;
            TxtBoxQty.Text = box.QuantityPerParent.ToString();
            TxtBoxRetail.Text = box.RetailPrice.ToString("0.##");
            TxtBoxWholesale.Text = box.WholesalePrice == box.RetailPrice ? "" : box.WholesalePrice.ToString("0.##");
        }

        ChkHasCarton.IsChecked = carton != null;
        if (carton != null)
        {
            TxtCartonName.Text = carton.Name;
            TxtCartonQty.Text = carton.QuantityPerParent.ToString();
            TxtCartonRetail.Text = carton.RetailPrice.ToString("0.##");
            TxtCartonWholesale.Text = carton.WholesalePrice == carton.RetailPrice ? "" : carton.WholesalePrice.ToString("0.##");

            bool hasBox = units.Any(u => u.UnitType == UnitType.Box && u.ParentUnitId == carton.Id);
        }

        UpdatePieceDependentFields();
        _loaded = true;
    }

    private void ChkHasBox_Checked(object sender, RoutedEventArgs e)
    {
        BoxPanel.IsEnabled = true;
        BoxPanel.Opacity = 1;
        _dirtyFields.Clear();
        UpdatePieceDependentFields();
        UpdateAutoPrices();
    }

    private void ChkHasBox_Unchecked(object sender, RoutedEventArgs e)
    {
        BoxPanel.IsEnabled = false;
        BoxPanel.Opacity = 0.5;
        _dirtyFields.Clear();
        UpdatePieceDependentFields();
        UpdateAutoPrices();
    }

    private void ChkHasCarton_Checked(object sender, RoutedEventArgs e)
    {
        CartonPanel.IsEnabled = true;
        CartonPanel.Opacity = 1;
        _dirtyFields.Clear();
        UpdatePieceDependentFields();
        UpdateAutoPrices();
    }

    private void ChkHasCarton_Unchecked(object sender, RoutedEventArgs e)
    {
        CartonPanel.IsEnabled = false;
        CartonPanel.Opacity = 0.5;
        _dirtyFields.Clear();
        UpdatePieceDependentFields();
        UpdateAutoPrices();
    }

    private void ChkHasPiece_Checked(object sender, RoutedEventArgs e)
    {
        PiecePanel.IsEnabled = true;
        PiecePanel.Opacity = 1;
        _dirtyFields.Clear();
        UpdatePieceDependentFields();
        UpdateAutoPrices();
    }

    private void ChkHasPiece_Unchecked(object sender, RoutedEventArgs e)
    {
        PiecePanel.IsEnabled = false;
        PiecePanel.Opacity = 0.5;
        _dirtyFields.Clear();
        UpdatePieceDependentFields();
        UpdateAutoPrices();
    }

    private void UpdatePieceDependentFields()
    {
        bool hasPiece = ChkHasPiece.IsChecked == true;
        bool hasBox = ChkHasBox.IsChecked == true;
        bool hasCarton = ChkHasCarton.IsChecked == true;

        if (hasBox && !hasPiece)
        {
            BoxQtyCol.Width = new GridLength(0);
            TxtBoxQty.Visibility = Visibility.Collapsed;
        }
        else
        {
            BoxQtyCol.Width = new GridLength(1, GridUnitType.Star);
            TxtBoxQty.Visibility = Visibility.Visible;
        }

        if (hasCarton && !hasBox && !hasPiece)
        {
            CartonQtyCol.Width = new GridLength(0);
            TxtCartonQty.Visibility = Visibility.Collapsed;
        }
        else
        {
            CartonQtyCol.Width = new GridLength(1, GridUnitType.Star);
            TxtCartonQty.Visibility = Visibility.Visible;
        }

        // Box label & hint
        if (hasPiece)
        {
            TxtBoxUnitLabel.Text = "العلبة تحتوي على: قطع";
            TxtBoxHint.Text = "* السعر يُحتسب تلقائياً من سعر القطعة × عدد القطع";
            TxtBoxHint.Visibility = Visibility.Visible;
        }
        else
        {
            TxtBoxUnitLabel.Text = "العلبة - وحدة مستقلة";
            TxtBoxHint.Visibility = Visibility.Collapsed;
        }

        // Carton label & hint
        if (hasCarton)
        {
            if (hasBox)
            {
                TxtCartonUnitLabel.Text = "الكرتونة تحتوي على: علب";
                TxtCartonHint.Text = "* السعر يُحتسب تلقائياً من سعر العلبة × عدد العلب";
                TxtCartonHint.Visibility = Visibility.Visible;
            }
            else if (hasPiece)
            {
                TxtCartonUnitLabel.Text = "الكرتونة تحتوي على: قطع مباشرة";
                TxtCartonHint.Text = "* السعر يُحتسب تلقائياً من سعر القطعة × عدد القطع";
                TxtCartonHint.Visibility = Visibility.Visible;
            }
            else
            {
                TxtCartonUnitLabel.Text = "الكرتونة - وحدة مستقلة";
                TxtCartonHint.Text = "* أدخل السعر يدوياً";
                TxtCartonHint.Visibility = Visibility.Visible;
            }
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

        bool hasPiece = ChkHasPiece.IsChecked == true;
        bool hasBox = ChkHasBox.IsChecked == true;
        bool hasCarton = ChkHasCarton.IsChecked == true;

        decimal pieceRetail = 0, pieceWholesale = 0;
        bool pieceValid = hasPiece && decimal.TryParse(TxtPieceRetail.Text, out pieceRetail);
        if (hasPiece && !pieceValid)
        {
            _isUpdating = false;
            return;
        }
        if (pieceValid)
            pieceWholesale = decimal.TryParse(TxtPieceWholesale.Text, out decimal pw) ? pw : pieceRetail;

        bool boxQtyValid = false;
        int boxQty = 0;
        if (hasBox && int.TryParse(TxtBoxQty.Text, out boxQty) && boxQty > 0)
            boxQtyValid = true;

        bool cartonQtyValid = false;
        int cartonQty = 0;
        if (hasCarton && int.TryParse(TxtCartonQty.Text, out cartonQty) && cartonQty > 0)
            cartonQtyValid = true;

        // Box prices from piece
        if (hasBox && pieceValid && boxQtyValid)
        {
            if (!_dirtyFields.Contains("BoxRetail"))
                TxtBoxRetail.Text = (pieceRetail * boxQty).ToString("0.##");
            if (!_dirtyFields.Contains("BoxWholesale"))
                TxtBoxWholesale.Text = (pieceWholesale * boxQty).ToString("0.##");
        }

        // Carton prices
        if (hasCarton && cartonQtyValid)
        {
            bool cartonFromBox = hasBox;
            decimal boxRetail = 0, boxWholesale = 0;
            bool boxPricesValid = false;

            if (cartonFromBox)
            {
                boxPricesValid = decimal.TryParse(TxtBoxRetail.Text, out boxRetail);
                boxWholesale = decimal.TryParse(TxtBoxWholesale.Text, out decimal bw) ? bw : boxRetail;
            }

            if (cartonFromBox && boxPricesValid)
            {
                if (!_dirtyFields.Contains("CartonRetail"))
                    TxtCartonRetail.Text = (boxRetail * cartonQty).ToString("0.##");
                if (!_dirtyFields.Contains("CartonWholesale"))
                    TxtCartonWholesale.Text = (boxWholesale * cartonQty).ToString("0.##");
            }
            else if (pieceValid)
            {
                if (!_dirtyFields.Contains("CartonRetail"))
                    TxtCartonRetail.Text = (pieceRetail * cartonQty).ToString("0.##");
                if (!_dirtyFields.Contains("CartonWholesale"))
                    TxtCartonWholesale.Text = (pieceWholesale * cartonQty).ToString("0.##");
            }
        }

        _isUpdating = false;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text) || TxtName.Text == ProductApp.Converters.WatermarkBehavior.GetWatermark(TxtName))
        {
            NotificationManager.ShowError("الرجاء إدخال اسم المنتج");
            return;
        }

        bool hasPiece = ChkHasPiece.IsChecked == true;
        bool hasBox = ChkHasBox.IsChecked == true;
        bool hasCarton = ChkHasCarton.IsChecked == true;

        if (!hasPiece && !hasBox && !hasCarton)
        {
            NotificationManager.ShowError("الرجاء اختيار نوع تعبئة واحد على الأقل (قطعة، علبة، كرتونة)");
            return;
        }

        if (hasPiece && (string.IsNullOrWhiteSpace(TxtPieceRetail.Text) || !decimal.TryParse(TxtPieceRetail.Text, out _)))
        {
            NotificationManager.ShowError("الرجاء إدخال سعر القطاعي للقطعة");
            return;
        }
        if (hasBox && (string.IsNullOrWhiteSpace(TxtBoxRetail.Text) || !decimal.TryParse(TxtBoxRetail.Text, out _)))
        {
            NotificationManager.ShowError("الرجاء إدخال سعر القطاعي للعلبة");
            return;
        }
        if (hasCarton && (string.IsNullOrWhiteSpace(TxtCartonRetail.Text) || !decimal.TryParse(TxtCartonRetail.Text, out _)))
        {
            NotificationManager.ShowError("الرجاء إدخال سعر القطاعي للكرتونة");
            return;
        }

        Product product;
        if (_product != null)
        {
            product = _product;
            _db.Attach(product);
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

        ProductUnit? pieceUnit = null;
        ProductUnit? boxUnit = null;
        ProductUnit? cartonUnit = null;

        if (hasPiece)
        {
            decimal pieceRetail = decimal.Parse(TxtPieceRetail.Text);
            decimal pieceWholesale = decimal.TryParse(TxtPieceWholesale.Text, out decimal pw) ? pw : pieceRetail;

            pieceUnit = new ProductUnit
            {
                ProductId = product.Id,
                Name = string.IsNullOrWhiteSpace(TxtPieceName.Text) ? "قطعة" : TxtPieceName.Text.Trim(),
                UnitType = UnitType.Piece,
                RetailPrice = pieceRetail,
                WholesalePrice = pieceWholesale,
                IsBaseUnit = !hasBox && !hasCarton,
                QuantityPerParent = 1
            };
            _db.ProductUnits.Add(pieceUnit);
            _db.SaveChanges();
        }

        if (hasBox)
        {
            bool boxQtyValid = int.TryParse(TxtBoxQty.Text, out int boxQty) && boxQty > 0;
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
                QuantityPerParent = boxQtyValid ? boxQty : 1,
                IsBaseUnit = !hasPiece && !hasCarton
            };
            _db.ProductUnits.Add(boxUnit);
            _db.SaveChanges();
        }

        if (hasCarton)
        {
            bool cartonQtyValid = int.TryParse(TxtCartonQty.Text, out int cartonQty) && cartonQty > 0;
            decimal cartonRetail = decimal.TryParse(TxtCartonRetail.Text, out decimal cr) ? cr : 0;
            decimal cartonWholesale = decimal.TryParse(TxtCartonWholesale.Text, out decimal cw) ? cw : cartonRetail;
            string cartonName = string.IsNullOrWhiteSpace(TxtCartonName.Text) ? "كرتونة" : TxtCartonName.Text.Trim();

            cartonUnit = new ProductUnit
            {
                ProductId = product.Id,
                Name = cartonName,
                UnitType = UnitType.Carton,
                RetailPrice = cartonRetail,
                WholesalePrice = cartonWholesale,
                QuantityPerParent = cartonQtyValid ? cartonQty : 1,
                IsBaseUnit = !hasPiece && !hasBox
            };
            _db.ProductUnits.Add(cartonUnit);
            _db.SaveChanges();
        }

        // Link hierarchy
        if (pieceUnit != null)
        {
            if (boxUnit != null)
            {
                pieceUnit.ParentUnitId = boxUnit.Id;
                if (cartonUnit != null)
                    boxUnit.ParentUnitId = cartonUnit.Id;
            }
            else if (cartonUnit != null)
            {
                pieceUnit.ParentUnitId = cartonUnit.Id;
            }
        }
        else if (boxUnit != null && cartonUnit != null)
        {
            boxUnit.ParentUnitId = cartonUnit.Id;
        }

        _db.SaveChanges();
        DialogClosed?.Invoke(this, true);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}
