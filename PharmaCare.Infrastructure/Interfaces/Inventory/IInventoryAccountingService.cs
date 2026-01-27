using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;

namespace PharmaCare.Infrastructure.Interfaces.Inventory;

/// <summary>
/// Central service for atomic inventory + accounting operations.
/// 
/// CRITICAL RULES:
/// 1. ALL inventory operations MUST go through this service
/// 2. StockMovement is ALWAYS linked to JournalEntry
/// 3. FIFO costing is enforced for all outgoing movements
/// 4. Transaction is atomic - both succeed or both fail
/// </summary>
public interface IInventoryAccountingService
{
    /// <summary>
    /// Processes a sale - reduces inventory (FIFO) and creates COGS + Revenue entries
    /// </summary>
    Task<InventoryAccountingResult> ProcessSaleAsync(
        int storeId,
        int saleId,
        DateTime saleDate,
        IEnumerable<SaleLineDto> saleLines,
        int paymentAccountId,
        int userId,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a sale return - increases inventory and reverses COGS + Revenue
    /// </summary>
    Task<InventoryAccountingResult> ProcessSaleReturnAsync(
        int storeId,
        int saleReturnId,
        DateTime returnDate,
        IEnumerable<SaleReturnLineDto> returnLines,
        int paymentAccountId,
        int userId,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a GRN - increases inventory and creates Inventory + AP entries
    /// </summary>
    Task<InventoryAccountingResult> ProcessPurchaseAsync(
        int storeId,
        int grnId,
        DateTime grnDate,
        IEnumerable<GrnLineDto> grnLines,
        int supplierId,
        int userId,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a purchase return - decreases inventory and reverses Inventory + AP
    /// </summary>
    Task<InventoryAccountingResult> ProcessPurchaseReturnAsync(
        int storeId,
        int purchaseReturnId,
        DateTime returnDate,
        IEnumerable<PurchaseReturnLineDto> returnLines,
        int supplierId,
        int userId,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a stock adjustment - increases/decreases inventory and posts adjustment entry
    /// </summary>
    Task<InventoryAccountingResult> ProcessAdjustmentAsync(
        int storeId,
        int adjustmentId,
        DateTime adjustmentDate,
        IEnumerable<StockAdjustmentLineDto> adjustmentLines,
        int userId,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a stock transfer between stores
    /// </summary>
    Task<InventoryAccountingResult> ProcessTransferAsync(
        int sourceStoreId,
        int destinationStoreId,
        int transferId,
        DateTime transferDate,
        IEnumerable<TransferLineDto> transferLines,
        int userId,
        CancellationToken ct = default);

    /// <summary>
    /// Processes a write-off (expired/damaged goods)
    /// </summary>
    Task<InventoryAccountingResult> ProcessWriteOffAsync(
        int storeId,
        int writeOffId,
        DateTime writeOffDate,
        IEnumerable<WriteOffLineDto> writeOffLines,
        int userId,
        CancellationToken ct = default);
}

#region DTOs

/// <summary>
/// Result of inventory accounting operation
/// </summary>
public class InventoryAccountingResult
{
    public bool Success { get; set; }
    public int JournalEntryId { get; set; }
    public string JournalEntryNumber { get; set; } = string.Empty;
    public List<int> StockMovementIds { get; set; } = new();
    public decimal TotalCost { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Sale line for processing
/// </summary>
public class SaleLineDto
{
    public int ProductBatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal? NetAmount { get; set; } // Optional: net amount after discount
    public decimal LineTotal => NetAmount ?? (Quantity * UnitPrice);
}

/// <summary>
/// Sale return line for processing
/// </summary>
public class SaleReturnLineDto
{
    public int ProductBatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal CostTotal => Quantity * UnitCost;
    public decimal PriceTotal => Quantity * UnitPrice;
}

/// <summary>
/// GRN line for processing
/// </summary>
public class GrnLineDto
{
    public int ProductBatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal => Quantity * UnitCost;
}

/// <summary>
/// Purchase return line for processing
/// </summary>
public class PurchaseReturnLineDto
{
    public int ProductBatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal => Quantity * UnitCost;
}

/// <summary>
/// Stock adjustment line for processing
/// </summary>
public class StockAdjustmentLineDto
{
    public int ProductBatchId { get; set; }
    public decimal QuantityChange { get; set; } // Positive = increase, Negative = decrease
    public decimal UnitCost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal TotalCost => Math.Abs(QuantityChange) * UnitCost;
}

/// <summary>
/// Transfer line for processing
/// </summary>
public class TransferLineDto
{
    public int ProductBatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost => Quantity * UnitCost;
}

/// <summary>
/// Write-off line for processing
/// </summary>
public class WriteOffLineDto
{
    public int ProductBatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal TotalCost => Quantity * UnitCost;
}

/// <summary>
/// Result of FIFO cost allocation
/// </summary>
public class FifoCostResult
{
    public int BatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost => Quantity * UnitCost;
}

#endregion
