namespace ProductApp.Models;

public class ReportItem
{
    public string ProductName { get; set; } = "";
    public string CartonDisplay { get; set; } = "";
    public string BoxDisplay { get; set; } = "";
    public string PieceDisplay { get; set; } = "";
    public string RetailRevDisplay { get; set; } = "";
    public string WholesaleRevDisplay { get; set; } = "";
    public string RetailCostDisplay { get; set; } = "";
    public string WholesaleCostDisplay { get; set; } = "";
    public string ProfitDisplay { get; set; } = "";
    public string ProfitPercentDisplay { get; set; } = "";

    public int CartonQty { get; set; }
    public int BoxQty { get; set; }
    public int PieceQty { get; set; }
    public decimal RetailRev { get; set; }
    public decimal WholesaleRev { get; set; }
    public decimal RetailCost { get; set; }
    public decimal WholesaleCost { get; set; }
    public decimal TotalRev { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Profit { get; set; }
}