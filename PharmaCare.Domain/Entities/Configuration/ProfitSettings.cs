using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Entities.Configuration;

/// <summary>
/// Global profit settings for calculating sale prices.
/// This acts as a single-row configuration table.
/// </summary>
public class ProfitSettings
{
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

    public DateTime UpdatedAt { get; set; }
    
    public int UpdatedBy { get; set; }
}
