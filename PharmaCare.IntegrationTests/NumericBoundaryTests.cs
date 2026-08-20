using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.IntegrationTests;

/// <summary>
/// Boundary probes on the numbers themselves.
///
/// <para>
/// Two shapes are covered. First, PRECISION: quantity is <c>decimal(18,4)</c> while money is
/// <c>decimal(18,2)</c>, and the line maths runs in C# against the unrounded value before SQL
/// applies the column scale — so a quantity below the storable precision can be charged for
/// without ever moving. Second, MAGNITUDE: <c>AccountingConstants.MaxTransactionAmount</c> is
/// checked in ExpenseService and JournalVoucherService only, so the trading documents that
/// actually move stock and money have no ceiling at all.
/// </para>
///
/// <para>Each test asserts the CORRECT behaviour, so a failing test is a confirmed defect.</para>
/// </summary>
[Collection(Collections.Database)]
public class NumericBoundaryTests
{
    private readonly DatabaseFixture _fixture;

    public NumericBoundaryTests(DatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_quantity_below_the_storable_precision_cannot_be_charged_for()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, 10m);

        var before = await tenant.StockOnHandAsync(world.Product.ProductID);

        // 0.00004 rounds to 0.0000 in a decimal(18,4) column, but the revenue is computed in C#
        // from the unrounded quantity first.
        StockMain sale;
        try
        {
            sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
            {
                Party_ID = world.Customer.PartyID,
                TransactionDate = AppTime.Now,
                PaidAmount = 0,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 0.00004m, UnitPrice = 1_000_000m }
                }
            }, TenantData.TestUserId, world.Cash.AccountID, overrideCreditLimit: true);
        }
        catch (Exception)
        {
            return; // Refusing a sub-precision quantity is the correct outcome.
        }

        var after = await tenant.StockOnHandAsync(world.Product.ProductID);
        var moved = before - after;

        var reloaded = await tenant.Db.StockMains.AsNoTracking()
            .FirstAsync(s => s.StockMainID == sale.StockMainID);

        Assert.False(moved == 0m && reloaded.TotalAmount > 0m,
            $"A sale charged {reloaded.TotalAmount:N2} while moving {moved} units of stock. " +
            "The quantity was rounded away by the decimal(18,4) column but the revenue was " +
            "computed from the unrounded value, so the books show a sale of nothing.");
    }

    [Fact]
    public async Task A_sale_beyond_the_transaction_sanity_cap_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, 10m);

        // Expenses and journal vouchers are capped at 100,000,000. A sale is not — and a sale
        // posts to exactly the same ledger.
        var act = () => tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new()
                {
                    Product_ID = world.Product.ProductID,
                    Quantity = 100,
                    UnitPrice = AccountingConstants.MaxTransactionAmount // 100m per unit x 100
                }
            }
        }, TenantData.TestUserId, world.Cash.AccountID, overrideCreditLimit: true);

        var ex = await Record.ExceptionAsync(act);

        Assert.True(ex is InvalidOperationException,
            "A sale of 10,000,000,000.00 was accepted. AccountingConstants.MaxTransactionAmount " +
            "guards ExpenseService and JournalVoucherService but no trading document, so the one " +
            "sanity ceiling in the system does not apply to the documents that carry the most value. " +
            $"Actual outcome: {(ex == null ? "accepted" : ex.GetType().Name + ": " + ex.Message)}");
    }

    [Fact]
    public async Task A_goods_receipt_beyond_the_transaction_sanity_cap_is_rejected()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        var act = () => tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new()
                {
                    Product_ID = world.Product.ProductID,
                    Quantity = 1000,
                    UnitPrice = AccountingConstants.MaxTransactionAmount,
                    CostPrice = AccountingConstants.MaxTransactionAmount
                }
            }
        }, TenantData.TestUserId);

        var ex = await Record.ExceptionAsync(act);

        Assert.True(ex is InvalidOperationException,
            "A goods receipt of 100,000,000,000.00 was accepted with no sanity ceiling. " +
            $"Actual outcome: {(ex == null ? "accepted" : ex.GetType().Name + ": " + ex.Message)}");
    }

    [Fact]
    public async Task An_arithmetic_overflow_on_a_line_is_a_handled_validation_error()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();

        // decimal(18,2) tops out just under 10^16. This line computes to 10^18, so if nothing
        // validates it the failure surfaces from SQL Server as a raw overflow rather than as a
        // message the operator can act on.
        var act = () => tenant.Get<IPurchaseService>().CreateAsync(new StockMain
        {
            Party_ID = world.Supplier.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new()
                {
                    Product_ID = world.Product.ProductID,
                    Quantity = 1_000_000_000m,
                    UnitPrice = 1_000_000_000m,
                    CostPrice = 1_000_000_000m
                }
            }
        }, TenantData.TestUserId);

        var ex = await Record.ExceptionAsync(act);

        Assert.True(ex is InvalidOperationException,
            "An out-of-range line amount surfaced as " +
            $"{(ex == null ? "no error at all" : ex.GetType().Name)} instead of a handled " +
            "validation error. Anything that is not InvalidOperationException reaches the " +
            "controller's generic handler and becomes an opaque failure for the user.");
    }

    [Fact]
    public async Task Stock_stays_exact_across_many_fractional_movements()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 30, 10m);

        // 0.1 is not representable in binary floating point. It IS exact in decimal, so 300
        // movements of 0.1 must land exactly on zero — this pins that nothing in the pipeline
        // has quietly become a double.
        for (var i = 0; i < 300; i++)
        {
            await tenant.Get<ISaleService>().CreateAsync(new StockMain
            {
                Party_ID = world.Customer.PartyID,
                TransactionDate = AppTime.Now,
                PaidAmount = 0,
                StockDetails = new List<StockDetail>
                {
                    new() { Product_ID = world.Product.ProductID, Quantity = 0.1m, UnitPrice = 20m }
                }
            }, TenantData.TestUserId, world.Cash.AccountID, overrideCreditLimit: true);
        }

        var onHand = await tenant.StockOnHandAsync(world.Product.ProductID);
        Assert.Equal(0m, onHand);
    }

    [Fact]
    public async Task A_product_priced_in_fractions_reconciles_revenue_to_the_ledger()
    {
        using var tenant = await _fixture.NewTenantAsync();
        var world = await tenant.SeedWorldAsync();
        await tenant.ReceiveStockAsync(world.Supplier, world.Product, 100, 1m);

        // Three lines that each round on their own. The header total and the posted revenue must
        // agree exactly, whichever way each line rounded.
        var sale = await tenant.Get<ISaleService>().CreateAsync(new StockMain
        {
            Party_ID = world.Customer.PartyID,
            TransactionDate = AppTime.Now,
            PaidAmount = 0,
            StockDetails = new List<StockDetail>
            {
                new() { Product_ID = world.Product.ProductID, Quantity = 3, UnitPrice = 3.333m },
                new() { Product_ID = world.Product.ProductID, Quantity = 7, UnitPrice = 1.115m },
                new() { Product_ID = world.Product.ProductID, Quantity = 11, UnitPrice = 2.005m }
            }
        }, TenantData.TestUserId, world.Cash.AccountID, overrideCreditLimit: true);

        var header = await tenant.Db.StockMains.AsNoTracking()
            .FirstAsync(s => s.StockMainID == sale.StockMainID);

        var revenue = await tenant.Db.VoucherDetails.AsNoTracking()
            .Where(d => d.Account_ID == world.Category.SaleAccount_ID
                     && d.Voucher!.Status == "Posted")
            .SumAsync(d => d.CreditAmount - d.DebitAmount);

        Assert.Equal(header.TotalAmount, revenue);
    }
}
