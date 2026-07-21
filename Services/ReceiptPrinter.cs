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
                Options = new EncodingOptions { Width = 240, Height = 60, Margin = 2, PureBarcode = true }
            };
            var bitmap = writer.Write(text);
            if (bitmap == null) return "";
            using var ms = new MemoryStream();
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(ms);
            return "data:image/png;base64," + Convert.ToBase64String(ms.ToArray());
        }
        catch { return ""; }
    }

    private static string ToArabicNumerals(string input) => input;

    private static string FormatDateArabic(DateTime dt)
    {
        // yyyy/MM/dd - hh:mm ص/م
        string amPm = dt.Hour < 12 ? "ص" : "م";
        int hour12  = dt.Hour % 12;
        if (hour12 == 0) hour12 = 12;
        string raw = $"{dt.Year}/{dt.Month:D2}/{dt.Day:D2} - {hour12:D2}:{dt.Minute:D2} {amPm}";
        return ToArabicNumerals(raw);
    }

    public string BuildReceiptHtml(Invoice invoice, List<OrderItem> items, AppConfig config)
    {
        var remaining    = invoice.Remaining;
        var locationName = config.PrintLocationName ? config.LocationName : "";

        var grouped = items
            .GroupBy(i => (i.ProductId, i.PriceType))
            .Select(g => new
            {
                ProductName = g.First().Product.Name,
                PriceType   = g.Key.PriceType,
                Units       = g.First().Product.Units.ToList(),
                Cartons     = g.Sum(i => i.CartonQuantity),
                Boxes       = g.Sum(i => i.BoxQuantity),
                Pieces      = g.Sum(i => i.PieceQuantity),
                UnitPrice   = g.First().UnitPrice,
                Total       = g.Sum(i => i.Total)
            })
            .OrderBy(g => g.ProductName)
            .ThenBy(g => g.PriceType)
            .ToList();

        var itemsRows = new StringBuilder();
        foreach (var g in grouped)
        {
            var units = g.Units;
            var cartonUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Carton);
            var boxUnit    = units.FirstOrDefault(u => u.UnitType == UnitType.Box);
            var pieceUnit  = units.FirstOrDefault(u => u.UnitType == UnitType.Piece);

            bool isWholesale = g.PriceType == PriceType.Wholesale;

            // بناء سطر الكمية والسعر لكل وحدة
            var qtyLines   = new List<string>();
            var priceLines = new List<string>();

            if (g.Cartons > 0 && cartonUnit != null)
            {
                decimal p = isWholesale ? cartonUnit.WholesalePrice : cartonUnit.RetailPrice;
                qtyLines.Add($"{ToArabicNumerals(g.Cartons.ToString())} كرتونة");
                priceLines.Add(ToArabicNumerals($"{p:0.##}"));
            }
            if (g.Boxes > 0 && boxUnit != null)
            {
                decimal p = isWholesale ? boxUnit.WholesalePrice : boxUnit.RetailPrice;
                qtyLines.Add($"{ToArabicNumerals(g.Boxes.ToString())} علبة");
                priceLines.Add(ToArabicNumerals($"{p:0.##}"));
            }
            if (g.Pieces > 0 && pieceUnit != null)
            {
                decimal p = isWholesale ? pieceUnit.WholesalePrice : pieceUnit.RetailPrice;
                qtyLines.Add($"{ToArabicNumerals(g.Pieces.ToString())} قطعة");
                priceLines.Add(ToArabicNumerals($"{p:0.##}"));
            }

            // fallback لو ما فيش units
            if (qtyLines.Count == 0)
            {
                var qtyParts = new List<string>();
                if (g.Cartons > 0) qtyParts.Add($"{ToArabicNumerals(g.Cartons.ToString())} كرتونة");
                if (g.Boxes   > 0) qtyParts.Add($"{ToArabicNumerals(g.Boxes.ToString())} علبة");
                if (g.Pieces  > 0) qtyParts.Add($"{ToArabicNumerals(g.Pieces.ToString())} قطعة");
                qtyLines.AddRange(qtyParts);
                priceLines.Add(ToArabicNumerals($"{g.UnitPrice:0.##}"));
            }

            bool hasBothTypes = grouped.Count(x => x.ProductName == g.ProductName) > 1;
            var priceLabel = hasBothTypes
                ? (isWholesale ? "<br/><small>(جملة)</small>" : "<br/><small>(قطاعي)</small>") : "";

            itemsRows.Append($@"
        <tr>
          <td class=""item-name"">{System.Net.WebUtility.HtmlEncode(g.ProductName)}{priceLabel}</td>
          <td class=""item-quantity"">{string.Join("<br/>", qtyLines)}</td>
          <td class=""item-price"">{string.Join("<br/>", priceLines)}</td>
          <td class=""item-total"">{ToArabicNumerals(g.Total.ToString("0.##"))}</td>
        </tr>");
        }

        var locationInfoHtml = "";
        if ((config.PrintLocationAddress     && !string.IsNullOrWhiteSpace(config.LocationAddress))     ||
            (config.PrintLocationPhone       && !string.IsNullOrWhiteSpace(config.LocationPhone))       ||
            (config.PrintLocationDescription && !string.IsNullOrWhiteSpace(config.LocationDescription)))
        {
            var sb = new StringBuilder("<div class=\"location-info\">");
            if (config.PrintLocationAddress && !string.IsNullOrWhiteSpace(config.LocationAddress))
                sb.Append($"<div>{System.Net.WebUtility.HtmlEncode(config.LocationAddress)}</div>");
            if (config.PrintLocationPhone && !string.IsNullOrWhiteSpace(config.LocationPhone))
                sb.Append($"<div>{System.Net.WebUtility.HtmlEncode(config.LocationPhone)}</div>");
            if (config.PrintLocationDescription && !string.IsNullOrWhiteSpace(config.LocationDescription))
                sb.Append($"<div>{System.Net.WebUtility.HtmlEncode(config.LocationDescription)}</div>");
            sb.Append("</div>");
            locationInfoHtml = sb.ToString();
        }

        return $@"<!DOCTYPE html>
<html dir=""rtl"" lang=""ar"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>فاتورة #{invoice.Id}</title>
  <style>
    @import url('https://fonts.googleapis.com/css2?family=Tajawal:wght@400;500;700;800;900&display=swap');
    * {{ font-family: 'Tajawal', sans-serif; -webkit-print-color-adjust: exact; print-color-adjust: exact; box-sizing: border-box; }}
    body {{ margin: 0; padding: 8px 8px; font-size: 11px; color: #000; font-weight: 600; width: auto; max-width: auto; text-align: center; direction: rtl; }}
    .header {{ text-align: center; margin-bottom: 8px; margin-top: 0; font-weight: 700; border-bottom: 2px dashed #000; padding-bottom: 6px; }}
    .org-name {{ font-size: 1.4em; font-weight: 900; margin-bottom: 6px; color: #000; }}
    .title {{ font-size: 1.1em; font-weight: 800; margin-bottom: 6px; color: #000; }}
    .info {{ margin-bottom: 4px; font-weight: 600; font-size: 0.9em; }}
    .divider {{ border-top: 2px dashed #000; margin: 10px 0; }}
    .section-title {{ font-size: 1.1em; font-weight: 800; margin: 10px 0 6px 0; text-align: center; background: #e0e0e0; padding: 4px; border-radius: 4px; }}
    .items-table {{ width: 100%; border-collapse: collapse; margin-bottom: 10px; font-size: 0.85em; border: 2px solid #000; table-layout: fixed; }}
    .items-table thead {{ background: #e0e0e0; font-weight: 800; }}
    .items-table th {{ padding: 3px 3px; text-align: center; border: 1.5px solid #000; font-size: 0.9em; word-wrap: break-word; }}
    .items-table td {{ padding: 3px 3px; text-align: center; border: 1px solid #000; font-weight: 600; word-wrap: break-word; overflow-wrap: break-word; }}
    .items-table .item-name {{ text-align: center; font-weight: 700; padding-right: 5px; width: 40% !important; }}
    .items-table .item-quantity {{ width: 22% !important; }}
    .items-table .item-price {{ width: 19% !important; }}
    .items-table .item-total {{ width: 19% !important; }}
    .items-table th:first-child {{ text-align: center; padding-right: 5px; }}
    small {{ font-size: 0.8em; color: #555; }}
    .total-section {{ margin-top: 12px; text-align: center; font-weight: 800; }}
    .total-row {{ text-align: center; padding: 6px 8px; margin-bottom: 4px; font-size: 1.1em; font-weight: 700; }}
    .total-row.grand-total {{ font-size: 1.4em; font-weight: 900; background: #000; color: #fff; border-radius: 4px; margin-top: 8px; margin-bottom: 8px; padding: 8px; }}
    .total-row.paid {{ font-size: 1.3em; font-weight: 900; }}
    .total-row.remaining {{ font-size: 1.3em; font-weight: 900; border: 1.5px solid #000; border-radius: 4px; padding: 6px 8px; }}
    .thank-you {{ text-align: center; margin-top: 10px; margin-bottom: 8px; font-size: 1.1em; font-weight: 700; }}
    .location-info {{ background: #f5f5f5; padding: 6px; margin: 4px 0; text-align: center; font-size: 0.9em; color: #555; }}
    .footer {{ margin-top: 12px; text-align: center; font-size: 1.1em; color: #000; border-top: 2px dashed #000; padding-top: 10px; padding-bottom: 10px; font-weight: 800; }}
    strong {{ font-weight: 800; }}
    @media print {{
      @page {{ size: auto; margin: 0; }}
      body {{ margin: 0; padding: 0; font-weight: 600; width: auto; }}
      .no-print {{ display: none !important; }}
      .items-table {{ border: 2px solid #000 !important; }}
      .items-table th, .items-table td {{ border: 1px solid #000 !important; }}
      * {{ -webkit-print-color-adjust: exact !important; print-color-adjust: exact !important; }}
    }}
    @media screen {{ body {{ max-width: auto; margin: 0 auto; background: #fff; }} }}
  </style>
</head>
<body>
  <div class=""header"">
    {(string.IsNullOrWhiteSpace(locationName) ? "" : $"<div class=\"org-name\">{System.Net.WebUtility.HtmlEncode(locationName)}</div>")}
    <div class=""title"" style=""font-weight: 900; font-size: 22px;"">فاتورة #{invoice.Id}</div>
    <div class=""info"">{FormatDateArabic(invoice.CreatedAt)}</div>
    {(invoice.CustomerId == null ? "" : $"<div class=\"info\">العميل: {System.Net.WebUtility.HtmlEncode(invoice.CustomerName)}</div>")}
  </div>
  <div class=""section-title"">المنتجات</div>
  <table class=""items-table"">
    <thead>
      <tr>
        <th style=""width: 40%;"">المنتج</th>
        <th style=""width: 22%;"">الكمية</th>
        <th style=""width: 19%;"">السعر</th>
        <th style=""width: 19%;"">الإجمالي</th>
      </tr>
    </thead>
    <tbody>{itemsRows}</tbody>
  </table>
  <div class=""divider""></div>
  <div class=""total-section"">
    {(invoice.Discount > 0 ? $"<div class=\"total-row\">الخصم: {ToArabicNumerals(invoice.Discount.ToString("0.##"))} ج.م</div>" : "")}
    <div class=""total-row grand-total"">الإجمالي: {ToArabicNumerals(invoice.TotalAmount.ToString("0.##"))} ج.م</div>
    <div class=""total-row paid"">المدفوع: {ToArabicNumerals(invoice.TotalPaid.ToString("0.##"))} ج.م</div>
    {(remaining <= 0 ? "" : $"<div class=\"total-row remaining\">المتبقي: {ToArabicNumerals(remaining.ToString("0.##"))} ج.م</div>")}
  </div>
  <div class=""thank-you"">شكراً لزيارتكم</div>
  <div class=""divider""></div>
  {locationInfoHtml}
  <div class=""footer"">
    <strong style=""font-weight: 900; font-size: 14px;"">تم تصميم وتطوير هذا النظام بواسطة المهندس مصطفى طلعت للحلول البرمجيه - 01116626164</strong>
  </div>
</body>
</html>";
    }

    public void Print(Invoice invoice)
    {
        _db.Entry(invoice).Reference(i => i.Customer).Load();
        _db.Entry(invoice).Collection(i => i.Orders).Load();
        var items = _db.OrderItems
            .Include(oi => oi.Product).ThenInclude(p => p.Units)
            .Where(oi => oi.Order.InvoiceId == invoice.Id)
            .ToList();
        var config = AppConfig.Load();
        var html = BuildReceiptHtml(invoice, items, config);
        Views.PrintPreviewDialog.Show(html, $"فاتورة #{invoice.Id}", invoice, items, config);
    }

    public void PrintDirect(Invoice invoice, List<OrderItem> items, AppConfig config)
    {
        try
        {
            var server = new System.Printing.LocalPrintServer();
            var queue  = new System.Printing.PrintQueue(server, config.PrinterName);

            // عرض الورق من الـ driver تلقائياً
            var mediaSize = queue.DefaultPrintTicket.PageMediaSize;
            double paperWidth = mediaSize?.Width ?? 302;
            double margin     = 6;
            double innerWidth = Math.Max(paperWidth - (margin * 2), 100);

            var receipt = BuildReceiptVisual(invoice, items, config, innerWidth);
            receipt.Measure(new Size(innerWidth, double.PositiveInfinity));
            receipt.Arrange(new Rect(new Size(innerWidth, receipt.DesiredSize.Height)));
            receipt.UpdateLayout();

            double receiptHeight = receipt.DesiredSize.Height + margin * 2;

            var fixedDoc    = new FixedDocument();
            var pageContent = new PageContent();
            var fixedPage   = new FixedPage { Width = paperWidth, Height = receiptHeight, Background = Brushes.White };

            FixedPage.SetLeft(receipt, margin);
            FixedPage.SetTop(receipt,  margin);
            fixedPage.Children.Add(receipt);
            pageContent.Child = fixedPage;
            fixedDoc.Pages.Add(pageContent);

            fixedPage.Measure(new Size(paperWidth, receiptHeight));
            fixedPage.Arrange(new Rect(new Size(paperWidth, receiptHeight)));
            fixedPage.UpdateLayout();

            var ticket = queue.DefaultPrintTicket.Clone();
            ticket.PageOrientation = System.Printing.PageOrientation.Portrait;
            ticket.CopyCount = 1;

            var writer = System.Printing.PrintQueue.CreateXpsDocumentWriter(queue);
            writer.Write(fixedDoc, ticket);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"تعذرت الطباعة:\n{ex.Message}", "خطأ في الطباعة",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private StackPanel BuildReceiptVisual(Invoice invoice, List<OrderItem> items, AppConfig config, double width)
    {
        var panel = new StackPanel
        {
            Width = width,
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var locationName = config.PrintLocationName ? config.LocationName : "";

        if (!string.IsNullOrWhiteSpace(locationName))
            panel.Children.Add(MakeText(locationName, 14, FontWeights.Black, horizontal: HorizontalAlignment.Center));

        panel.Children.Add(MakeText("MTE Stock", 12, FontWeights.Bold,
            foreground: new SolidColorBrush(Color.FromRgb(51, 51, 51)), horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeText($"فاتورة رقم {invoice.Id}", 10, FontWeights.SemiBold,
            foreground: new SolidColorBrush(Color.FromRgb(102, 102, 102)), horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeText(FormatDateArabic(invoice.CreatedAt), 10, FontWeights.Normal,
            foreground: new SolidColorBrush(Color.FromRgb(102, 102, 102)), horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeDashedSeparator());

        if (invoice.CustomerId != null && !string.IsNullOrWhiteSpace(invoice.CustomerName))
        {
            var custBox = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Padding = new Thickness(6),
                Margin  = new Thickness(0, 2, 0, 2)
            };
            custBox.Child = MakeText(invoice.CustomerName, 11, FontWeights.Bold, horizontal: HorizontalAlignment.Center);
            panel.Children.Add(custBox);
        }

        var grouped = items
            .GroupBy(i => (i.ProductId, i.PriceType))
            .OrderBy(g => g.First().Product.Name)
            .ThenBy(g => g.Key.PriceType)
            .ToList();

        panel.Children.Add(MakeText("المنتجات", 11, FontWeights.ExtraBold,
            background: new SolidColorBrush(Color.FromRgb(224, 224, 224)), horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeTableRow(
            new[] { "المنتج", "الكمية", "السعر", "الإجمالي" },
            new[] { 0.38, 0.24, 0.19, 0.19 }, width - 16, FontWeights.ExtraBold, isHeader: true));

        foreach (var g in grouped)
        {
            var units      = g.First().Product.Units.ToList();
            var cartonUnit = units.FirstOrDefault(u => u.UnitType == UnitType.Carton);
            var boxUnit    = units.FirstOrDefault(u => u.UnitType == UnitType.Box);
            var pieceUnit  = units.FirstOrDefault(u => u.UnitType == UnitType.Piece);
            bool isWholesale = g.Key.PriceType == PriceType.Wholesale;

            var qtyLines   = new List<string>();
            var priceLines = new List<string>();

            if (g.Sum(i => i.CartonQuantity) > 0 && cartonUnit != null)
            {
                decimal p = isWholesale ? cartonUnit.WholesalePrice : cartonUnit.RetailPrice;
                qtyLines.Add($"{ToArabicNumerals(g.Sum(i => i.CartonQuantity).ToString())} كرتونة");
                priceLines.Add(ToArabicNumerals($"{p:0.##}"));
            }
            if (g.Sum(i => i.BoxQuantity) > 0 && boxUnit != null)
            {
                decimal p = isWholesale ? boxUnit.WholesalePrice : boxUnit.RetailPrice;
                qtyLines.Add($"{ToArabicNumerals(g.Sum(i => i.BoxQuantity).ToString())} علبة");
                priceLines.Add(ToArabicNumerals($"{p:0.##}"));
            }
            if (g.Sum(i => i.PieceQuantity) > 0 && pieceUnit != null)
            {
                decimal p = isWholesale ? pieceUnit.WholesalePrice : pieceUnit.RetailPrice;
                qtyLines.Add($"{ToArabicNumerals(g.Sum(i => i.PieceQuantity).ToString())} قطعة");
                priceLines.Add(ToArabicNumerals($"{p:0.##}"));
            }

            // fallback لو ما فيش units
            if (qtyLines.Count == 0)
            {
                int cartons = g.Sum(i => i.CartonQuantity);
                int boxes   = g.Sum(i => i.BoxQuantity);
                int pieces  = g.Sum(i => i.PieceQuantity);
                if (cartons > 0) qtyLines.Add($"{ToArabicNumerals(cartons.ToString())} كرتونة");
                if (boxes   > 0) qtyLines.Add($"{ToArabicNumerals(boxes.ToString())} علبة");
                if (pieces  > 0) qtyLines.Add($"{ToArabicNumerals(pieces.ToString())} قطعة");
                priceLines.Add(ToArabicNumerals($"{g.First().UnitPrice:0.##}"));
            }

            var qty = string.Join("\n", qtyLines);
            var prc = string.Join("\n", priceLines);

            bool hasBothTypes = grouped.GroupBy(x => x.Key.ProductId).Any(grp =>
                grp.Select(x => x.Key.PriceType).Distinct().Count() > 1 &&
                grp.Any(x => x.Key.ProductId == g.Key.ProductId));
            var priceLabel = hasBothTypes
                ? (isWholesale ? "\n(جملة)" : "\n(قطاعي)") : "";

            decimal total = g.Sum(i => i.Total);
            panel.Children.Add(MakeTableRow(
                new[] { g.First().Product.Name + priceLabel, qty, prc, ToArabicNumerals($"{total:0.##}") },
                new[] { 0.38, 0.24, 0.19, 0.19 }, width - 16, FontWeights.Bold));
        }

        panel.Children.Add(MakeDashedSeparator());
        panel.Children.Add(MakeTotalBox($"الإجمالي: {ToArabicNumerals(invoice.TotalAmount.ToString("0.##"))} ج.م", Brushes.Black, Brushes.White, 14, isBold: true));
        panel.Children.Add(MakeTotalBox($"المدفوع: {ToArabicNumerals(invoice.TotalPaid.ToString("0.##"))} ج.م",    Brushes.White, Brushes.Black, 13));
        if (invoice.Remaining > 0)
            panel.Children.Add(MakeTotalBox($"المتبقي: {ToArabicNumerals(invoice.Remaining.ToString("0.##"))} ج.م", Brushes.White, Brushes.Black, 13, border: true));

        panel.Children.Add(MakeText("شكراً لزيارتكم", 12, FontWeights.Bold, horizontal: HorizontalAlignment.Center));

        if ((config.PrintLocationAddress     && !string.IsNullOrWhiteSpace(config.LocationAddress))     ||
            (config.PrintLocationPhone       && !string.IsNullOrWhiteSpace(config.LocationPhone))       ||
            (config.PrintLocationDescription && !string.IsNullOrWhiteSpace(config.LocationDescription)))
        {
            panel.Children.Add(MakeDashedSeparator());
            if (config.PrintLocationAddress && !string.IsNullOrWhiteSpace(config.LocationAddress))
                panel.Children.Add(MakeText(config.LocationAddress, 9, FontWeights.Normal,
                    foreground: new SolidColorBrush(Color.FromRgb(85, 85, 85)), horizontal: HorizontalAlignment.Center));
            if (config.PrintLocationPhone && !string.IsNullOrWhiteSpace(config.LocationPhone))
                panel.Children.Add(MakeText(config.LocationPhone, 9, FontWeights.Normal,
                    foreground: new SolidColorBrush(Color.FromRgb(85, 85, 85)), horizontal: HorizontalAlignment.Center));
            if (config.PrintLocationDescription && !string.IsNullOrWhiteSpace(config.LocationDescription))
                panel.Children.Add(MakeText(config.LocationDescription, 9, FontWeights.Normal,
                    foreground: new SolidColorBrush(Color.FromRgb(85, 85, 85)), horizontal: HorizontalAlignment.Center));
        }

        panel.Children.Add(MakeDashedSeparator());
        panel.Children.Add(MakeText("تم تصميم وتطوير هذا النظام بواسطة", 7, FontWeights.Normal,
            foreground: new SolidColorBrush(Color.FromRgb(136, 136, 136)), horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeText("المهندس مصطفى طلعت للحلول البرمجيه", 8, FontWeights.Bold,
            foreground: new SolidColorBrush(Color.FromRgb(102, 102, 102)), horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeText("01116626164", 8, FontWeights.Bold,
            foreground: new SolidColorBrush(Color.FromRgb(102, 102, 102)), horizontal: HorizontalAlignment.Center));

        return panel;
    }

    private static UIElement MakeTableRow(string[] cells, double[] widths, double totalWidth,
        FontWeight weight, bool isHeader = false)
    {
        var grid = new Grid
        {
            Width = totalWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            FlowDirection = FlowDirection.RightToLeft,
            Background = isHeader ? new SolidColorBrush(Color.FromRgb(224, 224, 224)) : Brushes.Transparent
        };
        for (int i = 0; i < cells.Length; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(widths[i], GridUnitType.Star) });
        for (int i = 0; i < cells.Length; i++)
        {
            var border = new Border { BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.5), Padding = new Thickness(2), ClipToBounds = true };
            var tb = new TextBlock
            {
                Text = cells[i], FontSize = 9, FontWeight = FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            border.Child = tb;
            Grid.SetColumn(border, i);
            grid.Children.Add(border);
        }
        return new Border { Width = totalWidth, ClipToBounds = true, HorizontalAlignment = HorizontalAlignment.Center, Child = grid };
    }

    private static Border MakeTotalBox(string text, Brush background, Brush foreground,
        double fontSize, bool isBold = false, bool border = false)
    {
        return new Border
        {
            Background = background, CornerRadius = new CornerRadius(3),
            Padding = new Thickness(6, 4, 6, 4), Margin = new Thickness(0, 2, 0, 2),
            BorderBrush = border ? Brushes.Black : Brushes.Transparent,
            BorderThickness = border ? new Thickness(1.5) : new Thickness(0),
            Child = new TextBlock
            {
                Text = text, FontSize = fontSize,
                FontWeight = isBold ? FontWeights.Black : FontWeights.Bold,
                Foreground = foreground, TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch
            }
        };
    }

    private static Border MakeDashedSeparator() =>
        new Border { Margin = new Thickness(0, 4, 0, 4), BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = new SolidColorBrush(Color.FromRgb(153, 153, 153)) };

    public void PrintTestPage(PrintQueue? queue)
    {
        var panel = new StackPanel { Width = 280, Background = Brushes.White, FlowDirection = FlowDirection.RightToLeft };
        panel.Children.Add(MakeText("MTE Stock", 18, FontWeights.Black, horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeText("صفحة اختبار الطباعة", 14, FontWeights.Bold, horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeSeparator(8));
        panel.Children.Add(MakeText("إذا كنت ترى هذه الصفحة فإن الطابعة تعمل بشكل صحيح", 11, FontWeights.Normal, horizontal: HorizontalAlignment.Center));
        panel.Children.Add(MakeSeparator(8));
        panel.Children.Add(MakeText(FormatDateArabic(DateTime.Now), 10, FontWeights.Normal, foreground: Brushes.Gray, horizontal: HorizontalAlignment.Center));
        panel.Measure(new Size(280, double.PositiveInfinity));
        panel.Arrange(new Rect(new Size(280, panel.DesiredSize.Height)));
        if (queue != null)
        {
            var writer = PrintQueue.CreateXpsDocumentWriter(queue);
            writer.Write(panel, queue.DefaultPrintTicket);
        }
        else
        {
            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() != true) return;
            printDialog.PrintVisual(panel, "اختبار الطباعة");
        }
    }

    private static TextBlock MakeText(string text, double fontSize, FontWeight weight,
        Brush? foreground = null, HorizontalAlignment horizontal = HorizontalAlignment.Right, Brush? background = null)
    {
        return new TextBlock
        {
            Text = text, FontSize = fontSize, FontWeight = FontWeights.Bold,
            Foreground = foreground ?? Brushes.Black, HorizontalAlignment = horizontal,
            TextAlignment = horizontal == HorizontalAlignment.Center ? TextAlignment.Center : TextAlignment.Right,
            TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2), Background = background
        };
    }

    private static Border MakeSeparator(double height) =>
        new Border { Height = height, BorderThickness = new Thickness(0, 0, 0, 1), BorderBrush = new SolidColorBrush(Color.FromRgb(200, 200, 200)) };
}
