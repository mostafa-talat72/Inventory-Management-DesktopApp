using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;
using ZXing;
using ZXing.Common;
using ZXing.Rendering;
using System.IO;
using System.Text;

namespace ProductApp.Services;

public class ReceiptPrinter
{
    private readonly AppDbContext _db;

    public ReceiptPrinter(AppDbContext db)
    {
        _db = db;
    }

    private string BuildBarcodeSvg(string text)
    {
        try
        {
            var writer = new BarcodeWriter<WriteableBitmap>
            {
                Format = BarcodeFormat.CODE_128,
                Options = new EncodingOptions
                {
                    Width = 240,
                    Height = 60,
                    Margin = 2,
                    PureBarcode = true
                }
            };
            var bitmap = writer.Write(text);
            if (bitmap == null) return "";

            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(ms);
            return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return "";
        }
    }

    public string BuildReceiptHtml(Invoice invoice, List<OrderItem> items, AppConfig config)
    {
        var remaining = invoice.Remaining;
        var barcodeDataUri = BuildBarcodeSvg(invoice.Id.ToString("D4"));

        // Build items rows
        var itemsRows = new StringBuilder();
        foreach (var item in items)
        {
            var qtyParts = new List<string>();
            if (item.CartonQuantity > 0) qtyParts.Add($"<span class='qty-unit'>{item.CartonQuantity}</span> كرتونة");
            if (item.BoxQuantity > 0) qtyParts.Add($"<span class='qty-unit'>{item.BoxQuantity}</span> علبة");
            if (item.PieceQuantity > 0) qtyParts.Add($"<span class='qty-unit'>{item.PieceQuantity}</span> قطعة");
            var qtyText = string.Join(" + ", qtyParts);

            itemsRows.Append($@"
            <tr>
                <td class='item-name'>{System.Net.WebUtility.HtmlEncode(item.Product.Name)}</td>
                <td class='item-qty'>{qtyText}</td>
                <td class='item-price'>{item.UnitPrice:0.##}</td>
                <td class='item-total'>{item.Total:0.##}</td>
            </tr>");
        }

        var locationName = config.PrintLocationName ? config.LocationName : "";

        return $@"<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head>
<meta charset='UTF-8'>
<style>
    @page {{ size: 80mm auto; margin: 0; }}
    * {{ margin: 0; padding: 0; box-sizing: border-box; font-family: 'Segoe UI', Tahoma, Arial, sans-serif; }}
    body {{ padding: 10px 0; background: #f0f2f5; color: #000; font-size: 11px; font-weight: 600; text-align: center; direction: rtl; display: flex; justify-content: center; }}
    .receipt {{ width: 280px; margin: 0 auto; padding: 10px 8px; background: #fff; text-align: center; border-radius: 4px; }}
    .header {{ border-bottom: 2px dashed #000; padding-bottom: 8px; margin-bottom: 8px; text-align: center; }}
    .org-name {{ font-size: 20px; font-weight: 900; margin-bottom: 4px; color: #000; }}
    .sub-title {{ font-size: 13px; font-weight: 700; margin-bottom: 4px; color: #333; }}
    .info {{ font-size: 10px; font-weight: 600; color: #666; margin-bottom: 2px; }}
    .dashed {{ border-top: 2px dashed #999; margin: 8px 0; }}
    .customer-box {{ background: #f5f5f5; padding: 8px; margin: 4px 0; text-align: center; }}
    .customer-name {{ font-size: 12px; font-weight: 700; }}
    .customer-phone {{ font-size: 10px; color: #888; }}
    .barcode {{ margin: 6px 0; }}
    .barcode img {{ width: 220px; height: auto; }}
    .section-title {{ background: #e0e0e0; padding: 4px; font-size: 12px; font-weight: 800; margin: 6px 0; border-radius: 4px; text-align: center; }}
    table {{ width: 100%; border-collapse: collapse; margin: 4px 0; font-size: 10px; border: 2px solid #000; }}
    th {{ background: #e0e0e0; padding: 4px 2px; border: 1px solid #000; font-weight: 800; font-size: 10px; text-align: center; }}
    td {{ padding: 4px 2px; border: 1px solid #000; font-weight: 600; text-align: center; }}
    .item-name {{ text-align: center; font-weight: 700; }}
    .qty-unit {{ font-weight: 800; }}
    .total-box {{ background: #000; color: #fff; padding: 8px; margin: 4px 0; border-radius: 4px; text-align: center; }}
    .total-box .label {{ font-size: 11px; }}
    .total-box .amount {{ font-size: 18px; font-weight: 900; }}
    .paid-box {{ background: #2e7d32; color: #fff; padding: 8px; margin: 4px 0; border-radius: 4px; text-align: center; }}
    .paid-box .label {{ font-size: 11px; }}
    .paid-box .amount {{ font-size: 16px; font-weight: 900; }}
    .remaining-box {{ background: #f57f17; color: #fff; padding: 8px; margin: 4px 0; border-radius: 4px; text-align: center; }}
    .remaining-box .label {{ font-size: 11px; }}
    .remaining-box .amount {{ font-size: 16px; font-weight: 900; }}
    .thanks {{ font-size: 13px; font-weight: 700; margin: 8px 0; text-align: center; }}
    .location-info {{ background: #f5f5f5; padding: 8px; margin: 4px 0; text-align: center; }}
    .location-info div {{ font-size: 10px; color: #555; margin-bottom: 2px; }}
    .footer {{ margin-top: 6px; padding-top: 8px; border-top: 2px dashed #999; text-align: center; }}
    .footer div {{ font-size: 8px; color: #888; margin-bottom: 1px; }}
</style>
</head>
<body>
    <div class='receipt'>
        <div class='header'>
            {(string.IsNullOrWhiteSpace(locationName) ? "" : $"<div class='org-name'>{System.Net.WebUtility.HtmlEncode(locationName)}</div>")}
            <div class='sub-title'>MTE Stock</div>
            <div class='info'>فاتورة رقم {invoice.Id}</div>
            <div class='info'>{invoice.CreatedAt:yyyy/MM/dd - hh:mm tt}</div>
        </div>

    {(invoice.CustomerId == null ? "" : $@"
    <div class='customer-box'>
        <div class='customer-name'>{System.Net.WebUtility.HtmlEncode(invoice.CustomerName)}</div>
    </div>")}

        {(string.IsNullOrWhiteSpace(barcodeDataUri) ? "" : $@"
        <div class='barcode'><img src='{barcodeDataUri}' alt='barcode' /></div>")}

        <div class='section-title'>المنتجات</div>

        <table>
            <thead>
                <tr>
                    <th style='width:40%'>المنتج</th>
                    <th style='width:20%'>الكمية</th>
                    <th style='width:20%'>السعر</th>
                    <th style='width:20%'>الإجمالي</th>
                </tr>
            </thead>
            <tbody>
                {itemsRows}
            </tbody>
        </table>

        <div class='dashed'></div>

        <div class='total-box'>
            <div class='label'>الإجمالي</div>
            <div class='amount'>{invoice.TotalAmount:0.##} ج.م</div>
        </div>
        <div class='paid-box'>
            <div class='label'>المدفوع</div>
            <div class='amount'>{invoice.TotalPaid:0.##} ج.م</div>
        </div>
        {(remaining <= 0 ? "" : $@"
        <div class='remaining-box'>
            <div class='label'>المتبقي</div>
            <div class='amount'>{remaining:0.##} ج.م</div>
        </div>")}

        <div class='thanks'>شكراً لزيارتكم</div>

        <div class='dashed'></div>

        {(config.PrintLocationAddress && !string.IsNullOrWhiteSpace(config.LocationAddress) ||
          config.PrintLocationPhone && !string.IsNullOrWhiteSpace(config.LocationPhone) ||
          config.PrintLocationDescription && !string.IsNullOrWhiteSpace(config.LocationDescription) ? $@"
        <div class='location-info'>
            {(config.PrintLocationAddress && !string.IsNullOrWhiteSpace(config.LocationAddress) ? $"<div>{System.Net.WebUtility.HtmlEncode(config.LocationAddress)}</div>" : "")}
            {(config.PrintLocationPhone && !string.IsNullOrWhiteSpace(config.LocationPhone) ? $"<div>{System.Net.WebUtility.HtmlEncode(config.LocationPhone)}</div>" : "")}
            {(config.PrintLocationDescription && !string.IsNullOrWhiteSpace(config.LocationDescription) ? $"<div>{System.Net.WebUtility.HtmlEncode(config.LocationDescription)}</div>" : "")}
        </div>" : "")}

        <div class='footer'>
            <div>تم تصميم وتطوير هذا النظام بواسطة</div>
            <div style='font-weight:700;color:#666'>المهندس مصطفى طلعت للحلول البرمجيه</div>
            <div style='font-weight:700;color:#666'>01116626164</div>
        </div>
    </div>
</body>
</html>";
    }

    public void Print(Invoice invoice)
    {
        _db.Entry(invoice).Reference(i => i.Customer).Load();
        _db.Entry(invoice).Collection(i => i.Orders).Load();

        var items = _db.OrderItems
            .Include(oi => oi.Product)
            .Where(oi => oi.Order.InvoiceId == invoice.Id)
            .ToList();

        var config = AppConfig.Load();
        var html = BuildReceiptHtml(invoice, items, config);

        Views.PrintPreviewDialog.Show(html, $"فاتورة #{invoice.Id}");
    }

    public void PrintTestPage(PrintQueue? queue)
    {
        var panel = new StackPanel
        {
            Width = 280,
            Background = Brushes.White,
            FlowDirection = FlowDirection.RightToLeft
        };

        panel.Children.Add(MakeText("MTE Stock", 18, FontWeights.Black, horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeText("صفحة اختبار الطباعة", 14, FontWeights.Bold, horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeSeparator(8));
        panel.Children.Add(MakeText("إذا كنت ترى هذه الصفحة فإن الطابعة تعمل بشكل صحيح", 11, FontWeights.Normal, horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeSeparator(8));
        panel.Children.Add(MakeText(DateTime.Now.ToString("yyyy/MM/dd - hh:mm tt"), 10, FontWeights.Normal, foreground: Brushes.Gray, horizontal: HorizontalAlignment.Center));

        panel.Measure(new Size(280, double.PositiveInfinity));
        panel.Arrange(new Rect(new Size(280, panel.DesiredSize.Height)));

        if (queue != null)
        {
            var writer = PrintQueue.CreateXpsDocumentWriter(queue);
            var ticket = queue.DefaultPrintTicket;
            writer.Write(panel, ticket);
        }
        else
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true) return;
            printDialog.PrintVisual(panel, "اختبار الطباعة");
        }
    }

    private static TextBlock MakeText(string text, double fontSize, FontWeight weight, Brush? foreground = null, HorizontalAlignment horizontal = HorizontalAlignment.Right, Brush? background = null)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            FontWeight = weight,
            Foreground = foreground ?? Brushes.Black,
            HorizontalAlignment = horizontal,
            TextAlignment = horizontal == HorizontalAlignment.Center ? TextAlignment.Center : TextAlignment.Right,
            Margin = new Thickness(0, 2, 0, 2),
            Background = background
        };
    }

    private static Border MakeSeparator(double height)
    {
        return new Border
        {
            Height = height,
            BorderThickness = new Thickness(0, 0, 0, 1),
            BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200))
        };
    }
}
