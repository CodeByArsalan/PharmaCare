using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Tenancy;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Security;
using PharmaCare.Domain.Entities.Tenancy;

namespace PharmaCare.Infrastructure.Implementations.Tenancy;

/// <summary>
/// Provisions a new pharmacy and seeds its complete per-tenant reference set.
/// Seeding runs inside an explicit tenant scope so every row is stamped with the new pharmacy id.
/// </summary>
public class TenantProvisioningService : ITenantProvisioningService
{
    private readonly PharmaCareDBContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWork _unitOfWork;

    public TenantProvisioningService(
        PharmaCareDBContext context,
        UserManager<User> userManager,
        ICurrentTenant currentTenant,
        IUnitOfWork unitOfWork)
    {
        _context = context;
        _userManager = userManager;
        _currentTenant = currentTenant;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProvisionResult> ProvisionAsync(ProvisionPharmacyRequest request, int actingUserId)
    {
        var code = (request.PharmacyCode ?? string.Empty).Trim();
        var email = (request.AdminEmail ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(request.PharmacyName) || string.IsNullOrWhiteSpace(code))
        {
            return Fail("Pharmacy name and code are required.");
        }
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.AdminPassword))
        {
            return Fail("Admin email and password are required.");
        }
        if (await _context.Pharmacies.AnyAsync(p => p.Code == code))
        {
            return Fail($"A pharmacy with code '{code}' already exists.");
        }
        if (await _userManager.FindByEmailAsync(email) != null)
        {
            return Fail($"A user with email '{email}' already exists.");
        }

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // 1. Create the pharmacy (NOT tenant-owned, so created outside the tenant scope).
            var pharmacy = new Pharmacy
            {
                Name = request.PharmacyName.Trim(),
                Code = code,
                Status = "Active",
                Phone = request.Phone,
                Address = request.Address,
                IsActive = true,
                CreatedAt = AppTime.Now,
                CreatedBy = actingUserId
            };
            _context.Pharmacies.Add(pharmacy);
            await _context.SaveChangesAsync();

            // 2. Everything below is stamped with the new pharmacy id.
            using (_currentTenant.BeginScope(pharmacy.PharmacyID))
            {
                await SeedChartOfAccountsAsync(actingUserId);
                SeedPriceTypesAndSettings(actingUserId);
                SeedFinancialPeriod(actingUserId);
                var adminRole = SeedAdminRoleWithAllPages(actingUserId);
                await _context.SaveChangesAsync(); // persist role + rolepages -> RoleID available

                // 3. First admin user for this pharmacy.
                var adminUser = new User
                {
                    UserName = email,
                    Email = email,
                    FullName = string.IsNullOrWhiteSpace(request.AdminFullName) ? "Administrator" : request.AdminFullName.Trim(),
                    IsActive = true,
                    Pharmacy_ID = pharmacy.PharmacyID,
                    IsPlatformAdmin = false,
                    CreatedAt = AppTime.Now,
                    CreatedBy = actingUserId
                };

                var createResult = await _userManager.CreateAsync(adminUser, request.AdminPassword);
                if (!createResult.Succeeded)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    return Fail(string.Join(" ", createResult.Errors.Select(e => e.Description)));
                }

                _context.UserRoles_Custom.Add(new UserRole
                {
                    User_ID = adminUser.Id,
                    Role_ID = adminRole.RoleID
                });
                await _context.SaveChangesAsync();
            }

            await _unitOfWork.CommitTransactionAsync();
            return new ProvisionResult { Success = true, PharmacyId = pharmacy.PharmacyID };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync();
            return Fail(ex.Message);
        }
    }

    private static ProvisionResult Fail(string message) => new() { Success = false, ErrorMessage = message };

    /// <summary>
    /// Seeds a minimal but complete chart of accounts across 5 families
    /// (Assets, Liabilities, Capital, Revenue, Expense). Codes AR_HEAD/AR_SUB/AP_HEAD/AP_SUB are
    /// resolved by PartyService; the "Discount Allowed" subhead is resolved by SaleService; the
    /// Sales/COGS/Stock/Damage accounts are assigned to categories by the pharmacy later.
    /// </summary>
    private async Task SeedChartOfAccountsAsync(int userId)
    {
        // Global account types (already seeded once) resolved by code.
        var types = await _context.AccountTypes.AsNoTracking().ToDictionaryAsync(t => t.Code, t => t.AccountTypeID, StringComparer.OrdinalIgnoreCase);
        int Type(string code) => types.TryGetValue(code, out var id) ? id : types.Values.First();

        var assets = new AccountFamily { FamilyName = "Assets" };
        var liabilities = new AccountFamily { FamilyName = "Liabilities" };
        var capital = new AccountFamily { FamilyName = "Capital" };
        var income = new AccountFamily { FamilyName = "Revenue" };
        var expenses = new AccountFamily { FamilyName = "Expense" };
        _context.AccountFamilies.AddRange(assets, liabilities, capital, income, expenses);
        await _context.SaveChangesAsync();

        var currentAssets = new AccountHead { HeadName = "Current Assets", Code = "AR_HEAD", AccountFamily_ID = assets.AccountFamilyID };
        var currentLiabilities = new AccountHead { HeadName = "Current Liabilities", Code = "AP_HEAD", AccountFamily_ID = liabilities.AccountFamilyID };
        var capitalHead = new AccountHead { HeadName = "Owner's Equity", AccountFamily_ID = capital.AccountFamilyID };
        var revenueHead = new AccountHead { HeadName = "Revenue", AccountFamily_ID = income.AccountFamilyID };
        var expenseHead = new AccountHead { HeadName = "Expenses", AccountFamily_ID = expenses.AccountFamilyID };
        _context.AccountHeads.AddRange(currentAssets, currentLiabilities, capitalHead, revenueHead, expenseHead);
        await _context.SaveChangesAsync();

        var cashBankSub = new AccountSubhead { SubheadName = "Cash & Bank", AccountHead_ID = currentAssets.AccountHeadID };
        var receivablesSub = new AccountSubhead { SubheadName = "Trade Receivables", Code = "AR_SUB", AccountHead_ID = currentAssets.AccountHeadID };
        var inventorySub = new AccountSubhead { SubheadName = "Inventory", AccountHead_ID = currentAssets.AccountHeadID };
        var payablesSub = new AccountSubhead { SubheadName = "Trade Payables", Code = "AP_SUB", AccountHead_ID = currentLiabilities.AccountHeadID };
        var capitalSub = new AccountSubhead { SubheadName = "Capital", AccountHead_ID = capitalHead.AccountHeadID };
        var salesSub = new AccountSubhead { SubheadName = "Sales Revenue", AccountHead_ID = revenueHead.AccountHeadID };
        var costOfSalesSub = new AccountSubhead { SubheadName = "Cost of Sales", AccountHead_ID = expenseHead.AccountHeadID };
        var damageSub = new AccountSubhead { SubheadName = "Damage & Loss", AccountHead_ID = expenseHead.AccountHeadID };
        var discountSub = new AccountSubhead { SubheadName = "Discount Allowed", AccountHead_ID = expenseHead.AccountHeadID };
        _context.AccountSubheads.AddRange(cashBankSub, receivablesSub, inventorySub, payablesSub, capitalSub, salesSub, costOfSalesSub, damageSub, discountSub);
        await _context.SaveChangesAsync();

        Account Acct(string name, AccountSubhead sub, AccountHead head, string typeCode) => new()
        {
            Name = name,
            AccountSubhead_ID = sub.AccountSubheadID,
            AccountHead_ID = head.AccountHeadID,
            AccountType_ID = Type(typeCode),
            IsSystemAccount = true,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = userId
        };

        // AccountType codes must match the app's existing types (the Category dropdowns filter by
        // these types): STK=Stock, COGS, SALE, DMG=Damage, GEN=General. Cash/Bank are types 1/2.
        _context.Accounts.AddRange(
            Acct("Cash in Hand", cashBankSub, currentAssets, "CASH"),
            Acct("Bank Account", cashBankSub, currentAssets, "BANK"),
            Acct("Inventory / Stock", inventorySub, currentAssets, "STK"),
            Acct("Owner's Capital", capitalSub, capitalHead, "GEN"),
            Acct("Sales Revenue", salesSub, revenueHead, "SALE"),
            Acct("Cost of Goods Sold", costOfSalesSub, expenseHead, "COGS"),
            Acct("Damage & Loss", damageSub, expenseHead, "DMG"),
            Acct("Discount Allowed", discountSub, expenseHead, "GEN"));
        await _context.SaveChangesAsync();
    }

    private void SeedPriceTypesAndSettings(int userId)
    {
        _context.PriceTypes.AddRange(
            new PriceType { PriceTypeName = "Retail", IsActive = true, CreatedAt = AppTime.Now, CreatedBy = userId },
            new PriceType { PriceTypeName = "Wholesale", IsActive = true, CreatedAt = AppTime.Now, CreatedBy = userId });

        _context.ProfitSettings.Add(new ProfitSettings
        {
            RetailProfitPercent = 20m,
            WholesaleProfitPercent = 10m,
            PriceRoundingStep = 1.00m,
            UpdatedAt = AppTime.Now,
            UpdatedBy = userId
        });
    }

    private void SeedFinancialPeriod(int userId)
    {
        var year = AppTime.Now.Year;
        _context.FinancialPeriods.Add(new FinancialPeriod
        {
            Name = $"FY {year}",
            StartDate = new DateTime(year, 1, 1),
            EndDate = new DateTime(year, 12, 31),
            IsClosed = false,
            CreatedAt = AppTime.Now,
            CreatedBy = userId
        });
    }

    private Role SeedAdminRoleWithAllPages(int userId)
    {
        var adminRole = new Role
        {
            Name = "Administrator",
            Description = "Full access to all pharmacy functions.",
            IsSystemRole = true,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = userId
        };
        _context.Roles_Custom.Add(adminRole);

        // Pages are global (the app's feature catalog). Grant the admin role everything.
        var pages = _context.Pages.AsNoTracking().Select(p => p.PageID).ToList();
        foreach (var pageId in pages)
        {
            adminRole.RolePages.Add(new RolePage
            {
                Page_ID = pageId,
                CanView = true,
                CanCreate = true,
                CanEdit = true,
                CanDelete = true
            });
        }

        return adminRole;
    }
}
