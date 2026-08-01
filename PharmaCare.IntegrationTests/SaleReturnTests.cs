using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Sale returns are where a POS leaks money: the return price, the returnable quantity and the
/// resulting customer credit are all attacker-controllable from the browser, and the goods coming
/// back into stock must be provably the goods that went out.
/// </summary>
[Collection(Collections.Database)]
public class SaleReturnTests
{
    private readonly DatabaseFixture _fixture;

    public SaleReturnTests(DatabaseFixture fixture) => _fixture = fixture;

    private static StockMain Return(int saleId, int productId, decimal qty, decimal unitPrice)
        => new()
        {
            ReferenceStockMain_ID = saleId,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = productId, Quantity = qty, UnitPrice = unitPrice }
            }
        };

    [Fact]
    public async Task Returned_goods_come_back_into_stock()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var beforeReturn = await tenant.StockOnHandAsync(world.Product.ProductID);
        await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 4, unitPrice: 20m), TenantData.TestUserId);

        var afterReturn = await tenant.StockOnHandAsync(world.Product.ProductID);
        Assert.Equal(90m, beforeReturn);
        Assert.Equal(94m, afterReturn);
    }

    [Fact]
    public async Task Sale_return_posts_a_balanced_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 4, unitPrice: 20m), TenantData.TestUserId);

        var voucher = await tenant.Db.Vouchers
            .Include(v => v.VoucherDetails)
            .FirstAsync(v => v.SourceTable == "StockMain" && v.SourceID == ret.StockMainID);

        Assert.Equal(voucher.VoucherDetails.Sum(d => d.DebitAmount), voucher.VoucherDetails.Sum(d => d.CreditAmount));
        Assert.Equal(voucher.TotalDebit, voucher.TotalCredit);
    }

    [Fact]
    public async Task Returning_more_than_was_sold_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 5, unitPrice: 20m, paid: 100m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleReturnService>().CreateAsync(
                Return(sale.StockMainID, world.Product.ProductID, qty: 6, unitPrice: 20m), TenantData.TestUserId));
    }

    /// <summary>
    /// Two returns that are each individually valid must not be able to exceed the sale in total.
    /// </summary>
    [Fact]
    public async Task Successive_returns_cannot_exceed_the_sold_quantity_in_aggregate()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var service = tenant.Get<ISaleReturnService>();
        await service.CreateAsync(Return(sale.StockMainID, world.Product.ProductID, 6, 20m), TenantData.TestUserId);
        await service.CreateAsync(Return(sale.StockMainID, world.Product.ProductID, 4, 20m), TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Return(sale.StockMainID, world.Product.ProductID, 1, 20m), TenantData.TestUserId));
    }

    /// <summary>
    /// A voided return frees its quantity again — otherwise a mistaken return permanently burns
    /// the customer's right to return those goods.
    /// </summary>
    [Fact]
    public async Task Voiding_a_return_releases_the_quantity_for_a_later_return()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var service = tenant.Get<ISaleReturnService>();
        var first = await service.CreateAsync(Return(sale.StockMainID, world.Product.ProductID, 10, 20m), TenantData.TestUserId);
        await service.VoidAsync(first.StockMainID, "keyed twice", TenantData.TestUserId);

        var second = await service.CreateAsync(Return(sale.StockMainID, world.Product.ProductID, 10, 20m), TenantData.TestUserId);
        Assert.Equal("Approved", second.Status);
    }

    /// <summary>
    /// The browser posts the return price. Refunding at a higher price than the customer paid is
    /// free money, so the server must overwrite it with the original sale price.
    /// </summary>
    [Fact]
    public async Task Return_price_is_forced_back_to_the_original_sale_price()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 2, unitPrice: 5_000m), TenantData.TestUserId);

        Assert.Equal(20m, ret.StockDetails.First().UnitPrice);
        Assert.Equal(40m, ret.TotalAmount);
    }

    [Fact]
    public async Task A_return_line_for_a_product_that_was_not_sold_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var other = await tenant.SeedProductAsync(world.Category, world.SubCategory, name: "Brufen");
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.ReceiveStockAsync(world.Supplier, other, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 5, unitPrice: 20m, paid: 100m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleReturnService>().CreateAsync(
                Return(sale.StockMainID, other.ProductID, qty: 1, unitPrice: 20m), TenantData.TestUserId));
    }

    [Fact]
    public async Task A_return_with_zero_quantity_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 5, unitPrice: 20m, paid: 100m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleReturnService>().CreateAsync(
                Return(sale.StockMainID, world.Product.ProductID, qty: 0, unitPrice: 20m), TenantData.TestUserId));
    }

    [Fact]
    public async Task A_return_that_references_no_sale_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleReturnService>().CreateAsync(new StockMain
            {
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 1, UnitPrice = 20m }
                }
            }, TenantData.TestUserId));
    }

    /// <summary>
    /// A return against a fully-paid sale leaves the customer in credit. That credit must be
    /// recorded as an open credit note, not silently dropped.
    /// </summary>
    [Fact]
    public async Task Returning_against_a_fully_paid_sale_raises_a_credit_note_for_the_overpayment()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 3, unitPrice: 20m), TenantData.TestUserId);

        var creditNote = await tenant.Db.Set<CreditNote>()
            .FirstOrDefaultAsync(c => c.SourceStockMain_ID == ret.StockMainID);

        Assert.NotNull(creditNote);
        Assert.Equal(60m, creditNote!.TotalAmount);
        Assert.Equal(60m, creditNote.BalanceAmount);
        Assert.Equal("Open", creditNote.Status);
    }

    /// <summary>
    /// On an unpaid credit sale nothing has been overpaid, so a return should reduce the balance
    /// rather than mint a credit note the customer could spend twice.
    /// </summary>
    [Fact]
    public async Task Returning_against_an_unpaid_sale_reduces_the_balance_and_raises_no_credit_note()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var customer = await tenant.SeedCustomerAsync("Account Customer", creditLimit: 100_000m);
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0m,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 20m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 4, unitPrice: 20m), TenantData.TestUserId);

        var reloaded = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == sale.StockMainID);
        var creditNotes = await tenant.Db.Set<CreditNote>().CountAsync(c => c.SourceStockMain_ID == ret.StockMainID);

        Assert.Equal(120m, reloaded.BalanceAmount); // 200 sold, 80 returned
        Assert.Equal(0, creditNotes);
    }

    /// <summary>
    /// Voiding a return takes the goods back OUT of stock. If they were re-sold in the meantime
    /// that would drive stock negative, so the void must be refused instead.
    /// </summary>
    [Fact]
    public async Task Voiding_a_return_whose_goods_were_already_re_sold_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 10, unitPrice: 20m), TenantData.TestUserId);

        // The returned goods go straight back out of the door on a second sale.
        await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<ISaleReturnService>().VoidAsync(ret.StockMainID, "reversed in error", TenantData.TestUserId));
    }

    [Fact]
    public async Task Voiding_a_return_reverses_its_voucher_and_restores_the_sale_balance()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var customer = await tenant.SeedCustomerAsync("Account Customer", creditLimit: 100_000m);
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0m,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 10, UnitPrice = 20m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID);

        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 4, unitPrice: 20m), TenantData.TestUserId);

        await tenant.Get<ISaleReturnService>().VoidAsync(ret.StockMainID, "customer changed their mind", TenantData.TestUserId);

        var reloaded = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == sale.StockMainID);
        var reversal = await tenant.Db.Vouchers.AnyAsync(v => v.ReversesVoucher_ID == ret.Voucher_ID);

        Assert.Equal(200m, reloaded.BalanceAmount);
        Assert.True(reversal, "voiding a return must post a reversal voucher, not delete the original");
    }

    /// <summary>
    /// Nothing is ever hard-deleted: the voided return and its voucher must both still be there.
    /// </summary>
    [Fact]
    public async Task A_voided_return_is_retained_not_deleted()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 4, unitPrice: 20m), TenantData.TestUserId);
        await tenant.Get<ISaleReturnService>().VoidAsync(ret.StockMainID, "error", TenantData.TestUserId);

        var stored = await tenant.Db.StockMains.AsNoTracking().FirstOrDefaultAsync(s => s.StockMainID == ret.StockMainID);
        Assert.NotNull(stored);
        Assert.Equal("Void", stored!.Status);
        Assert.Equal("error", stored.VoidReason);
        Assert.True(await tenant.Db.Vouchers.AnyAsync(v => v.VoucherID == ret.Voucher_ID));
    }

    /// <summary>Voided returns must not keep goods on the shelf that were taken back out.</summary>
    [Fact]
    public async Task A_voided_return_no_longer_counts_towards_stock()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var ret = await tenant.Get<ISaleReturnService>().CreateAsync(
            Return(sale.StockMainID, world.Product.ProductID, qty: 4, unitPrice: 20m), TenantData.TestUserId);
        Assert.Equal(94m, await tenant.StockOnHandAsync(world.Product.ProductID));

        await tenant.Get<ISaleReturnService>().VoidAsync(ret.StockMainID, "error", TenantData.TestUserId);
        Assert.Equal(90m, await tenant.StockOnHandAsync(world.Product.ProductID));
    }
}
