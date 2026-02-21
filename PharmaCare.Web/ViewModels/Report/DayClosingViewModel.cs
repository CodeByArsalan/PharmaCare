namespace PharmaCare.Web.ViewModels.Report;

public class DayClosingViewModel
{
    public DateTime Date { get; set; }

    // Sales
    public decimal TotalSales { get; set; }
    public decimal CashSales { get; set; }
    public decimal CreditSales { get; set; } // Total - Cash
    public int SalesCount { get; set; }

    // Returns
    public decimal TotalReturns { get; set; }
    public int ReturnsCount { get; set; }

    // Money In
    public decimal TotalCashReceived { get; set; } // Cash Sales + Customer Payments (Receipt Vouchers)

    // Money Out
    public decimal TotalExpenses { get; set; } // Cash Payments (Expenses + Supplier Payments)

    // Net
    public decimal NetSales => TotalSales - TotalReturns;
    public decimal CashInHand => TotalCashReceived - TotalExpenses; // Simple daily cash flow
}
