using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Base;

namespace PharmaCare.Domain.Entities.Configuration;

/// <summary>
/// Party entity representing Customers and Suppliers.
/// </summary>
public class Party : BaseEntityWithStatus
{
    [Key]
    public int PartyID { get; set; }

    /// <summary>
    /// Party type: "Customer" or "Supplier"
    /// </summary>
    [Required]
    [StringLength(20)]
    public string PartyType { get; set; } = "Customer";


    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(100)]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(50)]
    public string? ContactNumber { get; set; }

    [StringLength(50)]
    public string? AccountNumber { get; set; }

    [StringLength(50)]
    public string? IBAN { get; set; }

    /// <summary>
    /// Opening balance for the party
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningBalance { get; set; }

    /// <summary>
    /// Maximum credit allowed (for customers)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditLimit { get; set; }
}
