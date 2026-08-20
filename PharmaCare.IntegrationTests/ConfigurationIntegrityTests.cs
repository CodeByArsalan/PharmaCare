using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Configuration;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Master-data and chart-of-accounts probes.
///
/// <para>
/// Every earlier sweep attacked the TRANSACTION services. This one attacks the configuration
/// underneath them — products, categories, parties and ledger accounts — because a transaction
/// service can only be as correct as the master data it posts against. The recurring shape here is
/// a field that is perfectly safe to set at CREATE time and quietly destructive to change once the
/// row has traded: opening stock, the category that decides which accounts a product posts to, the
/// type of a party, the type of a ledger account.
/// </para>
///
/// <para>
/// Each test asserts the CORRECT behaviour, so a failing test is a confirmed defect.
/// </para>
/// </summary>
[Collection(Collections.Database)]
public class ConfigurationIntegrityTests
{
    private readonly DatabaseFixture _fixture;

    public ConfigurationIntegrityTests(DatabaseFixture fixture) => _fixture = fixture;

    // ------------------------------------------------------------------------------------------
    // Party type / status gates on the purchase side.
    //
    // SaleService resolves its counterparty through a filtered query that demands the party be
    // active, be a Customer/Both, and have an ACTIVE ledger account (SaleService.cs:638-652).
    // PurchaseService resolves its supplier with a bare `FirstOrDefaultAsync(p => p.PartyID == ...)`
    // and only checks that Account_ID is non-null (PurchaseService.cs:683-688). These probes pin
    // that asymmetry.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_goods_receipt_cannot_be_booked_against_a_customer()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (category, subCategory) = await tenant.SeedCategoryAsync();
        var product = await tenant.SeedProductAsync(category, subCategory);

        // A pure Customer: its ledger account lives under Accounts Receivable, an ASSET.
        var customer = await tenant.SeedCustomerAsync("Retail Buyer");

        try
        {
            await tenant.Get<IPurchaseService>().CreateAsync(new StockMain
            {
                Party_ID = customer.PartyID,
                TransactionDate = AppTime.Now,
                PaidAmount = 0,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = product.ProductID, Quantity = 5, UnitPrice = 10m, CostPrice = 10m }
                }
            }, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return; // Refusing the receipt is the correct outcome.
        }

        // It was accepted. Show the damage: the purchase voucher CREDITS the counterparty, so a
        // receivable — a debit-balance asset — has been pushed negative, and the money owed to
        // this "supplier" is nowhere in payables because every payables query filters on PartyType.
        var arAccountId = (await tenant.Db.Parties.AsNoTracking()
            .FirstAsync(p => p.PartyID == customer.PartyID)).Account_ID;

        var arBalance = await tenant.Db.VoucherDetails.AsNoTracking()
            .Where(d => d.Account_ID == arAccountId && d.Voucher!.Status == "Posted")
            .SumAsync(d => d.DebitAmount - d.CreditAmount);

        Assert.True(arBalance >= 0m,
            $"A goods receipt was booked against a CUSTOMER. Its receivable account now stands at " +
            $"{arBalance:N2} — a liability parked inside an asset, invisible to every payables report.");
    }

    [Fact]
    public async Task A_goods_receipt_cannot_be_booked_against_a_deactivated_supplier()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (category, subCategory) = await tenant.SeedCategoryAsync();
        var product = await tenant.SeedProductAsync(category, subCategory);
        var supplier = await tenant.SeedSupplierAsync();

        await tenant.Get<IPartyService>().ToggleStatusAsync(supplier.PartyID, TenantData.TestUserId);

        var act = () => tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = supplier.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = product.ProductID, Quantity = 5, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        await Assert.ThrowsAnyAsync<Exception>(act);
    }

    [Fact]
    public async Task A_goods_receipt_cannot_post_to_a_deactivated_ledger_account()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (category, subCategory) = await tenant.SeedCategoryAsync();
        var product = await tenant.SeedProductAsync(category, subCategory);
        var supplier = await tenant.SeedSupplierAsync();

        // Deactivate the supplier's own payable account, leaving the party itself active.
        // Written straight to the row on purpose: AccountService now refuses this, but databases
        // that pre-date that guard still contain such rows, so the POSTING path must defend itself
        // rather than trust that nothing upstream ever let it happen.
        var accountId = supplier.Account_ID!.Value;
        var account = await tenant.Db.Accounts.FirstAsync(a => a.AccountID == accountId);
        account.IsActive = false;
        await tenant.Db.SaveChangesAsync();

        var act = () => tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = supplier.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = product.ProductID, Quantity = 5, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        await Assert.ThrowsAnyAsync<Exception>(act);
    }

    [Fact]
    public async Task A_purchase_order_cannot_be_raised_against_a_customer()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (category, subCategory) = await tenant.SeedCategoryAsync();
        var product = await tenant.SeedProductAsync(category, subCategory);
        var customer = await tenant.SeedCustomerAsync("Retail Buyer");

        var act = () => tenant.Get<IPurchaseOrderService>().CreateAsync(new StockMain
        {
            Party_ID = customer.PartyID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = product.ProductID, Quantity = 5, UnitPrice = 10m, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        await Assert.ThrowsAnyAsync<Exception>(act);
    }

    [Fact]
    public async Task A_party_with_an_unrecognised_type_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();

        // "Vendor" matches neither the supplier branch nor the customer branch in
        // PartyService.CreateCoreAsync, so the party is created with NO ledger account at all —
        // a counterparty that can never post. It should be refused outright.
        Party created;
        try
        {
            created = await tenant.Get<IPartyService>().CreateAsync(new Party
            {
                Name = "Mystery Vendor",
                PartyType = "Vendor",
                OpeningBalance = 500m
            }, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        Assert.True(created.Account_ID.HasValue,
            "A party was saved with an unrecognised PartyType and therefore no ledger account. " +
            "It is selectable in the UI but every posting against it fails at voucher time, and " +
            "its 500.00 opening balance never reached the general ledger.");
    }

    [Fact]
    public async Task A_customer_that_has_traded_cannot_be_switched_to_a_supplier()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 20, 10m);
        await tenant.SellAsync(world, qty: 5, unitPrice: 20m, paid: 0m, overrideCreditLimit: true);

        // The party's ledger account was created under ACCOUNTS RECEIVABLE and stays there.
        // Flipping the label to Supplier makes every payables report and supplier-balance query
        // read an asset account as if it were a liability.
        var party = await tenant.Db.Parties.FirstAsync(p => p.PartyID == world.Customer.PartyID);
        party.PartyType = "Supplier";

        var act = () => tenant.Get<IPartyService>().UpdateAsync(party, TenantData.TestUserId);

        await Assert.ThrowsAnyAsync<Exception>(act);
    }

    // ------------------------------------------------------------------------------------------
    // Product master data that back-dates itself.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Editing_a_product_cannot_retroactively_change_its_opening_stock()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 20, 10m);

        var before = await tenant.StockOnHandAsync(world.Product.ProductID);
        Assert.Equal(20m, before);

        // Every unit of stock in this system arrives through a document that also posts a voucher.
        // OpeningQuantity is the one exception, and ProductService.UpdateAsync copies it straight
        // off the edit form — so raising it mints inventory with no movement row and no GL entry.
        var product = await tenant.Get<IProductService>().GetByIdAsync(world.Product.ProductID);
        product!.OpeningQuantity = 1000;

        try
        {
            await tenant.Get<IProductService>().UpdateAsync(product, TenantData.TestUserId);
        }
        catch (Exception)
        {
            // Rejecting the edit is a perfectly good outcome.
            return;
        }

        var after = await tenant.StockOnHandAsync(world.Product.ProductID);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Editing_a_product_cannot_drive_its_stock_negative()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (category, subCategory) = await tenant.SeedCategoryAsync();
        var product = await tenant.SeedProductAsync(category, subCategory, openingQuantity: 50);
        var customer = await tenant.SeedCustomerAsync();
        var cash = await tenant.CashAccountAsync();

        // Sell 40 of the 50 opening units.
        await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = product.ProductID, Quantity = 40, UnitPrice = 20m }
            }
        }, TenantData.TestUserId, cash.AccountID, overrideCreditLimit: true);

        var loaded = await tenant.Get<IProductService>().GetByIdAsync(product.ProductID);
        loaded!.OpeningQuantity = 0; // 40 units already left the building

        try
        {
            await tenant.Get<IProductService>().UpdateAsync(loaded, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        var onHand = await tenant.StockOnHandAsync(product.ProductID);
        Assert.True(onHand >= 0,
            $"Editing opening stock drove derived stock on hand to {onHand}. " +
            "Negative inventory is unsellable-but-reported and corrupts every valuation.");
    }

    [Fact]
    public async Task Changing_a_products_category_after_it_has_traded_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 20, 10m);

        // A second category pointing at a DIFFERENT stock account.
        var db = tenant.Db;
        var otherStock = new PharmaCare.Domain.Entities.Accounting.Account
        {
            Name = "Inventory / Stock (Secondary)",
            AccountHead_ID = (await db.Accounts.FirstAsync(a => a.Name == "Inventory / Stock")).AccountHead_ID,
            AccountSubhead_ID = (await db.Accounts.FirstAsync(a => a.Name == "Inventory / Stock")).AccountSubhead_ID,
            AccountType_ID = (await db.Accounts.FirstAsync(a => a.Name == "Inventory / Stock")).AccountType_ID,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TenantData.TestUserId
        };
        db.Accounts.Add(otherStock);
        await db.SaveChangesAsync();

        var (category2, subCategory2) = await tenant.SeedCategoryAsync("Cosmetics");
        var cat2 = await db.Categories.FirstAsync(c => c.CategoryID == category2.CategoryID);
        cat2.StockAccount_ID = otherStock.AccountID;
        await db.SaveChangesAsync();

        var product = await tenant.Get<IProductService>().GetByIdAsync(world.Product.ProductID);
        product!.Category_ID = category2.CategoryID;
        product.SubCategory_ID = subCategory2.SubCategoryID;

        try
        {
            await tenant.Get<IProductService>().UpdateAsync(product, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        // The goods were debited to the ORIGINAL stock account. Selling them now credits the NEW
        // one, so the original never clears and the balance sheet carries inventory that is gone.
        await tenant.SellAsync(world, qty: 20, unitPrice: 25m, paid: 0m, overrideCreditLimit: true);

        var originalStockAccountId = world.Category.StockAccount_ID;
        var residue = await db.VoucherDetails.AsNoTracking()
            .Where(d => d.Account_ID == originalStockAccountId && d.Voucher!.Status == "Posted")
            .SumAsync(d => d.DebitAmount - d.CreditAmount);

        Assert.Equal(0m, residue);
    }

    [Fact]
    public async Task Changing_a_categorys_stock_account_after_it_has_traded_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 20, 10m);

        var db = tenant.Db;
        var source = await db.Accounts.AsNoTracking().FirstAsync(a => a.Name == "Inventory / Stock");
        var otherStock = new PharmaCare.Domain.Entities.Accounting.Account
        {
            Name = "Inventory / Stock (Relocated)",
            AccountHead_ID = source.AccountHead_ID,
            AccountSubhead_ID = source.AccountSubhead_ID,
            AccountType_ID = source.AccountType_ID,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = TenantData.TestUserId
        };
        db.Accounts.Add(otherStock);
        await db.SaveChangesAsync();

        var category = await tenant.Get<ICategoryService>().GetByIdAsync(world.Category.CategoryID);
        category!.StockAccount_ID = otherStock.AccountID;

        try
        {
            await tenant.Get<ICategoryService>().UpdateAsync(category, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        await tenant.SellAsync(world, qty: 20, unitPrice: 25m, paid: 0m, overrideCreditLimit: true);

        var residue = await db.VoucherDetails.AsNoTracking()
            .Where(d => d.Account_ID == source.AccountID && d.Voucher!.Status == "Posted")
            .SumAsync(d => d.DebitAmount - d.CreditAmount);

        Assert.Equal(0m, residue);
    }

    [Fact]
    public async Task Increasing_units_in_pack_cannot_push_the_wholesale_price_below_cost()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 20, 10m); // cost 10 / unit

        var productService = tenant.Get<IProductService>();

        // A box of 10 at 150 is 15 per unit — comfortably above the 10 cost, and accepted.
        var product = await productService.GetByIdAsync(world.Product.ProductID);
        product!.UnitsInPack = 10;
        await productService.UpdateAsync(product, TenantData.TestUserId);

        await productService.SaveProductPricesAsync(world.Product.ProductID, new List<ProductPriceDto>
        {
            new()
            {
                PriceTypeId = AccountingConstants.WholesalePriceTypeId,
                PriceTypeName = "Wholesale",
                Price = 150m
            }
        }, TenantData.TestUserId);

        // Now widen the pack without touching the price. 150 for a box of 30 is 5 per unit —
        // half the cost. The below-cost gate only runs when the PRICE is saved, so this edit
        // walks straight past it.
        var reloaded = await productService.GetByIdAsync(world.Product.ProductID);
        reloaded!.UnitsInPack = 30;

        try
        {
            await productService.UpdateAsync(reloaded, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        var stored = await tenant.Db.ProductPrices.AsNoTracking()
            .FirstAsync(p => p.Product_ID == world.Product.ProductID
                          && p.PriceType_ID == AccountingConstants.WholesalePriceTypeId);

        var perUnit = stored.SalePrice / 30m;
        Assert.True(perUnit >= 10m,
            $"Wholesale price is now {perUnit:N2} per unit against a cost of 10.00 — the pack-size " +
            "edit bypassed the below-cost block that guards SaveProductPricesAsync.");
    }

    [Fact]
    public async Task A_deactivated_product_still_holding_stock_is_not_silently_dropped_from_valuation()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 20, 10m);

        var report = tenant.Get<PharmaCare.Application.Interfaces.Reports.IInventoryReportService>();
        var before = await report.GetCurrentStockReportAsync(new PharmaCare.Application.ViewModels.Report.DateRangeFilter());
        var valueBefore = before.Rows.Sum(r => r.StockValue);
        Assert.Equal(200m, valueBefore);

        // Deactivating a product is a catalogue gesture — it stops the till offering it. It must
        // not make 200.00 of inventory vanish from the valuation while the GL still carries it.
        await tenant.Get<IProductService>().ToggleStatusAsync(world.Product.ProductID, TenantData.TestUserId);

        var after = await report.GetCurrentStockReportAsync(new PharmaCare.Application.ViewModels.Report.DateRangeFilter());
        var valueAfter = after.Rows.Sum(r => r.StockValue);

        var glStock = await tenant.Db.VoucherDetails.AsNoTracking()
            .Where(d => d.Account_ID == world.Category.StockAccount_ID && d.Voucher!.Status == "Posted")
            .SumAsync(d => d.DebitAmount - d.CreditAmount);

        Assert.Equal(glStock, valueAfter);
    }

    // ------------------------------------------------------------------------------------------
    // Chart of accounts.
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_ledger_account_cannot_be_promoted_to_a_system_account_from_the_edit_form()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var customer = await tenant.SeedCustomerAsync();

        var accountService = tenant.Get<IAccountService>();
        var account = await accountService.GetByIdAsync(customer.Account_ID!.Value);
        Assert.False(account!.IsSystemAccount);

        account.IsSystemAccount = true;
        await accountService.UpdateAsync(account, TenantData.TestUserId);

        var reloaded = await tenant.Db.Accounts.AsNoTracking()
            .FirstAsync(a => a.AccountID == customer.Account_ID!.Value);

        Assert.False(reloaded.IsSystemAccount,
            "IsSystemAccount is bound straight off the edit form. The flag is what protects the " +
            "provisioned chart of accounts, so it must not be settable by the party editing an account.");
    }

    [Fact]
    public async Task An_account_that_has_posted_entries_cannot_be_retyped_as_cash()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 20, 10m);
        await tenant.SellAsync(world, qty: 5, unitPrice: 20m, paid: 0m, overrideCreditLimit: true);

        var accountService = tenant.Get<IAccountService>();
        var arAccount = await accountService.GetByIdAsync(world.Customer.Account_ID!.Value);

        // Every "is this really cash?" gate in the app is a lookup on AccountType. Re-typing a
        // receivable as CASH turns it into a valid tender account, so a receipt can be "banked"
        // into the very balance it is supposed to settle.
        arAccount!.AccountType_ID = AccountingConstants.CashAccountTypeId;

        try
        {
            await accountService.UpdateAsync(arAccount, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        // Accepted. Now measure how far it goes: is the re-typed receivable accepted as the tender
        // account on a sale? If so, a customer's own balance settles their purchase and no cash
        // ever moves — every cash/bank gate in the app is a lookup on AccountType, so one edit
        // defeats all of them at once.
        var tenderAccepted = true;
        try
        {
            await tenant.Get<ISaleService>().CreateAsync(new StockMain
            {
                Party_ID = world.Customer.PartyID,
                TransactionDate = AppTime.Now,
                PaidAmount = 100m,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 5, UnitPrice = 20m }
                }
            }, TenantData.TestUserId, world.Customer.Account_ID!.Value, overrideCreditLimit: true);
        }
        catch (Exception)
        {
            tenderAccepted = false;
        }

        Assert.Fail(
            "A receivable account was re-typed as CASH from the account edit form. " +
            (tenderAccepted
                ? "It was then accepted as the tender account on a sale — the customer's own balance paid for their purchase."
                : "A sale still refused it as tender, so the blast radius is limited to reporting and account classification."));
    }


    [Fact]
    public async Task A_system_account_cannot_be_deactivated_out_from_under_the_ledger()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var stockAccountId = world.Category.StockAccount_ID!.Value;
        var accountService = tenant.Get<IAccountService>();

        try
        {
            await accountService.ToggleStatusAsync(stockAccountId, TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        var account = await tenant.Db.Accounts.AsNoTracking().FirstAsync(a => a.AccountID == stockAccountId);
        if (account.IsActive) return;

        // Deactivation went through. Measure what it actually costs: does the next goods receipt
        // refuse to post (an operational outage), or does it post into an inactive account anyway
        // (a silent one, invisible on any screen that filters on IsActive)?
        string outcome;
        try
        {
            await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, 10m);

            var posted = await tenant.Db.VoucherDetails.AsNoTracking()
                .AnyAsync(d => d.Account_ID == stockAccountId && d.Voucher!.Status == "Posted");
            outcome = posted
                ? "the next goods receipt posted into the deactivated account anyway — the balance is live but hidden from every IsActive-filtered screen"
                : "the next goods receipt posted nothing to it";
        }
        catch (Exception ex)
        {
            outcome = $"the next goods receipt then failed outright ({ex.GetType().Name}), taking purchasing down";
        }

        Assert.Fail(
            "The inventory control account was deactivated while a category still posts to it, and " +
            outcome + ". Deactivating an account that master data still references should be refused up front.");
    }

    [Fact]
    public async Task Deleting_an_account_head_that_still_owns_accounts_is_refused_cleanly()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var customer = await tenant.SeedCustomerAsync();

        var account = await tenant.Db.Accounts.AsNoTracking()
            .FirstAsync(a => a.AccountID == customer.Account_ID!.Value);
        var headId = account.AccountHead_ID!.Value;

        var headService = tenant.Get<IAccountHeadService>();

        // A hard delete of a head that still owns accounts must be a handled refusal, not a raw
        // FK violation surfacing as an unhandled DbUpdateException.
        var ex = await Record.ExceptionAsync(() => headService.DeleteAsync(headId));

        // Whatever the mechanism, two things must hold afterwards: the head survives, and no
        // account was silently orphaned by having its AccountHead_ID nulled out from under it.
        using var fresh = await _fixture.ScopeForAsync(tenant.PharmacyId);

        var stillThere = await fresh.Db.AccountHeads.AsNoTracking()
            .AnyAsync(h => h.AccountHeadID == headId);
        Assert.True(stillThere,
            $"An account head that still owns accounts was deleted (outcome: {ex?.GetType().Name ?? "no exception"}).");

        var stillParented = await fresh.Db.Accounts.AsNoTracking()
            .FirstAsync(a => a.AccountID == customer.Account_ID!.Value);
        Assert.Equal(headId, stillParented.AccountHead_ID);
    }

    [Fact]
    public async Task Profit_settings_reject_a_negative_margin()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var settingsService = tenant.Get<IProfitSettingsService>();

        try
        {
            await settingsService.UpdateAsync(
                retailProfitPercent: -50m,
                wholesaleProfitPercent: -50m,
                priceRoundingStep: 1m,
                TenantData.TestUserId);
        }
        catch (Exception)
        {
            return;
        }

        var settings = await settingsService.GetAsync();
        Assert.True(settings.RetailProfitPercent >= 0m,
            "A negative retail margin was stored. Only the rounding step is clamped in " +
            "ProfitSettingsService.UpdateAsync; the two percentages are taken as given.");
    }
}
