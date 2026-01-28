using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.Inventory;

/// <summary>
/// Represents a stock movement record with mandatory accounting link.
/// GOLDEN RULE: No stock movement without journal entry.
/// </summary>
public class StockMovement
{
    public int StockMovementID { get; set; }

    // ========== LOCATION & PRODUCT ==========

    /// <summary>
    /// Store where the movement occurred
    /// </summary>
    public int Store_ID { get; set; }

    /// <summary>
    /// Product batch affected (required for valuation)
    /// </summary>
    public int ProductBatch_ID { get; set; }

    // ========== MOVEMENT DETAILS ==========

    /// <summary>
    /// Type of movement: SALE, SALE_RETURN, PURCHASE, PURCHASE_RETURN, 
    /// ADJ_INCREASE, ADJ_DECREASE, WRITE_OFF, TRANSFER_OUT, TRANSFER_IN
    /// </summary>
    public string MovementType { get; set; } = string.Empty;

    /// <summary>
    /// Quantity moved: Positive for incoming, Negative for outgoing
    /// </summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal Quantity { get; set; }

    // ========== COSTING (CRITICAL FOR VALUATION) ==========

    /// <summary>
    /// Unit cost at time of movement (from batch CostPrice)
    /// </summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal UnitCost { get; set; }

    /// <summary>
    /// Total cost = |Quantity| * UnitCost (absolute value for reporting)
    /// </summary>
    [Column(TypeName = "decimal(18, 4)")]
    public decimal TotalCost { get; set; }

    // ========== REFERENCES ==========

    /// <summary>
    /// Reference type: Sale, GRN, StockAdjustment, PurchaseReturn, WriteOff, Transfer
    /// </summary>
    public string ReferenceType { get; set; } = string.Empty;

    /// <summary>
    /// Reference number (e.g., S-2026-00001, GRN-2026-00001)
    /// </summary>
    public string ReferenceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Reference ID (SaleID, GrnID, etc.) for linking
    /// </summary>
    public int? ReferenceID { get; set; }

    // ========== MANDATORY ACCOUNTING LINK ==========



    // ========== TRANSFER LINKING ==========

    /// <summary>
    /// For transfers: ID of the paired movement (OUT links to IN, IN links to OUT)
    /// </summary>
    public int? RelatedMovement_ID { get; set; }

    // ========== AUDIT ==========

    public int CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.Now;

    // ========== NAVIGATION PROPERTIES ==========

    public virtual Store? Store { get; set; }
    public virtual ProductBatch? ProductBatch { get; set; }

    
    /// <summary>
    /// Account Voucher (Replaces JournalEntry)
    /// </summary>
    [ForeignKey("AccountVoucher")]
    public int? Voucher_ID { get; set; }
    public virtual AccountVoucher? AccountVoucher { get; set; }
    public virtual StockMovement? RelatedMovement { get; set; }
}
