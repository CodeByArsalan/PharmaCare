using Microsoft.AspNetCore.Mvc.Rendering;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Infrastructure.Implementations;

public class ComboboxRepository : IComboboxRepository
{
    private readonly PharmaCareDBContext _context;
    private readonly LogDbContext _logContext;

    public ComboboxRepository(PharmaCareDBContext context, LogDbContext logContext)
    {
        _context = context;
        _logContext = logContext;
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

    public IEnumerable<SelectListItem> GetActivePartiesByType(string partyType, int? selectedValue = null)
    {
        return _context.Parties
            .Where(p => p.IsActive && p.PartyType == partyType)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.PartyID.ToString(),
                Text = p.Name,
                Selected = selectedValue.HasValue && p.PartyID == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetPriceTypes(int? selectedValue = null)
    {
        return _context.PriceTypes
            .OrderBy(pt => pt.PriceTypeName)
            .Select(pt => new SelectListItem
            {
                Value = pt.PriceTypeID.ToString(),
                Text = pt.PriceTypeName,
                Selected = selectedValue.HasValue && pt.PriceTypeID == selectedValue.Value
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

    public IEnumerable<SelectListItem> GetAccountsByType(int accountTypeId, int? selectedValue = null)
    {
        return _context.Accounts
            .Where(a => a.IsActive && a.AccountType_ID == accountTypeId)
            .OrderBy(a => a.Name)
            .Select(a => new SelectListItem
            {
                Value = a.AccountID.ToString(),
                Text = a.Name,
                Selected = selectedValue.HasValue && a.AccountID == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetCashBankAccounts(int? selectedValue = null)
    {
        // Assuming Cash (ID 1) and Bank (ID 2) types, or specific logic?
        // Let's replicate AccountService logic: usually AccountType 1 and 2 or strictly defined
        // "Cash" and "Bank" account types.
        // Based on PurchaseController logic: _accountService.GetCashBankAccountsAsync()
        // We'll trust account service names or types. Let's assume Type 1 & 2 for now or fetch by name.
        // Actually, let's look at AccountService... for now, strict replacement:
        // Cash = 10, Bank = 11 (Common IDs). 
        // Let's use a broader query or just fetch all active accounts for now if unsure, 
        // BUT strict is better. I'll rely on AccountType names "Cash" and "Bank".
        
        var typeIds = _context.AccountTypes
            .Where(t => t.Name == "Cash" || t.Name == "Bank")
            .Select(t => t.AccountTypeID)
            .ToList();

        return _context.Accounts
            .Where(a => a.IsActive && typeIds.Contains(a.AccountType_ID))
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

    #region Transaction Dropdowns

    public IEnumerable<SelectListItem> GetManualVoucherTypes(int? selectedValue = null)
    {
        return _context.VoucherTypes
            .Where(t => t.IsActive && !t.IsAutoGenerated)
            .OrderBy(t => t.Name)
            .Select(t => new SelectListItem
            {
                Value = t.VoucherTypeID.ToString(),
                Text = t.Name,
                Selected = selectedValue.HasValue && t.VoucherTypeID == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetActivityTypes(int? selectedValue = null)
    {
        return Enum.GetValues<Domain.Enums.ActivityType>()
            .Select(t => new SelectListItem
            {
                Value = ((int)t).ToString(),
                Text = t.ToString(),
                Selected = selectedValue.HasValue && (int)t == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetEntityNamesForLog(string? selectedValue = null)
    {
        // This might be heavy if table is huge, but it's for filter dropdown
        return _logContext.ActivityLogs
            .Select(l => l.EntityName)
            .Distinct()
            .OrderBy(n => n)
            .Select(n => new SelectListItem
            {
                Value = n,
                Text = n,
                Selected = selectedValue == n
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetPaymentMethods(string? selectedValue = null)
    {
        var methods = new[] { "Cash", "Bank", "Cheque" };
        return methods.Select(m => new SelectListItem
        {
            Value = m,
            Text = m,
            Selected = selectedValue == m
        }).ToList();
    }

    public IEnumerable<SelectListItem> GetPaymentStatuses(string? selectedValue = null)
    {
        var statuses = new[] { "All", "Paid", "Partial", "Unpaid" };
        return statuses.Select(s => new SelectListItem
        {
            Value = s,
            Text = s,
            Selected = selectedValue == s
        }).ToList();
    }

    public IEnumerable<SelectListItem> GetSales(int? selectedValue = null)
    {
        return _context.StockMains
            .Where(s => s.TransactionType.Code == "SALE")
            .OrderByDescending(s => s.TransactionDate)
            .Select(s => new SelectListItem
            {
                Value = s.StockMainID.ToString(),
                Text = $"{s.TransactionNo} ({s.TransactionDate:dd/MM/yyyy})",
                Selected = selectedValue.HasValue && s.StockMainID == selectedValue.Value
            })
            .ToList();
    }

    public IEnumerable<SelectListItem> GetPurchases(int? selectedValue = null)
    {
        return _context.StockMains
            .Where(s => s.TransactionType.Code == "GRN")
            .OrderByDescending(s => s.TransactionDate)
            .Select(s => new SelectListItem
            {
                Value = s.StockMainID.ToString(),
                Text = $"{s.TransactionNo} ({s.TransactionDate:dd/MM/yyyy})",
                Selected = selectedValue.HasValue && s.StockMainID == selectedValue.Value
            })
            .ToList();
    }

    #endregion
}
