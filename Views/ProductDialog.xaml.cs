using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class ProductDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private static readonly string ImagesFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MTE Stock", "ProductImages");

    private readonly AppDbContext _db;
    private readonly Product? _product;
    private readonly HashSet<string> _dirtyFields = [];
    private string? _selectedImagePath;
    private bool _loaded;
    private bool _isUpdating;

    public ProductDialog(AppDbContext db, Product? product = null, string? prefillBarcode = null)
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
            if (!string.IsNullOrWhiteSpace(prefillBarcode))
                TxtBarcode.Text = prefillBarcode.Trim();
        }

        _loaded = true;
        UpdateUnitLabels();
    }

    private void LoadProductData()
    {
        _loaded = false;

        TxtName.Text = _product!.Name;
        TxtDescription.Text = _product.Description;
        TxtBarcode.Text = _product.Barcode ?? "";
        BtnSave.Content = "حفظ التعديلات";

        if (!string.IsNullOrWhiteSpace(_product.ImagePath) && File.Exists(_product.ImagePath))
        {
            _selectedImagePath = _product.ImagePath;
            ShowImagePreview(_product.ImagePath);
        }

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
            TxtPieceMin.Text = piece.MinStockLevel > 0 ? piece.MinStockLevel.ToString() : "";
        }

        ChkHasBox.IsChecked = box != null;
        if (box != null)
        {
            TxtBoxName.Text = box.Name;
            TxtBoxQty.Text = box.QuantityPerParent.ToString();
            TxtBoxRetail.Text = box.RetailPrice.ToString("0.##");
            TxtBoxWholesale.Text = box.WholesalePrice == box.RetailPrice ? "" : box.WholesalePrice.ToString("0.##");
            TxtBoxMin.Text = box.MinStockLevel > 0 ? box.MinStockLevel.ToString() : "";
        }

        ChkHasCarton.IsChecked = carton != null;
        if (carton != null)
        {
            TxtCartonName.Text = carton.Name;
            TxtCartonQty.Text = carton.QuantityPerParent.ToString();
            TxtCartonRetail.Text = carton.RetailPrice.ToString("0.##");
            TxtCartonWholesale.Text = carton.WholesalePrice == carton.RetailPrice ? "" : carton.WholesalePrice.ToString("0.##");
            TxtCartonMin.Text = carton.MinStockLevel > 0 ? carton.MinStockLevel.ToString() : "";

            bool hasBox = units.Any(u => u.UnitType == UnitType.Box && u.ParentUnitId == carton.Id);
        }

        UpdateUnitLabels();
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

        var pieceName = PieceName;
        var boxName = BoxName;
        var cartonName = CartonName;

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
            TxtBoxUnitLabel.Text = $"{boxName} تحتوي على: {pieceName}";
            TxtBoxHint.Text = $"* السعر يُحتسب تلقائياً من سعر {pieceName} × عدد {pieceName}";
            TxtBoxHint.Visibility = Visibility.Visible;
        }
        else
        {
            TxtBoxUnitLabel.Text = $"{boxName} - وحدة مستقلة";
            TxtBoxHint.Visibility = Visibility.Collapsed;
        }

        // Carton label & hint
        if (hasCarton)
        {
            if (hasBox)
            {
                TxtCartonUnitLabel.Text = $"{cartonName} تحتوي على: {boxName}";
                TxtCartonHint.Text = $"* السعر يُحتسب تلقائياً من سعر {boxName} × عدد {boxName}";
                TxtCartonHint.Visibility = Visibility.Visible;
            }
            else if (hasPiece)
            {
                TxtCartonUnitLabel.Text = $"{cartonName} تحتوي على: {pieceName} مباشرة";
                TxtCartonHint.Text = $"* السعر يُحتسب تلقائياً من سعر {pieceName} × عدد {pieceName}";
                TxtCartonHint.Visibility = Visibility.Visible;
            }
            else
            {
                TxtCartonUnitLabel.Text = $"{cartonName} - وحدة مستقلة";
                TxtCartonHint.Text = "* أدخل السعر يدوياً";
                TxtCartonHint.Visibility = Visibility.Visible;
            }
        }

        PieceMinField.Visibility = hasPiece ? Visibility.Visible : Visibility.Collapsed;
        BoxMinField.Visibility = hasBox ? Visibility.Visible : Visibility.Collapsed;
        CartonMinField.Visibility = hasCarton ? Visibility.Visible : Visibility.Collapsed;
    }

    private string PieceName => string.IsNullOrWhiteSpace(TxtPieceName?.Text) ? "قطعة" : TxtPieceName.Text.Trim();
    private string BoxName => string.IsNullOrWhiteSpace(TxtBoxName?.Text) ? "علبة" : TxtBoxName.Text.Trim();
    private string CartonName => string.IsNullOrWhiteSpace(TxtCartonName?.Text) ? "كرتونة" : TxtCartonName.Text.Trim();

    private void UpdateUnitLabels()
    {
        if (ChkHasPiece == null || ChkHasBox == null || ChkHasCarton == null) return;

        ChkHasPiece.Content = $"يوجد {PieceName}";
        ChkHasBox.Content = $"يوجد {BoxName}";
        ChkHasCarton.Content = $"يوجد {CartonName}";
        TxtPieceTitle.Text = PieceName;
        TxtBoxTitle.Text = BoxName;
        TxtCartonTitle.Text = CartonName;
        if (TxtPieceMinLabel != null)
            TxtPieceMinLabel.Text = $"({PieceName}):";
        if (TxtBoxMinLabel != null)
            TxtBoxMinLabel.Text = $"({BoxName}):";
        if (TxtCartonMinLabel != null)
            TxtCartonMinLabel.Text = $"({CartonName}):";
        UpdatePieceDependentFields();
    }

    private void UnitName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loaded) return;
        UpdateUnitLabels();
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
        bool pieceValid = hasPiece && TryParseDecimal(TxtPieceRetail.Text, out pieceRetail);
        if (hasPiece && !pieceValid)
        {
            _isUpdating = false;
            return;
        }
        if (pieceValid)
            pieceWholesale = TryParseDecimal(TxtPieceWholesale.Text, out decimal pw) ? pw : pieceRetail;

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
                boxPricesValid = TryParseDecimal(TxtBoxRetail.Text, out boxRetail);
                boxWholesale = TryParseDecimal(TxtBoxWholesale.Text, out decimal bw) ? bw : boxRetail;
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
        if (SaveProduct())
            DialogClosed?.Invoke(this, true);
    }

    private void BtnSaveAndAdd_Click(object sender, RoutedEventArgs e)
    {
        if (SaveProduct())
        {
            NotificationManager.ShowSuccess("تم الحفظ — يمكنك إضافة منتج آخر");
            ResetForm();
        }
    }

    private void ResetForm()
    {
        _loaded = false;
        _dirtyFields.Clear();

        TxtName.Text = string.Empty;
        TxtDescription.Text = string.Empty;
        TxtBarcode.Text = string.Empty;

        ChkHasPiece.IsChecked = false;
        TxtPieceName.Text = "قطعة";
        TxtPieceRetail.Text = string.Empty;
        TxtPieceWholesale.Text = string.Empty;

        ChkHasBox.IsChecked = true;
        TxtBoxName.Text = "علبة";
        TxtBoxQty.Text = string.Empty;
        TxtBoxRetail.Text = string.Empty;
        TxtBoxWholesale.Text = string.Empty;

        ChkHasCarton.IsChecked = false;
        TxtCartonName.Text = "كرتونة";
        TxtCartonQty.Text = string.Empty;
        TxtCartonRetail.Text = string.Empty;
        TxtCartonWholesale.Text = string.Empty;

        _loaded = true;
        TxtName.Focus();
    }

    private bool SaveProduct()
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text) || TxtName.Text == ProductApp.Converters.WatermarkBehavior.GetWatermark(TxtName))
        {
            NotificationManager.ShowError("الرجاء إدخال اسم المنتج");
            return false;
        }

        var name = TxtName.Text.Trim();
        var excludeId = _product?.Id ?? 0;
        if (_db.Products.Any(p => !p.IsDeleted && p.Name == name && p.Id != excludeId))
        {
            NotificationManager.ShowError("هذا الاسم موجود بالفعل");
            return false;
        }

        bool hasPiece = ChkHasPiece.IsChecked == true;
        bool hasBox = ChkHasBox.IsChecked == true;
        bool hasCarton = ChkHasCarton.IsChecked == true;

        if (!hasPiece && !hasBox && !hasCarton)
        {
            NotificationManager.ShowError($"الرجاء اختيار نوع تعبئة واحد على الأقل ({PieceName}، {BoxName}، {CartonName})");
            return false;
        }

        if (hasPiece && (string.IsNullOrWhiteSpace(TxtPieceRetail.Text) || !TryParseDecimal(TxtPieceRetail.Text, out _)))
        {
            NotificationManager.ShowError($"الرجاء إدخال سعر القطاعي لـ{PieceName}");
            return false;
        }
        if (hasBox && (string.IsNullOrWhiteSpace(TxtBoxRetail.Text) || !TryParseDecimal(TxtBoxRetail.Text, out _)))
        {
            NotificationManager.ShowError($"الرجاء إدخال سعر القطاعي لـ{BoxName}");
            return false;
        }
        if (hasCarton && (string.IsNullOrWhiteSpace(TxtCartonRetail.Text) || !TryParseDecimal(TxtCartonRetail.Text, out _)))
        {
            NotificationManager.ShowError($"الرجاء إدخال سعر القطاعي لـ{CartonName}");
            return false;
        }

        Product product;
        if (_product != null)
        {
            product = _db.Products.Find(_product!.Id)!;
            var oldUnits = _db.ProductUnits.Where(u => u.ProductId == product.Id).ToList();
            _db.ProductUnits.RemoveRange(oldUnits);
        }
        else
        {
            product = new Product();
            _db.Products.Add(product);
        }

        product.Name = name;
        product.Description = TxtDescription.Text?.Trim();
        var barcodeText = TxtBarcode.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(barcodeText) && barcodeText != ProductApp.Converters.WatermarkBehavior.GetWatermark(TxtBarcode))
        {
            if (_db.Products.Any(p => !p.IsDeleted && p.Barcode == barcodeText && p.Id != excludeId))
            {
                NotificationManager.ShowError("هذا الباركود مستخدم لمنتج آخر");
                return false;
            }
            product.Barcode = barcodeText;
        }
        else
        {
            product.Barcode = null;
        }
        product.ImagePath = _selectedImagePath;
        _db.SaveChanges();

        ProductUnit? pieceUnit = null;
        ProductUnit? boxUnit = null;
        ProductUnit? cartonUnit = null;

        if (hasPiece)
        {
            TryParseDecimal(TxtPieceRetail.Text, out decimal pieceRetail);
            decimal pieceWholesale = TryParseDecimal(TxtPieceWholesale.Text, out decimal pw) ? pw : pieceRetail;
            pieceUnit = new ProductUnit
            {
                ProductId = product.Id,
                Name = string.IsNullOrWhiteSpace(TxtPieceName.Text) ? "قطعة" : TxtPieceName.Text.Trim(),
                UnitType = UnitType.Piece,
                RetailPrice = pieceRetail,
                WholesalePrice = pieceWholesale,
                MinStockLevel = int.TryParse(TxtPieceMin.Text?.Trim(), out int pieceMin) && pieceMin > 0 ? pieceMin : 0,
                IsBaseUnit = !hasBox && !hasCarton,
                QuantityPerParent = 1
            };
            _db.ProductUnits.Add(pieceUnit);
            _db.SaveChanges();
        }

        if (hasBox)
        {
            bool boxQtyValid = int.TryParse(TxtBoxQty.Text, out int boxQty) && boxQty > 0;
            decimal boxRetail = TryParseDecimal(TxtBoxRetail.Text, out decimal br) ? br : 0;
            decimal boxWholesale = TryParseDecimal(TxtBoxWholesale.Text, out decimal bw) ? bw : boxRetail;
            boxUnit = new ProductUnit
            {
                ProductId = product.Id,
                Name = string.IsNullOrWhiteSpace(TxtBoxName.Text) ? "علبة" : TxtBoxName.Text.Trim(),
                UnitType = UnitType.Box,
                RetailPrice = boxRetail,
                WholesalePrice = boxWholesale,
                MinStockLevel = int.TryParse(TxtBoxMin.Text?.Trim(), out int boxMin) && boxMin > 0 ? boxMin : 0,
                QuantityPerParent = boxQtyValid ? boxQty : 1,
                IsBaseUnit = !hasPiece && !hasCarton
            };
            _db.ProductUnits.Add(boxUnit);
            _db.SaveChanges();
        }

        if (hasCarton)
        {
            bool cartonQtyValid = int.TryParse(TxtCartonQty.Text, out int cartonQty) && cartonQty > 0;
            decimal cartonRetail = TryParseDecimal(TxtCartonRetail.Text, out decimal cr) ? cr : 0;
            decimal cartonWholesale = TryParseDecimal(TxtCartonWholesale.Text, out decimal cw) ? cw : cartonRetail;
            cartonUnit = new ProductUnit
            {
                ProductId = product.Id,
                Name = string.IsNullOrWhiteSpace(TxtCartonName.Text) ? "كرتونة" : TxtCartonName.Text.Trim(),
                UnitType = UnitType.Carton,
                RetailPrice = cartonRetail,
                WholesalePrice = cartonWholesale,
                MinStockLevel = int.TryParse(TxtCartonMin.Text?.Trim(), out int cartonMin) && cartonMin > 0 ? cartonMin : 0,
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
                if (cartonUnit != null) boxUnit.ParentUnitId = cartonUnit.Id;
            }
            else if (cartonUnit != null)
                pieceUnit.ParentUnitId = cartonUnit.Id;
        }
        else if (boxUnit != null && cartonUnit != null)
            boxUnit.ParentUnitId = cartonUnit.Id;

        _db.SaveChanges();
        return true;
    }

    private static bool TryParseDecimal(string? text, out decimal value) =>
        decimal.TryParse(text?.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out value);

    private void BtnScanBarcode_Click(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this) is not MainWindow mainWindow) return;
        var scanner = new BarcodeScannerDialog();
        mainWindow.ShowOverlay(scanner);
        scanner.ScanFinished += (_, code) =>
        {
            mainWindow.HideOverlay();
            if (!string.IsNullOrWhiteSpace(code))
            {
                TxtBarcode.Text = code.Trim();
                NotificationManager.ShowSuccess("تم إدخال الباركود: " + code);
            }
        };
    }

    private void BtnChooseImage_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "اختيار صورة المنتج",
            Filter = "صور (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            Directory.CreateDirectory(ImagesFolder);
            var ext = Path.GetExtension(dialog.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".png";
            var dest = Path.Combine(ImagesFolder, $"{Guid.NewGuid():N}{ext}");
            File.Copy(dialog.FileName, dest, true);

            if (_selectedImagePath != null &&
                !string.IsNullOrWhiteSpace(_product?.ImagePath) &&
                !string.Equals(_selectedImagePath, _product.ImagePath, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(_selectedImagePath); } catch { }
            }

            _selectedImagePath = dest;
            ShowImagePreview(dest);
        }
        catch (Exception ex)
        {
            NotificationManager.ShowError($"تعذر حفظ الصورة:\n{ex.Message}");
        }
    }

    private void BtnRemoveImage_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedImagePath != null &&
            !string.Equals(_selectedImagePath, _product?.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            try { File.Delete(_selectedImagePath); } catch { }
        }
        _selectedImagePath = null;
        ImgPreview.Source = null;
        ImgPreview.Visibility = Visibility.Collapsed;
        ImgPlaceholder.Visibility = Visibility.Visible;
    }

    private void ShowImagePreview(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 112;
            bitmap.EndInit();
            ImgPreview.Source = bitmap;
            ImgPreview.Visibility = Visibility.Visible;
            ImgPlaceholder.Visibility = Visibility.Collapsed;
        }
        catch
        {
            ImgPreview.Source = null;
            ImgPreview.Visibility = Visibility.Collapsed;
            ImgPlaceholder.Visibility = Visibility.Visible;
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, false);
    }
}

