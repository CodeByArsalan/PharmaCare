using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Domain.Entities.Configuration;

/// <summary>
/// Product category with linked accounting accounts.
/// </summary>
public class Category : BaseEntityWithStatus
{
    [Key]
    public int CategoryID { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // Accounting Links
    [ForeignKey("SaleAccount")]
    public int? SaleAccount_ID { get; set; }
    public Account? SaleAccount { get; set; }

    [ForeignKey("StockAccount")]
    public int? StockAccount_ID { get; set; }
    public Account? StockAccount { get; set; }

    [ForeignKey("COGSAccount")]
    public int? COGSAccount_ID { get; set; }
    public Account? COGSAccount { get; set; }

    [ForeignKey("DamageAccount")]
    public int? DamageAccount_ID { get; set; }
    public Account? DamageAccount { get; set; }

    // Navigation
    public ICollection<SubCategory> SubCategories { get; set; } = new List<SubCategory>();
}
