using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Returning goods to a supplier takes stock OUT. The dangerous case is returning goods that have
/// already been sold to a customer — the books would show inventory that physically is not there.
/// </summary>
[Collection(Collections.Database)]
public class PurchaseReturnTests
{
    private readonly DatabaseFixture _fixture;

    public PurchaseReturnTests(DatabaseFixture fixture) => _fixture = fixture;

    private static StockMain Return(int supplierId, int grnId, int productId, decimal qty, decimal cost)
        => new()
        {
            Party_ID = supplierId,
            ReferenceStockMain_ID = grnId,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = productId, Quantity = qty, CostPrice = cost }
            }
        };

    [Fact]
    public async Task Returning_to_the_supplier_removes_the_goods_from_stock()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await tenant.Get<IPurchaseReturnService>().CreateAsync(
            Return(world.Supplier.PartyID, grn.StockMainID, world.Product.ProductID, 30, 10m), TenantData.TestUserId);

        Assert.Equal(70m, await tenant.StockOnHandAsync(world.Product.ProductID));
    }

    [Fact]
    public async Task Purchase_return_posts_a_balanced_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var ret = await tenant.Get<IPurchaseReturnService>().CreateAsync(
            Return(world.Supplier.PartyID, grn.StockMainID, world.Product.ProductID, 30, 10m), TenantData.TestUserId);

        var voucher = await tenant.Db.Vouchers
            .Include(v => v.VoucherDetails)
            .FirstAsync(v => v.SourceTable == "StockMain" && v.SourceID == ret.StockMainID);

        Assert.Equal(voucher.VoucherDetails.Sum(d => d.DebitAmount), voucher.VoucherDetails.Sum(d => d.CreditAmount));
        Assert.Equal(300m, voucher.TotalDebit);
    }

    [Fact]
    public async Task Returning_more_than_the_GRN_received_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 50, unitCost: 10m);
        // Extra stock from a second GRN, so the failure can only come from the per-GRN check.
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 500, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseReturnService>().CreateAsync(
                Return(world.Supplier.PartyID, grn.StockMainID, world.Product.ProductID, 51, 10m), TenantData.TestUserId));
    }

    /// <summary>
    /// The GRN allows the return but the goods are gone. Without the on-hand check this drives
    /// derived stock negative.
    /// </summary>
    [Fact]
    public async Task Returning_goods_that_have_already_been_sold_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, unitCost: 10m);
        await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseReturnService>().CreateAsync(
                Return(world.Supplier.PartyID, grn.StockMainID, world.Product.ProductID, 10, 10m), TenantData.TestUserId));
    }

    [Fact]
    public async Task Returning_against_another_suppliers_GRN_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var otherSupplier = await tenant.SeedSupplierAsync("Beta Wholesale");
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseReturnService>().CreateAsync(
                Return(otherSupplier.PartyID, grn.StockMainID, world.Product.ProductID, 10, 10m), TenantData.TestUserId));
    }

    [Fact]
    public async Task A_purchase_return_with_no_reference_GRN_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
            {
                Party_ID = world.Supplier.PartyID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 10, CostPrice = 10m }
                }
            }, TenantData.TestUserId));
    }

    [Fact]
    public async Task Successive_purchase_returns_cannot_exceed_the_GRN_in_aggregate()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 500, unitCost: 10m);

        var service = tenant.Get<IPurchaseReturnService>();
        await service.CreateAsync(Return(world.Supplier.PartyID, grn.StockMainID, world.Product.ProductID, 60, 10m), TenantData.TestUserId);
        await service.CreateAsync(Return(world.Supplier.PartyID, grn.StockMainID, world.Product.ProductID, 40, 10m), TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Return(world.Supplier.PartyID, grn.StockMainID, world.Product.ProductID, 1, 10m), TenantData.TestUserId));
    }

    /// <summary>A return reduces what is owed to the supplier on that GRN.</summary>
    [Fact]
    public async Task A_return_reduces_the_outstanding_balance_on_the_reference_GRN()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await tenant.Get<IPurchaseReturnService>().CreateAsync(
            Return(world.Supplier.PartyID, grn.StockMainID, world.Product.ProductID, 30, 10m), TenantData.TestUserId);

        var reloaded = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == grn.StockMainID);
        Assert.Equal(700m, reloaded.BalanceAmount); // 1000 received less a 300 return
    }

    [Fact]
    public async Task Voiding_a_purchase_return_puts_the_goods_back_and_reverses_the_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var ret = await tenant.Get<IPurchaseReturnService>().CreateAsync(
            Return(world.Supplier.PartyID, grn.StockMainID, world.Product.ProductID, 30, 10m), TenantData.TestUserId);
        Assert.Equal(70m, await tenant.StockOnHandAsync(world.Product.ProductID));

        await tenant.Get<IPurchaseReturnService>().VoidAsync(ret.StockMainID, "supplier refused", TenantData.TestUserId);

        Assert.Equal(100m, await tenant.StockOnHandAsync(world.Product.ProductID));
        Assert.True(await tenant.Db.Vouchers.AnyAsync(v => v.ReversesVoucher_ID == ret.Voucher_ID));

        var reloadedGrn = await tenant.Db.StockMains.AsNoTracking().FirstAsync(s => s.StockMainID == grn.StockMainID);
        Assert.Equal(1000m, reloadedGrn.BalanceAmount);
    }

    [Fact]
    public async Task Voiding_the_same_purchase_return_twice_is_a_no_op()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var ret = await tenant.Get<IPurchaseReturnService>().CreateAsync(
            Return(world.Supplier.PartyID, grn.StockMainID, world.Product.ProductID, 30, 10m), TenantData.TestUserId);

        Assert.True(await tenant.Get<IPurchaseReturnService>().VoidAsync(ret.StockMainID, "first", TenantData.TestUserId));
        Assert.False(await tenant.Get<IPurchaseReturnService>().VoidAsync(ret.StockMainID, "second", TenantData.TestUserId));

        // A second reversal voucher would double-count the credit back to the supplier.
        var reversals = await tenant.Db.Vouchers.CountAsync(v => v.ReversesVoucher_ID == ret.Voucher_ID);
        Assert.Equal(1, reversals);
    }
}
