using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Domain.Entities.Transactions;

/// <summary>
/// Stock Detail - Line items for stock transactions.
/// </summary>
public class StockDetail
{
    [Key]
    public int StockDetailID { get; set; }

    [ForeignKey("StockMain")]
    public int StockMain_ID { get; set; }
    public StockMain? StockMain { get; set; }

    [ForeignKey("Product")]
    public int Product_ID { get; set; }
    public Product? Product { get; set; }

    /// <summary>
    /// Quantity (always positive, direction from TransactionType)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; }

    /// <summary>
    /// Selling/unit price
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Cost price for COGS calculation
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPrice { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// Line total = (Quantity * UnitPrice) - DiscountAmount
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    /// <summary>
    /// Line cost = Quantity * CostPrice
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal LineCost { get; set; }

    [StringLength(200)]
    public string? Remarks { get; set; }
}
