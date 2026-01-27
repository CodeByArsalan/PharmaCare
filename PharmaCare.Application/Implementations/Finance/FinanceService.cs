using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Finance;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.Finance;
using PharmaCare.Domain.Models.SaleManagement;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.Finance;

public class FinanceService : IFinanceService
{
    private readonly IRepository<Expense> _expenseRepo;
    private readonly IRepository<Sale> _saleRepo;
    private readonly IAccountingService _accountingService;

    public FinanceService(
        IRepository<Expense> expenseRepo,
        IRepository<Sale> saleRepo,
        IAccountingService accountingService)
    {
        _expenseRepo = expenseRepo;
        _saleRepo = saleRepo;
        _accountingService = accountingService;
    }

    #region DASHBOARD/REPORTS
    public async Task<FinanceDashboardDto> GetFinanceDashboard()
    {
        var today = DateTime.Today;

        // Lookup accounts by AccountType_ID
        var cashAccount = await _accountingService.GetFirstAccountByTypeId(1);      // Cash
        var bankAccount = await _accountingService.GetFirstAccountByTypeId(2);      // Bank

        // Get balances from accounting system
        var totalCashOnHand = cashAccount != null
            ? await _accountingService.GetAccountBalance(cashAccount.AccountID)
            : 0;

        var totalBankBalance = bankAccount != null
            ? await _accountingService.GetAccountBalance(bankAccount.AccountID)
            : 0;

        // Petty cash - no separate account type, set to 0
        var totalPettyCash = 0m;

        // Today's expenses
        var todayExpenses = (await _expenseRepo.GetAll())
            .Where(e => e.ExpenseDate >= today && e.ExpenseDate <= today.AddDays(1))
            .Sum(e => e.Amount);

        // Today's sales (from Sale entity)
        var todaySales = await _saleRepo.FindByCondition(s => s.SaleDate.Date == today)
            .SumAsync(s => s.Total);

        return new FinanceDashboardDto
        {
            TotalCashOnHand = totalCashOnHand,
            TotalBankBalance = totalBankBalance,
            TotalPettyCash = totalPettyCash,
            TodayExpenses = todayExpenses,
            TodaySales = todaySales,
            ActiveShiftsCount = 0,
            BankAccountBalances = new List<BankAccountBalanceDto>(),
            RecentTransactions = new List<RecentTransactionDto>()
        };
    }
    #endregion
}

