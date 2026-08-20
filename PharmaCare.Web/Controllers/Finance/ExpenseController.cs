using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Web.Filters;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.Finance;

[Authorize]
public class ExpenseController : BaseController
{
    private readonly IExpenseService _expenseService;
    private readonly IAccountService _accountService;

    public ExpenseController(
        IExpenseService expenseService,
        IAccountService accountService)
    {
        _expenseService = expenseService;
        _accountService = accountService;
    }

    public async Task<IActionResult> ExpensesIndex(int? categoryId, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 25)
    {
        page = NormalizePage(page);
        pageSize = NormalizePageSize(pageSize);
        var pagedResult = await _expenseService.GetPagedAsync(categoryId, fromDate, toDate, page, pageSize);

        var categories = await _expenseService.GetCategoriesAsync();
        ViewBag.Categories = new SelectList(categories.Where(c => c.IsActive), "ExpenseCategoryID", "Name", categoryId);
        
        ViewBag.SelectedCategory = categoryId;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;

        return View(pagedResult);
    }

    public async Task<IActionResult> AddExpense()
    {
        await LoadExpenseDropdownsAsync();
        return View(new Expense
        {
            ExpenseDate = AppTime.Now
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExpense(
        [Bind("ExpenseCategory_ID,ExpenseDate,Amount,SourceAccount_ID,VendorName,Reference,Description")] Expense expense)
    {
        // Remove navigation property validations
        ModelState.Remove("ExpenseCategory");
        ModelState.Remove("SourceAccount");
        ModelState.Remove("ExpenseAccount");
        ModelState.Remove("ExpenseAccount_ID");
        ModelState.Remove("Voucher");
        ModelState.Remove("ExpenseID");

        if (ModelState.IsValid)
        {
            try
            {
                await _expenseService.CreateAsync(expense, CurrentUserId);
                ShowMessage(MessageType.Success, "Expense recorded successfully!");
                return RedirectToAction(nameof(ExpensesIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", SafeErrorMessage(ex, "AddExpense"));
            }
        }

        await LoadExpenseDropdownsAsync();
        return View(expense);
    }

    public async Task<IActionResult> ViewExpense(string id)
    {
        int expenseId = Utility.DecryptId(id);
        if (expenseId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Expense ID.");
            return RedirectToAction(nameof(ExpensesIndex));
        }

        var expense = await _expenseService.GetByIdAsync(expenseId);
        if (expense == null)
        {
            ShowMessage(MessageType.Error, "Expense not found.");
            return RedirectToAction(nameof(ExpensesIndex));
        }

        return View(expense);
    }

    // Approval is what posts the expense voucher to the general ledger. Raising a draft and
    // approving it must not be the same permission.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("Expense", "ExpensesIndex", PermissionType = "edit")]
    public async Task<IActionResult> Approve(string id)
    {
        int expenseId = Utility.DecryptId(id);
        if (expenseId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Expense ID.");
            return RedirectToAction(nameof(ExpensesIndex));
        }

        try
        {
            var result = await _expenseService.ApproveAsync(expenseId, CurrentUserId);
            if (result)
            {
                ShowMessage(MessageType.Success, "Expense approved and posted to accounting!");
            }
            else
            {
                ShowMessage(MessageType.Error, "Failed to approve expense.");
            }
        }
        catch (Exception ex)
        {
            ShowMessage(MessageType.Error, SafeErrorMessage(ex, "Approve"));
        }

        return RedirectToAction(nameof(ExpensesIndex));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("Expense", "ExpensesIndex", PermissionType = "delete")]
    public async Task<IActionResult> Void(string id, string voidReason)
    {
        int expenseId = Utility.DecryptId(id);
        if (expenseId == 0)
        {
            ShowMessage(MessageType.Error, "Invalid Expense ID.");
            return RedirectToAction(nameof(ExpensesIndex));
        }

        if (string.IsNullOrWhiteSpace(voidReason))
        {
            ShowMessage(MessageType.Error, "Void reason is required.");
            return RedirectToAction(nameof(ExpensesIndex));
        }

        try
        {
            var result = await _expenseService.VoidAsync(expenseId, voidReason, CurrentUserId);
            if (result)
            {
                ShowMessage(MessageType.Success, "Expense voided successfully!");
            }
            else
            {
                ShowMessage(MessageType.Error, "Failed to void expense.");
            }
        }
        catch (Exception ex)
        {
            ShowMessage(MessageType.Error, SafeErrorMessage(ex, "Void"));
        }

        return RedirectToAction(nameof(ExpensesIndex));
    }

    /// <summary>
    /// Gets accounts filtered by type (AJAX).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAccountsByType(int typeId)
    {
        var accounts = await _accountService.GetAllAsync();
        var result = accounts
            .Where(a => a.IsActive && a.AccountType_ID == typeId)
            .Select(a => new { id = a.AccountID, name = a.Name });
        return Json(result);
    }

    /// <summary>
    /// Gets the default expense account for a category (AJAX).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCategoryDefaults(int categoryId)
    {
        var category = await _expenseService.GetCategoryByIdAsync(categoryId);
        if (category == null) return Json(new { });
        return Json(new
        {
            defaultExpenseAccountId = category.DefaultExpenseAccount_ID
        });
    }

    /// <summary>
    /// Displays list of expense categories.
    /// </summary>
    public async Task<IActionResult> ExpenseCategoriesIndex()
    {
        var categories = await _expenseService.GetCategoriesAsync();
        return View(categories);
    }

    /// <summary>
    /// Shows form to add a new expense category.
    /// </summary>
    public async Task<IActionResult> AddExpenseCategory()
    {
        await LoadCategoryDropdownsAsync();
        return View(new ExpenseCategory());
    }

    /// <summary>
    /// Creates an expense category.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExpenseCategory(
        [Bind("Name,Parent_ID,DefaultExpenseAccount_ID")] ExpenseCategory category)
    {
        ModelState.Remove("ParentCategory");
        ModelState.Remove("DefaultExpenseAccount");
        ModelState.Remove("ChildCategories");
        ModelState.Remove("Expenses");

        if (ModelState.IsValid)
        {
            try
            {
                await _expenseService.CreateCategoryAsync(category, CurrentUserId);
                ShowMessage(MessageType.Success, "Expense Category created successfully!");
                return RedirectToAction(nameof(ExpenseCategoriesIndex));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", SafeErrorMessage(ex, "AddExpenseCategory"));
            }
        }

        await LoadCategoryDropdownsAsync();
        return View(category);
    }

    /// <summary>
    /// Shows form to edit an expense category.
    /// </summary>
    public async Task<IActionResult> EditExpenseCategory(string id)
    {
        int categoryId = Utility.DecryptId(id);
        if (categoryId == 0) return NotFound();

        var category = await _expenseService.GetCategoryByIdAsync(categoryId);
        if (category == null) return NotFound();

        await LoadCategoryDropdownsAsync(categoryId);
        return View(category);
    }

    /// <summary>
    /// Updates an expense category.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditExpenseCategory(string id,
        [Bind("ExpenseCategoryID,Name,Parent_ID,DefaultExpenseAccount_ID,IsActive")] ExpenseCategory category)
    {
        int categoryId = Utility.DecryptId(id);
        if (categoryId != category.ExpenseCategoryID) return NotFound();

        ModelState.Remove("ParentCategory");
        ModelState.Remove("DefaultExpenseAccount");
        ModelState.Remove("ChildCategories");
        ModelState.Remove("Expenses");

        if (ModelState.IsValid)
        {
            var updated = await _expenseService.UpdateCategoryAsync(category, CurrentUserId);
            if (!updated) return NotFound();

            ShowMessage(MessageType.Success, "Expense Category updated successfully!");
            return RedirectToAction(nameof(ExpenseCategoriesIndex));
        }

        await LoadCategoryDropdownsAsync(categoryId);
        return View(category);
    }

    /// <summary>
    /// Toggles expense category status (active/inactive).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteExpenseCategory(string id)
    {
        int categoryId = Utility.DecryptId(id);
        if (categoryId == 0) return NotFound();

        await _expenseService.ToggleCategoryStatusAsync(categoryId, CurrentUserId);
        ShowMessage(MessageType.Success, "Category status updated successfully!");
        return RedirectToAction(nameof(ExpenseCategoriesIndex));
    }

    // ========================================================================
    //  EXPENSE BUDGETS
    // ========================================================================

    [HttpGet]
    public async Task<IActionResult> BudgetManagement(int? year, int? month)
    {
        var targetYear = year ?? AppTime.Now.Year;
        var targetMonth = month ?? AppTime.Now.Month;

        var budgets = await _expenseService.GetBudgetsAsync(targetYear, targetMonth);
        
        ViewBag.Year = targetYear;
        ViewBag.Month = targetMonth;

        return View(budgets);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [LinkedToPage("Expense", "ExpensesIndex", PermissionType = "edit")]
    public async Task<IActionResult> BudgetManagement(int year, int month, List<PharmaCare.Application.ViewModels.Report.ExpenseBudgetVM> budgets)
    {
        try
        {
            await _expenseService.SaveBudgetsAsync(year, month, budgets, CurrentUserId);
            ShowMessage(MessageType.Success, "Budgets saved successfully!");
            return RedirectToAction(nameof(BudgetManagement), new { year, month });
        }
        catch (Exception ex)
        {
            ShowMessage(MessageType.Error, SafeErrorMessage(ex, "BudgetManagement"));
            ViewBag.Year = year;
            ViewBag.Month = month;
            return View(budgets);
        }
    }

    // ========================================================================
    //  PRIVATE HELPERS
    // ========================================================================

    private async Task LoadExpenseDropdownsAsync()
    {
        // Categories
        var categories = await _expenseService.GetCategoriesAsync();
        ViewBag.Categories = new SelectList(
            categories.Where(c => c.IsActive),
            "ExpenseCategoryID",
            "Name"
        );

        // Accounts (exclude Cash/Bank for proper expense account selection)
        var accounts = await _accountService.GetAllAsync();
        ViewBag.ExpenseAccounts = new SelectList(
            accounts.Where(a => a.IsActive && 
                               (a.AccountType == null || (a.AccountType.Code != "CASH" && a.AccountType.Code != "BANK"))),
            "AccountID",
            "Name"
        );

        // Source accounts (Cash/Bank only)
        ViewBag.SourceAccounts = new SelectList(
            accounts.Where(a => a.IsActive && (
                a.AccountType != null && (
                    a.AccountType.Code == "CASH" ||
                    a.AccountType.Code == "BANK"
                )
            )),
            "AccountID",
            "Name"
        );
    }

    private async Task LoadCategoryDropdownsAsync(int? excludeId = null)
    {
        // Parent categories (exclude self to prevent circular reference)
        var categories = await _expenseService.GetCategoriesAsync();
        ViewBag.ParentCategories = new SelectList(
            categories.Where(c => c.IsActive && c.ExpenseCategoryID != excludeId),
            "ExpenseCategoryID",
            "Name"
        );

        // Expense accounts (exclude Cash/Bank)
        var accounts = await _accountService.GetAllAsync();
        ViewBag.ExpenseAccounts = new SelectList(
            accounts.Where(a => a.IsActive && 
                               (a.AccountType == null || (a.AccountType.Code != "CASH" && a.AccountType.Code != "BANK"))),
            "AccountID",
            "Name"
        );
    }
}
