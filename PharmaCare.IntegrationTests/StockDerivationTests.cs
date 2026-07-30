using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Stock is derived from movement history rather than stored, and it used to be derived three
/// different ways. The inventory report hard-coded the transaction codes it counted, so stock
/// adjustments were silently missing from it while the POS counted them — the report and the till
/// disagreed about what was on the shelf.
/// </summary>
[Collection(Collections.Database)]
public class StockDerivationTests
{
    private readonly DatabaseFixture _fixture;

    public StockDerivationTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Report_stock_matches_POS_availability()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, quantity: 100, unitCost: 10m);

        var report = await tenant.Get<IInventoryReportService>()
            .GetCurrentStockReportAsync(new DateRangeFilter());
        var pos = await tenant.Get<IProductService>()
            .GetStockStatusAsync(new List<int> { world.Product.ProductID });

        var reported = report.Rows.Single(r => r.ProductId == world.Product.ProductID).CurrentStock;

        Assert.Equal(100m, reported);
        Assert.Equal(reported, pos[world.Product.ProductID]);
    }

    [Fact]
    public async Task Stock_write_off_reduces_reported_stock()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, quantity: 100, unitCost: 10m);

        await tenant.Get<IStockAdjustmentService>().CreateAsync(new StockMain
        {
            AdjustmentType = "Write-off",
            AdjustmentReason = "Expired",
            TransactionDate = AppTime.Now,
            Remarks = "Integration test write-off",
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 30m }
            }
        }, TenantData.TestUserId);

        var report = await tenant.Get<IInventoryReportService>()
            .GetCurrentStockReportAsync(new DateRangeFilter());
        var row = report.Rows.Single(r => r.ProductId == world.Product.ProductID);

        // The regression: this used to still read 100 because the report ignored SADJ transactions.
        Assert.Equal(70m, row.CurrentStock);
        Assert.Equal(-30m, row.AdjustedQty);

        var pos = await tenant.Get<IProductService>()
            .GetStockStatusAsync(new List<int> { world.Product.ProductID });
        Assert.Equal(row.CurrentStock, pos[world.Product.ProductID]);
    }

    [Fact]
    public async Task Stock_is_valued_at_last_GRN_cost_not_opening_price()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var (category, subCategory) = await tenant.SeedCategoryAsync();
        var product = await tenant.SeedProductAsync(category, subCategory, openingPrice: 20m);
        var supplier = await tenant.SeedSupplierAsync();

        await tenant.ReceiveStockAsync(supplier, product, quantity: 10, unitCost: 20m);
        await tenant.ReceiveStockAsync(supplier, product, quantity: 10, unitCost: 28m); // price rose

        var report = await tenant.Get<IInventoryReportService>()
            .GetCurrentStockReportAsync(new DateRangeFilter());
        var row = report.Rows.Single(r => r.ProductId == product.ProductID);

        Assert.Equal(20m, row.CurrentStock);
        Assert.Equal(28m, row.CostPrice);          // was OpeningPrice (20) before the fix
        Assert.Equal(20m * 28m, row.StockValue);
    }

    [Fact]
    public async Task Draft_and_void_transactions_do_not_move_stock()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        var grn = await tenant.ReceiveStockAsync(world.Supplier, world.Product, quantity: 50, unitCost: 10m);

        await tenant.Get<IPurchaseService>().VoidAsync(grn.StockMainID, "test void", TenantData.TestUserId);

        var report = await tenant.Get<IInventoryReportService>()
            .GetCurrentStockReportAsync(new DateRangeFilter());
        var row = report.Rows.Single(r => r.ProductId == world.Product.ProductID);

        Assert.Equal(0m, row.CurrentStock);
    }
}
