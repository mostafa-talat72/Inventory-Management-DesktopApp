using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.EntityFrameworkCore;
using ProductApp.Data;
using ProductApp.Models;

namespace ProductApp.Views;

public partial class ReportsPage : Page
{
    private readonly AppDbContext _db;
    private List<dynamic>? _lastReportData;
    private DateTime _lastFrom;
    private DateTime _lastTo;

    public ReportsPage()
    {
        InitializeComponent();
        _db = new AppDbContext();

        DateFrom.SelectedDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        DateTo.SelectedDate = DateTime.Now;

        Loaded += (_, _) => ShowReport_Click(null!, null!);
    }

    private void ShowReport_Click(object sender, RoutedEventArgs e)
    {
        DateTime from = DateFrom.SelectedDate ?? DateTime.MinValue;
        DateTime to = DateTo.SelectedDate?.AddDays(1) ?? DateTime.MaxValue;
        _lastFrom = from;
        _lastTo = to;

        var invoiceIds = _db.Invoices
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != InvoiceStatus.Cancelled)
            .Select(i => i.Id)
            .ToList();

        var items = _db.OrderItems
            .Include(oi => oi.Product)
            .Where(oi => oi.Order != null && invoiceIds.Contains(oi.Order.InvoiceId))
            .ToList();

        var invoices = _db.Invoices
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to)
            .ToList();

        var invoiceCount = invoiceIds.Count;
        var totalSales = items.Sum(i => i.Total);
        var totalCost = items.Sum(i => i.CostPrice);
        var totalProfit = totalSales - totalCost;

        TxtTotalSales.Text = totalSales.ToString("0.##") + " ج.م";
        TxtTotalCost.Text = totalCost.ToString("0.##") + " ج.م";
        TxtTotalProfit.Text = totalProfit.ToString("0.##") + " ج.م";
        TxtInvoiceCount.Text = invoiceCount.ToString();

        UpdateProfitMargin(totalSales, totalProfit);
        LoadPeriodComparison(from, to, totalSales, totalProfit, invoiceCount);
        LoadInvoiceStatus(invoices);
        LoadTopProducts(items);
        LoadCustomerAnalysis(from, to);
        LoadDailyTrend(from, to);

        var reportData = items.GroupBy(i => i.Product.Name).Select(g =>
        {
            var retailItems = g.Where(i => i.PriceType == PriceType.Retail).ToList();
            var wholesaleItems = g.Where(i => i.PriceType == PriceType.Wholesale).ToList();

            var retailRevenue = retailItems.Sum(i => i.Total);
            var wholesaleRevenue = wholesaleItems.Sum(i => i.Total);
            var retailCost = retailItems.Sum(i => i.CostPrice);
            var wholesaleCost = wholesaleItems.Sum(i => i.CostPrice);

            var totalRevenue = retailRevenue + wholesaleRevenue;
            var totalCost = retailCost + wholesaleCost;
            var profit = totalRevenue - totalCost;

            var totalCartons = g.Sum(i => i.CartonQuantity);
            var totalBoxes = g.Sum(i => i.BoxQuantity);
            var totalPieces = g.Sum(i => i.PieceQuantity);

            return new
            {
                ProductName = g.Key,
                CartonDisplay = totalCartons > 0 ? $"كرتونة: {totalCartons:0}" : "",
                BoxDisplay = totalBoxes > 0 ? $"علبة: {totalBoxes:0}" : "",
                PieceDisplay = totalPieces > 0 ? $"قطعة: {totalPieces:0}" : "",
                RetailRevDisplay = retailRevenue > 0 ? $"قطاعي: {retailRevenue:0.##} ج.م" : "",
                WholesaleRevDisplay = wholesaleRevenue > 0 ? $"جملة: {wholesaleRevenue:0.##} ج.م" : "",
                RetailCostDisplay = retailCost > 0 ? $"قطاعي: {retailCost:0.##} ج.م" : "",
                WholesaleCostDisplay = wholesaleCost > 0 ? $"جملة: {wholesaleCost:0.##} ج.م" : "",
                ProfitDisplay = profit.ToString("0.##") + " ج.م",
                ProfitPercentDisplay = totalRevenue > 0
                    ? (profit / totalRevenue * 100).ToString("0.#") + "%"
                    : "0%",
                _cartonQty = totalCartons,
                _boxQty = totalBoxes,
                _pieceQty = totalPieces,
                _retailRev = retailRevenue,
                _wholesaleRev = wholesaleRevenue,
                _retailCost = retailCost,
                _wholesaleCost = wholesaleCost,
                _totalRev = totalRevenue,
                _totalCost = totalCost,
                _profit = profit
            };
        }).ToList();

        _lastReportData = reportData.Cast<dynamic>().ToList();
        ReportGrid.ItemsSource = reportData;
        EmptyState.Visibility = reportData.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        ReportFooter.Visibility = reportData.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ReportCountBadge.Visibility = reportData.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtReportCount.Text = reportData.Count.ToString();

        var footerCarton = reportData.Sum(r => (int)r._cartonQty);
        var footerBox = reportData.Sum(r => (int)r._boxQty);
        var footerPiece = reportData.Sum(r => (int)r._pieceQty);
        var footerRetailRev = reportData.Sum(r => (decimal)r._retailRev);
        var footerWholesaleRev = reportData.Sum(r => (decimal)r._wholesaleRev);
        var footerRetailCost = reportData.Sum(r => (decimal)r._retailCost);
        var footerWholesaleCost = reportData.Sum(r => (decimal)r._wholesaleCost);
        TxtFooterCarton.Text = footerCarton > 0 ? "كرتونة: " + footerCarton.ToString("0") : "";
        TxtFooterCarton.Visibility = footerCarton > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtFooterBox.Text = footerBox > 0 ? "علبة: " + footerBox.ToString("0") : "";
        TxtFooterBox.Visibility = footerBox > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtFooterPiece.Text = footerPiece > 0 ? "قطعة: " + footerPiece.ToString("0") : "";
        TxtFooterPiece.Visibility = footerPiece > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtFooterRetailRev.Text = footerRetailRev > 0 ? "قطاعي: " + footerRetailRev.ToString("0.##") + " ج.م" : "";
        TxtFooterRetailRev.Visibility = footerRetailRev > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtFooterWholesaleRev.Text = footerWholesaleRev > 0 ? "جملة: " + footerWholesaleRev.ToString("0.##") + " ج.م" : "";
        TxtFooterWholesaleRev.Visibility = footerWholesaleRev > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtFooterRetailCost.Text = footerRetailCost > 0 ? "قطاعي: " + footerRetailCost.ToString("0.##") + " ج.م" : "";
        TxtFooterRetailCost.Visibility = footerRetailCost > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtFooterWholesaleCost.Text = footerWholesaleCost > 0 ? "جملة: " + footerWholesaleCost.ToString("0.##") + " ج.م" : "";
        TxtFooterWholesaleCost.Visibility = footerWholesaleCost > 0 ? Visibility.Visible : Visibility.Collapsed;
        TxtFooterProfit.Text = reportData.Sum(r => (decimal)r._profit).ToString("0.##") + " ج.م";
        TxtFooterMargin.Text = totalSales > 0 ? (totalProfit / totalSales * 100).ToString("0.#") + "%" : "0%";
    }

    private void UpdateProfitMargin(decimal totalSales, decimal totalProfit)
    {
        if (totalSales > 0)
        {
            var marginPercent = (double)(totalProfit / totalSales * 100);
            TxtProfitMargin.Text = marginPercent.ToString("0.#") + "%";
            if (marginPercent >= 30)
            {
                TxtProfitIndicator.Text = "ممتاز";
                ProfitIndicator.Background = (Brush)new BrushConverter().ConvertFromString("#E8F5E9")!;
                TxtProfitIndicator.Foreground = (Brush)new BrushConverter().ConvertFromString("#2E7D32")!;
            }
            else if (marginPercent >= 10)
            {
                TxtProfitIndicator.Text = "جيد";
                ProfitIndicator.Background = (Brush)new BrushConverter().ConvertFromString("#FFF8E1")!;
                TxtProfitIndicator.Foreground = (Brush)new BrushConverter().ConvertFromString("#F57F17")!;
            }
            else if (marginPercent >= 0)
            {
                TxtProfitIndicator.Text = "ضعيف";
                ProfitIndicator.Background = (Brush)new BrushConverter().ConvertFromString("#FFEBEE")!;
                TxtProfitIndicator.Foreground = (Brush)new BrushConverter().ConvertFromString("#C62828")!;
            }
            else
            {
                TxtProfitIndicator.Text = "خسارة";
                ProfitIndicator.Background = (Brush)new BrushConverter().ConvertFromString("#FCE4EC")!;
                TxtProfitIndicator.Foreground = (Brush)new BrushConverter().ConvertFromString("#B71C1C")!;
            }
            ProfitMarginCard.Visibility = Visibility.Visible;
        }
        else
        {
            ProfitMarginCard.Visibility = Visibility.Collapsed;
        }
    }

    private void LoadPeriodComparison(DateTime from, DateTime to, decimal currentSales, decimal currentProfit, int currentCount)
    {
        var days = (to - from).TotalDays;
        if (days <= 0) { PeriodComparisonCard.Visibility = Visibility.Collapsed; return; }

        var prevFrom = from.AddDays(-days);
        var prevTo = from;

        var prevInvoiceIds = _db.Invoices
            .Where(i => i.InvoiceDate >= prevFrom && i.InvoiceDate < prevTo && i.Status != InvoiceStatus.Cancelled)
            .Select(i => i.Id).ToList();

        var prevItems = _db.OrderItems
            .Where(oi => oi.Order != null && prevInvoiceIds.Contains(oi.Order.InvoiceId))
            .ToList();

        var prevSales = prevItems.Sum(i => i.Total);
        var prevCost = prevItems.Sum(i => i.CostPrice);
        var prevProfit = prevSales - prevCost;
        var prevCount = prevInvoiceIds.Count;

        TxtComparisonPeriod.Text = $"مقارنة {prevFrom:dd/MM/yyyy} -> {from.AddDays(-1):dd/MM/yyyy}";

        TxtPrevSales.Text = prevSales.ToString("0.##") + " ج.م";
        TxtPrevCost.Text = prevCost.ToString("0.##") + " ج.م";
        TxtPrevProfit.Text = prevProfit.ToString("0.##") + " ج.م";
        TxtPrevCount.Text = prevCount.ToString();

        SetChangeIndicator(SalesChangeBadge, SalesArrow, TxtSalesChange, prevSales, currentSales);
        SetChangeIndicator(ProfitChangeBadge, ProfitArrow, TxtProfitChange, prevProfit, currentProfit);
        SetChangeIndicator(CountChangeBadge, CountArrow, TxtCountChange, prevCount, currentCount);

        PeriodComparisonCard.Visibility = Visibility.Visible;
    }

    private static void SetChangeIndicator(Border badge, Path arrow, TextBlock txt, decimal prevVal, decimal currentVal)
    {
        if (prevVal == 0)
        {
            if (currentVal > 0)
            {
                badge.Background = new SolidColorBrush(Color.FromArgb(40, 46, 125, 50));
                arrow.Fill = (Brush)new BrushConverter().ConvertFromString("#2E7D32")!;
                arrow.Data = Geometry.Parse("M7.41 15.41L12 10.83l4.59 4.58L18 14l-6-6-6 6z");
                txt.Text = "جديد";
                txt.Foreground = (Brush)new BrushConverter().ConvertFromString("#2E7D32")!;
            }
            else
            {
                badge.Visibility = Visibility.Collapsed;
            }
            return;
        }

        var change = (double)((currentVal - prevVal) / prevVal * 100);
        txt.Text = (change >= 0 ? "+" : "") + change.ToString("0.#") + "%";

        if (change >= 0)
        {
            badge.Background = new SolidColorBrush(Color.FromArgb(40, 46, 125, 50));
            arrow.Fill = (Brush)new BrushConverter().ConvertFromString("#2E7D32")!;
            arrow.Data = Geometry.Parse("M7.41 15.41L12 10.83l4.59 4.58L18 14l-6-6-6 6z");
            txt.Foreground = (Brush)new BrushConverter().ConvertFromString("#2E7D32")!;
        }
        else
        {
            badge.Background = new SolidColorBrush(Color.FromArgb(40, 198, 40, 40));
            arrow.Fill = (Brush)new BrushConverter().ConvertFromString("#C62828")!;
            arrow.Data = Geometry.Parse("M7.41 8.59L12 13.17l4.59-4.58L18 10l-6 6-6-6z");
            txt.Foreground = (Brush)new BrushConverter().ConvertFromString("#C62828")!;
        }
    }

    private static void SetChangeIndicator(Border badge, Path arrow, TextBlock txt, int prevVal, int currentVal)
    {
        SetChangeIndicator(badge, arrow, txt, (decimal)prevVal, (decimal)currentVal);
    }

    private void LoadInvoiceStatus(System.Collections.Generic.List<Invoice> invoices)
    {
        TxtPaidCount.Text = invoices.Count(i => i.Status == InvoiceStatus.Paid).ToString();
        TxtPartialCount.Text = invoices.Count(i => i.Status == InvoiceStatus.PartiallyPaid).ToString();
        TxtOpenCount.Text = invoices.Count(i => i.Status == InvoiceStatus.Open).ToString();
        TxtCancelledCount.Text = invoices.Count(i => i.Status == InvoiceStatus.Cancelled).ToString();
    }

    private void LoadTopProducts(System.Collections.Generic.List<OrderItem> items)
    {
        var top = items.GroupBy(i => i.Product!.Name)
            .Select(g => new { Name = g.Key, Sales = g.Sum(i => i.Total), Cost = g.Sum(i => i.CostPrice) })
            .OrderByDescending(x => x.Sales)
            .Take(10)
            .ToList();

        var maxSales = top.Count > 0 ? top.Max(x => x.Sales) : 1;

        var rankColors = new[] { "#FFB300", "#78909C", "#A1887F" };
        var rankBg = new[] { "#FFF8E1", "#F5F5F5", "#EFEBE9" };

        var cardData = top.Select((x, i) =>
        {
            var idx = i < 3 ? i : -1;
            var surfaceBgHex = ThemeHex("SurfaceBackground", "#F8F9FA");
            return new
            {
                Rank = (i + 1).ToString(),
                Name = x.Name,
                SalesDisplay = x.Sales.ToString("0.##") + " ج.م",
                BarWidth = (double)(x.Sales / maxSales * 180),
                BarColor = idx >= 0 ? rankColors[idx] : "#B0BEC5",
                RankColor = idx >= 0 ? rankColors[idx] : "#B0BEC5",
                RankBg = idx >= 0 ? rankColors[idx] : surfaceBgHex
            };
        }).ToList();

        TopProductsList.ItemsSource = cardData;
        EmptyTopProducts.Visibility = cardData.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void LoadCustomerAnalysis(DateTime from, DateTime to)
    {
        var invoiceData = _db.Invoices
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != InvoiceStatus.Cancelled && i.CustomerId != null)
            .GroupBy(i => new { i.CustomerId, Name = i.Customer!.Name })
            .Select(g => new
            {
                g.Key.CustomerId,
                g.Key.Name,
                Sales = g.Sum(i => (double?)(double)i.TotalAmount) ?? 0,
                Count = g.Count()
            })
            .ToList()
            .OrderByDescending(x => x.Sales)
            .Take(10)
            .ToList();

        var maxSales = invoiceData.Count > 0 ? invoiceData.Max(x => x.Sales) : 1;

        var rankColors = new[] { "#FFB300", "#78909C", "#A1887F" };
        var rankBg = new[] { "#FFF8E1", "#F5F5F5", "#EFEBE9" };

        var cardData = invoiceData.Select((x, i) =>
        {
            var idx = i < 3 ? i : -1;
            var surfaceBgHex = ThemeHex("SurfaceBackground", "#F8F9FA");
            return new
            {
                Rank = (i + 1).ToString(),
                Name = x.Name,
                SalesDisplay = x.Sales.ToString("0.##") + " ج.م",
                InvoiceCount = $"فواتير: {x.Count}",
                RankColor = idx >= 0 ? rankColors[idx] : "#B0BEC5",
                RankBg = idx >= 0 ? rankColors[idx] : surfaceBgHex
            };
        }).ToList();

        TopCustomersList.ItemsSource = cardData;
        EmptyTopCustomers.Visibility = cardData.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void LoadDailyTrend(DateTime from, DateTime to)
    {
        ChartCanvas.Children.Clear();
        AxisCanvas.Children.Clear();

        var dailySales = _db.Invoices
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to && i.Status != InvoiceStatus.Cancelled)
            .GroupBy(i => i.InvoiceDate.Date)
            .Select(g => new { Date = g.Key, Sales = g.Sum(i => (decimal?)i.TotalAmount) ?? 0 })
            .OrderBy(x => x.Date)
            .ToList();

        var dailyItems = _db.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order != null && oi.Order.Invoice != null
                && oi.Order.Invoice.InvoiceDate >= from && oi.Order.Invoice.InvoiceDate <= to
                && oi.Order.Invoice.Status != InvoiceStatus.Cancelled)
            .GroupBy(oi => oi.Order!.Invoice!.InvoiceDate.Date)
            .Select(g => new { Date = g.Key, Cost = g.Sum(i => (decimal?)i.CostPrice) ?? 0 })
            .ToList();

        var dailyCosts = dailyItems.ToDictionary(x => x.Date, x => x.Cost);

        if (dailySales.Count == 0)
        {
            DailyTrendCard.Visibility = Visibility.Collapsed;
            return;
        }

        var totalSales = dailySales.Sum(x => x.Sales);
        var avgDaily = totalSales / dailySales.Count;
        var maxDay = dailySales.MaxBy(x => x.Sales)!;
        var minDay = dailySales.MinBy(x => x.Sales)!;

        TxtTrendSubtitle.Text = $"آخر {dailySales.Count} يوم • من {dailySales.First().Date:dd/MM} إلى {dailySales.Last().Date:dd/MM}";
        TxtTrendTotal.Text = totalSales.ToString("0.##") + " ج.م";
        TxtTrendAvg.Text = avgDaily.ToString("0.##") + " ج.م";
        TxtTrendMax.Text = maxDay.Sales.ToString("0.##") + " ج.م";
        TxtTrendMin.Text = minDay.Sales.ToString("0.##") + " ج.م";

        // — Calculate data series —
        var revData = dailySales.Select(d => (double)d.Sales).ToArray();
        var costData = dailySales.Select(d => (double)dailyCosts.GetValueOrDefault(d.Date, 0)).ToArray();
        var profitData = dailySales.Select(d => revData[Array.IndexOf(dailySales.ToArray(), d)] - costData[Array.IndexOf(dailySales.ToArray(), d)]).ToArray();
        // Better: loop
        var profitArr = new double[dailySales.Count];
        var maxVal = 0.0;
        for (int i = 0; i < dailySales.Count; i++)
        {
            profitArr[i] = revData[i] - costData[i];
            maxVal = Math.Max(maxVal, Math.Max(revData[i], Math.Max(costData[i], Math.Abs(profitArr[i]))));
        }
        if (maxVal == 0) maxVal = 1;

        // — Chart dimensions —
        var topMargin = 30.0;
        var bottomMargin = 28.0;
        var plotH = 170.0;
        var chartH = topMargin + plotH + bottomMargin;
        var xSpacing = dailySales.Count > 1 ? 55.0 : 100.0;
        var chartW = Math.Max(400.0, (dailySales.Count - 1) * xSpacing + 20);
        var xStart = 10.0;

        ChartCanvas.Width = chartW;
        ChartCanvas.Height = chartH;

        // — Legend inside chart area —
        var legend = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var legItems = new[]
        {
            ("#3F51B5", "الإيراد"),
            ("#E53935", "التكلفة"),
            ("#2E7D32", "الربح")
        };
        var legX = 0.0;
        foreach (var (color, text) in legItems)
        {
            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(legX > 0 ? 16 : 0, 0, 0, 0) };
            item.Children.Add(new Border { Width = 12, Height = 3, CornerRadius = new CornerRadius(2), Background = (Brush)new BrushConverter().ConvertFromString(color)!, VerticalAlignment = VerticalAlignment.Center });
            item.Children.Add(new TextBlock { Text = text, FontSize = 10,
            Foreground = Application.Current.TryFindResource("BodyTextBrush") as Brush
                         ?? (Brush)new BrushConverter().ConvertFromString("#78909C")!,
            Margin = new Thickness(5, 0, 0, 0) });
            Canvas.SetLeft(item, legX);
            Canvas.SetTop(item, 4);
            ChartCanvas.Children.Add(item);
            legX += 80;
        }

        // — Grid lines & axis labels —
        var gridBrush  = Application.Current.TryFindResource("BorderBrushLight") as Brush
                         ?? (Brush)new BrushConverter().ConvertFromString("#E8EAF6")!;
        var labelColor = Application.Current.TryFindResource("MutedTextBrush") as Brush
                         ?? (Brush)new BrushConverter().ConvertFromString("#90A4AE")!;

        for (int i = 0; i <= 4; i++)
        {
            var ratio = i / 4.0;
            var y = topMargin + plotH * (1.0 - ratio);
            var val = maxVal * ratio;

            var line = new Line
            {
                X1 = 0, Y1 = y, X2 = chartW, Y2 = y,
                Stroke = gridBrush,
                StrokeThickness = 0.5,
                StrokeDashArray = new DoubleCollection(new[] { 3.0, 3.0 })
            };
            ChartCanvas.Children.Add(line);

            var lbl = new TextBlock
            {
                Text = val.ToString("0"),
                FontSize = 8,
                Foreground = labelColor
            };
            Canvas.SetLeft(lbl, 2);
            Canvas.SetTop(lbl, y - 5);
            AxisCanvas.Children.Add(lbl);
        }

        // — Baseline —
        var baseLine = new Line
        {
            X1 = 0, Y1 = topMargin + plotH, X2 = chartW, Y2 = topMargin + plotH,
            Stroke = Application.Current.TryFindResource("BorderBrushLight") as Brush
                     ?? (Brush)new BrushConverter().ConvertFromString("#B0BEC5")!,
            StrokeThickness = 1
        };
        ChartCanvas.Children.Add(baseLine);

        // — Build points for each series —
        var revPoints = new PointCollection();
        var costPoints = new PointCollection();
        var profitPoints = new PointCollection();
        var dotData = new List<(double x, double rev, double cost, double profit, DateTime date, bool isWeekend)>();

        for (int i = 0; i < dailySales.Count; i++)
        {
            var x = xStart + i * xSpacing;
            revPoints.Add(new Point(x, topMargin + plotH * (1.0 - revData[i] / maxVal)));
            costPoints.Add(new Point(x, topMargin + plotH * (1.0 - costData[i] / maxVal)));
            profitPoints.Add(new Point(x, topMargin + plotH * (1.0 - profitArr[i] / maxVal)));
            dotData.Add((x, revData[i], costData[i], profitArr[i], dailySales[i].Date,
                dailySales[i].Date.DayOfWeek == DayOfWeek.Friday || dailySales[i].Date.DayOfWeek == DayOfWeek.Saturday));
        }

        // — Draw polylines (behind dots) —
        var revLine = new Polyline
        {
            Points = revPoints,
            Stroke = (Brush)new BrushConverter().ConvertFromString("#3F51B5")!,
            StrokeThickness = 3,
            StrokeLineJoin = PenLineJoin.Round
        };
        ChartCanvas.Children.Add(revLine);

        // — Gradient fill under revenue line —
        var revFill = new Polygon
        {
            Fill = new LinearGradientBrush(
                Color.FromRgb(63, 81, 181),
                Color.FromArgb(20, 63, 81, 181),
                90.0),
            StrokeThickness = 0
        };
        var revFillPoints = new PointCollection(revPoints);
        revFillPoints.Add(new Point(revPoints[^1].X, topMargin + plotH));
        revFillPoints.Insert(0, new Point(revPoints[0].X, topMargin + plotH));
        revFill.Points = revFillPoints;
        ChartCanvas.Children.Insert(ChartCanvas.Children.Count - 1, revFill);

        var costLine = new Polyline
        {
            Points = costPoints,
            Stroke = (Brush)new BrushConverter().ConvertFromString("#E53935")!,
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round
        };
        ChartCanvas.Children.Add(costLine);

        var profitLine = new Polyline
        {
            Points = profitPoints,
            Stroke = (Brush)new BrushConverter().ConvertFromString("#2E7D32")!,
            StrokeThickness = 2.5,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeDashArray = new DoubleCollection(new[] { 5.0, 3.0 })
        };
        ChartCanvas.Children.Add(profitLine);

        // — Add dots and date labels on top of lines —
        var dotColors = new[] { "#3F51B5", "#E53935", "#2E7D32" };
        var dotLabels = new[] { "الإيراد", "التكلفة", "الربح" };

        foreach (var (x, rev, cost, profit, date, isWeekend) in dotData)
        {
            var dotVals = new[] { rev, cost, profit };
            for (int s = 0; s < 3; s++)
            {
                var dotY = topMargin + plotH * (1.0 - dotVals[s] / maxVal);
                var dot = new Ellipse
                {
                    Width = 7, Height = 7,
                    Fill = (Brush)new BrushConverter().ConvertFromString(dotColors[s])!,
                    Stroke = Brushes.White,
                    StrokeThickness = 1.5,
                    ToolTip = $"{dotLabels[s]}: {dotVals[s]:0.##} ج.م"
                };
                Canvas.SetLeft(dot, x - 3.5);
                Canvas.SetTop(dot, dotY - 3.5);
                ChartCanvas.Children.Add(dot);
            }

            var dateLbl = new TextBlock
            {
                Text = date.ToString("dd/MM"),
                FontSize = 8,
                FontWeight = isWeekend ? FontWeights.Bold : FontWeights.Normal,
                Foreground = isWeekend
                    ? (Brush)new BrushConverter().ConvertFromString("#EF5350")!
                    : Application.Current.TryFindResource("MutedTextBrush") as Brush
                      ?? (Brush)new BrushConverter().ConvertFromString("#90A4AE")!
            };
            Canvas.SetLeft(dateLbl, x - 14);
            Canvas.SetTop(dateLbl, topMargin + plotH + 6);
            ChartCanvas.Children.Add(dateLbl);
        }

        DailyTrendCard.Visibility = Visibility.Visible;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_lastReportData == null || _lastReportData.Count == 0)
        {
            MessageBox.Show("لا توجد بيانات للتصدير. قم بعرض التقرير أولاً.", "تصدير", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var saveDialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV Files (*.csv)|*.csv",
            FileName = $"تقرير_مبيعات_{DateTime.Now:yyyyMMdd}.csv"
        };

        if (saveDialog.ShowDialog() != true) return;

        try
        {
            using var writer = new System.IO.StreamWriter(saveDialog.FileName, false, System.Text.Encoding.UTF8);
            writer.WriteLine("المنتج,كرتونة,علبة,قطعة,إيراد قطاعي,إيراد جملة,تكلفة قطاعي,تكلفة جملة,الربح,نسبة الربح");

            foreach (dynamic item in _lastReportData)
            {
                var name = item.ProductName?.ToString()?.Replace(",", " ") ?? "";
                var carton = item._cartonQty?.ToString() ?? "0";
                var box = item._boxQty?.ToString() ?? "0";
                var piece = item._pieceQty?.ToString() ?? "0";
                var retailRev = item.RetailRevDisplay?.ToString() ?? "0";
                var wholesaleRev = item.WholesaleRevDisplay?.ToString() ?? "0";
                var retailCost = item.RetailCostDisplay?.ToString() ?? "0";
                var wholesaleCost = item.WholesaleCostDisplay?.ToString() ?? "0";
                var profit = item.ProfitDisplay?.ToString() ?? "0";
                var margin = item.ProfitPercentDisplay?.ToString() ?? "0%";
                writer.WriteLine($"{name},{carton},{box},{piece},{retailRev},{wholesaleRev},{retailCost},{wholesaleCost},{profit},{margin}");
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = saveDialog.FileName,
                UseShellExecute = true
            });
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"خطأ في التصدير: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Gets the hex color string of a DynamicResource brush, falling back to a default.</summary>
    private static string ThemeHex(string resourceKey, string fallback)
    {
        if (Application.Current.TryFindResource(resourceKey) is SolidColorBrush b)
        {
            var c = b.Color;
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
        return fallback;
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
