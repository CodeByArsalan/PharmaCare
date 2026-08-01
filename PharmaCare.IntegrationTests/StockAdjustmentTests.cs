using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Stock adjustments are the only way stock moves without a customer or supplier behind it, which
/// makes them the obvious route for making a shortfall disappear. Every adjustment must therefore
/// carry a valued accounting entry — a movement with no voucher is an untraceable write-off.
/// </summary>
[Collection(Collections.Database)]
public class StockAdjustmentTests
{
    private readonly DatabaseFixture _fixture;

    public StockAdjustmentTests(DatabaseFixture fixture) => _fixture = fixture;

    private static StockMain Adjustment(string type, int productId, decimal qty, string reason = "Expired")
        => new()
        {
            TransactionDate = AppTime.Now,
            AdjustmentType = type,
            AdjustmentReason = reason,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = productId, Quantity = qty }
            }
        };

    [Fact]
    public async Task A_write_off_reduces_stock_and_posts_the_loss_against_the_damage_account()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var adj = await tenant.Get<IStockAdjustmentService>()
            .CreateAsync(Adjustment("Write-off", world.Product.ProductID, 8), TenantData.TestUserId);

        Assert.Equal(92m, await tenant.StockOnHandAsync(world.Product.ProductID));

        var lines = await tenant.Db.VoucherDetails
            .Include(d => d.Account)
            .Where(d => d.Voucher_ID == adj.Voucher_ID)
            .ToListAsync();

        Assert.Equal(80m, lines.Where(l => l.Account!.Name == "Damage & Loss").Sum(l => l.DebitAmount));
        Assert.Equal(80m, lines.Where(l => l.Account!.Name == "Inventory / Stock").Sum(l => l.CreditAmount));
        Assert.Equal(lines.Sum(l => l.DebitAmount), lines.Sum(l => l.CreditAmount));
    }

    [Fact]
    public async Task A_write_in_increases_stock_and_reverses_the_loss()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var adj = await tenant.Get<IStockAdjustmentService>()
            .CreateAsync(Adjustment("Write-in", world.Product.ProductID, 5, "Found in back store"), TenantData.TestUserId);

        Assert.Equal(105m, await tenant.StockOnHandAsync(world.Product.ProductID));

        var lines = await tenant.Db.VoucherDetails
            .Include(d => d.Account)
            .Where(d => d.Voucher_ID == adj.Voucher_ID)
            .ToListAsync();

        Assert.Equal(50m, lines.Where(l => l.Account!.Name == "Inventory / Stock").Sum(l => l.DebitAmount));
        Assert.Equal(50m, lines.Where(l => l.Account!.Name == "Damage & Loss").Sum(l => l.CreditAmount));
    }

    [Fact]
    public async Task Writing_off_more_than_is_on_hand_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IStockAdjustmentService>()
                .CreateAsync(Adjustment("Write-off", world.Product.ProductID, 11), TenantData.TestUserId));
    }

    [Fact]
    public async Task An_unrecognised_adjustment_type_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IStockAdjustmentService>()
                .CreateAsync(Adjustment("Shrinkage", world.Product.ProductID, 1), TenantData.TestUserId));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task A_non_positive_adjustment_quantity_is_rejected(decimal quantity)
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IStockAdjustmentService>()
                .CreateAsync(Adjustment("Write-off", world.Product.ProductID, quantity), TenantData.TestUserId));
    }

    [Fact]
    public async Task An_adjustment_with_no_lines_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        await tenant.SeedWorldAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IStockAdjustmentService>().CreateAsync(new StockMain
            {
                TransactionDate = AppTime.Now,
                AdjustmentType = "Write-off",
                AdjustmentReason = "Nothing",
                StockDetails = new List<StockDetail>()
            }, TenantData.TestUserId));
    }

    /// <summary>
    /// A product that has never been received and has no opening price cannot be valued, so
    /// adjusting it would move stock with a zero-value voucher — refuse instead.
    /// </summary>
    [Fact]
    public async Task Adjusting_a_product_that_cannot_be_valued_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var unvalued = await tenant.SeedProductAsync(
            world.Category, world.SubCategory, name: "Unpriced Item", openingPrice: 0m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IStockAdjustmentService>()
                .CreateAsync(Adjustment("Write-in", unvalued.ProductID, 5), TenantData.TestUserId));
    }

    /// <summary>Adjustments are valued at the last GRN cost, not the product's opening price.</summary>
    [Fact]
    public async Task An_adjustment_is_valued_at_the_latest_GRN_cost()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var product = await tenant.SeedProductAsync(
            world.Category, world.SubCategory, name: "Repriced", openingPrice: 3m);

        await tenant.ReceiveStockAsync(world.Supplier, product, 50, unitCost: 7m);
        await tenant.ReceiveStockAsync(world.Supplier, product, 50, unitCost: 11m);

        var adj = await tenant.Get<IStockAdjustmentService>()
            .CreateAsync(Adjustment("Write-off", product.ProductID, 10), TenantData.TestUserId);

        Assert.Equal(110m, adj.TotalAmount); // 10 at the latest cost of 11, not 3 or 7
    }

    [Fact]
    public async Task Voiding_a_write_off_restores_the_stock_and_reverses_the_voucher()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var adj = await tenant.Get<IStockAdjustmentService>()
            .CreateAsync(Adjustment("Write-off", world.Product.ProductID, 8), TenantData.TestUserId);
        Assert.Equal(92m, await tenant.StockOnHandAsync(world.Product.ProductID));

        await tenant.Get<IStockAdjustmentService>().VoidAsync(adj.StockMainID, "counted wrong", TenantData.TestUserId);

        Assert.Equal(100m, await tenant.StockOnHandAsync(world.Product.ProductID));
        Assert.True(await tenant.Db.Vouchers.AnyAsync(v => v.ReversesVoucher_ID == adj.Voucher_ID));
    }

    /// <summary>
    /// Voiding a write-in removes stock. If it has since been sold, the void must be refused.
    /// </summary>
    [Fact]
    public async Task Voiding_a_write_in_whose_stock_was_already_sold_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 10, unitCost: 10m);

        var adj = await tenant.Get<IStockAdjustmentService>()
            .CreateAsync(Adjustment("Write-in", world.Product.ProductID, 5, "Found"), TenantData.TestUserId);

        await tenant.SellAsync(world, qty: 15, unitPrice: 20m, paid: 300m);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IStockAdjustmentService>().VoidAsync(adj.StockMainID, "miscount", TenantData.TestUserId));
    }

    [Fact]
    public async Task Voiding_an_adjustment_without_a_reason_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var adj = await tenant.Get<IStockAdjustmentService>()
            .CreateAsync(Adjustment("Write-off", world.Product.ProductID, 8), TenantData.TestUserId);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tenant.Get<IStockAdjustmentService>().VoidAsync(adj.StockMainID, "   ", TenantData.TestUserId));
    }

    /// <summary>
    /// Every other transaction service stamps SourceTable + SourceID on its voucher, which is how
    /// a voucher is traced back to what caused it. An adjustment voucher that only records the
    /// table is unattributable from the ledger side.
    /// </summary>
    [Fact]
    public async Task An_adjustment_voucher_records_which_adjustment_produced_it()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, unitCost: 10m);

        var adj = await tenant.Get<IStockAdjustmentService>()
            .CreateAsync(Adjustment("Write-off", world.Product.ProductID, 8), TenantData.TestUserId);

        var voucher = await tenant.Db.Vouchers.AsNoTracking().FirstAsync(v => v.VoucherID == adj.Voucher_ID);

        Assert.Equal("StockMain", voucher.SourceTable);
        Assert.Equal(adj.StockMainID, voucher.SourceID);
    }
}
