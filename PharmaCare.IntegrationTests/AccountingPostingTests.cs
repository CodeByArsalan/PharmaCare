using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// The double-entry engine: every transaction must leave the books balanced, and voiding must
/// reverse rather than delete. None of this had automated coverage.
/// </summary>
[Collection(Collections.Database)]
public class AccountingPostingTests
{
    private readonly DatabaseFixture _fixture;

    public AccountingPostingTests(DatabaseFixture fixture) => _fixture = fixture;

    private static StockMain CashSale(Party customer, Product product, decimal qty, decimal unitPrice, decimal paid)
        => new()
        {
            Party_ID = customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = paid,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = product.ProductID, Quantity = qty, UnitPrice = unitPrice }
            }
        };

    [Fact]
    public async Task Sale_posts_a_balanced_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var sale = await tenant.Get<ISaleService>().CreateAsync(
            CashSale(world.Customer, world.Product, qty: 5, unitPrice: 20m, paid: 100m),
            TenantData.TestUserId, world.Cash.AccountID);

        var vouchers = await tenant.Db.Vouchers
            .Include(v => v.VoucherDetails)
            .Where(v => v.SourceTable == "StockMain" && v.SourceID == sale.StockMainID)
            .ToListAsync();

        Assert.NotEmpty(vouchers);
        foreach (var voucher in vouchers)
        {
            var debits = voucher.VoucherDetails.Sum(d => d.DebitAmount);
            var credits = voucher.VoucherDetails.Sum(d => d.CreditAmount);
            Assert.Equal(debits, credits);
            Assert.Equal(voucher.TotalDebit, voucher.TotalCredit);
        }
    }

    [Fact]
    public async Task Sale_debits_COGS_and_credits_stock_at_authoritative_cost()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var sale = await tenant.Get<ISaleService>().CreateAsync(
            CashSale(world.Customer, world.Product, qty: 5, unitPrice: 20m, paid: 100m),
            TenantData.TestUserId, world.Cash.AccountID);

        var lines = await tenant.Db.VoucherDetails
            .Include(d => d.Account)
            .Where(d => d.Voucher!.SourceTable == "StockMain" && d.Voucher.SourceID == sale.StockMainID)
            .ToListAsync();

        var cogs = lines.Where(l => l.Account!.Name == "Cost of Goods Sold").Sum(l => l.DebitAmount);
        var stock = lines.Where(l => l.Account!.Name == "Inventory / Stock").Sum(l => l.CreditAmount);
        var revenue = lines.Where(l => l.Account!.Name == "Sales Revenue").Sum(l => l.CreditAmount);

        Assert.Equal(50m, cogs);     // 5 units at the GRN cost of 10
        Assert.Equal(50m, stock);
        Assert.Equal(100m, revenue); // 5 units at 20
    }

    [Fact]
    public async Task Sale_below_cost_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleService>().CreateAsync(
                CashSale(world.Customer, world.Product, qty: 1, unitPrice: 4m, paid: 4m),
                TenantData.TestUserId, world.Cash.AccountID));
    }

    [Fact]
    public async Task Selling_more_than_is_in_stock_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 5, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleService>().CreateAsync(
                CashSale(world.Customer, world.Product, qty: 50, unitPrice: 20m, paid: 0m),
                TenantData.TestUserId, world.Cash.AccountID));
    }

    [Fact]
    public async Task Voiding_a_sale_reverses_its_vouchers_and_returns_the_stock()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var sale = await tenant.Get<ISaleService>().CreateAsync(
            CashSale(world.Customer, world.Product, qty: 10, unitPrice: 20m, paid: 200m),
            TenantData.TestUserId, world.Cash.AccountID);

        await tenant.Get<ISaleService>().VoidAsync(sale.StockMainID, "test", TenantData.TestUserId);

        var vouchers = await tenant.Db.Vouchers
            .Include(v => v.VoucherDetails)
            .Where(v => v.SourceTable == "StockMain" && v.SourceID == sale.StockMainID)
            .ToListAsync();

        // Originals are marked reversed and a mirror-image voucher exists for each.
        Assert.Contains(vouchers, v => v.IsReversed);
        Assert.Contains(vouchers, v => v.ReversesVoucher_ID != null);

        // Net effect on the books is zero.
        Assert.Equal(vouchers.Sum(v => v.VoucherDetails.Sum(d => d.DebitAmount)),
                     vouchers.Sum(v => v.VoucherDetails.Sum(d => d.CreditAmount)));

        var stock = await tenant.Get<IProductService>()
            .GetStockStatusAsync(new List<int> { world.Product.ProductID });
        Assert.Equal(100m, stock[world.Product.ProductID]);
    }

    [Fact]
    public async Task Party_opening_balance_reaches_the_general_ledger()
    {
        using var tenant = await _fixture.NewTenantAsync();

        var customer = await tenant.SeedCustomerAsync("Opening Balance Customer", openingBalance: 250m);

        var voucher = await tenant.Db.Vouchers
            .Include(v => v.VoucherDetails)
            .SingleAsync(v => v.SourceTable == "Party" && v.SourceID == customer.PartyID);

        Assert.Equal(voucher.TotalDebit, voucher.TotalCredit);

        var partyLine = voucher.VoucherDetails.Single(d => d.Account_ID == customer.Account_ID);
        Assert.Equal(250m, partyLine.DebitAmount);   // customer owes us
        Assert.Equal(0m, partyLine.CreditAmount);
    }

    [Fact]
    public async Task Supplier_opening_balance_is_credited_not_debited()
    {
        using var tenant = await _fixture.NewTenantAsync();

        var supplier = await tenant.SeedSupplierAsync("Opening Balance Supplier", openingBalance: 300m);

        var voucher = await tenant.Db.Vouchers
            .Include(v => v.VoucherDetails)
            .SingleAsync(v => v.SourceTable == "Party" && v.SourceID == supplier.PartyID);

        var partyLine = voucher.VoucherDetails.Single(d => d.Account_ID == supplier.Account_ID);
        Assert.Equal(300m, partyLine.CreditAmount);  // we owe them
        Assert.Equal(0m, partyLine.DebitAmount);
    }

    [Fact]
    public async Task Editing_an_opening_balance_posts_only_the_difference()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var parties = tenant.Get<IPartyService>();

        var customer = await tenant.SeedCustomerAsync("Adjusted Customer", openingBalance: 250m);

        await parties.UpdateAsync(new Party
        {
            PartyID = customer.PartyID,
            Name = customer.Name,
            PartyType = customer.PartyType,
            OpeningBalance = 100m,
            IsActive = true
        }, TenantData.TestUserId);

        var vouchers = await tenant.Db.Vouchers
            .Include(v => v.VoucherDetails)
            .Where(v => v.SourceTable == "Party" && v.SourceID == customer.PartyID)
            .ToListAsync();

        Assert.Equal(2, vouchers.Count);

        // Net movement on the party's ledger account equals the new opening balance.
        var lines = vouchers.SelectMany(v => v.VoucherDetails)
            .Where(d => d.Account_ID == customer.Account_ID)
            .ToList();
        Assert.Equal(100m, lines.Sum(d => d.DebitAmount) - lines.Sum(d => d.CreditAmount));
    }

    [Fact]
    public async Task Trial_balance_is_balanced_after_a_full_trading_cycle()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.Get<ISaleService>().CreateAsync(
            CashSale(world.Customer, world.Product, qty: 10, unitPrice: 25m, paid: 250m),
            TenantData.TestUserId, world.Cash.AccountID);

        var trialBalance = await tenant.Get<IFinancialReportService>()
            .GetTrialBalanceAsync(AppTime.Today.AddDays(1));

        Assert.True(trialBalance.IsBalanced,
            $"Trial balance is out by {trialBalance.TotalDebit - trialBalance.TotalCredit:N2}");
    }
}
