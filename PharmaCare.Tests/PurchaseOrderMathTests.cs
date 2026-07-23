using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Tests;

public class PurchaseOrderMathTests
{
    private static StockMain Po(decimal discountPercent = 0, params (int productId, decimal qty, decimal lineTotal)[] lines)
    {
        return new StockMain
        {
            StockMainID = 1,
            DiscountPercent = discountPercent,
            StockDetails = lines.Select(l => new StockDetail
            {
                Product_ID = l.productId,
                Quantity = l.qty,
                LineTotal = l.lineTotal,
                UnitPrice = l.qty > 0 ? l.lineTotal / l.qty : 0
            }).ToList()
        };
    }

    private static Dictionary<int, decimal> Received(params (int productId, decimal qty)[] items)
        => items.ToDictionary(i => i.productId, i => i.qty);

    [Fact]
    public void NoDetails_ReturnsZero()
    {
        var po = new StockMain { StockDetails = new List<StockDetail>() };

        Assert.Equal(0m, PurchaseOrderMath.RemainingTotal(po, Received()));
    }

    [Fact]
    public void NothingReceived_ReturnsFullValue()
    {
        var po = Po(0, (1, 10m, 100m), (2, 5m, 250m));

        Assert.Equal(350m, PurchaseOrderMath.RemainingTotal(po, Received()));
    }

    [Fact]
    public void PartiallyReceived_ReturnsRemainingValueAtLineRate()
    {
        var po = Po(0, (1, 10m, 100m)); // rate 10/unit

        Assert.Equal(30m, PurchaseOrderMath.RemainingTotal(po, Received((1, 7m))));
    }

    [Fact]
    public void FullyReceived_ReturnsZero()
    {
        var po = Po(0, (1, 10m, 100m));

        Assert.Equal(0m, PurchaseOrderMath.RemainingTotal(po, Received((1, 10m))));
    }

    [Fact]
    public void OverReceived_ClampsToZero_NeverNegative()
    {
        var po = Po(0, (1, 10m, 100m));

        Assert.Equal(0m, PurchaseOrderMath.RemainingTotal(po, Received((1, 15m))));
    }

    [Fact]
    public void HeaderDiscount_IsAppliedToRemainingValue()
    {
        var po = Po(10m, (1, 10m, 100m)); // 100 remaining − 10% = 90

        Assert.Equal(90m, PurchaseOrderMath.RemainingTotal(po, Received()));
    }

    [Fact]
    public void DuplicateProductLines_AreAggregated()
    {
        // Two lines of the same product: 6 @ 10 and 4 @ 10; received 8 → remaining 2 @ 10.
        var po = Po(0, (1, 6m, 60m), (1, 4m, 40m));

        Assert.Equal(20m, PurchaseOrderMath.RemainingTotal(po, Received((1, 8m))));
    }
}
