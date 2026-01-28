using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Products;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.Inventory;

/// <summary>
/// Unified line items table for all inventory transactions.
/// Replaces: GrnItem, PurchaseOrderItem, PurchaseReturnItem, SaleLine, SalesReturnLine, 
///           StockTransferItem, StockTakeItem, and absorbs StockMovement tracking
/// </summary>
public class StockDetail
{
    [Key]
    public int StockDetailID { get; set; }

    [ForeignKey("StockMain")]
    public int StockMain_ID { get; set; }
    public StockMain? StockMain { get; set; }

    // ========== PRODUCT ==========

    [ForeignKey("Product")]
    public int Product_ID { get; set; }
    public Product? Product { get; set; }

    [ForeignKey("ProductBatch")]
    public int? ProductBatch_ID { get; set; }
    public ProductBatch? ProductBatch { get; set; }

    // ========== QUANTITIES ==========

    /// <summary>
    /// Quantity in transaction (always positive, direction from InvoiceType)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Selling price per unit
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Cost price per unit (for COGS calculation)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal PurchasePrice { get; set; }

    // ========== DISCOUNT ==========

    [Column(TypeName = "decimal(5,2)")]
    public decimal? DiscountPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    // ========== CALCULATED ==========

    /// <summary>
    /// Line total = (Quantity * UnitPrice) - DiscountAmount
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    /// <summary>
    /// Line cost = Quantity * PurchasePrice (for COGS)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal LineCost { get; set; }

    // ========== STOCK TAKE SPECIFIC ==========

    /// <summary>
    /// System quantity at time of stock take
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? SystemQuantity { get; set; }

    /// <summary>
    /// Physical count quantity
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? PhysicalQuantity { get; set; }

    /// <summary>
    /// Variance = PhysicalQuantity - SystemQuantity
    /// </summary>
    [NotMapped]
    public decimal? Variance => PhysicalQuantity.HasValue && SystemQuantity.HasValue 
        ? PhysicalQuantity.Value - SystemQuantity.Value 
        : null;

    /// <summary>
    /// Cost of variance for stock take adjustments
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? VarianceCost { get; set; }

    // ========== RETURN SPECIFIC ==========

    [MaxLength(100)]
    public string? ReturnReason { get; set; }

    // ========== MOVEMENT TRACKING (absorbed from StockMovement) ==========

    /// <summary>
    /// Movement type for tracking: SALE, PURCHASE, SALE_RETURN, etc.
    /// Derived from StockMain.InvoiceType but stored for query performance
    /// </summary>
    [MaxLength(30)]
    public string? MovementType { get; set; }

    /// <summary>
    /// Total cost of this movement (for inventory valuation)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? TotalCost { get; set; }
}
