using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.DTOs.Configuration;

/// <summary>
/// The canonical result of resolving a product's selling price. Produced by the single
/// pricing resolver so every call site (POS, wholesale enforcement) agrees on the number,
/// its origin, and whether it breaches cost.
/// </summary>
public class ResolvedPrice
{
    /// <summary>Per-unit selling price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>Per-box selling price (UnitPrice × UnitsInPack; the authoritative figure for wholesale).</summary>
    public decimal BoxPrice { get; set; }

    /// <summary>Per-unit cost the price was measured against.</summary>
    public decimal CostPrice { get; set; }

    /// <summary>Whether the price came from an explicit product price or the cost+margin formula.</summary>
    public PriceSource Source { get; set; }

    /// <summary>True when the resolved unit price is below cost (only possible for stale explicit prices).</summary>
    public bool BelowCost { get; set; }
}
