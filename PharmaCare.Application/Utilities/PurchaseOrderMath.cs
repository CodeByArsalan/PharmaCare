using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Utilities;

/// <summary>
/// Shared purchase-order money math. Keep this the ONLY implementation — the PO list
/// (auto-complete/re-open) and supplier payments (advance cap) must agree on what the
/// not-yet-received portion of a PO is worth.
/// </summary>
public static class PurchaseOrderMath
{
    /// <summary>
    /// Value of the not-yet-received portion of a purchase order: per product,
    /// (ordered − received) × the line's effective unit rate, then the PO's header
    /// discount percent applied. Rounded to 2dp, never negative.
    /// </summary>
    public static decimal RemainingTotal(StockMain purchaseOrder, IReadOnlyDictionary<int, decimal> receivedQtyByProduct)
    {
        if (purchaseOrder.StockDetails == null || purchaseOrder.StockDetails.Count == 0)
        {
            return 0;
        }

        decimal remainingTotal = 0;
        foreach (var detailGroup in purchaseOrder.StockDetails.GroupBy(d => d.Product_ID))
        {
            var orderedQty = detailGroup.Sum(d => d.Quantity);
            receivedQtyByProduct.TryGetValue(detailGroup.Key, out var receivedQty);
            var remainingQty = Math.Max(0, orderedQty - receivedQty);
            if (remainingQty <= 0)
            {
                continue;
            }

            // Weighted-average rate across ALL lines of the product — a PO may carry several
            // lines of the same product at different rates, and pricing the remainder at the
            // first line's rate would misvalue the advance cap.
            var groupTotal = detailGroup.Sum(d => d.LineTotal);
            var unitRate = orderedQty > 0 ? (groupTotal / orderedQty) : detailGroup.First().UnitPrice;
            remainingTotal += Math.Round(remainingQty * unitRate, 2);
        }

        if (purchaseOrder.DiscountPercent > 0)
        {
            remainingTotal -= Math.Round(remainingTotal * purchaseOrder.DiscountPercent / 100, 2);
        }

        return Math.Max(0, Math.Round(remainingTotal, 2));
    }
}
