using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Configuration;

public class PriceType : BaseEntityWithStatus, ITenantEntity
{
    // Tenant (pharmacy) that owns this row. Auto-filtered and stamped by the DbContext.
    public int Pharmacy_ID { get; set; }

    [Key]
    public int PriceTypeID { get; set; }

    [Required]
    [StringLength(100)]
    public string PriceTypeName { get; set; } = string.Empty;
}
