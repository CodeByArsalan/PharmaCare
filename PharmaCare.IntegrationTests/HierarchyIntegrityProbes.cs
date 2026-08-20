using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Probes the PARENT levels of the two hierarchies the application classifies everything by:
/// AccountFamily -> AccountHead -> AccountSubhead -> Account, and Category -> SubCategory -> Product.
///
/// <para>
/// The previous sweep froze classification on the LEAF of the accounting tree: an Account's type,
/// head and subhead are now immutable once it has posted. The three levels above it were left
/// exactly as they were. <c>AccountHeadService.UpdateAsync</c> and
/// <c>AccountSubHeadService.UpdateAsync</c> are eight lines each — assign the name, assign the
/// parent id, save — with no ownership check, no in-use check and no validation of the parent id
/// they are handed. Re-parenting a head moves every account beneath it into a different section of
/// the balance sheet without touching a single account row.
/// </para>
///
/// <para>
/// The second theme is cross-tenant referential integrity. Every foreign key in this schema is a
/// single column; none of them is composite with Pharmacy_ID. The tenant filter hides the parent
/// row from queries but the database will still accept the reference, so any service that writes a
/// caller-supplied parent id without checking it can hang one pharmacy's row off another's.
/// </para>
///
/// <para>Each test asserts the CORRECT behaviour, so a failing test is a confirmed defect.</para>
/// </summary>
[Collection(Collections.Database)]
public class HierarchyIntegrityProbes
{
    private readonly DatabaseFixture _fixture;

    public HierarchyIntegrityProbes(DatabaseFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------------------------------------
    // Re-parenting a live branch of the chart of accounts.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_account_head_holding_posted_accounts_cannot_be_moved_to_another_family()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        // Trade, so the receivables branch of the chart of accounts carries a real balance.
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, 10m);
        await tenant.SellAsync(world, qty: 4, unitPrice: 25m, paid: 0m);

        var arHead = await tenant.Db.AccountHeads.AsNoTracking().FirstAsync(h => h.Code == "AR_HEAD");
        var originalFamily = arHead.AccountFamily_ID;
        var expenseFamily = await tenant.Db.AccountFamilies.AsNoTracking()
            .Where(f => f.FamilyName == "Expense").Select(f => f.AccountFamilyID).FirstAsync();

        // Refusal-by-exception is a legitimate outcome; the stored value not moving is the invariant.
        await Record.ExceptionAsync(() => tenant.Get<IAccountHeadService>().UpdateAsync(new AccountHead
        {
            AccountHeadID = arHead.AccountHeadID,
            HeadName = arHead.HeadName,
            AccountFamily_ID = expenseFamily
        }));

        var storedFamily = await tenant.Db.AccountHeads.AsNoTracking()
            .Where(h => h.AccountHeadID == arHead.AccountHeadID)
            .Select(h => h.AccountFamily_ID)
            .FirstAsync();

        var strandedReceivable = await tenant.Db.VoucherDetails.AsNoTracking()
            .Where(d => d.Voucher!.Status == "Posted" && d.Account!.AccountHead_ID == arHead.AccountHeadID)
            .SumAsync(d => d.DebitAmount - d.CreditAmount);

        Assert.True(storedFamily == originalFamily,
            $"The head holding every customer receivable was re-classified from family {originalFamily} " +
            $"to {storedFamily} in one edit, carrying {strandedReceivable:N2} of live asset balance with " +
            "it. No account row changed, so nothing in the chart-of-accounts screen shows what moved.");
    }

    [Fact]
    public async Task An_account_subhead_holding_posted_accounts_cannot_be_moved_to_another_head()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, 10m);
        await tenant.SellAsync(world, qty: 4, unitPrice: 25m, paid: 0m);

        var arSub = await tenant.Db.AccountSubheads.AsNoTracking().FirstAsync(s => s.Code == "AR_SUB");
        var originalHead = arSub.AccountHead_ID;
        var payablesHead = await tenant.Db.AccountHeads.AsNoTracking()
            .Where(h => h.Code == "AP_HEAD").Select(h => h.AccountHeadID).FirstAsync();

        await Record.ExceptionAsync(() => tenant.Get<IAccountSubHeadService>().UpdateAsync(new AccountSubhead
        {
            AccountSubheadID = arSub.AccountSubheadID,
            SubheadName = arSub.SubheadName,
            AccountHead_ID = payablesHead
        }));

        var storedHead = await tenant.Db.AccountSubheads.AsNoTracking()
            .Where(s => s.AccountSubheadID == arSub.AccountSubheadID)
            .Select(s => s.AccountHead_ID)
            .FirstAsync();

        Assert.True(storedHead == originalHead,
            "The Accounts Receivable subhead was re-parented under Current Liabilities while it held " +
            "posted customer balances. Every account beneath it now reports on the opposite side of " +
            "the balance sheet.");
    }

    [Fact]
    public async Task An_account_head_cannot_be_parented_to_another_pharmacys_family()
    {
        using var first = await _fixture.NewTenantAsync();
        using var second = await _fixture.NewTenantAsync();

        var foreignFamily = await second.Db.AccountFamilies.AsNoTracking()
            .Where(f => f.FamilyName == "Assets").Select(f => f.AccountFamilyID).FirstAsync();

        AccountHead? created = null;
        try
        {
            created = await first.Get<IAccountHeadService>().CreateAsync(new AccountHead
            {
                HeadName = "Borrowed Classification",
                AccountFamily_ID = foreignFamily
            });
        }
        catch (Exception)
        {
            return; // Refusing the foreign parent is the correct outcome.
        }

        // The tenant filter hides the parent, so the head is orphaned from its owner's point of
        // view: every screen that renders its family shows a blank, and the chart of accounts has
        // a branch pointing outside the pharmacy.
        var parentIsVisible = await first.Db.AccountFamilies.AsNoTracking()
            .AnyAsync(f => f.AccountFamilyID == created.AccountFamily_ID);

        Assert.True(parentIsVisible,
            "An account head was created under ANOTHER pharmacy's account family. The foreign key is " +
            "a bare AccountFamily_ID with no Pharmacy_ID component, so the database accepts it and " +
            "the tenant filter then hides the parent, leaving an unresolvable branch.");
    }

    [Fact]
    public async Task An_account_subhead_cannot_be_parented_to_another_pharmacys_head()
    {
        using var first = await _fixture.NewTenantAsync();
        using var second = await _fixture.NewTenantAsync();

        var foreignHead = await second.Db.AccountHeads.AsNoTracking()
            .Where(h => h.Code == "AR_HEAD").Select(h => h.AccountHeadID).FirstAsync();

        AccountSubhead? created = null;
        try
        {
            created = await first.Get<IAccountSubHeadService>().CreateAsync(new AccountSubhead
            {
                SubheadName = "Borrowed Subhead",
                AccountHead_ID = foreignHead
            });
        }
        catch (Exception)
        {
            return;
        }

        var parentIsVisible = await first.Db.AccountHeads.AsNoTracking()
            .AnyAsync(h => h.AccountHeadID == created.AccountHead_ID);

        Assert.True(parentIsVisible,
            "An account subhead was created under ANOTHER pharmacy's account head. The subhead is " +
            "stamped with this pharmacy but its parent is not, so the two can never be read back " +
            "together — a required navigation across the tenant boundary drops the row entirely.");
    }

    [Fact]
    public async Task Deleting_an_account_head_that_still_owns_accounts_fails_with_a_readable_error()
    {
        using var tenant = await _fixture.NewTenantAsync();

        var arHead = await tenant.Db.AccountHeads.AsNoTracking().FirstAsync(h => h.Code == "AR_HEAD");

        var error = await Record.ExceptionAsync(
            () => tenant.Get<IAccountHeadService>().DeleteAsync(arHead.AccountHeadID));

        // The referential constraint does stop the delete — but only the application's own
        // validation exceptions are surfaced to the user (BaseController.SafeErrorMessage), so a
        // raw DbUpdateException becomes a generic "unexpected error" with no cause given.
        Assert.True(error is null or InvalidOperationException or ArgumentException,
            $"Hard-deleting an account head in use surfaced as {error?.GetType().Name}, which the " +
            "controller replaces with a generic message. The user is told nothing about why the " +
            "delete failed.");

        var survived = await tenant.Db.AccountHeads.AsNoTracking()
            .AnyAsync(h => h.AccountHeadID == arHead.AccountHeadID);
        Assert.True(survived);
    }

    // ------------------------------------------------------------------------------------------
    // The product hierarchy.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_subcategory_cannot_be_parented_to_another_pharmacys_category()
    {
        using var first = await _fixture.NewTenantAsync();
        using var second = await _fixture.NewTenantAsync();

        var (foreignCategory, _) = await second.SeedCategoryAsync("Cosmetics");

        SubCategory? created = null;
        try
        {
            created = await first.Get<ISubCategoryService>().CreateAsync(new SubCategory
            {
                Name = "Borrowed Group",
                Category_ID = foreignCategory.CategoryID
            }, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        var parentIsVisible = await first.Db.Categories.AsNoTracking()
            .AnyAsync(c => c.CategoryID == created.Category_ID);

        // Category_ID is a required navigation, so a query that includes the parent becomes an
        // INNER JOIN — and the tenant filter then removes the CHILD from its own owner's results.
        var readableWithItsParent = await first.Db.SubCategories.AsNoTracking()
            .Include(s => s.Category)
            .AnyAsync(s => s.SubCategoryID == created.SubCategoryID);

        Assert.True(parentIsVisible && readableWithItsParent,
            "A sub-category was created under ANOTHER pharmacy's category (parent visible to its " +
            $"owner: {parentIsVisible}; row still listable: {readableWithItsParent}). Category_ID is " +
            "a bare foreign key with no Pharmacy_ID component, so the database accepts the reference " +
            "and the tenant filter then hides the parent — SubCategoryService.GetAllAsync includes " +
            "the category, so the row renders with a blank category it can never be re-assigned from, " +
            "and any product filed under it inherits a classification its own pharmacy cannot see.");
    }

    [Fact]
    public async Task A_subcategory_that_products_already_use_cannot_be_moved_to_another_category()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, 10m);

        var (otherCategory, _) = await tenant.SeedCategoryAsync("Toiletries");

        await Record.ExceptionAsync(() => tenant.Get<ISubCategoryService>().UpdateAsync(new SubCategory
        {
            SubCategoryID = world.SubCategory.SubCategoryID,
            Name = world.SubCategory.Name,
            Category_ID = otherCategory.CategoryID,
            IsActive = true
        }, TenantData.TestUserId));

        var storedCategory = await tenant.Db.SubCategories.AsNoTracking()
            .Where(s => s.SubCategoryID == world.SubCategory.SubCategoryID)
            .Select(s => s.Category_ID)
            .FirstAsync();

        var productCategory = await tenant.Db.Products.AsNoTracking()
            .Where(p => p.ProductID == world.Product.ProductID)
            .Select(p => p.Category_ID)
            .FirstAsync();

        Assert.True(storedCategory == productCategory,
            $"The sub-category moved to category {storedCategory} while its product stayed in " +
            $"{productCategory}. The product's Category_ID is what decides which stock, sales and " +
            "COGS accounts it posts to, and it is now inconsistent with its own sub-category — the " +
            "same stranding the previous sweep closed on the product itself.");
    }

    [Fact]
    public async Task A_product_cannot_be_filed_under_a_subcategory_from_a_different_category()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (categoryA, subCategoryA) = await tenant.SeedCategoryAsync("Medicines");
        var (_, subCategoryB) = await tenant.SeedCategoryAsync("Toiletries");

        Product? created = null;
        try
        {
            created = await tenant.Get<IProductService>().CreateAsync(new Product
            {
                Name = "Mismatched Item",
                Category_ID = categoryA.CategoryID,
                SubCategory_ID = subCategoryB.SubCategoryID, // belongs to Toiletries, not Medicines
                OpeningPrice = 10m,
                OpeningQuantity = 0,
                ReorderLevel = 1,
                UnitsInPack = 1
            }, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        var stored = await tenant.Db.Products.AsNoTracking()
            .Where(p => p.ProductID == created.ProductID)
            .Select(p => new { p.Category_ID, SubCategoryParent = p.SubCategory!.Category_ID })
            .FirstAsync();

        Assert.True(stored.Category_ID == stored.SubCategoryParent,
            $"A product is filed under category {stored.Category_ID} but its sub-category belongs to " +
            $"category {stored.SubCategoryParent}. Filtering the catalogue by category and then by " +
            "sub-category returns nothing, and the two answers to \"what kind of product is this\" " +
            $"disagree. The category's own sub-category ({subCategoryA.SubCategoryID}) was never used.");
    }

    [Fact]
    public async Task A_subcategory_still_used_by_active_products_cannot_be_deactivated()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        await Record.ExceptionAsync(() => tenant.Get<ISubCategoryService>().ToggleStatusAsync(
            world.SubCategory.SubCategoryID, TenantData.TestUserId));

        var subActive = await tenant.Db.SubCategories.AsNoTracking()
            .Where(s => s.SubCategoryID == world.SubCategory.SubCategoryID)
            .Select(s => s.IsActive)
            .FirstAsync();

        var productActive = await tenant.Db.Products.AsNoTracking()
            .Where(p => p.ProductID == world.Product.ProductID)
            .Select(p => p.IsActive)
            .FirstAsync();

        Assert.False(productActive && !subActive,
            "A sub-category was deactivated while an ACTIVE product still belongs to it. The product " +
            "stays sellable, but editing it can no longer re-select its own sub-category — the " +
            "dropdown filters on IsActive — so the next save silently reassigns or blanks it.");
    }

    [Fact]
    public async Task Two_categories_in_one_pharmacy_cannot_share_a_name()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var categories = tenant.Get<ICategoryService>();

        async Task<int> AccountId(string name) => await tenant.Db.Accounts.AsNoTracking()
            .Where(a => a.Name == name).Select(a => a.AccountID).FirstAsync();

        var stock = await AccountId("Inventory / Stock");
        var sales = await AccountId("Sales Revenue");
        var cogs = await AccountId("Cost of Goods Sold");
        var damage = await AccountId("Damage & Loss");

        Category Fresh() => new()
        {
            Name = "Medicines",
            StockAccount_ID = stock,
            SaleAccount_ID = sales,
            COGSAccount_ID = cogs,
            DamageAccount_ID = damage,
            IsActive = true
        };

        await categories.CreateAsync(Fresh(), TenantData.TestUserId);

        try
        {
            await categories.CreateAsync(Fresh(), TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        var count = await tenant.Db.Categories.AsNoTracking().CountAsync(c => c.Name == "Medicines");

        Assert.Equal(1, count);
    }
}
