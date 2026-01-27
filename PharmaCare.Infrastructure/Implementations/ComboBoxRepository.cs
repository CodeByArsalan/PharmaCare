using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Infrastructure.Implementations;

public class ComboBoxRepository(PharmaCareDBContext _dbContext) : IComboBoxRepository
{
    public async Task<SelectList> GetCategoriesAsync(object? selectedValue = null)
    {
        var categories = await _dbContext.Categories
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
        return new SelectList(categories, "CategoryID", "CategoryName", selectedValue);
    }
    public async Task<SelectList> GetUserTypesAsync(object? selectedValue = null)
    {
        var userTypes = await _dbContext.UserTypes
            .Where(ut => ut.IsActive)
            .OrderBy(ut => ut.UserType)
            .ToListAsync();
        return new SelectList(userTypes, "UserTypeID", "UserType", selectedValue);
    }
    public async Task<SelectList> GetSuppliersAsync(object? selectedValue = null)
    {
        var suppliers = await _dbContext.Parties
            .Where(p => p.PartyType == "Supplier" || p.PartyType == "Both")
            .OrderBy(p => p.PartyName)
            .ToListAsync();
        return new SelectList(suppliers, "PartyID", "PartyName", selectedValue);
    }
    public async Task<SelectList> GetProductsAsync(object? selectedValue = null)
    {
        var products = await _dbContext.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.ProductName)
            .ToListAsync();
        return new SelectList(products, "ProductID", "ProductName", selectedValue);
    }
    public async Task<SelectList> GetPendingPurchaseOrdersAsync(object? selectedValue = null)
    {
        var purchaseOrders = await _dbContext.PurchaseOrders
            .Where(po => po.Status == "Pending")
            .OrderByDescending(po => po.OrderDate)
            .ToListAsync();
        return new SelectList(purchaseOrders, "PurchaseOrderID", "PurchaseOrderNumber", selectedValue);
    }
    public async Task<SelectList> GetCustomersAsync(object? selectedValue = null)
    {
        var customers = await _dbContext.Parties
            .Where(p => (p.PartyType == "Customer" || p.PartyType == "Both") && p.IsActive)
            .OrderBy(p => p.PartyName)
            .ToListAsync();
        return new SelectList(customers, "PartyID", "PartyName", selectedValue);
    }

    public async Task<SelectList> GetStoresByLoginUserIDAsync(int loginUserId, object? selectedValue = null)
    {
        var user = await _dbContext.Users
            .Include(u => u.Store)
            .FirstOrDefaultAsync(u => u.Id == loginUserId);

        if (user == null) return new SelectList(Enumerable.Empty<SelectListItem>());

        // Admin sees all stores
        if (user.UserType_ID == 1)
        {
            var allStores = await _dbContext.Stores
                .OrderBy(s => s.Name)
                .ToListAsync();
            return new SelectList(allStores, "StoreID", "Name", selectedValue);
        }

        // Branch user sees only their assigned store
        if (user.Store_ID.HasValue)
        {
            var stores = await _dbContext.Stores
                .Where(s => s.StoreID == user.Store_ID.Value)
                .ToListAsync();
            return new SelectList(stores, "StoreID", "Name", selectedValue);
        }

        return new SelectList(Enumerable.Empty<SelectListItem>());
    }

    public async Task<SelectList> GetRootCategoriesAsync(object? selectedValue = null)
    {
        var categories = await _dbContext.Categories
            .OrderBy(c => c.CategoryName)
            .ToListAsync();
        return new SelectList(categories, "CategoryID", "CategoryName", selectedValue);
    }

    public async Task<SelectList> GetSubCategoriesAsync(int parentId, object? selectedValue = null)
    {
        var subCategories = await _dbContext.SubCategories
            .Where(sc => sc.Category_ID == parentId)
            .OrderBy(sc => sc.SubCategoryName)
            .ToListAsync();
        return new SelectList(subCategories, "SubCategoryID", "SubCategoryName", selectedValue);
    }

    public async Task<SelectList> GetAccountTypesAsync(object? selectedValue = null)
    {
        var types = await _dbContext.AccountTypes
            .OrderBy(at => at.AccountTypeID)
            .ToListAsync();
        return new SelectList(types, "AccountTypeID", "TypeName", selectedValue);
    }

    public async Task<SelectList> GetAllAccountsAsync(object? selectedValue = null)
    {
        var accounts = await _dbContext.ChartOfAccounts
            .Where(a => a.IsActive)
            .OrderBy(a => a.AccountName)
            .ToListAsync();
        return new SelectList(accounts, "AccountID", "AccountName", selectedValue);
    }

    public async Task<SelectList> GetHeadsAsync(object? selectedValue = null)
    {
        var heads = await _dbContext.Heads
            .OrderBy(h => h.HeadName)
            .ToListAsync();
        return new SelectList(heads, "HeadID", "HeadName", selectedValue);
    }

    public async Task<SelectList> GetSubheadsAsync(int headId, object? selectedValue = null)
    {
        var subheads = await _dbContext.Subheads
            .Where(s => s.Head_ID == headId)
            .OrderBy(s => s.SubheadName)
            .ToListAsync();
        return new SelectList(subheads, "SubheadID", "SubheadName", selectedValue);
    }

    // ========== ACCOUNT TYPE SPECIFIC METHODS ==========

    /// <summary>Cash (1) + Bank (2) accounts for payment dropdown</summary>
    public async Task<SelectList> GetPaymentAccountsAsync(object? selectedValue = null)
    {
        var accounts = await _dbContext.ChartOfAccounts
            .Where(a => a.IsActive && (a.AccountType_ID == 1 || a.AccountType_ID == 2))
            .OrderBy(a => a.AccountName)
            .ToListAsync();
        return new SelectList(accounts, "AccountID", "AccountName", selectedValue);
    }

    /// <summary>General (5) accounts for expense dropdown</summary>
    public async Task<SelectList> GetExpenseAccountsAsync(object? selectedValue = null)
    {
        var accounts = await _dbContext.ChartOfAccounts
            .Where(a => a.IsActive && a.AccountType_ID == 5)
            .OrderBy(a => a.AccountName)
            .ToListAsync();
        return new SelectList(accounts, "AccountID", "AccountName", selectedValue);
    }

    /// <summary>Inventory (6) accounts</summary>
    public async Task<SelectList> GetInventoryAccountsAsync(object? selectedValue = null)
    {
        var accounts = await _dbContext.ChartOfAccounts
            .Where(a => a.IsActive && a.AccountType_ID == 6)
            .OrderBy(a => a.AccountName)
            .ToListAsync();
        return new SelectList(accounts, "AccountID", "AccountName", selectedValue);
    }

    /// <summary>Consumptions (7) accounts</summary>
    public async Task<SelectList> GetConsumptionAccountsAsync(object? selectedValue = null)
    {
        var accounts = await _dbContext.ChartOfAccounts
            .Where(a => a.IsActive && a.AccountType_ID == 7)
            .OrderBy(a => a.AccountName)
            .ToListAsync();
        return new SelectList(accounts, "AccountID", "AccountName", selectedValue);
    }

    /// <summary>Sale Account (8)</summary>
    public async Task<SelectList> GetSaleAccountsAsync(object? selectedValue = null)
    {
        var accounts = await _dbContext.ChartOfAccounts
            .Where(a => a.IsActive && a.AccountType_ID == 8)
            .OrderBy(a => a.AccountName)
            .ToListAsync();
        return new SelectList(accounts, "AccountID", "AccountName", selectedValue);
    }

    /// <summary>Damage Expense Stock (9) accounts</summary>
    public async Task<SelectList> GetDamageAccountsAsync(object? selectedValue = null)
    {
        var accounts = await _dbContext.ChartOfAccounts
            .Where(a => a.IsActive && a.AccountType_ID == 9)
            .OrderBy(a => a.AccountName)
            .ToListAsync();
        return new SelectList(accounts, "AccountID", "AccountName", selectedValue);
    }

    /// <summary>Expense categories</summary>
    public async Task<SelectList> GetExpenseCategoriesAsync(object? selectedValue = null)
    {
        var categories = await _dbContext.ExpenseCategories
            .Where(ec => ec.IsActive)
            .OrderBy(ec => ec.Name)
            .ToListAsync();
        return new SelectList(categories, "ExpenseCategoryID", "Name", selectedValue);
    }

    // ========== SYNCHRONOUS METHODS FOR RAZOR VIEWS ==========

    public SelectList GetCategories(object? selectedValue = null)
    {
        return GetCategoriesAsync(selectedValue).GetAwaiter().GetResult();
    }
    public SelectList GetUserTypes(object? selectedValue = null)
    {
        return GetUserTypesAsync(selectedValue).GetAwaiter().GetResult();
    }
    public SelectList GetSuppliers(object? selectedValue = null)
    {
        return GetSuppliersAsync(selectedValue).GetAwaiter().GetResult();
    }
    public SelectList GetProducts(object? selectedValue = null)
    {
        return GetProductsAsync(selectedValue).GetAwaiter().GetResult();
    }
    public SelectList GetPendingPurchaseOrders(object? selectedValue = null)
    {
        return GetPendingPurchaseOrdersAsync(selectedValue).GetAwaiter().GetResult();
    }
    public SelectList GetCustomers(object? selectedValue = null)
    {
        return GetCustomersAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetStoresByLoginUserID(int loginUserId, object? selectedValue = null)
    {
        return GetStoresByLoginUserIDAsync(loginUserId, selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetRootCategories(object? selectedValue = null)
    {
        return GetRootCategoriesAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetSubCategories(int parentId, object? selectedValue = null)
    {
        return GetSubCategoriesAsync(parentId, selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetAccountTypes(object? selectedValue = null)
    {
        return GetAccountTypesAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetAllAccounts(object? selectedValue = null)
    {
        return GetAllAccountsAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetHeads(object? selectedValue = null)
    {
        return GetHeadsAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetSubheads(int headId, object? selectedValue = null)
    {
        return GetSubheadsAsync(headId, selectedValue).GetAwaiter().GetResult();
    }

    // Sync wrappers for new account-specific methods
    public SelectList GetPaymentAccounts(object? selectedValue = null)
    {
        return GetPaymentAccountsAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetExpenseAccounts(object? selectedValue = null)
    {
        return GetExpenseAccountsAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetInventoryAccounts(object? selectedValue = null)
    {
        return GetInventoryAccountsAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetConsumptionAccounts(object? selectedValue = null)
    {
        return GetConsumptionAccountsAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetSaleAccounts(object? selectedValue = null)
    {
        return GetSaleAccountsAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetDamageAccounts(object? selectedValue = null)
    {
        return GetDamageAccountsAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetExpenseCategories(object? selectedValue = null)
    {
        return GetExpenseCategoriesAsync(selectedValue).GetAwaiter().GetResult();
    }

    // ========== NEW STATIC LIST METHODS ==========

    /// <summary>Get all stores</summary>
    public async Task<SelectList> GetStoresAsync(object? selectedValue = null)
    {
        var stores = await _dbContext.Stores
            .OrderBy(s => s.Name)
            .ToListAsync();
        return new SelectList(stores, "StoreID", "Name", selectedValue);
    }

    /// <summary>Get party types: Customer, Supplier, Both</summary>
    public Task<SelectList> GetPartyTypesAsync(object? selectedValue = null)
    {
        var types = new[] { "Customer", "Supplier","Both" };
        return Task.FromResult(new SelectList(types, selectedValue));
    }

    /// <summary>Get refund methods: Cash, Credit</summary>
    public Task<SelectList> GetRefundMethodsAsync(object? selectedValue = null)
    {
        var methods = new[] { "Cash", "Credit" };
        return Task.FromResult(new SelectList(methods, selectedValue));
    }

    /// <summary>Get return reasons</summary>
    public Task<SelectList> GetReturnReasonsAsync(object? selectedValue = null)
    {
        var reasons = new[] { "Defective", "WrongItem", "Expired", "ChangeOfMind", "Other" };
        return Task.FromResult(new SelectList(reasons, selectedValue));
    }

    /// <summary>Get quotation statuses</summary>
    public Task<SelectList> GetQuotationStatusesAsync(object? selectedValue = null)
    {
        var statuses = new[] { "", "Draft", "Converted", "Expired", "Cancelled" };
        return Task.FromResult(new SelectList(statuses, selectedValue));
    }

    /// <summary>Get head families: Assets, Liabilities, Capital, Income, Expense</summary>
    public Task<SelectList> GetHeadFamiliesAsync(object? selectedValue = null)
    {
        var families = new[] { "Assets", "Liabilities", "Capital", "Income", "Expense" };
        return Task.FromResult(new SelectList(families, selectedValue));
    }

    // Sync wrappers for new static list methods
    public SelectList GetStores(object? selectedValue = null)
    {
        return GetStoresAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetPartyTypes(object? selectedValue = null)
    {
        return GetPartyTypesAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetRefundMethods(object? selectedValue = null)
    {
        return GetRefundMethodsAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetReturnReasons(object? selectedValue = null)
    {
        return GetReturnReasonsAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetQuotationStatuses(object? selectedValue = null)
    {
        return GetQuotationStatusesAsync(selectedValue).GetAwaiter().GetResult();
    }

    public SelectList GetHeadFamilies(object? selectedValue = null)
    {
        return GetHeadFamiliesAsync(selectedValue).GetAwaiter().GetResult();
    }
}
