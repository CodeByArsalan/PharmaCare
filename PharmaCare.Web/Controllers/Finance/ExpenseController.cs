using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.Finance;

/// <summary>
/// Controller for managing Expenses and Expense Categories.
/// </summary>
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

    // ========================================================================
    //  EXPENSE ACTIONS
    // ========================================================================

    /// <summary>
    /// Displays list of all expenses.
    /// </summary>
    public async Task<IActionResult> ExpensesIndex()
    {
        var expenses = await _expenseService.GetAllAsync();
        return View(expenses);
    }

    /// <summary>
    /// Shows form to record a new expense.
    /// </summary>
    public async Task<IActionResult> AddExpense()
    {
        await LoadExpenseDropdownsAsync();
        return View(new Expense
        {
            ExpenseDate = DateTime.Now
        });
    }

    /// <summary>
    /// Creates a new expense.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExpense(Expense expense)
    {
        // Remove navigation property validations
        ModelState.Remove("ExpenseCategory");
        ModelState.Remove("SourceAccount");
        ModelState.Remove("ExpenseAccount");
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
                ModelState.AddModelError("", "Error recording expense: " + ex.Message);
            }
        }

        await LoadExpenseDropdownsAsync();
        return View(expense);
    }

    /// <summary>
    /// Shows expense details.
    /// </summary>
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

    /// <summary>
    /// Voids an expense.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
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

        var result = await _expenseService.VoidAsync(expenseId, voidReason, CurrentUserId);
        if (result)
        {
            ShowMessage(MessageType.Success, "Expense voided successfully!");
        }
        else
        {
            ShowMessage(MessageType.Error, "Failed to void expense.");
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

    // ========================================================================
    //  EXPENSE CATEGORY ACTIONS
    // ========================================================================

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
    public async Task<IActionResult> AddExpenseCategory(ExpenseCategory category)
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
                ModelState.AddModelError("", "Error: " + ex.Message);
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
    public async Task<IActionResult> EditExpenseCategory(string id, ExpenseCategory category)
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

        // Accounts (all active for expense account selection)
        var accounts = await _accountService.GetAllAsync();
        ViewBag.ExpenseAccounts = new SelectList(
            accounts.Where(a => a.IsActive),
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

        // Expense accounts
        var accounts = await _accountService.GetAllAsync();
        ViewBag.ExpenseAccounts = new SelectList(
            accounts.Where(a => a.IsActive),
            "AccountID",
            "Name"
        );
    }
}
