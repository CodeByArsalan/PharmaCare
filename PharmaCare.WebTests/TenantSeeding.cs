using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PharmaCare.Application.DTOs.Transactions;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Infrastructure;
using PharmaCare.Web.Utilities;

namespace PharmaCare.WebTests;

/// <summary>
/// Seeds documents through the REAL application services (so the ledger stays consistent) for the
/// probes to then attack over HTTP. Runs inside a tenant-pinned DI scope.
/// </summary>
public static class TenantSeeding
{
    public sealed record SeededVoucher(int VoucherId, string EncryptedId);

    /// <summary>Creates a balanced, posted manual Journal Voucher and returns its id (raw + URL-encrypted).</summary>
    public static Task<SeededVoucher> SeedJournalVoucherAsync(this WebTestFixture fx)
        => fx.Factory.RunInTenantScopeAsync(fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var jvService = sp.GetRequiredService<IJournalVoucherService>();

            var jvType = await db.VoucherTypes.FirstAsync(t => t.Code == "JV");
            var cash = await db.Accounts.FirstAsync(a => a.Name == "Cash in Hand");
            var capital = await db.Accounts.FirstAsync(a => a.Name == "Owner's Capital");

            var dto = new JournalVoucherDto
            {
                VoucherType_ID = jvType.VoucherTypeID,
                VoucherDate = AppTime.Now,
                Narration = "Web-test seed voucher",
                TotalDebit = 100m,
                TotalCredit = 100m,
                VoucherDetails = new List<JournalVoucherDetailDto>
                {
                    new() { Account_ID = cash.AccountID, DebitAmount = 100m, CreditAmount = 0m, Description = "seed" },
                    new() { Account_ID = capital.AccountID, DebitAmount = 0m, CreditAmount = 100m, Description = "seed" }
                }
            };

            var voucher = await jvService.CreateJournalVoucherAsync(dto, TenantConstants.SeedUserId);
            return new SeededVoucher(voucher.VoucherID, Utility.EncryptId(voucher.VoucherID));
        });

    /// <summary>True if the voucher has been marked reversed (the void side-effect the RBAC probe checks).</summary>
    public static Task<bool> IsVoucherReversedAsync(this WebTestFixture fx, int voucherId)
        => fx.Factory.RunInTenantScopeAsync(fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var v = await db.Vouchers.AsNoTracking().FirstOrDefaultAsync(x => x.VoucherID == voucherId);
            return v?.IsReversed ?? false;
        });

    public sealed record SeededProduct(int ProductId, string EncryptedId, int CategoryId, int SubCategoryId, int UnitsInPack);

    /// <summary>Seeds a category (wired to the provisioned accounts), sub-category and one product.</summary>
    public static Task<SeededProduct> SeedProductAsync(this WebTestFixture fx, bool active = true)
        => fx.Factory.RunInTenantScopeAsync(fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();

            async Task<int> AccountId(string name) => (await db.Accounts.FirstAsync(a => a.Name == name)).AccountID;

            var category = new PharmaCare.Domain.Entities.Configuration.Category
            {
                Name = $"Cat-{Guid.NewGuid():N}".Substring(0, 12),
                StockAccount_ID = await AccountId("Inventory / Stock"),
                SaleAccount_ID = await AccountId("Sales Revenue"),
                COGSAccount_ID = await AccountId("Cost of Goods Sold"),
                DamageAccount_ID = await AccountId("Damage & Loss"),
                IsActive = true,
                CreatedAt = AppTime.Now,
                CreatedBy = TenantConstants.SeedUserId
            };
            db.Categories.Add(category);
            await db.SaveChangesAsync();

            var sub = new PharmaCare.Domain.Entities.Configuration.SubCategory
            {
                Name = "General",
                Category_ID = category.CategoryID,
                IsActive = true,
                CreatedAt = AppTime.Now,
                CreatedBy = TenantConstants.SeedUserId
            };
            db.SubCategories.Add(sub);
            await db.SaveChangesAsync();

            var product = new PharmaCare.Domain.Entities.Configuration.Product
            {
                Name = $"Prod-{Guid.NewGuid():N}".Substring(0, 12),
                Category_ID = category.CategoryID,
                SubCategory_ID = sub.SubCategoryID,
                OpeningPrice = 10m,
                OpeningQuantity = 0,
                ReorderLevel = 5,
                UnitsInPack = 1,
                IsActive = active,
                CreatedAt = AppTime.Now,
                CreatedBy = TenantConstants.SeedUserId
            };
            db.Products.Add(product);
            await db.SaveChangesAsync();

            return new SeededProduct(product.ProductID, Utility.EncryptId(product.ProductID),
                category.CategoryID, sub.SubCategoryID, product.UnitsInPack);
        });

    public static Task DeactivateProductAsync(this WebTestFixture fx, int productId)
        => fx.Factory.RunInTenantScopeAsync(fx.PharmacyId, async sp =>
        {
            var svc = sp.GetRequiredService<PharmaCare.Application.Interfaces.Configuration.IProductService>();
            var product = await svc.GetByIdAsync(productId);
            if (product!.IsActive)
                await svc.ToggleStatusAsync(productId, TenantConstants.SeedUserId);
        });

    public static Task<bool> IsProductActiveAsync(this WebTestFixture fx, int productId)
        => fx.Factory.RunInTenantScopeAsync(fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var p = await db.Products.AsNoTracking().FirstAsync(x => x.ProductID == productId);
            return p.IsActive;
        });

    public sealed record SeededPurchase(int StockMainId, string EncryptedId, int PartyId, int ProductId, string RowVersionBase64);

    /// <summary>Receives stock via a real GRN (unpaid, so it stays editable) and returns its identity + RowVersion.</summary>
    public static Task<SeededPurchase> SeedPurchaseAsync(this WebTestFixture fx)
        => fx.Factory.RunInTenantScopeAsync(fx.PharmacyId, async sp =>
        {
            var product = await fx.SeedProductInScopeAsync(sp);
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var purchaseService = sp.GetRequiredService<IPurchaseService>();
            var partyService = sp.GetRequiredService<PharmaCare.Application.Interfaces.Configuration.IPartyService>();

            var supplier = await partyService.CreateAsync(new PharmaCare.Domain.Entities.Configuration.Party
            {
                Name = $"Sup-{Guid.NewGuid():N}".Substring(0, 12),
                PartyType = "Supplier",
                OpeningBalance = 0m
            }, TenantConstants.SeedUserId);

            var grn = await purchaseService.CreateAsync(new StockMain
            {
                Party_ID = supplier.PartyID,
                TransactionDate = AppTime.Now,
                PaidAmount = 0,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = product.ProductID, Quantity = 10, UnitPrice = 5m, CostPrice = 5m }
                }
            }, TenantConstants.SeedUserId);

            var reloaded = await db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == grn.StockMainID);
            var rv = Convert.ToBase64String(reloaded.RowVersion ?? Array.Empty<byte>());
            return new SeededPurchase(grn.StockMainID, Utility.EncryptId(grn.StockMainID),
                supplier.PartyID, product.ProductID, rv);
        });

    public static Task<int> CashAccountIdAsync(this WebTestFixture fx)
        => fx.Factory.RunInTenantScopeAsync(fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            return (await db.Accounts.FirstAsync(a => a.Name == "Cash in Hand")).AccountID;
        });

    public static Task<int> SeedCustomerAsync(this WebTestFixture fx)
        => fx.Factory.RunInTenantScopeAsync(fx.PharmacyId, async sp =>
        {
            var partyService = sp.GetRequiredService<PharmaCare.Application.Interfaces.Configuration.IPartyService>();
            var customer = await partyService.CreateAsync(new PharmaCare.Domain.Entities.Configuration.Party
            {
                Name = $"Cust-{Guid.NewGuid():N}".Substring(0, 12),
                PartyType = "Customer",
                CreditLimit = 0m,
                OpeningBalance = 0m
            }, TenantConstants.SeedUserId);
            return customer.PartyID;
        });

    /// <summary>Creates a sale through the real SaleService (no HTTP, so no TempData set) and returns
    /// its URL-encrypted id.</summary>
    public static Task<string> SeedSaleViaServiceAsync(
        this WebTestFixture fx, int customerId, int productId, int cashAccountId,
        decimal qty, decimal unitPrice, decimal paid)
        => fx.Factory.RunInTenantScopeAsync(fx.PharmacyId, async sp =>
        {
            var saleService = sp.GetRequiredService<ISaleService>();
            var sale = await saleService.CreateAsync(new StockMain
            {
                Party_ID = customerId,
                TransactionDate = AppTime.Now,
                PaidAmount = paid,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = productId, Quantity = qty, UnitPrice = unitPrice }
                }
            }, TenantConstants.SeedUserId, cashAccountId, overrideCreditLimit: true);
            return Utility.EncryptId(sale.StockMainID);
        });

    public static Task<string?> GetPurchaseRemarksAsync(this WebTestFixture fx, int stockMainId)
        => fx.Factory.RunInTenantScopeAsync(fx.PharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var s = await db.StockMains.AsNoTracking().FirstAsync(x => x.StockMainID == stockMainId);
            return s.Remarks;
        });

    public sealed record SecondTenant(int PharmacyId, int AdminRoleId, int SamplePageId, string AdminEmail, string AdminPassword);

    /// <summary>Provisions a SECOND, isolated pharmacy and returns its admin role + a page id, for
    /// the cross-tenant SavePermissions probe.</summary>
    public static async Task<SecondTenant> ProvisionSecondTenantAsync(this WebTestFixture fx)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"admin-b-{suffix}@webtest.local";
        const string password = "TenantBPass123";

        var pharmacyId = await fx.Factory.RunInTenantScopeAsync(0, async sp =>
        {
            var provisioning = sp.GetRequiredService<PharmaCare.Application.Interfaces.Tenancy.ITenantProvisioningService>();
            var result = await provisioning.ProvisionAsync(new PharmaCare.Application.Interfaces.Tenancy.ProvisionPharmacyRequest
            {
                PharmacyName = $"Tenant B {suffix}",
                PharmacyCode = $"B{suffix}",
                AdminEmail = email,
                AdminPassword = password,
                AdminFullName = "Tenant B Admin"
            }, actingUserId: 0);
            if (!result.Success) throw new InvalidOperationException($"Tenant B provisioning failed: {result.ErrorMessage}");
            return result.PharmacyId;
        });

        return await fx.Factory.RunInTenantScopeAsync(pharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            var role = await db.Roles_Custom.AsNoTracking().FirstAsync(r => r.Name == "Administrator");
            var pageId = await db.Pages.AsNoTracking().Select(p => p.PageID).FirstAsync();
            return new SecondTenant(pharmacyId, role.RoleID, pageId, email, password);
        });
    }

    /// <summary>Distinct Pharmacy_IDs that own a RolePage for <paramref name="roleId"/> (filters OFF).</summary>
    public static Task<List<int>> RolePageOwnerPharmaciesAsync(this WebTestFixture fx, int anyScopePharmacyId, int roleId)
        => fx.Factory.RunInTenantScopeAsync(anyScopePharmacyId, async sp =>
        {
            var db = sp.GetRequiredService<PharmaCareDBContext>();
            return await db.RolePages.IgnoreQueryFilters()
                .Where(rp => rp.Role_ID == roleId)
                .Select(rp => rp.Pharmacy_ID)
                .Distinct()
                .ToListAsync();
        });

    // Internal: seed a product inside an existing scope (used by SeedPurchaseAsync).
    private static async Task<PharmaCare.Domain.Entities.Configuration.Product> SeedProductInScopeAsync(
        this WebTestFixture fx, IServiceProvider sp)
    {
        var db = sp.GetRequiredService<PharmaCareDBContext>();
        async Task<int> AccountId(string name) => (await db.Accounts.FirstAsync(a => a.Name == name)).AccountID;

        var category = new PharmaCare.Domain.Entities.Configuration.Category
        {
            Name = $"Cat-{Guid.NewGuid():N}".Substring(0, 12),
            StockAccount_ID = await AccountId("Inventory / Stock"),
            SaleAccount_ID = await AccountId("Sales Revenue"),
            COGSAccount_ID = await AccountId("Cost of Goods Sold"),
            DamageAccount_ID = await AccountId("Damage & Loss"),
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TenantConstants.SeedUserId
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var sub = new PharmaCare.Domain.Entities.Configuration.SubCategory
        {
            Name = "General",
            Category_ID = category.CategoryID,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TenantConstants.SeedUserId
        };
        db.SubCategories.Add(sub);
        await db.SaveChangesAsync();

        var product = new PharmaCare.Domain.Entities.Configuration.Product
        {
            Name = $"Prod-{Guid.NewGuid():N}".Substring(0, 12),
            Category_ID = category.CategoryID,
            SubCategory_ID = sub.SubCategoryID,
            OpeningPrice = 10m,
            OpeningQuantity = 0,
            ReorderLevel = 5,
            UnitsInPack = 1,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TenantConstants.SeedUserId
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}

public static class TenantConstants
{
    public const int SeedUserId = 0;
}
