using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;

namespace PharmaCare.Domain.Models.SaleManagement;

/// <summary>
/// Represents a sales quotation/estimate that can be converted to a sale
/// </summary>
public class Quotation : BaseModel
{
    [Key]
    public int QuotationID { get; set; }

    [Required]
    [StringLength(50)]
    public string QuotationNumber { get; set; } = string.Empty;

    [ForeignKey("Store")]
    public int Store_ID { get; set; }

    [ForeignKey("Party")]
    public int? Party_ID { get; set; }

    [StringLength(200)]
    public string? CustomerName { get; set; }

    [StringLength(50)]
    public string? CustomerPhone { get; set; }

    public DateTime QuotationDate { get; set; }

    /// <summary>
    /// Quote valid until this date
    /// </summary>
    public DateTime ValidUntil { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal DiscountPercent { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// Status: Draft, Sent, Converted, Expired, Cancelled
    /// </summary>
    [StringLength(20)]
    public string Status { get; set; } = "Draft";

    /// <summary>
    /// If converted, links to the created sale
    /// </summary>
    [ForeignKey("ConvertedSale")]
    public int? ConvertedSale_ID { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    // Navigation properties
    public Store? Store { get; set; }
    public Party? Party { get; set; }
    public Sale? ConvertedSale { get; set; }
    public ICollection<QuotationLine> QuotationLines { get; set; } = new List<QuotationLine>();
}
