using PharmaCare.Application.DTOs.Finance;
using PharmaCare.Domain.Models.Finance;

namespace PharmaCare.Application.Interfaces.Finance;

public interface IExpenseService
{
    // ========== EXPENSE CATEGORY MANAGEMENT ==========
    Task<List<ExpenseCategory>> GetExpenseCategories();
    Task<ExpenseCategory?> GetExpenseCategoryById(int id);
    Task<bool> CreateExpenseCategory(ExpenseCategory category, int userId);
    Task<bool> UpdateExpenseCategory(ExpenseCategory category, int userId);
    Task<bool> DeleteExpenseCategory(int id);

    // ========== EXPENSE MANAGEMENT ==========
    Task<List<Expense>> GetExpenses(int? categoryId = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<Expense?> GetExpenseById(int id);
    Task<bool> CreateExpense(Expense expense, int userId, int? storeId = null);
    Task<bool> UpdateExpense(Expense expense, int userId, int? storeId = null);
    Task<bool> DeleteExpense(int id, int userId);
    Task<ExpenseSummaryDto> GetExpenseSummary(DateTime fromDate, DateTime toDate);
}
