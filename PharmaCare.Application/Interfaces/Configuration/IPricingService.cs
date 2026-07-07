using PharmaCare.Application.DTOs.Configuration;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Interfaces.Configuration;

/// <summary>
/// Single source of truth for turning cost + settings + any explicit price into the actual
/// selling price. Collapses the previously duplicated "explicit price ?? cost×margin" logic
/// so the POS and wholesale-enforcement paths can never diverge.
/// </summary>
public interface IPricingService
{
    /// <summary>The PriceType id that represents wholesale (per-box) pricing.</summary>
    int WholesalePriceTypeId { get; }

    /// <summary>
    /// Resolves the selling price for a product. Uses <paramref name="explicitPrice"/> when one
    /// exists (&gt; 0); otherwise derives it from <paramref name="cost"/> and the margin in
    /// <paramref name="settings"/>, rounded to the configured step and never allowed below cost.
    /// Also reports the source and whether the result is below cost.
    /// </summary>
    ResolvedPrice Resolve(int priceTypeId, decimal cost, int unitsInPack, decimal? explicitPrice, ProfitSettings settings);
}
