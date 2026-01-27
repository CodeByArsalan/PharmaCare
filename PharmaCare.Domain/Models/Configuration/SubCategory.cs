using System.ComponentModel.DataAnnotations;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Products;

namespace PharmaCare.Domain.Models.Configuration;

/// <summary>
/// Represents a subcategory within a category
/// </summary>
public class SubCategory : BaseModelWithStatus
{
    [Key]
    public int SubCategoryID { get; set; }

    [Required(ErrorMessage = "Category is required")]
    [Display(Name = "Category")]
    public int Category_ID { get; set; }



    [Required(ErrorMessage = "SubCategory Name is required")]
    [Display(Name = "SubCategory Name")]
    [StringLength(100)]
    public string SubCategoryName { get; set; } = string.Empty;

    // Navigation properties
    public virtual Category? Category { get; set; }
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
