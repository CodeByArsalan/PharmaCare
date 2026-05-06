using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.Implementations.Finance;

/// <summary>
/// Service for managing expenses with double-entry accounting.
/// DR: Expense Account, CR: Source (Cash/Bank) Account
/// </summary>
public class ExpenseService : IExpenseService
{
    private readonly IRepository<Expense> _expenseRepository;
    private readonly IRepository<ExpenseCategory> _categoryRepository;
    private readonly IRepository<Voucher> _voucherRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<ExpenseBudget> _budgetRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFinancialPeriodService _financialPeriodService;

    private const string CASH_EXPENSE_VOUCHER_CODE = "CP";  // Cash Payment
    private const string BANK_EXPENSE_VOUCHER_CODE = "BP";  // Bank Payment

    public ExpenseService(
        IRepository<Expense> expenseRepository,
        IRepository<ExpenseCategory> categoryRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Account> accountRepository,
        IRepository<ExpenseBudget> budgetRepository,
        IUnitOfWork unitOfWork,
        IFinancialPeriodService financialPeriodService)
    {
        _expenseRepository = expenseRepository;
        _categoryRepository = categoryRepository;
        _voucherRepository = voucherRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _accountRepository = accountRepository;
        _budgetRepository = budgetRepository;
        _unitOfWork = unitOfWork;
        _financialPeriodService = financialPeriodService;
    }

    // ========================================================================
    //  EXPENSE CRUD
    // ========================================================================

    public async Task<IEnumerable<Expense>> GetAllAsync()
    {
        return await _expenseRepository.Query()
            .AsNoTracking()
            .Include(e => e.ExpenseCategory)
            .Include(e => e.SourceAccount)
            .Include(e => e.ExpenseAccount)
            .Include(e => e.Voucher)
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.ExpenseID)
            .ToListAsync();
    }

    public async Task<PharmaCare.Application.DTOs.PagedResult<Expense>> GetPagedAsync(
        int? categoryId, DateTime? from, DateTime? to, int page, int pageSize)
    {
        IQueryable<Expense> query = _expenseRepository.Query()
            .AsNoTracking()
            .Include(e => e.ExpenseCategory)
            .Include(e => e.SourceAccount)
            .Include(e => e.ExpenseAccount)
            .Include(e => e.Voucher);

        if (categoryId.HasValue)
            query = query.Where(e => e.ExpenseCategory_ID == categoryId.Value);

        if (from.HasValue)
            query = query.Where(e => e.ExpenseDate >= from.Value.Date);

        if (to.HasValue)
            query = query.Where(e => e.ExpenseDate <= to.Value.Date.AddDays(1).AddTicks(-1));

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.ExpenseDate)
            .ThenByDescending(e => e.ExpenseID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PharmaCare.Application.DTOs.PagedResult<Expense>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize
        };
    }

    public async Task<Expense?> GetByIdAsync(int id)
    {
        return await _expenseRepository.Query()
            .AsNoTracking()
            .Include(e => e.ExpenseCategory)
            .Include(e => e.SourceAccount)
            .Include(e => e.ExpenseAccount)
            .Include(e => e.Voucher)
                .ThenInclude(v => v!.VoucherType)
            .Include(e => e.Voucher)
                .ThenInclude(v => v!.VoucherDetails)
                    .ThenInclude(vd => vd.Account)
            .FirstOrDefaultAsync(e => e.ExpenseID == id);
    }

    public async Task<Expense> CreateAsync(Expense expense, int userId)
    {
        if (await _financialPeriodService.IsPeriodLockedAsync(expense.ExpenseDate))
            throw new InvalidOperationException("The financial period for this expense date is closed.");

        return await ExecuteInTransactionAsync(async () =>
        {
            // Fetch category to get its default expense account
            var category = await _categoryRepository.Query()
                .FirstOrDefaultAsync(c => c.ExpenseCategoryID == expense.ExpenseCategory_ID);

            if (category == null)
                throw new InvalidOperationException("Expense category not found.");

            if (category.DefaultExpenseAccount_ID == null || category.DefaultExpenseAccount_ID == 0)
                throw new InvalidOperationException($"Expense category '{category.Name}' does not have a default expense account configured. Please configure it first.");

            // Automatically set the expense account based on the category
            expense.ExpenseAccount_ID = category.DefaultExpenseAccount_ID.Value;

            // Validate accounts
            var sourceAccount = await _accountRepository.Query()
                .Include(a => a.AccountType)
                .FirstOrDefaultAsync(a => a.AccountID == expense.SourceAccount_ID);

            if (sourceAccount == null)
                throw new InvalidOperationException("Source account not found.");

            var expenseAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == expense.ExpenseAccount_ID);

            if (expenseAccount == null)
                throw new InvalidOperationException("Expense account not found.");

            if (expense.Amount <= 0)
                throw new InvalidOperationException("Expense amount must be greater than zero.");

            if (expense.Amount > 100000000) // 100 Million limit
                throw new InvalidOperationException("Expense amount exceeds sanity limit (100 Million).");

            // Set audit fields
            expense.CreatedAt = DateTime.Now;
            expense.CreatedBy = userId;
            expense.Status = TransactionStatus.Draft;

            await _expenseRepository.AddAsync(expense);
            await _unitOfWork.SaveChangesAsync();

            return expense;
        });
    }

    public async Task<bool> ApproveAsync(int expenseId, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            var expense = await _expenseRepository.Query()
                .Include(e => e.ExpenseCategory)
                .Include(e => e.SourceAccount)
                .Include(e => e.ExpenseAccount)
                .FirstOrDefaultAsync(e => e.ExpenseID == expenseId);

            if (expense == null)
                throw new InvalidOperationException("Expense not found.");

            if (expense.Status != TransactionStatus.Draft)
                throw new InvalidOperationException($"Expense is already {expense.Status}.");

            if (await _financialPeriodService.IsPeriodLockedAsync(expense.ExpenseDate))
                throw new InvalidOperationException("The financial period for this expense date is closed.");

            // Create accounting voucher (DR: Expense, CR: Cash/Bank)
            var voucher = await CreateExpenseVoucherAsync(expense, expense.ExpenseAccount!, expense.SourceAccount!, userId);
            await _unitOfWork.SaveChangesAsync();

            expense.Voucher_ID = voucher.VoucherID;
            expense.Status = TransactionStatus.Approved;
            expense.ApprovedBy_ID = userId;
            expense.ApprovedAt = DateTime.Now;

            _expenseRepository.Update(expense);
            await _unitOfWork.SaveChangesAsync();

            return true;
        });
    }

    public async Task<bool> VoidAsync(int expenseId, string reason, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            var expense = await _expenseRepository.Query()
                .Include(e => e.Voucher)
                    .ThenInclude(v => v!.VoucherDetails)
                .Include(e => e.Voucher)
                    .ThenInclude(v => v!.VoucherType)
                .Include(e => e.SourceAccount)
                .Include(e => e.ExpenseAccount)
                .FirstOrDefaultAsync(e => e.ExpenseID == expenseId);

            if (expense == null)
                throw new InvalidOperationException("Expense not found.");

            if (expense.Status == TransactionStatus.Void)
                throw new InvalidOperationException("Expense is already void.");

            if (await _financialPeriodService.IsPeriodLockedAsync(expense.ExpenseDate))
                throw new InvalidOperationException("The financial period for this expense date is closed.");

            // Create reversal voucher if original voucher exists (only for Approved ones)
            if (expense.Status == TransactionStatus.Approved && expense.Voucher != null && !expense.Voucher.IsReversed)
            {
                var reversalVoucher = await CreateReversalVoucherAsync(expense.Voucher, reason, userId);
                await _unitOfWork.SaveChangesAsync();

                // Link original and reversal
                expense.Voucher.IsReversed = true;
                expense.Voucher.ReversedByVoucher_ID = reversalVoucher.VoucherID;
                expense.Voucher.VoidReason = reason;
                _voucherRepository.Update(expense.Voucher);
            }

            // Update status and audit fields
            expense.Status = TransactionStatus.Void;
            expense.UpdatedAt = DateTime.Now;
            expense.UpdatedBy = userId;
            _expenseRepository.Update(expense);

            await _unitOfWork.SaveChangesAsync();
            return true;
        });
    }

    // ========================================================================
    //  EXPENSE CATEGORY CRUD
    // ========================================================================

    public async Task<IEnumerable<ExpenseCategory>> GetCategoriesAsync()
    {
        return await _categoryRepository.Query()
            .AsNoTracking()
            .Include(c => c.ParentCategory)
            .Include(c => c.DefaultExpenseAccount)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<ExpenseCategory?> GetCategoryByIdAsync(int id)
    {
        return await _categoryRepository.Query()
            .Include(c => c.ParentCategory)
            .Include(c => c.DefaultExpenseAccount)
            .FirstOrDefaultAsync(c => c.ExpenseCategoryID == id);
    }

    public async Task<ExpenseCategory> CreateCategoryAsync(ExpenseCategory category, int userId)
    {
        category.CreatedAt = DateTime.Now;
        category.CreatedBy = userId;
        category.IsActive = true;

        await _categoryRepository.AddAsync(category);
        await _unitOfWork.SaveChangesAsync();
        return category;
    }

    public async Task<bool> UpdateCategoryAsync(ExpenseCategory category, int userId)
    {
        var existing = await _categoryRepository.GetByIdAsync(category.ExpenseCategoryID);
        if (existing == null) return false;

        existing.Name = category.Name;
        existing.Parent_ID = category.Parent_ID;
        existing.DefaultExpenseAccount_ID = category.DefaultExpenseAccount_ID;
        existing.UpdatedAt = DateTime.Now;
        existing.UpdatedBy = userId;

        _categoryRepository.Update(existing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task ToggleCategoryStatusAsync(int categoryId, int userId)
    {
        var category = await _categoryRepository.GetByIdAsync(categoryId);
        if (category == null) return;

        category.IsActive = !category.IsActive;
        category.UpdatedAt = DateTime.Now;
        category.UpdatedBy = userId;

        _categoryRepository.Update(category);
        await _unitOfWork.SaveChangesAsync();
    }

    // ========================================================================
    //  PRIVATE HELPERS
    // ========================================================================

    /// <summary>
    /// Creates an expense voucher with double-entry accounting.
    /// DR: Expense Account (increases expense)
    /// CR: Source Cash/Bank Account (decreases asset)
    /// </summary>
    private async Task<Voucher> CreateExpenseVoucherAsync(
        Expense expense,
        Account expenseAccount,
        Account sourceAccount,
        int userId)
    {
        // Determine voucher type based on source account type
        var sourceAccountType = sourceAccount.AccountType?.Code ?? "";
        var voucherTypeCode = sourceAccountType.Equals("BANK", StringComparison.OrdinalIgnoreCase)
            ? BANK_EXPENSE_VOUCHER_CODE
            : CASH_EXPENSE_VOUCHER_CODE;

        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == voucherTypeCode);

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{voucherTypeCode}' not found. Please ensure it exists in the database.");

        var voucherNo = await GenerateVoucherNoAsync(voucherTypeCode);

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = expense.ExpenseDate,
            TotalDebit = expense.Amount,
            TotalCredit = expense.Amount,
            Status = "Posted",
            SourceTable = "Expense",
            SourceID = expense.ExpenseID,
            Narration = $"Expense: {expense.Description ?? "N/A"}. Vendor: {expense.VendorName ?? "N/A"}. Ref: {expense.Reference ?? "N/A"}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = new List<VoucherDetail>
            {
                // Debit: Expense Account - increases expense
                new VoucherDetail
                {
                    Account_ID = expenseAccount.AccountID,
                    DebitAmount = expense.Amount,
                    CreditAmount = 0,
                    Description = $"Expense: {expense.Description ?? expense.ExpenseCategory?.Name ?? "General"}"
                },
                // Credit: Source (Cash/Bank) Account - decreases asset
                new VoucherDetail
                {
                    Account_ID = sourceAccount.AccountID,
                    DebitAmount = 0,
                    CreditAmount = expense.Amount,
                    Description = $"Payment from {sourceAccount.Name}"
                }
            }
        };

        await _voucherRepository.AddAsync(voucher);
        return voucher;
    }

    /// <summary>
    /// Creates a reversal voucher that mirrors the original (swapped DR/CR).
    /// </summary>
    private async Task<Voucher> CreateReversalVoucherAsync(Voucher originalVoucher, string reason, int userId)
    {
        var voucherType = originalVoucher.VoucherType
            ?? await _voucherTypeRepository.GetByIdAsync(originalVoucher.VoucherType_ID);

        // Use JV type for reversals
        var jvType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == "JV");

        var reversalVoucherType = jvType ?? voucherType!;
        var voucherNo = await GenerateVoucherNoAsync(reversalVoucherType.Code);

        var reversalVoucher = new Voucher
        {
            VoucherType_ID = reversalVoucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = DateTime.Now,
            TotalDebit = originalVoucher.TotalCredit,
            TotalCredit = originalVoucher.TotalDebit,
            Status = "Posted",
            SourceTable = "Expense",
            SourceID = originalVoucher.SourceID,
            Narration = $"REVERSAL of {originalVoucher.VoucherNo}. Reason: {reason}",
            ReversesVoucher_ID = originalVoucher.VoucherID,
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = originalVoucher.VoucherDetails.Select(d => new VoucherDetail
            {
                Account_ID = d.Account_ID,
                DebitAmount = d.CreditAmount,   // Swap
                CreditAmount = d.DebitAmount,   // Swap
                Description = $"REVERSAL: {d.Description}",
                Party_ID = d.Party_ID,
                Product_ID = d.Product_ID
            }).ToList()
        };

        await _voucherRepository.AddAsync(reversalVoucher);
        return reversalVoucher;
    }

    private async Task<string> GenerateVoucherNoAsync(string voucherTypeCode)
    {
        var datePrefix = $"{voucherTypeCode}-{DateTime.Now:yyyyMMdd}-";

        var lastVoucher = await _voucherRepository.Query()
            .Where(v => v.VoucherNo.StartsWith(datePrefix))
            .OrderByDescending(v => v.VoucherNo)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (lastVoucher != null)
        {
            var parts = lastVoucher.VoucherNo.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
    }

    private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var result = await operation();
            await _unitOfWork.CommitTransactionAsync();
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    // ========================================================================
    //  EXPENSE BUDGETS
    // ========================================================================

    public async Task<IEnumerable<PharmaCare.Application.ViewModels.Report.ExpenseBudgetVM>> GetBudgetsAsync(int year, int month)
    {
        var categories = await _categoryRepository.Query()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();

        var budgets = await _budgetRepository.Query()
            .Where(b => b.Year == year && b.Month == month)
            .ToListAsync();

        return categories.Select(c => new PharmaCare.Application.ViewModels.Report.ExpenseBudgetVM
        {
            CategoryId = c.ExpenseCategoryID,
            CategoryName = c.Name,
            BudgetAmount = budgets.FirstOrDefault(b => b.ExpenseCategory_ID == c.ExpenseCategoryID)?.BudgetAmount ?? 0,
            Remarks = budgets.FirstOrDefault(b => b.ExpenseCategory_ID == c.ExpenseCategoryID)?.Remarks
        });
    }

    public async Task SaveBudgetsAsync(int year, int month, List<PharmaCare.Application.ViewModels.Report.ExpenseBudgetVM> budgets, int userId)
    {
        foreach (var budgetDto in budgets)
        {
            var existing = await _budgetRepository.Query()
                .FirstOrDefaultAsync(b => b.Year == year && b.Month == month && b.ExpenseCategory_ID == budgetDto.CategoryId);

            if (existing != null)
            {
                existing.BudgetAmount = budgetDto.BudgetAmount;
                existing.Remarks = budgetDto.Remarks;
                existing.UpdatedAt = DateTime.Now;
                existing.UpdatedBy = userId;
                _budgetRepository.Update(existing);
            }
            else if (budgetDto.BudgetAmount > 0)
            {
                var newBudget = new ExpenseBudget
                {
                    ExpenseCategory_ID = budgetDto.CategoryId,
                    Year = year,
                    Month = month,
                    BudgetAmount = budgetDto.BudgetAmount,
                    Remarks = budgetDto.Remarks,
                    CreatedAt = DateTime.Now,
                    CreatedBy = userId
                };
                await _budgetRepository.AddAsync(newBudget);
            }
        }

        await _unitOfWork.SaveChangesAsync();
    }
}
