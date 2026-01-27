using Microsoft.AspNetCore.Mvc.Rendering;

namespace PharmaCare.Infrastructure.Interfaces;

/// <summary>
/// Centralized repository for all dropdown/combobox data across the application.
/// Provides both async and sync methods for flexibility in controllers and views.
/// </summary>
public interface IComboBoxRepository
{
    // Async methods for controllers
    Task<SelectList> GetCategoriesAsync(object? selectedValue = null);
    Task<SelectList> GetUserTypesAsync(object? selectedValue = null);
    Task<SelectList> GetSuppliersAsync(object? selectedValue = null);
    Task<SelectList> GetProductsAsync(object? selectedValue = null);
    Task<SelectList> GetPendingPurchaseOrdersAsync(object? selectedValue = null);
    Task<SelectList> GetCustomersAsync(object? selectedValue = null);
    Task<SelectList> GetStoresByLoginUserIDAsync(int loginUserId, object? selectedValue = null);
    Task<SelectList> GetRootCategoriesAsync(object? selectedValue = null);
    Task<SelectList> GetSubCategoriesAsync(int parentId, object? selectedValue = null);
    Task<SelectList> GetAccountTypesAsync(object? selectedValue = null);
    Task<SelectList> GetAllAccountsAsync(object? selectedValue = null);
    Task<SelectList> GetHeadsAsync(object? selectedValue = null);
    Task<SelectList> GetSubheadsAsync(int headId, object? selectedValue = null);

    // Account type specific async methods
    Task<SelectList> GetPaymentAccountsAsync(object? selectedValue = null);   // Cash (1) + Bank (2)
    Task<SelectList> GetExpenseAccountsAsync(object? selectedValue = null);   // General (5)
    Task<SelectList> GetInventoryAccountsAsync(object? selectedValue = null); // Inventory (6)
    Task<SelectList> GetConsumptionAccountsAsync(object? selectedValue = null); // Consumptions (7)
    Task<SelectList> GetSaleAccountsAsync(object? selectedValue = null);      // Sale Account (8)
    Task<SelectList> GetDamageAccountsAsync(object? selectedValue = null);    // Damage Expense Stock (9)
    Task<SelectList> GetExpenseCategoriesAsync(object? selectedValue = null);

    // Synchronous methods for Razor views (asp-items)
    SelectList GetCategories(object? selectedValue = null);
    SelectList GetUserTypes(object? selectedValue = null);
    SelectList GetSuppliers(object? selectedValue = null);
    SelectList GetProducts(object? selectedValue = null);
    SelectList GetPendingPurchaseOrders(object? selectedValue = null);
    SelectList GetCustomers(object? selectedValue = null);
    SelectList GetStoresByLoginUserID(int loginUserId, object? selectedValue = null);
    SelectList GetRootCategories(object? selectedValue = null);
    SelectList GetSubCategories(int parentId, object? selectedValue = null);
    SelectList GetAccountTypes(object? selectedValue = null);
    SelectList GetAllAccounts(object? selectedValue = null);
    SelectList GetHeads(object? selectedValue = null);
    SelectList GetSubheads(int headId, object? selectedValue = null);

    // Account type specific sync methods for Razor views
    SelectList GetPaymentAccounts(object? selectedValue = null);
    SelectList GetExpenseAccounts(object? selectedValue = null);
    SelectList GetInventoryAccounts(object? selectedValue = null);
    SelectList GetConsumptionAccounts(object? selectedValue = null);
    SelectList GetSaleAccounts(object? selectedValue = null);
    SelectList GetDamageAccounts(object? selectedValue = null);
    SelectList GetExpenseCategories(object? selectedValue = null);

    // Static list dropdowns (async)
    Task<SelectList> GetStoresAsync(object? selectedValue = null);
    Task<SelectList> GetPartyTypesAsync(object? selectedValue = null);
    Task<SelectList> GetRefundMethodsAsync(object? selectedValue = null);
    Task<SelectList> GetReturnReasonsAsync(object? selectedValue = null);
    Task<SelectList> GetQuotationStatusesAsync(object? selectedValue = null);
    Task<SelectList> GetHeadFamiliesAsync(object? selectedValue = null);

    // Static list dropdowns (sync for Razor views)
    SelectList GetStores(object? selectedValue = null);
    SelectList GetPartyTypes(object? selectedValue = null);
    SelectList GetRefundMethods(object? selectedValue = null);
    SelectList GetReturnReasons(object? selectedValue = null);
    SelectList GetQuotationStatuses(object? selectedValue = null);
    SelectList GetHeadFamilies(object? selectedValue = null);
}
