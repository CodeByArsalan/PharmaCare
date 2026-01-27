using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Models.Finance;
using PharmaCare.Web.Utilities;

namespace PharmaCare.Web.Controllers.Finance;

public class ExpenseController : BaseController
{
    private readonly IExpenseService _expenseService;

    public ExpenseController(IExpenseService expenseService)
    {
        _expenseService = expenseService;
    }

    // ========== EXPENSE CATEGORY MANAGEMENT ==========

    #region EXPENSE CATEGORY MANAGEMENT
    public async Task<IActionResult> ExpenseCategories()
    {
        var categories = await _expenseService.GetExpenseCategories();
        return View(categories);
    }
    [HttpGet]
    public async Task<IActionResult> AddExpenseCategory()
    {
        ViewBag.ParentCategories = new SelectList(await _expenseService.GetExpenseCategories(), "ExpenseCategoryID", "Name");
        return View();
    }
    [HttpPost]
    public async Task<IActionResult> AddExpenseCategory(ExpenseCategory category)
    {
        if (await _expenseService.CreateExpenseCategory(category, LoginUserID))
        {
            ShowMessage(MessageBox.Success, "Expense category created successfully.");
            return RedirectToAction(nameof(ExpenseCategories));
        }
        ShowMessage(MessageBox.Error, "Failed to create expense category.");
        ViewBag.ParentCategories = new SelectList(await _expenseService.GetExpenseCategories(), "ExpenseCategoryID", "Name");
        return View(category);
    }
    [HttpGet]
    public async Task<IActionResult> EditExpenseCategory(int id)
    {
        var category = await _expenseService.GetExpenseCategoryById(id);
        if (category == null) return NotFound();
        ViewBag.ParentCategories = new SelectList(await _expenseService.GetExpenseCategories(), "ExpenseCategoryID", "Name", category.ParentCategory_ID);
        return View(category);
    }
    [HttpPost]
    public async Task<IActionResult> EditExpenseCategory(ExpenseCategory category)
    {
        if (await _expenseService.UpdateExpenseCategory(category, LoginUserID))
        {
            ShowMessage(MessageBox.Success, "Expense category updated successfully.");
            return RedirectToAction(nameof(ExpenseCategories));
        }
        ShowMessage(MessageBox.Error, "Failed to update expense category.");
        ViewBag.ParentCategories = new SelectList(await _expenseService.GetExpenseCategories(), "ExpenseCategoryID", "Name", category.ParentCategory_ID);
        return View(category);
    }
    [HttpPost]
    public async Task<IActionResult> DeleteExpenseCategory(int id)
    {
        if (await _expenseService.DeleteExpenseCategory(id))
        {
            ShowMessage(MessageBox.Success, "Expense category deleted successfully.");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to delete expense category.");
        }
        return RedirectToAction(nameof(ExpenseCategories));
    }
    #endregion

    // ========== EXPENSE MANAGEMENT ==========

    #region EXPENSE MANAGEMENT
    public async Task<IActionResult> Expenses(int? categoryId, DateTime? fromDate, DateTime? toDate)
    {
        var expenses = await _expenseService.GetExpenses(categoryId, fromDate, toDate);
        ViewBag.SelectedCategory = categoryId;
        ViewBag.FromDate = fromDate;
        ViewBag.ToDate = toDate;
        return View(expenses);
    }

    [HttpGet]
    public IActionResult AddExpense()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> AddExpense(Expense expense)
    {
        if (await _expenseService.CreateExpense(expense, LoginUserID, LoginUserStoreID))
        {
            ShowMessage(MessageBox.Success, "Expense recorded successfully.");
            return RedirectToAction(nameof(Expenses));
        }
        ShowMessage(MessageBox.Error, "Failed to record expense.");
        return View(expense);
    }

    [HttpGet]
    public async Task<IActionResult> EditExpense(int id)
    {
        var expense = await _expenseService.GetExpenseById(id);
        if (expense == null) return NotFound();
        return View(expense);
    }

    [HttpPost]
    public async Task<IActionResult> EditExpense(Expense expense)
    {
        if (await _expenseService.UpdateExpense(expense, LoginUserID, LoginUserStoreID))
        {
            ShowMessage(MessageBox.Success, "Expense updated successfully.");
            return RedirectToAction(nameof(Expenses));
        }
        ShowMessage(MessageBox.Error, "Failed to update expense.");
        return View(expense);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        if (await _expenseService.DeleteExpense(id, LoginUserID))
        {
            ShowMessage(MessageBox.Success, "Expense deleted successfully.");
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to delete expense.");
        }
        return RedirectToAction(nameof(Expenses));
    }

    public async Task<IActionResult> ExpenseReport(DateTime? fromDate, DateTime? toDate)
    {
        var fDate = fromDate ?? DateTime.Today.AddDays(-30);
        var tDate = toDate ?? DateTime.Today;

        var summary = await _expenseService.GetExpenseSummary(fDate, tDate);
        ViewBag.FromDate = fDate;
        ViewBag.ToDate = tDate;
        return View(summary);
    }
    #endregion
}
