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

            var sourceLine = detailGroup.First();
            var unitRate = sourceLine.Quantity > 0 ? (sourceLine.LineTotal / sourceLine.Quantity) : sourceLine.UnitPrice;
            remainingTotal += Math.Round(remainingQty * unitRate, 2);
        }

        if (purchaseOrder.DiscountPercent > 0)
        {
            remainingTotal -= Math.Round(remainingTotal * purchaseOrder.DiscountPercent / 100, 2);
        }

        return Math.Max(0, Math.Round(remainingTotal, 2));
    }
}
