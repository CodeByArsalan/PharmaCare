using PharmaCare.Domain.Models.Inventory;

namespace PharmaCare.Application.Interfaces.AccountManagement;

/// <summary>
/// Service for managing AccountVouchers (replaces JournalEntry operations for new system)
/// </summary>
public interface IVoucherService
{
    // ========== CREATE OPERATIONS ==========
    
    /// <summary>
    /// Create a new accounting voucher
    /// </summary>
    Task<AccountVoucher> CreateVoucherAsync(CreateVoucherRequest request);
    
    /// <summary>
    /// Create a voucher from a stock transaction
    /// </summary>
    Task<AccountVoucher> CreateVoucherFromStockTransactionAsync(int stockMainId);
    
    // ========== READ OPERATIONS ==========
    
    /// <summary>
    /// Get voucher by ID
    /// </summary>
    Task<AccountVoucher?> GetVoucherAsync(int voucherId);
    
    /// <summary>
    /// Get vouchers by source reference
    /// </summary>
    Task<IEnumerable<AccountVoucher>> GetVouchersBySourceAsync(string sourceTable, int sourceId);
    
    /// <summary>
    /// Get vouchers by type
    /// </summary>
    Task<IEnumerable<AccountVoucher>> GetVouchersByTypeAsync(int voucherTypeId, DateTime? fromDate = null, DateTime? toDate = null);

    /// <summary>
    /// Get all vouchers with optional date filtering
    /// </summary>
    Task<IEnumerable<AccountVoucher>> GetVouchersAsync(DateTime? fromDate = null, DateTime? toDate = null);
    
    // ========== UPDATE OPERATIONS ==========
    
    /// <summary>
    /// Post a draft voucher
    /// </summary>
    Task PostVoucherAsync(int voucherId);
    
    /// <summary>
    /// Reverse a posted voucher
    /// </summary>
    Task<AccountVoucher> ReverseVoucherAsync(int voucherId, string reason, int userId);
    
    // ========== HELPERS ==========
    
    /// <summary>
    /// Generate unique voucher code
    /// </summary>
    Task<string> GenerateVoucherCodeAsync(int voucherTypeId);

    /// <summary>
    /// Get all voucher types
    /// </summary>
    Task<IEnumerable<AccountVoucherType>> GetVoucherTypesAsync();
}

// ========== REQUEST DTOS ==========

public class CreateVoucherRequest
{
    public int VoucherTypeId { get; set; }
    public DateTime VoucherDate { get; set; } = DateTime.Now;
    public string? SourceTable { get; set; }
    public int? SourceId { get; set; }
    public int? StoreId { get; set; }
    public string? Narration { get; set; }
    public int CreatedBy { get; set; }
    public List<CreateVoucherLineRequest> Lines { get; set; } = new();
}

public class CreateVoucherLineRequest
{
    public int AccountId { get; set; }
    public decimal Dr { get; set; }
    public decimal Cr { get; set; }
    public int? ProductId { get; set; }
    public string? Particulars { get; set; }
    public int? StoreId { get; set; }
}
