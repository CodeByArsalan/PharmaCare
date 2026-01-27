using Microsoft.EntityFrameworkCore;
using PharmaCare.Infrastructure.Interfaces.Inventory;

namespace PharmaCare.Infrastructure.Inventory;

/// <summary>
/// FIFO (First-In-First-Out) Cost Calculator for inventory valuation.
/// Allocates costs from oldest batches first when processing outgoing inventory.
/// </summary>
public sealed class FifoCostCalculator
{
    private readonly PharmaCareDBContext _context;

    public FifoCostCalculator(PharmaCareDBContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Allocates cost for a single batch (weighted average not FIFO since we have batch-level tracking)
    /// </summary>
    public async Task<FifoCostResult> GetBatchCostAsync(
        int productBatchId,
        decimal quantity,
        CancellationToken ct)
    {
        var batch = await _context.ProductBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.ProductBatchID == productBatchId, ct);

        if (batch == null)
        {
            throw new InvalidOperationException($"ProductBatch with ID {productBatchId} not found.");
        }

        return new FifoCostResult
        {
            BatchId = batch.ProductBatchID,
            Quantity = quantity,
            UnitCost = batch.CostPrice
        };
    }

    /// <summary>
    /// Validates that sufficient stock exists for the operation
    /// </summary>
    public async Task<bool> ValidateStockAvailabilityAsync(
        int storeId,
        int productBatchId,
        decimal requiredQuantity,
        CancellationToken ct)
    {
        var inventory = await _context.StoreInventories
            .AsNoTracking()
            .FirstOrDefaultAsync(si =>
                si.Store_ID == storeId &&
                si.ProductBatch_ID == productBatchId, ct);

        if (inventory == null)
        {
            return false;
        }

        return inventory.QuantityOnHand >= requiredQuantity;
    }

    /// <summary>
    /// Gets the current unit cost for a batch
    /// </summary>
    public async Task<decimal> GetBatchUnitCostAsync(int productBatchId, CancellationToken ct)
    {
        var batch = await _context.ProductBatches
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.ProductBatchID == productBatchId, ct);

        return batch?.CostPrice ?? 0;
    }
}
