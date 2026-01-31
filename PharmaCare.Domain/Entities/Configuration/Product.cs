using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Configuration;

/// <summary>
/// Product entity with pricing. No batch/expiry tracking.
/// </summary>
public class Product : BaseEntityWithStatus
{
    [Key]
    public int ProductID { get; set; }

    [ForeignKey("Category")]
    public int? Category_ID { get; set; }
    public Category? Category { get; set; }

    [ForeignKey("SubCategory")]
    public int SubCategory_ID { get; set; }
    public SubCategory? SubCategory { get; set; }

    /// <summary>
    /// Auto-generated product code (PRD-0001)
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string? ShortCode { get; set; }

    /// <summary>
    /// Opening price for initial stock
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningPrice { get; set; }

    /// <summary>
    /// Opening quantity for initial stock
    /// </summary>
    public int OpeningQuantity { get; set; }

    /// <summary>
    /// Minimum stock level for reorder alerts
    /// </summary>
    public int ReorderLevel { get; set; }
}
