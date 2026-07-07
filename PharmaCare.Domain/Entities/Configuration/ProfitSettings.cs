using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Configuration;

/// <summary>
/// Global profit settings for calculating sale prices.
/// This acts as a single-row configuration table.
/// </summary>
public class ProfitSettings : ITenantEntity
{
    // Tenant (pharmacy) that owns this row. Auto-filtered and stamped by the DbContext.
    public int Pharmacy_ID { get; set; }

    [Key]
    public int SettingsID { get; set; }

    /// <summary>
    /// Percentage added to cost price for retail sale price.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal RetailProfitPercent { get; set; }

    /// <summary>
    /// Percentage added to cost price for wholesale box price calculation.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal WholesaleProfitPercent { get; set; }

    /// <summary>
    /// Currency step that formula-derived (cost + margin) sale prices are rounded to the nearest of.
    /// e.g. 1.00 rounds 47.30 → 47. A value of 0 disables rounding. Applies only to computed prices,
    /// never to explicit per-product prices.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PriceRoundingStep { get; set; } = 1.00m;

    public DateTime UpdatedAt { get; set; }
    
    public int UpdatedBy { get; set; }
}
