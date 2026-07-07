namespace PharmaCare.Application.Helpers;

/// <summary>
/// Single place that applies the configured price-rounding rule, so rounding behaviour is
/// consistent everywhere prices are computed.
/// </summary>
public static class PriceRounding
{
    /// <summary>
    /// Rounds <paramref name="value"/> to the nearest <paramref name="step"/> (away-from-zero at the
    /// midpoint). A step of 0 or less disables rounding and returns the value unchanged.
    /// </summary>
    public static decimal Apply(decimal value, decimal step)
    {
        if (step <= 0m)
        {
            return value;
        }

        return Math.Round(value / step, 0, MidpointRounding.AwayFromZero) * step;
    }
}
