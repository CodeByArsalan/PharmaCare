using PharmaCare.Application.Implementations.Configuration;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Tests;

public class PricingServiceTests
{
    private const int Retail = 1;
    private const int Wholesale = AccountingConstants.WholesalePriceTypeId;

    private readonly PricingService _sut = new();

    private static ProfitSettings Settings(decimal retailPercent = 20m, decimal wholesalePercent = 10m, decimal step = 1m)
        => new() { RetailProfitPercent = retailPercent, WholesaleProfitPercent = wholesalePercent, PriceRoundingStep = step };

    [Fact]
    public void Retail_Formula_AppliesProfitAndRounding()
    {
        // 100 * 1.20 = 120 → rounds to step 1 → 120
        var price = _sut.Resolve(Retail, cost: 100m, unitsInPack: 10, explicitPrice: null, Settings());

        Assert.Equal(120m, price.UnitPrice);
        Assert.Equal(1200m, price.BoxPrice);
        Assert.Equal(PriceSource.Formula, price.Source);
        Assert.False(price.BelowCost);
    }

    [Fact]
    public void Retail_Formula_NeverRoundsBelowCost()
    {
        // cost 12 at 1% margin = 12.12, which step-5 rounding pulls down to 10 (below cost) —
        // the resolver must clamp back up to cost instead.
        var price = _sut.Resolve(Retail, cost: 12m, unitsInPack: 1, explicitPrice: null, Settings(retailPercent: 1m, step: 5m));

        Assert.Equal(12m, price.UnitPrice);
        Assert.False(price.BelowCost);
    }

    [Fact]
    public void Retail_ExplicitPrice_WinsOverFormula()
    {
        var price = _sut.Resolve(Retail, cost: 100m, unitsInPack: 1, explicitPrice: 95m, Settings());

        Assert.Equal(95m, price.UnitPrice);
        Assert.Equal(PriceSource.Explicit, price.Source);
        Assert.True(price.BelowCost); // explicit below-cost is allowed but flagged
    }

    [Fact]
    public void Wholesale_ExplicitPrice_IsPerBox()
    {
        var price = _sut.Resolve(Wholesale, cost: 10m, unitsInPack: 10, explicitPrice: 110m, Settings());

        Assert.Equal(110m, price.BoxPrice);
        Assert.Equal(11m, price.UnitPrice);
        Assert.Equal(PriceSource.Explicit, price.Source);
    }

    [Fact]
    public void Wholesale_Formula_ClampsBoxPriceToTotalCost()
    {
        // Total cost = 10.2 × 10 = 102; formula gives 103.02, which step-50 rounding pulls
        // down to 100 (below total cost) — the resolver must clamp back up to 102.
        var price = _sut.Resolve(Wholesale, cost: 10.2m, unitsInPack: 10, explicitPrice: null, Settings(wholesalePercent: 1m, step: 50m));

        Assert.Equal(102m, price.BoxPrice);
        Assert.Equal(10.2m, price.UnitPrice);
        Assert.False(price.BelowCost);
    }

    [Fact]
    public void UnitsInPack_BelowOne_IsTreatedAsOne()
    {
        var price = _sut.Resolve(Retail, cost: 100m, unitsInPack: 0, explicitPrice: null, Settings());

        Assert.Equal(price.UnitPrice, price.BoxPrice); // pack of 1 → box price equals unit price
    }

    [Fact]
    public void ExplicitZeroPrice_IsIgnored_FallsBackToFormula()
    {
        var price = _sut.Resolve(Retail, cost: 100m, unitsInPack: 1, explicitPrice: 0m, Settings());

        Assert.Equal(PriceSource.Formula, price.Source);
        Assert.Equal(120m, price.UnitPrice);
    }
}
