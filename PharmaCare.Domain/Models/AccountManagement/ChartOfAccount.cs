using PharmaCare.Domain.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Domain.Models.AccountManagement;

/// <summary>
/// Chart of Accounts - defines all accounts used in the accounting system
/// </summary>
public class ChartOfAccount : BaseModelWithStatus
{
    [Key]
    public int AccountID { get; set; }

    [Required(ErrorMessage = "Head is required")]
    [Display(Name = "Head")]
    public int Head_ID { get; set; }

    [Required(ErrorMessage = "Subhead is required")]
    [Display(Name = "Subhead")]
    public int Subhead_ID { get; set; }
    [Required(ErrorMessage = "Account Name is required")]
    [StringLength(200)]
    [Display(Name = "Account Name")]
    public string AccountName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Account Type is required")]
    [Display(Name = "Account Type")]
    public int AccountType_ID { get; set; }

    [StringLength(50)]
    [Display(Name = "Account No")]
    public string? AccountNo { get; set; }

    [StringLength(50)]
    public string? IBAN { get; set; }

    [StringLength(500)]
    [Display(Name = "Account Address")]
    public string? AccountAddress { get; set; }

    // Navigation Properties
    public virtual Head? Head { get; set; }
    public virtual Subhead? Subhead { get; set; }
    public virtual AccountType? AccountType { get; set; }

}
