using PharmaCare.Domain.Models.Base;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Models.AccountManagement;

/// <summary>
/// Account Mapping - maps party types (Customer, Supplier, Both) to accounting heads/subheads/accounts
/// </summary>
public class AccountMapping : BaseModelWithStatus
{
    [Key]
    public int AccountMappingID { get; set; }

    /// <summary>
    /// Type of party being mapped: Customer, Supplier, Both
    /// </summary>
    [Required(ErrorMessage = "Party Type is required")]
    [StringLength(20)]
    [Display(Name = "Party Type")]
    public string PartyType { get; set; } = "Customer"; // Customer, Supplier, Both

    /// <summary>
    /// Optional Head mapping
    /// </summary>
    [Display(Name = "Head")]
    public int? Head_ID { get; set; }

    /// <summary>
    /// Optional Subhead mapping
    /// </summary>
    [Display(Name = "Subhead")]
    public int? Subhead_ID { get; set; }

    /// <summary>
    /// Optional direct Account mapping
    /// </summary>
    [Display(Name = "Account")]
    public int? Account_ID { get; set; }

    // Navigation Properties
    [ForeignKey("Head_ID")]
    public virtual Head? Head { get; set; }

    [ForeignKey("Subhead_ID")]
    public virtual Subhead? Subhead { get; set; }

    [ForeignKey("Account_ID")]
    public virtual ChartOfAccount? Account { get; set; }
}
