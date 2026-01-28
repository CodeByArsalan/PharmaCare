using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Configuration;

/// <summary>
/// Product sub-category under a parent category.
/// </summary>
public class SubCategory : BaseEntityWithStatus
{
    [Key]
    public int SubCategoryID { get; set; }

    [ForeignKey("Category")]
    public int Category_ID { get; set; }
    public Category? Category { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // Navigation
    public ICollection<Product> Products { get; set; } = new List<Product>();
}
