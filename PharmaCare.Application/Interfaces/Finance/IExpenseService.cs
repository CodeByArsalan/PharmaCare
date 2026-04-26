using PharmaCare.Domain.Entities.Finance;

namespace PharmaCare.Application.Interfaces.Finance;

/// <summary>
/// Service for managing expenses and expense categories.
/// </summary>
public interface IExpenseService
{
    // ========== EXPENSES ==========

    /// <summary>
    /// Gets all expenses with related data.
    /// </summary>
    Task<IEnumerable<Expense>> GetAllAsync();

    Task<PharmaCare.Application.DTOs.PagedResult<Expense>> GetPagedAsync(
        int? categoryId, DateTime? from, DateTime? to, int page, int pageSize);

    /// <summary>
    /// Gets an expense by ID with related data.
    /// </summary>
    Task<Expense?> GetByIdAsync(int id);

    /// <summary>
    /// Creates an expense and auto-generates its accounting voucher.
    /// DR: Expense Account, CR: Source (Cash/Bank) Account
    /// </summary>
    Task<Expense> CreateAsync(Expense expense, int userId);
    Task<bool> ApproveAsync(int expenseId, int userId);

    /// <summary>
    /// Voids an expense and reverses its accounting voucher.
    /// </summary>
    Task<bool> VoidAsync(int expenseId, string reason, int userId);

    // ========== EXPENSE CATEGORIES ==========

    /// <summary>
    /// Gets all expense categories.
    /// </summary>
    Task<IEnumerable<ExpenseCategory>> GetCategoriesAsync();

    /// <summary>
    /// Gets an expense category by ID.
    /// </summary>
    Task<ExpenseCategory?> GetCategoryByIdAsync(int id);

    /// <summary>
    /// Creates a new expense category.
    /// </summary>
    Task<ExpenseCategory> CreateCategoryAsync(ExpenseCategory category, int userId);

    /// <summary>
    /// Updates an expense category.
    /// </summary>
    Task<bool> UpdateCategoryAsync(ExpenseCategory category, int userId);

    /// <summary>
    /// Toggles the active status of an expense category.
    /// </summary>
    Task ToggleCategoryStatusAsync(int categoryId, int userId);

    // ========== EXPENSE BUDGETS ==========

    /// <summary>
    /// Gets budgets for a specific year and month. Includes categories even if no budget exists.
    /// </summary>
    Task<IEnumerable<PharmaCare.Application.ViewModels.Report.ExpenseBudgetVM>> GetBudgetsAsync(int year, int month);

    /// <summary>
    /// Saves or updates budgets for multiple categories in a specific month/year.
    /// </summary>
    Task SaveBudgetsAsync(int year, int month, List<PharmaCare.Application.ViewModels.Report.ExpenseBudgetVM> budgets, int userId);
}
