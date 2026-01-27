using PharmaCare.Domain.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Domain.Models.Configuration;

public class Party : BaseModelWithStatus
{
    [Key]
    public int PartyID { get; set; }
    [Required(ErrorMessage = "Party Type is required")]
    [StringLength(20)]
    [Display(Name = "Party Type")]
    public string PartyType { get; set; } = "Customer"; 
    [Required(ErrorMessage = "Party Name is required")]
    [StringLength(200)]
    [Display(Name = "Party Name")]
    public string PartyName { get; set; } = string.Empty;
    [StringLength(30)]
    [Display(Name = "Contact Number")]
    public string? ContactNumber { get; set; }
    [StringLength(50)]
    [Display(Name = "Account Number")]
    public string? AccountNumber { get; set; }
    [StringLength(50)]
    public string? IBAN { get; set; }
    [StringLength(500)]
    [Display(Name = "Account Address")]
    public string? AccountAddress { get; set; }
    [StringLength(500)]
    public string? Address { get; set; }

    public decimal OpeningBalance { get; set; } = 0.0M;
    public decimal CreditLimit { get; set; } = 0.0M;

    [StringLength(100)]
    public string? Market { get; set; }
}
