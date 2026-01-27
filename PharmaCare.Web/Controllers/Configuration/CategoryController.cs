using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.ViewModels;

namespace PharmaCare.Web.Controllers.Configuration;

public class CategoryController(ICategoryService _categoryService, ISubCategoryService _subCategoryService, PharmaCare.Infrastructure.Interfaces.IComboBoxRepository _comboBox) : BaseController
{
    // --- Combined View Actions ---
    public async Task<IActionResult> Categories()
    {
        var viewModel = new CategoriesViewModel
        {
            CategoryList = await _categoryService.GetAllCategories(),
            IsEditMode = false,
            SaleAccounts = _comboBox.GetSaleAccounts(),
            StockAccounts = _comboBox.GetInventoryAccounts(),
            COGSAccounts = _comboBox.GetConsumptionAccounts(),
            DamageExpenseAccounts = _comboBox.GetDamageAccounts(),
        };
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCategory(CategoriesViewModel viewModel)
    {
        var category = viewModel.CurrentCategory;
        category.CreatedBy = LoginUserID;

        if (ModelState.IsValid)
        {
            if (await _categoryService.CreateCategory(category))
            {
                ShowMessage(MessageBox.Success, "Category created successfully");
                return RedirectToAction(nameof(Categories));
            }
            else
            {
                ShowMessage(MessageBox.Error, "Failed to create Category.");
            }
        }

        // Reload lists on failure
        viewModel.CategoryList = await _categoryService.GetAllCategories();
        viewModel.SaleAccounts = _comboBox.GetSaleAccounts(category.SaleAccount_ID);
        viewModel.StockAccounts = _comboBox.GetInventoryAccounts(category.StockAccount_ID);
        viewModel.COGSAccounts = _comboBox.GetConsumptionAccounts(category.COGSAccount_ID);
        viewModel.DamageExpenseAccounts = _comboBox.GetDamageAccounts(category.DamageExpenseAccount_ID);

        return View("Categories", viewModel);
    }

    public async Task<IActionResult> EditCategory(string id)
    {
        if (string.IsNullOrEmpty(id)) return NotFound();

        int decryptedId = DecryptId(id);
        var category = await _categoryService.GetCategoryById(decryptedId);
        if (category == null) return NotFound();

        var viewModel = new CategoriesViewModel
        {
            CurrentCategory = category,
            CategoryList = await _categoryService.GetAllCategories(),
            IsEditMode = true,
            SaleAccounts = _comboBox.GetSaleAccounts(category.SaleAccount_ID),
            StockAccounts = _comboBox.GetInventoryAccounts(category.StockAccount_ID),
            COGSAccounts = _comboBox.GetConsumptionAccounts(category.COGSAccount_ID),
            DamageExpenseAccounts = _comboBox.GetDamageAccounts(category.DamageExpenseAccount_ID),
        };
        return View("Categories", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(CategoriesViewModel viewModel)
    {
        var category = viewModel.CurrentCategory;
        category.UpdatedBy = LoginUserID;

        if (ModelState.IsValid)
        {
            await _categoryService.UpdateCategory(category);
            ShowMessage(MessageBox.Success, "Category updated successfully");
            return RedirectToAction(nameof(Categories));
        }
        else
        {
            ShowMessage(MessageBox.Error, "Failed to update Category.");
        }

        // Reload lists on failure
        viewModel.CategoryList = await _categoryService.GetAllCategories();
        viewModel.SaleAccounts = _comboBox.GetSaleAccounts(category.SaleAccount_ID);
        viewModel.StockAccounts = _comboBox.GetInventoryAccounts(category.StockAccount_ID);
        viewModel.COGSAccounts = _comboBox.GetConsumptionAccounts(category.COGSAccount_ID);
        viewModel.DamageExpenseAccounts = _comboBox.GetDamageAccounts(category.DamageExpenseAccount_ID);

        return View("Categories", viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(string id)
    {
        int decryptedId = DecryptId(id);
        await _categoryService.DeleteCategory(decryptedId);
        ShowMessage(MessageBox.Warning, "Category Status Updated successfully");
        return RedirectToAction(nameof(Categories));
    }
    [HttpGet]
    public async Task<IActionResult> GetSubCategories(int id)
    {
        var subCategories = await _subCategoryService.GetSubCategoriesByCategoryId(id);
        return Json(subCategories.Select(sc => new { id = sc.SubCategoryID, name = sc.SubCategoryName }));
    }
}
