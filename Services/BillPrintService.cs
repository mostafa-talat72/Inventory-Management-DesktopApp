using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using QRCoder;

namespace ProductApp.Services;

public class BillPrintService
{
    private readonly AppDbContext _db;
    private readonly ReceiptPrinter _printer;

    public BillPrintService(AppDbContext db)
    {
        _db = db;
        _printer = new ReceiptPrinter(db);
    }

    public async Task PrintInvoice(Invoice invoice)
    {
        _db.Entry(invoice).Reference(i => i.Customer).Load();
        _db.Entry(invoice).Collection(i => i.Orders).Load();
        _db.Entry(invoice).Collection(i => i.Payments).Load();

        var items = _db.OrderItems
            .Include(oi => oi.Product).ThenInclude(p => p.Units)
            .Where(oi => oi.Order.InvoiceId == invoice.Id)
            .ToList();

        var config = AppConfig.Load();
        var html = _printer.BuildReceiptHtml(invoice, items, config);
        var tempFile = Path.Combine(Path.GetTempPath(), $"invoice_{invoice.Id}.html");
        await File.WriteAllTextAsync(tempFile, html);
        Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
    }
}
