using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Builds the minimum data a tenant needs to transact. Where a real service exists it is used
/// rather than inserting rows directly, so the helpers exercise production code too — a party is
/// created through PartyService (which also creates its ledger account), stock arrives through
/// PurchaseService (which posts the real GRN voucher).
/// </summary>
public static class TenantData
{
    public const int TestUserId = TestSessionService.TestUserId;

    /// <summary>
    /// Wires a category to the accounts TenantProvisioningService seeds. Without these a sale
    /// cannot post, because SaleService refuses to guess which account revenue belongs to.
    /// </summary>
    public static async Task<(Category Category, SubCategory SubCategory)> SeedCategoryAsync(
        this TenantScope tenant, string name = "Medicines")
    {
        var db = tenant.Db;

        async Task<int> AccountId(string accountName) =>
            (await db.Accounts.FirstAsync(a => a.Name == accountName)).AccountID;

        var category = new Category
        {
            Name = name,
            StockAccount_ID = await AccountId("Inventory / Stock"),
            SaleAccount_ID = await AccountId("Sales Revenue"),
            COGSAccount_ID = await AccountId("Cost of Goods Sold"),
            DamageAccount_ID = await AccountId("Damage & Loss"),
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TestUserId
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var subCategory = new SubCategory
        {
            Name = $"{name} - General",
            Category_ID = category.CategoryID,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TestUserId
        };
        db.SubCategories.Add(subCategory);
        await db.SaveChangesAsync();

        return (category, subCategory);
    }

    public static async Task<Product> SeedProductAsync(
        this TenantScope tenant,
        Category category,
        SubCategory subCategory,
        string name = "Panadol",
        decimal openingPrice = 10m,
        int openingQuantity = 0,
        int reorderLevel = 5,
        int unitsInPack = 1)
    {
        var db = tenant.Db;
        var product = new Product
        {
            Name = name,
            Category_ID = category.CategoryID,
            SubCategory_ID = subCategory.SubCategoryID,
            OpeningPrice = openingPrice,
            OpeningQuantity = openingQuantity,
            ReorderLevel = reorderLevel,
            UnitsInPack = unitsInPack,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TestUserId
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    /// <summary>Creates a party through PartyService, so it gets a real AR/AP ledger account.</summary>
    public static Task<Party> SeedCustomerAsync(
        this TenantScope tenant, string name = "Walk-in Customer",
        decimal creditLimit = 0m, decimal openingBalance = 0m)
        => tenant.Get<IPartyService>().CreateAsync(new Party
        {
            Name = name,
            PartyType = "Customer",
            CreditLimit = creditLimit,
            OpeningBalance = openingBalance
        }, TestUserId);

    public static Task<Party> SeedSupplierAsync(
        this TenantScope tenant, string name = "Acme Distributors", decimal openingBalance = 0m)
        => tenant.Get<IPartyService>().CreateAsync(new Party
        {
            Name = name,
            PartyType = "Supplier",
            OpeningBalance = openingBalance
        }, TestUserId);

    public static async Task<Account> CashAccountAsync(this TenantScope tenant)
        => await tenant.Db.Accounts.FirstAsync(a => a.Name == "Cash in Hand");

    /// <summary>
    /// Receives stock via a real GRN, which both establishes the product's authoritative cost and
    /// posts the purchase voucher.
    /// </summary>
    public static Task<StockMain> ReceiveStockAsync(
        this TenantScope tenant, Party supplier, Product product, decimal quantity, decimal unitCost)
        => tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = supplier.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new()
                {
                    Product_ID = product.ProductID,
                    Quantity = quantity,
                    UnitPrice = unitCost,
                    CostPrice = unitCost
                }
            }
        }, TestUserId);

    /// <summary>Derived stock-on-hand for one product, straight from the production service.</summary>
    public static async Task<decimal> StockOnHandAsync(this TenantScope tenant, int productId)
    {
        var stock = await tenant.Get<IProductService>().GetStockStatusAsync(new List<int> { productId });
        return stock.TryGetValue(productId, out var qty) ? qty : 0m;
    }

    /// <summary>Sells through the real SaleService, which posts the sale and receipt vouchers.</summary>
    public static Task<StockMain> SellAsync(
        this TenantScope tenant, TenantWorld world, decimal qty, decimal unitPrice, decimal paid,
        bool overrideCreditLimit = false)
        => tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = paid,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = qty, UnitPrice = unitPrice }
            }
        }, TestUserId, world.Cash.AccountID, overrideCreditLimit);

    /// <summary>
    /// Closes the financial period that provisioning created for the current year, which is the
    /// period every test transaction falls into. Used to prove the period lock actually bites.
    /// </summary>
    public static async Task CloseCurrentPeriodAsync(this TenantScope tenant)
    {
        var db = tenant.Db;
        var period = await db.FinancialPeriods
            .FirstAsync(p => AppTime.Today >= p.StartDate && AppTime.Today <= p.EndDate);

        await tenant.Get<IFinancialPeriodService>().ClosePeriodAsync(period.PeriodID, "locked by test", TestUserId);
    }

    /// <summary>An expense category wired to a real expense account, so expenses can post.</summary>
    public static async Task<ExpenseCategory> SeedExpenseCategoryAsync(
        this TenantScope tenant, string name = "Utilities")
    {
        var db = tenant.Db;
        var expenseAccount = await db.Accounts.FirstAsync(a => a.Name == "Damage & Loss");

        var category = new ExpenseCategory
        {
            Name = name,
            DefaultExpenseAccount_ID = expenseAccount.AccountID,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TestUserId
        };
        db.Set<ExpenseCategory>().Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    /// <summary>Sums every posted, non-reversal voucher line in the tenant — the trial balance.</summary>
    public static async Task<(decimal Debit, decimal Credit)> TrialBalanceAsync(this TenantScope tenant)
    {
        var lines = await tenant.Db.VoucherDetails
            .Where(d => d.Voucher!.Status == "Posted")
            .ToListAsync();

        return (lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
    }

    /// <summary>A tenant with a category, sub-category, product, customer and supplier ready to go.</summary>
    public static async Task<TenantWorld> SeedWorldAsync(this TenantScope tenant)
    {
        var (category, subCategory) = await tenant.SeedCategoryAsync();
        var product = await tenant.SeedProductAsync(category, subCategory);
        var customer = await tenant.SeedCustomerAsync();
        var supplier = await tenant.SeedSupplierAsync();
        var cash = await tenant.CashAccountAsync();
        return new TenantWorld(category, subCategory, product, customer, supplier, cash);
    }
}

public sealed record TenantWorld(
    Category Category,
    SubCategory SubCategory,
    Product Product,
    Party Customer,
    Party Supplier,
    Account Cash);
