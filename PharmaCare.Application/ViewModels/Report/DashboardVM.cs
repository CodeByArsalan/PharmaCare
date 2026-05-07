using System.Collections.Generic;

namespace PharmaCare.Application.ViewModels.Report;

public class DashboardVM
{
    // Tier 1 Data
    public DailySalesSummaryVM TodaySalesSummary { get; set; } = new();
    public decimal TodaySales { get; set; }
    public int TodayOrders { get; set; }
    public int ItemsSold { get; set; }
    public decimal CashCollected { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal OutstandingBalance { get; set; }
    
    // Charts Data
    public string WeeklyLabelsJson { get; set; } = "[]";
    public string WeeklySalesDataJson { get; set; } = "[]";
    public string WeeklyPurchaseDataJson { get; set; } = "[]";
    
    // Tier 1 & 2 Lists
    public List<LowStockRow> CriticalStock { get; set; } = new();
    public List<SalesByProductRow> TopProducts { get; set; } = new();
    public List<CustomerBalanceRow> TopOutstandingCustomers { get; set; } = new();
    
    // New Charts Data
    public string TopProductsLabelsJson { get; set; } = "[]";
    public string TopProductsDataJson { get; set; } = "[]";
    public string TopCustomersLabelsJson { get; set; } = "[]";
    public string TopCustomersDataJson { get; set; } = "[]";
    
    // Tier 2 Data
    public int TotalProducts { get; set; }
    public int LowStockCount { get; set; }
    
    // Capability Flags
    public bool CanViewSales { get; set; }
    public bool CanViewInventory { get; set; }
    public bool CanViewFinancials { get; set; }

    // Tier 3 Data
    public ProfitLossVM TodayProfitLoss { get; set; } = new();
    public CashFlowReportVM TodayCashFlow { get; set; } = new();
}
