using PharmaCare.Application.Helpers;

namespace PharmaCare.Tests;

public class PriceRoundingTests
{
    [Theory]
    [InlineData(12.30, 1.00, 12.00)]
    [InlineData(12.50, 1.00, 13.00)] // midpoint rounds away from zero
    [InlineData(12.70, 1.00, 13.00)]
    [InlineData(12.30, 0.50, 12.50)]
    [InlineData(12.24, 0.50, 12.00)]
    [InlineData(103.00, 5.00, 105.00)]
    public void Apply_RoundsToNearestStep(decimal value, decimal step, decimal expected)
    {
        Assert.Equal(expected, PriceRounding.Apply(value, step));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Apply_WithNonPositiveStep_ReturnsValueUnchanged(decimal step)
    {
        Assert.Equal(12.34m, PriceRounding.Apply(12.34m, step));
    }
}
