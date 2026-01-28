using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Finance;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Models.Finance;
using PharmaCare.Infrastructure.Interfaces;

using PharmaCare.Application.Interfaces.AccountManagement;

namespace PharmaCare.Application.Implementations.Finance;

public class ExpenseService(
    IRepository<ExpenseCategory> _expenseCategoryRepo,
    IRepository<Expense> _expenseRepo,
    IAccountingService _accountingService) : IExpenseService
{

    #region EXPENSE CATEGORY MANAGEMENT 

    public async Task<List<ExpenseCategory>> GetExpenseCategories()
    {
        return _expenseCategoryRepo.GetAllWithInclude(ec => ec.ParentCategory, ec => ec.ChildCategories)
            .Where(ec => ec.IsActive)
            .ToList();
    }
    public async Task<ExpenseCategory?> GetExpenseCategoryById(int id)
    {
        return await _expenseCategoryRepo.FindByCondition(ec => ec.ExpenseCategoryID == id)
            .Include(ec => ec.ParentCategory)
            .Include(ec => ec.ChildCategories)
            .FirstOrDefaultAsync();
    }
    public async Task<bool> CreateExpenseCategory(ExpenseCategory category, int userId)
    {
        category.IsActive = true;
        category.CreatedBy = userId;
        category.CreatedDate = DateTime.Now;
        return await _expenseCategoryRepo.Insert(category);
    }
    public async Task<bool> UpdateExpenseCategory(ExpenseCategory category, int userId)
    {
        var existing = _expenseCategoryRepo.GetById(category.ExpenseCategoryID);
        if (existing == null) return false;

        existing.Name = category.Name;
        existing.Description = category.Description;
        existing.ParentCategory_ID = category.ParentCategory_ID;
        existing.UpdatedBy = userId;
        existing.UpdatedDate = DateTime.Now;

        return await _expenseCategoryRepo.Update(existing);
    }
    public async Task<bool> DeleteExpenseCategory(int id)
    {
        var existing = _expenseCategoryRepo.GetById(id);
        if (existing == null) return false;

        existing.IsActive = false;
        existing.UpdatedDate = DateTime.Now;
        return await _expenseCategoryRepo.Update(existing);
    }

    #endregion

    #region EXPENSE MANAGEMENT 

    public async Task<List<Expense>> GetExpenses(int? categoryId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _expenseRepo.GetAllWithInclude(e => e.ExpenseCategory, e => e.SourceAccount, e => e.ExpenseAccount);

        if (categoryId.HasValue)
            query = query.Where(e => e.ExpenseCategory_ID == categoryId.Value);

        if (fromDate.HasValue)
            query = query.Where(e => e.ExpenseDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(e => e.ExpenseDate <= toDate.Value.AddDays(1));

        return await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();
    }
    public async Task<Expense?> GetExpenseById(int id)
    {
        return await _expenseRepo.FindByCondition(e => e.ExpenseID == id)
            .Include(e => e.ExpenseCategory)
            .FirstOrDefaultAsync();
    }
    public async Task<bool> CreateExpense(Expense expense, int userId, int? storeId = null)
    {
        expense.CreatedBy = userId;
        expense.CreatedDate = DateTime.Now;

        // Validate both accounts are selected
        if (!expense.SourceAccount_ID.HasValue || !expense.ExpenseAccount_ID.HasValue)
        {
            throw new InvalidOperationException("Both payment account and expense account must be selected.");
        }

        // Step 1: Insert expense first to get the ExpenseID
        if (!await _expenseRepo.Insert(expense))
            return false;

        // Step 2: Create Journal Entry with Source tracking
        var journalEntryDto = new PharmaCare.Application.DTOs.Accounting.JournalEntryDto
        {
            EntryDate = expense.ExpenseDate,
            PostingDate = DateTime.Now,
            EntryType = "Expense",
            Reference = expense.Reference,
            Description = expense.Description ?? "Business Expense",
            Source_Table = "Expense",           // Track source table
            Source_ID = expense.ExpenseID,      // Track source record ID
            Lines = new List<PharmaCare.Application.DTOs.Accounting.JournalEntryLineDto>
            {
                new() { Account_ID = expense.ExpenseAccount_ID.Value, DebitAmount = expense.Amount, Description = "Expense Debit", Store_ID = storeId },
                new() { Account_ID = expense.SourceAccount_ID.Value, CreditAmount = expense.Amount, Description = "Payment Credit", Store_ID = storeId }
            }
        };

        var journalEntryId = await _accountingService.CreateJournalEntry(journalEntryDto, userId);
        await _accountingService.PostJournalEntry(journalEntryId, userId);

        // Step 3: Update expense with the voucher reference
        expense.Voucher_ID = journalEntryId;
        return await _expenseRepo.Update(expense);
    }
    public async Task<bool> UpdateExpense(Expense expense, int userId, int? storeId = null)
    {
        var existing = _expenseRepo.GetById(expense.ExpenseID);
        if (existing == null) return false;

        // Check if financial impact changed (amount, accounts, or category)
        bool financialChanged = existing.Amount != expense.Amount ||
                                existing.SourceAccount_ID != expense.SourceAccount_ID ||
                                existing.ExpenseAccount_ID != expense.ExpenseAccount_ID ||
                                existing.ExpenseCategory_ID != expense.ExpenseCategory_ID;

        // If financial data changed and there's an existing journal entry, void it and create new
        if (financialChanged && existing.Voucher_ID.HasValue &&
            expense.SourceAccount_ID.HasValue && expense.ExpenseAccount_ID.HasValue)
        {
            await _accountingService.VoidJournalEntry(existing.Voucher_ID.Value, userId);

            var journalEntryDto = new PharmaCare.Application.DTOs.Accounting.JournalEntryDto
            {
                EntryDate = expense.ExpenseDate,
                PostingDate = DateTime.Now,
                EntryType = "Expense",
                Reference = expense.Reference,
                Description = expense.Description ?? "Business Expense (Updated)",
                Source_Table = "Expense",           // Track source table
                Source_ID = expense.ExpenseID,      // Track source record ID
                Lines = new List<PharmaCare.Application.DTOs.Accounting.JournalEntryLineDto>
                {
                    new() { Account_ID = expense.ExpenseAccount_ID.Value, DebitAmount = expense.Amount, Description = "Expense Debit", Store_ID = storeId },
                    new() { Account_ID = expense.SourceAccount_ID.Value, CreditAmount = expense.Amount, Description = "Payment Credit", Store_ID = storeId }
                }
            };

            var newJournalEntryId = await _accountingService.CreateJournalEntry(journalEntryDto, userId);
            await _accountingService.PostJournalEntry(newJournalEntryId, userId);
            existing.Voucher_ID = newJournalEntryId;
        }

        existing.Amount = expense.Amount;
        existing.Description = expense.Description;
        existing.ExpenseDate = expense.ExpenseDate;
        existing.ExpenseCategory_ID = expense.ExpenseCategory_ID;
        existing.SourceAccount_ID = expense.SourceAccount_ID;
        existing.ExpenseAccount_ID = expense.ExpenseAccount_ID;
        existing.VendorName = expense.VendorName;
        existing.ReceiptNumber = expense.ReceiptNumber;
        existing.Reference = expense.Reference;
        existing.UpdatedBy = userId;
        existing.UpdatedDate = DateTime.Now;

        return await _expenseRepo.Update(existing);
    }
    public async Task<bool> DeleteExpense(int id, int userId)
    {
        var existing = _expenseRepo.GetById(id);
        if (existing == null) return false;

        // Void the associated journal entry if exists
        if (existing.Voucher_ID.HasValue)
        {
            await _accountingService.VoidJournalEntry(existing.Voucher_ID.Value, userId);
        }

        return await _expenseRepo.Delete(existing);
    }
    public async Task<ExpenseSummaryDto> GetExpenseSummary(DateTime fromDate, DateTime toDate)
    {
        var expenses = await GetExpenses(null, fromDate, toDate);

        return new ExpenseSummaryDto
        {
            TotalExpenses = expenses.Sum(e => e.Amount),
            ByCategory = expenses.GroupBy(e => new { e.ExpenseCategory_ID, e.ExpenseCategory?.Name })
                .Select(g => new CategoryExpenseDto
                {
                    CategoryId = g.Key.ExpenseCategory_ID,
                    CategoryName = g.Key.Name ?? "Unknown",
                    Amount = g.Sum(e => e.Amount),
                    Count = g.Count()
                }).ToList(),
            BySourceAccount = expenses.GroupBy(e => new { e.SourceAccount_ID, e.SourceAccount?.AccountName })
                .Select(g => new SourceAccountExpenseDto
                {
                    AccountId = g.Key.SourceAccount_ID,
                    AccountName = g.Key.AccountName ?? "Unknown",
                    Amount = g.Sum(e => e.Amount),
                    Count = g.Count()
                }).ToList()
        };
    }

    #endregion
}
