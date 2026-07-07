using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Configuration;

/// <summary>
/// Append-only, effective-dated audit trail of every explicit sale-price change per
/// product + price type. The row whose <see cref="EffectiveTo"/> is null is the currently
/// effective price. When a price changes, the open row is closed (EffectiveTo stamped) and a
/// new open row is inserted — past rows are never mutated. This makes "what was the price on
/// date X" answerable and gives a full margin history without touching live pricing.
/// </summary>
public class ProductPriceHistory : ITenantEntity
{
    // Tenant (pharmacy) that owns this row. Auto-filtered and stamped by the DbContext.
    public int Pharmacy_ID { get; set; }

    [Key]
    public int ProductPriceHistoryID { get; set; }

    [ForeignKey("Product")]
    public int Product_ID { get; set; }
    public Product? Product { get; set; }

    [ForeignKey("PriceType")]
    public int PriceType_ID { get; set; }
    public PriceType? PriceType { get; set; }

    /// <summary>
    /// Sale price effective for this period. Per-unit for retail, per-box for wholesale — matches
    /// the semantics of <see cref="ProductPrice.SalePrice"/>.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal SalePrice { get; set; }

    /// <summary>Product cost at the moment this price took effect, captured for margin auditing.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPriceAtChange { get; set; }

    public DateTime EffectiveFrom { get; set; }

    /// <summary>Null while this is the currently-effective price; stamped when superseded or removed.</summary>
    public DateTime? EffectiveTo { get; set; }

    public int ChangedBy { get; set; }

    [StringLength(250)]
    public string? ChangeReason { get; set; }
}
