using PharmaCare.Application.Implementations.Transactions;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Tests;

/// <summary>
/// Exercises TransactionServiceBase.CalculateTotals — the shared header-total math every
/// transaction service uses, including the discount-spoof reset the AuditTests harness
/// verifies end-to-end.
/// </summary>
public class TransactionTotalsTests
{
    // CalculateTotals touches no injected dependencies, so nulls are safe here.
    private sealed class TestTransactionService : TransactionServiceBase
    {
        public TestTransactionService() : base(null!, null!, null!, null!) { }

        public void CalculateTotalsPublic(StockMain stockMain) => CalculateTotals(stockMain);
    }

    private readonly TestTransactionService _sut = new();

    private static StockMain Doc(decimal discountPercent, decimal discountAmount, decimal paid, params decimal[] lineTotals)
        => new()
        {
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            PaidAmount = paid,
            StockDetails = lineTotals.Select(t => new StockDetail { LineTotal = t }).ToList()
        };

    [Fact]
    public void SumsLineTotalsIntoSubTotal()
    {
        var doc = Doc(0, 0, 0, 100m, 250m, 49.99m);

        _sut.CalculateTotalsPublic(doc);

        Assert.Equal(399.99m, doc.SubTotal);
        Assert.Equal(399.99m, doc.TotalAmount);
    }

    [Fact]
    public void DiscountPercent_DrivesDiscountAmount()
    {
        var doc = Doc(discountPercent: 10m, discountAmount: 0, paid: 0, 200m);

        _sut.CalculateTotalsPublic(doc);

        Assert.Equal(20m, doc.DiscountAmount);
        Assert.Equal(180m, doc.TotalAmount);
    }

    [Fact]
    public void SpoofedDiscountAmount_WithZeroPercent_IsResetToZero()
    {
        // A client posting DiscountAmount=999 with DiscountPercent=0 must not get the discount.
        var doc = Doc(discountPercent: 0, discountAmount: 999m, paid: 0, 1000m);

        _sut.CalculateTotalsPublic(doc);

        Assert.Equal(0m, doc.DiscountAmount);
        Assert.Equal(1000m, doc.TotalAmount);
    }

    [Fact]
    public void SpoofedDiscountAmount_WithNonZeroPercent_IsRecomputedFromPercent()
    {
        var doc = Doc(discountPercent: 5m, discountAmount: 999m, paid: 0, 1000m);

        _sut.CalculateTotalsPublic(doc);

        Assert.Equal(50m, doc.DiscountAmount); // derived from percent, not the posted amount
        Assert.Equal(950m, doc.TotalAmount);
    }

    [Fact]
    public void BalanceAmount_IsTotalMinusPaid()
    {
        var doc = Doc(0, 0, paid: 300m, 1000m);

        _sut.CalculateTotalsPublic(doc);

        Assert.Equal(700m, doc.BalanceAmount);
    }

    [Fact]
    public void DiscountAmount_IsRoundedToTwoDecimals()
    {
        var doc = Doc(discountPercent: 3m, discountAmount: 0, paid: 0, 33.33m);

        _sut.CalculateTotalsPublic(doc);

        Assert.Equal(1.00m, doc.DiscountAmount); // 0.9999 rounds to 1.00
    }
}
