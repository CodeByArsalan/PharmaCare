using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PharmaCare.Application.DTOs.Finance;

// ========== DTOs ==========

public class ShiftSummaryDto
{
    public int ShiftId { get; set; }
    public string DrawerName { get; set; } = string.Empty;
    public decimal OpeningBalance { get; set; }
    public decimal TotalCashIn { get; set; }
    public decimal TotalCashOut { get; set; }
    public decimal ExpectedBalance { get; set; }
    public decimal? ActualBalance { get; set; }
    public decimal? Variance { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    // Note: Transactions property removed - CashTransaction entity was deleted
}

public class ExpenseSummaryDto
{
    public decimal TotalExpenses { get; set; }
    public List<CategoryExpenseDto> ByCategory { get; set; } = new();
    public List<SourceAccountExpenseDto> BySourceAccount { get; set; } = new();
}

public class CategoryExpenseDto
{
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class SourceAccountExpenseDto
{
    public int? AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
}

public class FinanceDashboardDto
{
    public decimal TotalCashOnHand { get; set; }
    public decimal TotalBankBalance { get; set; }
    public decimal TotalPettyCash { get; set; }
    public decimal TodayExpenses { get; set; }
    public decimal TodaySales { get; set; }
    public int ActiveShiftsCount { get; set; }
    public List<BankAccountBalanceDto> BankAccountBalances { get; set; } = new();
    public List<RecentTransactionDto> RecentTransactions { get; set; } = new();
}

public class BankAccountBalanceDto
{
    public int AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

public class RecentTransactionDto
{
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime Date { get; set; }
}
