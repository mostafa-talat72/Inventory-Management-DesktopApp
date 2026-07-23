using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using QRCoder;

namespace ProductApp.Services;

public class BillPrintService : IDisposable
{
    private readonly AppDbContext _db;
    private readonly InventoryService _inv;

    public BillPrintService(AppDbContext db)
    {
        _db = db;
        _inv = new InventoryService(db);
    }

    public async Task PrintInvoice(Invoice invoice)
    {
        var html = await GenerateReceiptHtml(invoice);
        var tempFile = Path.Combine(Path.GetTempPath(), $"invoice_{invoice.Id}.html");
        await File.WriteAllTextAsync(tempFile, html);
        Process.Start(new ProcessStartInfo(tempFile) { UseShellExecute = true });
    }

    private string GenerateQRCodeBase64(string text)
    {
        using var qrGen = new QRCodeGenerator();
        using var qrData = qrGen.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        var bytes = qrCode.GetGraphic(4, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 });
        return Convert.ToBase64String(bytes);
    }

    private Task<string> GenerateReceiptHtml(Invoice invoice)
    {
        _db.Entry(invoice).Reference(i => i.Customer).Load();
        _db.Entry(invoice).Collection(i => i.Orders).Load();
        _db.Entry(invoice).Collection(i => i.Payments).Load();

        var items = _db.OrderItems
            .Include(oi => oi.Product)
            .Include(oi => oi.ProductUnit)
            .Where(oi => oi.Order.InvoiceId == invoice.Id)
            .ToList();

        var totalPaid = invoice.TotalPaid;
        var remaining = invoice.Remaining;
        var discount = invoice.Discount;
        var customerPhone = invoice.Customer?.Phone;

        var orgName = "MTE Stock";

        var qrText = $"{orgName}\nرقم الفاتورة: {invoice.Id}";
        if (invoice.CustomerName != null)
            qrText += $"\nالعميل: {invoice.CustomerName}";
        qrText += $"\nالإجمالي: {invoice.TotalAmount:0.##} ج.م";
        qrText += $"\nالتاريخ: {invoice.CreatedAt:yyyy-MM-dd HH:mm}";

        var qrBase64 = GenerateQRCodeBase64(qrText);

        var dateStr = $"{invoice.CreatedAt:yyyy/MM/dd - hh:mm} {(invoice.CreatedAt.Hour < 12 ? "ص" : "م")}";

        var itemsRows = new StringBuilder();
        foreach (var item in items)
        {
            var qtyParts = new List<string>();
            if (item.CartonQuantity > 0) qtyParts.Add($"{item.CartonQuantity} كرتونة");
            if (item.BoxQuantity > 0) qtyParts.Add($"{item.BoxQuantity} علبة");
            if (item.PieceQuantity > 0) qtyParts.Add($"{item.PieceQuantity} قطعة");
            var qtyText = string.Join(" + ", qtyParts);

            var priceType = item.PriceType == PriceType.Wholesale ? "جملة" : "قطاعي";

            itemsRows.Append($@"
        <tr>
          <td class='item-name'>{item.Product.Name}</td>
          <td class='item-qty'>{qtyText}</td>
          <td class='item-price'>{item.UnitPrice:0.##}</td>
          <td class='item-total'>{item.Total:0.##}</td>
        </tr>");
        }

        var html = $@"<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head>
  <meta charset='UTF-8'>
  <title>فاتورة #{invoice.Id}</title>
  <style>
    @import url('https://fonts.googleapis.com/css2?family=Tajawal:wght@400;500;700;800;900&display=swap');
    * {{ font-family: 'Tajawal', sans-serif; -webkit-print-color-adjust: exact; print-color-adjust: exact; box-sizing: border-box; }}
    body {{ margin: 0; padding: 12px 10px; font-size: 11px; color: #000; font-weight: 600; text-align: center; direction: rtl; }}
    .header {{ text-align: center; margin-bottom: 8px; border-bottom: 2px dashed #000; padding-bottom: 8px; }}
    .org-name {{ font-size: 1.5em; font-weight: 900; color: #000; }}
    .bill-number {{ font-size: 22px; font-weight: 900; margin: 4px 0; }}
    .info {{ font-size: 0.95em; margin: 2px 0; font-weight: 600; }}
    .customer-info {{ background: #f5f5f5; padding: 8px; border-radius: 4px; margin: 8px 0; font-weight: 700; }}
    .divider {{ border-top: 2px dashed #000; margin: 10px 0; }}
    .section-title {{ font-size: 1.1em; font-weight: 800; margin: 10px 0 6px; text-align: center; background: #e0e0e0; padding: 4px; border-radius: 4px; }}
    .items-table {{ width: 100%; border-collapse: collapse; margin-bottom: 10px; font-size: 0.85em; border: 2px solid #000; }}
    .items-table thead {{ background: #e0e0e0; font-weight: 800; }}
    .items-table th {{ padding: 4px 3px; text-align: center; border: 1.5px solid #000; font-size: 0.9em; }}
    .items-table td {{ padding: 4px 3px; text-align: center; border: 1px solid #000; font-weight: 600; }}
    .items-table .item-name {{ text-align: center; font-weight: 700; }}
    .total-section {{ margin-top: 12px; font-weight: 800; }}
    .total-row {{ padding: 4px 8px; font-size: 1em; font-weight: 700; }}
    .total-row.grand-total {{ font-size: 1.4em; font-weight: 900; background: #000; color: #fff; border-radius: 4px; padding: 8px; margin: 8px 0; }}
    .total-row.paid {{ font-size: 1.1em; font-weight: 900; color: #2E7D32; }}
    .total-row.remaining {{ font-size: 1.1em; font-weight: 900; color: #F57F17; }}
    .qr-section {{ margin: 12px 0; text-align: center; border: 1px dashed #ccc; padding: 8px; border-radius: 8px; page-break-inside: avoid; }}
    .qr-code {{ margin: 8px auto; display: block; max-width: 130px; height: auto; }}
    .qr-text {{ font-size: 0.9em; color: #333; margin-top: 4px; font-weight: 700; }}
    .qr-subtitle {{ font-size: 0.8em; color: #666; font-weight: 600; }}
    .footer {{ margin-top: 12px; border-top: 2px dashed #000; padding-top: 8px; font-size: 1em; font-weight: 800; }}
    .thank-you {{ margin: 10px 0; font-size: 1.2em; font-weight: 700; }}
    @media print {{ @page {{ size: auto; margin: 5mm; }} body {{ margin: 0; padding: 5px; }} .no-print {{ display: none !important; }} .items-table th, .items-table td {{ border-color: #000 !important; }} * {{ -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }} }}
    @media screen {{ body {{ max-width: 380px; margin: 0 auto; background: #fff; }} }}
  </style>
</head>
<body>
  <div class='header'>
    <div class='org-name'>{orgName}</div>
    <div class='bill-number'>#{invoice.Id}</div>
    <div class='info'>{dateStr}</div>
    {(invoice.CustomerName != null ? $@"
    <div class='customer-info'>
      <div>{invoice.CustomerName}</div>
      {(customerPhone != null ? $"<div class='info'>{customerPhone}</div>" : "")}
    </div>" : "")}
  </div>

  <div class='section-title'>المنتجات</div>
  <table class='items-table'>
    <thead>
      <tr>
        <th style='width:35%'>المنتج</th>
        <th style='width:25%'>الكمية</th>
        <th style='width:20%'>السعر</th>
        <th style='width:20%'>الإجمالي</th>
      </tr>
    </thead>
    <tbody>
      {itemsRows}
    </tbody>
  </table>

  <div class='divider'></div>

  <div class='total-section'>
    {(discount > 0 ? $@"<div class='total-row'>الخصم: {discount:0.##} ج.م</div>" : "")}
    <div class='total-row grand-total'>الإجمالي: {invoice.TotalAmount:0.##} ج.م</div>
    <div class='total-row paid'>المدفوع: {totalPaid:0.##} ج.م</div>
    {(remaining > 0 ? $@"<div class='total-row remaining'>المتبقي: {remaining:0.##} ج.م</div>" : "")}
  </div>

  <div class='thank-you'>شكراً لتعاملكم معنا</div>

  <div class='qr-section'>
    <img src='data:image/png;base64,{qrBase64}' alt='QR' class='qr-code' />
    <div class='qr-text'>{orgName}</div>
    <div class='qr-subtitle'>فاتورة #{invoice.Id}</div>
  </div>

  <div class='footer'>تم بواسطة MTE Stock</div>

  <div class='no-print' style='margin-top:20px;text-align:center;padding:10px;'>
    <button onclick='window.print()' style='
      background:#1A237E; color:white; border:none; padding:10px 24px;
      font-size:14px; font-weight:700; cursor:pointer; border-radius:4px;'>
      🖨️ طباعة الفاتورة
    </button>
    <br><br>
    <button onclick='window.close()' style='
      background:#9E9E9E; color:white; border:none; padding:8px 20px;
      font-size:13px; font-weight:700; cursor:pointer; border-radius:4px;'>
      ❌ إغلاق
    </button>
  </div>
</body>
</html>";

        return Task.FromResult(html);
    }

    public void Dispose() => _db.Dispose();
}
