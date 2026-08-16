using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ProductApp.Services;

namespace ProductApp.Views;

public partial class SupplierInvoiceDetailsDialog : UserControl
{
    public event EventHandler<bool?>? DialogClosed;

    private readonly AppDbContext _db;
    private readonly SupplierInvoice _invoice;
    private readonly InventoryService _inv;

    public SupplierInvoiceDetailsDialog(AppDbContext db, SupplierInvoice invoice)
    {
        InitializeComponent();
        _db = db;
        _invoice = _db.SupplierInvoices
            .Include(i => i.Supplier)
            .Include(i => i.Items).ThenInclude(i => i.Product)
            .Include(i => i.Payments)
            .First(i => i.Id == invoice.Id);
        _inv = new InventoryService(_db);

        LoadData();
    }

    private void LoadData()
    {
        TxtTitle.Text = $"فاتورة مورد #{_invoice.Id}";
        TxtSubtitle.Text = $"{_invoice.SupplierName ?? "بدون مورد"} — {_invoice.CreatedAt:yyyy/MM/dd}";

        var (statusText, statusBg, statusFg) = _invoice.Status switch
        {
            InvoiceStatus.Paid => ("مدفوعة", "#E8F5E9", "#2E7D32"),
            InvoiceStatus.PartiallyPaid => ("مدفوعة جزئياً", "#FFF8E1", "#F57F17"),
            InvoiceStatus.Cancelled => ("ملغاة", "#F5F5F5", "#9E9E9E"),
            _ => ("غير مدفوعة", "#FFEBEE", "#C62828")
        };
        StatusBadge.Background = (Brush)new BrushConverter().ConvertFrom(statusBg)!;
        TxtStatus.Text = statusText;
        TxtStatus.Foreground = (Brush)new BrushConverter().ConvertFrom(statusFg)!;

        TxtTotal.Text = $"{_invoice.TotalAmount:0.##} ج.م";
        TxtPaid.Text = $"{_invoice.TotalPaid:0.##} ج.م";
        TxtRemaining.Text = $"{_invoice.Remaining:0.##} ج.م";

        BtnPay.Visibility = _invoice.Status is InvoiceStatus.Paid or InvoiceStatus.Cancelled
            ? Visibility.Collapsed
            : Visibility.Visible;
        BtnAddPayment.Visibility = BtnPay.Visibility;

        // المنتجات
        var itemItems = _invoice.Items.OrderBy(i => i.Id).Select(i =>
        {
            var units = i.Product.Units.ToList();
            var cartonName = units.FirstOrDefault(u => u.UnitType == UnitType.Carton)?.Name ?? "كرتونة";
            var boxName    = units.FirstOrDefault(u => u.UnitType == UnitType.Box)?.Name ?? "علبة";
            var pieceName  = units.FirstOrDefault(u => u.UnitType == UnitType.Piece)?.Name ?? "قطعة";

            var parts = new System.Collections.Generic.List<string>();
            if (i.CartonQuantity > 0) parts.Add($"{i.CartonQuantity} {cartonName}");
            if (i.BoxQuantity > 0)    parts.Add($"{i.BoxQuantity} {boxName}");
            if (i.PieceQuantity > 0)  parts.Add($"{i.PieceQuantity} {pieceName}");

            return new ItemRow
            {
                ProductName = i.Product.Name,
                QtyDisplay = parts.Count > 0 ? string.Join("، ", parts) : "—",
                CostDisplay = $"{i.CostPrice:0.##} ج.م"
            };
        }).ToList();
        ItemsList.ItemsSource = itemItems;

        // الدفعات
        var payments = _invoice.Payments.OrderByDescending(p => p.PaymentDate).Select(p =>
        {
            var row = new PaymentRow
            {
                Id = p.Id,
                DateDisplay = p.PaymentDate.ToString("yyyy/MM/dd"),
                Method = p.PaymentMethod ?? "نقدي",
                Notes = p.Notes ?? "",
                AmountDisplay = $"{p.Amount:0.##} ج.م"
            };
            row.DeleteCommand = new RelayCommand(() => DeletePayment(p.Id));
            return row;
        }).ToList();
        PaymentsList.ItemsSource = payments;
        TxtNoPayments.Visibility = payments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void DeletePayment(int paymentId)
    {
        ConfirmDialog.Show("حذف الدفعة", "هل أنت متأكد من حذف هذه الدفعة؟", result =>
        {
            if (!result) return;
            var payment = _db.SupplierPayments.FirstOrDefault(p => p.Id == paymentId);
            if (payment == null) return;
            _db.SupplierPayments.Remove(payment);
            _db.SaveChanges();

            _db.Entry(_invoice).Collection(i => i.Payments).Load();
            _invoice.TotalPaid = _invoice.Payments.Sum(p => p.Amount);
            _invoice.Status = _invoice.Remaining <= 0
                ? (_invoice.Payments.Count > 0 ? InvoiceStatus.Paid : InvoiceStatus.Open)
                : InvoiceStatus.PartiallyPaid;
            _db.SaveChanges();
            App.NotifyDataChanged();

            App.AppBackup?.BackupIfOnOperation();
            LoadData();
        }, ConfirmDialog.DialogType.Danger);
    }

    private void BtnAddPayment_Click(object sender, RoutedEventArgs e)
    {
        var mainWindow = (MainWindow)Window.GetWindow(this);
        var dialog = new SupplierPaymentDialog(_db, _invoice);
        mainWindow.ShowOverlay(dialog);
        dialog.DialogClosed += (s, r) =>
        {
            mainWindow.HideOverlay();
            if (r == true)
            {
                _db.Entry(_invoice).Reload();
                _db.Entry(_invoice).Collection(i => i.Payments).Load();
                LoadData();
            }
        };
    }

    private void BtnPay_Click(object sender, RoutedEventArgs e) => BtnAddPayment_Click(sender, e);

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        var printer = new ReceiptPrinter(_db);
        printer.PrintSupplierInvoice(_invoice);
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        ConfirmDialog.Show("حذف فاتورة المورد",
            $"هل أنت متأكد من حذف فاتورة المورد #{_invoice.Id}؟\nسيتم خصم الكميات من المخزون ولا يمكن التراجع.",
            result =>
            {
                if (result != true) return;

                var full = _db.SupplierInvoices
                    .Include(i => i.Items).ThenInclude(i => i.Product)
                    .Include(i => i.Payments)
                    .First(i => i.Id == _invoice.Id);

                foreach (var item in full.Items)
                {
                    int totalPieces = _inv.CalculatePieceEquivalent(item.Product, item.CartonQuantity, item.BoxQuantity, item.PieceQuantity);
                    if (totalPieces <= 0) continue;

                    var (fifoCost, consumed) = _inv.CalculateFifoCost(item.Product, totalPieces);
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
                NotificationManager.ShowSuccess("تم حذف الفاتورة وخصم الكميات من المخزون");
                DialogClosed?.Invoke(this, true);
            },
            ConfirmDialog.DialogType.Danger);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        DialogClosed?.Invoke(this, true);
    }

    public class ItemRow
    {
        public string ProductName { get; set; } = "";
        public string QtyDisplay { get; set; } = "";
        public string CostDisplay { get; set; } = "";
    }

    public class PaymentRow : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string DateDisplay { get; set; } = "";
        public string Method { get; set; } = "";
        public string Notes { get; set; } = "";
        public string AmountDisplay { get; set; } = "";
        public ICommand DeleteCommand { get; set; } = null!;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public RelayCommand(Action execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}