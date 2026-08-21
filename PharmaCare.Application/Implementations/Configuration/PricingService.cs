using PharmaCare.Application.DTOs.Configuration;
using PharmaCare.Application.Helpers;
using PharmaCare.Application.Interfaces.Configuration;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.Implementations.Configuration;

/// <inheritdoc cref="IPricingService"/>
public class PricingService : IPricingService
{
    public ResolvedPrice Resolve(bool isWholesale, decimal cost, int unitsInPack, decimal? explicitPrice, ProfitSettings settings)
    {
        if (unitsInPack < 1)
        {
            unitsInPack = 1;
        }

        var step = settings.PriceRoundingStep;
        var hasExplicit = explicitPrice.HasValue && explicitPrice.Value > 0m;

        decimal unitPrice;
        decimal boxPrice;

        if (isWholesale)
        {
            if (hasExplicit)
            {
                // Explicit wholesale price is stored per box.
                boxPrice = explicitPrice!.Value;
            }
            else
            {
                var formulaBox = PriceRounding.Apply(
                    cost * (1 + settings.WholesaleProfitPercent / 100m) * unitsInPack, step);
                // A formula price is never allowed below cost (rounding down can breach it at low margins).
                boxPrice = Math.Max(formulaBox, cost * unitsInPack);
            }

            unitPrice = boxPrice / unitsInPack;
        }
        else
        {
            if (hasExplicit)
            {
                unitPrice = explicitPrice!.Value;
            }
            else
            {
                var formulaUnit = PriceRounding.Apply(
                    cost * (1 + settings.RetailProfitPercent / 100m), step);
                unitPrice = Math.Max(formulaUnit, cost);
            }

            boxPrice = unitPrice * unitsInPack;
        }

        return new ResolvedPrice
        {
            UnitPrice = unitPrice,
            BoxPrice = boxPrice,
            CostPrice = cost,
            Source = hasExplicit ? PriceSource.Explicit : PriceSource.Formula,
            BelowCost = unitPrice < cost
        };
    }
}
