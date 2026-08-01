using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Stock on hand is <c>OpeningQuantity + SUM(StockDetail.Quantity * TransactionType.StockDirection)</c>.
/// That formula only works if exactly ONE of the two carries the sign. The convention throughout is
/// that <c>StockDirection</c> owns it and stored quantities are always positive magnitudes — so a
/// service that also negates its quantities cancels the direction out and moves stock the wrong way.
///
/// <para>
/// These tests pin the convention per transaction type, because the failure is silent: the voucher
/// is still correct and still balances, so only the quantity drifts.
/// </para>
/// </summary>
[Collection(Collections.Database)]
public class StockSignConventionTests
{
    private readonly DatabaseFixture _fixture;

    public StockSignConventionTests(DatabaseFixture fixture) => _fixture = fixture;

    private async Task<(decimal Quantity, int Direction)> MovementAsync(TenantScope tenant, int stockMainId)
    {
        var row = await tenant.Db.StockDetails
            .Include(d => d.StockMain)!.ThenInclude(m => m!.TransactionType)
            .AsNoTracking()
            .FirstAsync(d => d.StockMain_ID == stockMainId);

        return (row.Quantity, row.StockMain!.TransactionType!.StockDirection);
    }

    [Fact]
    public async Task A_goods_receipt_stores_a_positive_quantity_against_an_inbound_direction()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var (quantity, direction) = await MovementAsync(tenant, grn.StockMainID);

        Assert.Equal(100m, quantity);
        Assert.Equal(+1, direction);
        Assert.Equal(+100m, quantity * direction);
    }

    [Fact]
    public async Task A_sale_stores_a_positive_quantity_against_an_outbound_direction()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);
        var sale = await tenant.SellAsync(world, qty: 10, unitPrice: 20m, paid: 200m);

        var (quantity, direction) = await MovementAsync(tenant, sale.StockMainID);

        Assert.Equal(10m, quantity);
        Assert.Equal(-1, direction);
        Assert.Equal(-10m, quantity * direction);
    }

    [Fact]
    public async Task A_stock_write_off_stores_a_positive_quantity_against_an_outbound_direction()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var adj = await tenant.Get<IStockAdjustmentService>().CreateAsync(new StockMain
        {
            TransactionDate = AppTime.Now,
            AdjustmentType = "Write-off",
            AdjustmentReason = "Expired",
            StockDetails = new List<StockDetail> { new() { Product_ID = world.Product.ProductID, Quantity = 6 } }
        }, TenantData.TestUserId);

        var (quantity, direction) = await MovementAsync(tenant, adj.StockMainID);

        Assert.Equal(6m, quantity);
        Assert.Equal(-1, direction);
        Assert.Equal(-6m, quantity * direction);
    }

    /// <summary>
    /// A purchase return is an OUTBOUND movement: goods physically leave for the supplier. Its
    /// transaction type is already seeded with a direction of -1, so the stored quantity must be a
    /// positive magnitude like every other type.
    /// </summary>
    [Fact]
    public async Task A_purchase_return_stores_a_positive_quantity_against_an_outbound_direction()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var ret = await tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = grn.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 30, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        var (quantity, direction) = await MovementAsync(tenant, ret.StockMainID);

        Assert.Equal(-1, direction);
        Assert.Equal(30m, quantity);
        Assert.Equal(-30m, quantity * direction);
    }

    /// <summary>
    /// The consequence, stated in business terms: the accounting voucher CREDITS inventory (the
    /// asset falls), so the quantity on hand must fall too. If they disagree, the stock report and
    /// the balance sheet describe different warehouses.
    /// </summary>
    [Fact]
    public async Task A_purchase_return_moves_quantity_and_valuation_in_the_same_direction()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var before = await tenant.StockOnHandAsync(world.Product.ProductID);

        var ret = await tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = grn.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 30, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        var after = await tenant.StockOnHandAsync(world.Product.ProductID);

        var inventoryCredit = await tenant.Db.VoucherDetails
            .Where(d => d.Voucher_ID == ret.Voucher_ID && d.Account!.Name == "Inventory / Stock")
            .SumAsync(d => d.CreditAmount);

        Assert.Equal(300m, inventoryCredit);            // the books say inventory fell by 300
        Assert.Equal(before - 30m, after);              // so the shelf must have fallen by 30 units
    }

    /// <summary>
    /// The exploitable form: receiving and returning the same goods should be a round trip back to
    /// where you started, not a way to manufacture stock.
    /// </summary>
    [Fact]
    public async Task Receiving_and_returning_the_same_goods_nets_to_no_stock_change()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var opening = await tenant.StockOnHandAsync(world.Product.ProductID);

        for (var i = 0; i < 3; i++)
        {
            var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 50, unitCost: 10m);
            await tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
            {
                Party_ID = world.Supplier.PartyID,
                ReferenceStockMain_ID = grn.StockMainID,
                TransactionDate = AppTime.Now,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 50, CostPrice = 10m }
                }
            }, TenantData.TestUserId);
        }

        Assert.Equal(opening, await tenant.StockOnHandAsync(world.Product.ProductID));
    }

    /// <summary>
    /// The consequence that reaches the shop floor. The POS availability check reads the same
    /// derived figure, so any inflation of it is directly sellable: the till will happily dispense
    /// goods that were sent back to the supplier.
    /// </summary>
    [Fact]
    public async Task Stock_that_was_returned_to_the_supplier_cannot_be_sold_again()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        // Everything received goes straight back to the supplier — the shelf is empty.
        await tenant.Get<IPurchaseReturnService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            ReferenceStockMain_ID = grn.StockMainID,
            TransactionDate = AppTime.Now,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 100, CostPrice = 10m }
            }
        }, TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.SellAsync(world, qty: 100, unitPrice: 20m, paid: 2000m));
    }
}
