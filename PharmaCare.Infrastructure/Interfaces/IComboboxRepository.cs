using Microsoft.AspNetCore.Mvc.Rendering;

namespace PharmaCare.Infrastructure.Interfaces;

/// <summary>
/// Centralized repository for all dropdown/combobox data.
/// Inject directly in Razor views with @inject.
/// </summary>
public interface IComboboxRepository
{
    #region Configuration Dropdowns

    /// <summary>
    /// Gets active stores for dropdown.
    /// </summary>
    IEnumerable<SelectListItem> GetActiveStores(int? selectedValue = null);

    /// <summary>
    /// Gets active categories for dropdown.
    /// </summary>
    IEnumerable<SelectListItem> GetActiveCategories(int? selectedValue = null);

    /// <summary>
    /// Gets active subcategories for dropdown, optionally filtered by category.
    /// </summary>
    IEnumerable<SelectListItem> GetActiveSubCategories(int? categoryId = null, int? selectedValue = null);

    /// <summary>
    /// Gets active products for dropdown.
    /// </summary>
    IEnumerable<SelectListItem> GetActiveProducts(int? selectedValue = null);

    /// <summary>
    /// Gets active parties (suppliers/customers) for dropdown.
    /// </summary>
    IEnumerable<SelectListItem> GetActiveParties(int? selectedValue = null);

    #endregion

    #region Accounting Dropdowns

    /// <summary>
    /// Gets all account families for dropdown.
    /// </summary>
    IEnumerable<SelectListItem> GetAccountFamilies(int? selectedValue = null);

    /// <summary>
    /// Gets active account heads for dropdown.
    /// </summary>
    IEnumerable<SelectListItem> GetActiveAccountHeads(int? selectedValue = null);

    /// <summary>
    /// Gets active account subheads for dropdown, optionally filtered by head.
    /// </summary>
    IEnumerable<SelectListItem> GetActiveAccountSubHeads(int? headId = null, int? selectedValue = null);

    /// <summary>
    /// Gets all account types for dropdown.
    /// </summary>
    IEnumerable<SelectListItem> GetAccountTypes(int? selectedValue = null);

    /// <summary>
    /// Gets active chart of accounts for dropdown.
    /// </summary>
    IEnumerable<SelectListItem> GetActiveAccounts(int? selectedValue = null);

    #endregion

    #region Security Dropdowns

    /// <summary>
    /// Gets active roles for dropdown.
    /// </summary>
    IEnumerable<SelectListItem> GetActiveRoles(int? selectedValue = null);

    #endregion
}
