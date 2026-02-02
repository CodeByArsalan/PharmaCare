using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Infrastructure.Implementations;

public class ComboboxRepository : IComboboxRepository
{
    private readonly PharmaCareDBContext _context;

    public ComboboxRepository(PharmaCareDBContext context)
    {
        _context = context;
    }

    #region Configuration Dropdowns

    public IEnumerable<SelectListItem> GetActiveCategories(int? selectedValue = null)
    {
        return _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new SelectListItem
            {
                Value = c.CategoryID.ToString(),
                Text = c.Name,
                Selected = selectedValue.HasValue && c.CategoryID == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetActiveSubCategories(int? categoryId = null, int? selectedValue = null)
    {
        var query = _context.SubCategories
            .Where(sc => sc.IsActive);

        if (categoryId.HasValue)
        {
            query = query.Where(sc => sc.Category_ID == categoryId.Value);
        }

        return query
            .OrderBy(sc => sc.Name)
            .Select(sc => new SelectListItem
            {
                Value = sc.SubCategoryID.ToString(),
                Text = sc.Name,
                Selected = selectedValue.HasValue && sc.SubCategoryID == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetActiveProducts(int? selectedValue = null)
    {
        return _context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.ProductID.ToString(),
                Text = p.Name,
                Selected = selectedValue.HasValue && p.ProductID == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetActiveParties(int? selectedValue = null)
    {
        return _context.Parties
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.PartyID.ToString(),
                Text = p.Name,
                Selected = selectedValue.HasValue && p.PartyID == selectedValue.Value
            })
            .ToList();
    }

    #endregion

    #region Accounting Dropdowns

    public IEnumerable<SelectListItem> GetAccountFamilies(int? selectedValue = null)
    {
        return _context.AccountFamilies
            .OrderBy(f => f.FamilyName)
            .Select(f => new SelectListItem
            {
                Value = f.AccountFamilyID.ToString(),
                Text = f.FamilyName,
                Selected = selectedValue.HasValue && f.AccountFamilyID == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetActiveAccountHeads(int? selectedValue = null)
    {
        return _context.AccountHeads
            .OrderBy(h => h.HeadName)
            .Select(h => new SelectListItem
            {
                Value = h.AccountHeadID.ToString(),
                Text = h.HeadName,
                Selected = selectedValue.HasValue && h.AccountHeadID == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetActiveAccountSubHeads(int? headId = null, int? selectedValue = null)
    {
        var query = _context.AccountSubheads.AsQueryable();

        if (headId.HasValue)
        {
            query = query.Where(sh => sh.AccountHead_ID == headId.Value);
        }

        return query
            .OrderBy(sh => sh.SubheadName)
            .Select(sh => new SelectListItem
            {
                Value = sh.AccountSubheadID.ToString(),
                Text = sh.SubheadName,
                Selected = selectedValue.HasValue && sh.AccountSubheadID == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetAccountTypes(int? selectedValue = null)
    {
        return _context.AccountTypes
            .OrderBy(t => t.Name)
            .Select(t => new SelectListItem
            {
                Value = t.AccountTypeID.ToString(),
                Text = t.Name,
                Selected = selectedValue.HasValue && t.AccountTypeID == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetActiveAccounts(int? selectedValue = null)
    {
        return _context.Accounts
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new SelectListItem
            {
                Value = a.AccountID.ToString(),
                Text = a.Name,
                Selected = selectedValue.HasValue && a.AccountID == selectedValue.Value
            })
            .ToList();
    }

    #endregion

    #region Security Dropdowns

    public IEnumerable<SelectListItem> GetActiveRoles(int? selectedValue = null)
    {
        return _context.Roles_Custom
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .Select(r => new SelectListItem
            {
                Value = r.RoleID.ToString(),
                Text = r.Name,
                Selected = selectedValue.HasValue && r.RoleID == selectedValue.Value
            })
            .ToList();
    }

    #endregion
}
