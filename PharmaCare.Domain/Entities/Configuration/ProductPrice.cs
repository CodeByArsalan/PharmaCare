using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Configuration;

public class ProductPrice : BaseEntityWithStatus
{
    [Key]
    public int ProductPriceID { get; set; }

    [ForeignKey("Product")]
    public int Product_ID { get; set; }
    public Product? Product { get; set; }

    [ForeignKey("PriceType")]
    public int PriceType_ID { get; set; }
    public PriceType? PriceType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SalePrice { get; set; }
}
