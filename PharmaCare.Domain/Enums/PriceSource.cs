namespace PharmaCare.Domain.Enums;

/// <summary>
/// Indicates how a resolved sale price was arrived at, so the origin is never opaque.
/// </summary>
public enum PriceSource
{
    /// <summary>An explicit per-product price from the ProductPrices table was used.</summary>
    Explicit = 1,

    /// <summary>No explicit price existed; the price was derived from cost + ProfitSettings margin.</summary>
    Formula = 2
}
