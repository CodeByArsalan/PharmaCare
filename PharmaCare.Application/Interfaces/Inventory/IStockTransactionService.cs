using PharmaCare.Domain.Models.Inventory;

namespace PharmaCare.Application.Interfaces.Inventory;

/// <summary>
/// Unified service for all stock transactions using StockMain/StockDetail
/// </summary>
public interface IStockTransactionService
{
    // ========== CREATE OPERATIONS ==========
    
    /// <summary>
    /// Create a new stock transaction (Sale, Purchase, Return, etc.)
    /// </summary>
    Task<StockMain> CreateTransactionAsync(CreateTransactionRequest request);
    
    /// <summary>
    /// Add line items to an existing transaction
    /// </summary>
    Task AddTransactionLinesAsync(int stockMainId, IEnumerable<CreateTransactionLineRequest> lines);
    
    // ========== READ OPERATIONS ==========
    
    /// <summary>
    /// Get transaction by ID
    /// </summary>
    Task<StockMain?> GetTransactionAsync(int stockMainId);
    
    /// <summary>
    /// Get transactions by invoice type
    /// </summary>
    Task<IEnumerable<StockMain>> GetTransactionsByTypeAsync(int invoiceTypeId, DateTime? fromDate = null, DateTime? toDate = null);
    
    /// <summary>
    /// Get transactions by store
    /// </summary>
    Task<IEnumerable<StockMain>> GetTransactionsByStoreAsync(int storeId, int? invoiceTypeId = null);
    
    // ========== UPDATE OPERATIONS ==========
    
    /// <summary>
    /// Update transaction status
    /// </summary>
    Task UpdateStatusAsync(int stockMainId, string status);
    
    /// <summary>
    /// Process payment for a transaction
    /// </summary>
    Task ProcessPaymentAsync(int stockMainId, decimal amount, int accountId, string paymentMethod);
    
    /// <summary>
    /// Void a transaction with reason
    /// </summary>
    Task VoidTransactionAsync(int stockMainId, string reason, int userId);
    
    // ========== ACCOUNTING INTEGRATION ==========
    
    /// <summary>
    /// Generate accounting entries for a transaction
    /// </summary>
    Task<int?> GenerateAccountingEntriesAsync(int stockMainId);
    
    /// <summary>
    /// Complete a transaction - update inventory, generate movements, and finalize status
    /// </summary>
    Task<bool> CompleteTransactionAsync(int stockMainId, int userId);
}

// ========== REQUEST DTOS ==========

public class CreateTransactionRequest
{
    public int InvoiceTypeId { get; set; }
    public int StoreId { get; set; }
    public int? PartyId { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.Now;
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public int? AccountId { get; set; }
    public string? Remarks { get; set; }
    public string? SupplierInvoiceNo { get; set; }
    public int? ReferenceStockMainId { get; set; }
    public int? DestinationStoreId { get; set; }
    public int CreatedBy { get; set; }
    public List<CreateTransactionLineRequest> Lines { get; set; } = new();
}

public class CreateTransactionLineRequest
{
    public int ProductId { get; set; }
    public int? ProductBatchId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? ReturnReason { get; set; }
    public decimal? SystemQuantity { get; set; }
    public decimal? PhysicalQuantity { get; set; }
}
