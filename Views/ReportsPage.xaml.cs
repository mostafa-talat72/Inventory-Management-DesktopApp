using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;

namespace ProductApp.Views;

public partial class ReportsPage : Page
{
    private readonly AppDbContext _db;

    public ReportsPage()
    {
        InitializeComponent();
        _db = new AppDbContext();

        DateFrom.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        DateTo.SelectedDate = DateTime.Now;
    }

    private void ShowReport_Click(object sender, RoutedEventArgs e)
    {
        DateTime from = DateFrom.SelectedDate ?? DateTime.MinValue;
        DateTime to = DateTo.SelectedDate?.AddDays(1) ?? DateTime.MaxValue;

        var invoiceIds = _db.Invoices
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != InvoiceStatus.Cancelled)
            .Select(i => i.Id)
            .ToList();

        var items = _db.OrderItems
            .Include(oi => oi.Product)
            .Where(oi => oi.Order != null && invoiceIds.Contains(oi.Order.InvoiceId))
            .ToList();

        var invoiceCount = invoiceIds.Count;
        var totalSales = items.Sum(i => i.Total);
        var totalCost = items.Sum(i => i.CostPrice);
        var totalProfit = totalSales - totalCost;

        TxtTotalSales.Text = totalSales.ToString("N2") + " ج.م";
        TxtTotalCost.Text = totalCost.ToString("N2") + " ج.م";
        TxtTotalProfit.Text = totalProfit.ToString("N2") + " ج.م";
        TxtInvoiceCount.Text = invoiceCount.ToString();

        // Group by product
        var reportData = items.GroupBy(i => i.Product.Name).Select(g =>
        {
            var productSales = g.Sum(i => i.Total);
            var productCost = g.Sum(i => i.CostPrice);
            var productProfit = productSales - productCost;
            var totalQty = g.Sum(i => i.PieceQuantity + (i.BoxQuantity * GetBoxPieces(i.ProductId)) + (i.CartonQuantity * GetCartonPieces(i.ProductId)));
            return new
            {
                ProductName = g.Key,
                QtySold = totalQty,
                RevenueDisplay = productSales.ToString("N2") + " ج.م",
                CostDisplay = productCost.ToString("N2") + " ج.م",
                ProfitDisplay = productProfit.ToString("N2") + " ج.م",
                ProfitPercentDisplay = productSales > 0
                    ? (productProfit / productSales * 100).ToString("N1") + "%"
                    : "0%"
            };
        }).ToList();

        ReportGrid.ItemsSource = reportData;
    }

    private int GetBoxPieces(int productId)
    {
        var unit = _db.ProductUnits.FirstOrDefault(u => u.ProductId == productId && u.UnitType == UnitType.Piece && u.ParentUnit != null && u.ParentUnit.UnitType == UnitType.Box);
        return unit?.QuantityPerParent ?? 1;
    }

    private int GetCartonPieces(int productId)
    {
        var units = _db.ProductUnits.Where(u => u.ProductId == productId).ToList();
        int total = 1;
        var piece = units.FirstOrDefault(u => u.UnitType == UnitType.Piece);
        if (piece?.ParentUnit != null)
        {
            total = piece.QuantityPerParent;
            if (piece.ParentUnit.ParentUnit != null)
                total *= piece.ParentUnit.QuantityPerParent;
        }
        return total;
    }
}
