using PharmaCare.Domain.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Domain.Models.AccountManagement;

/// <summary>
/// Financial Subhead - third-level grouping under Head
/// </summary>
public class Subhead : BaseModelWithStatus
{
    [Key]
    public int SubheadID { get; set; }

    /// <summary>
    /// Foreign key to parent Head
    /// </summary>
    [Required(ErrorMessage = "Head is required")]
    [Display(Name = "Head")]
    public int Head_ID { get; set; }

    /// <summary>
    /// Display name for the subhead
    /// </summary>
    [Required(ErrorMessage = "Subhead Name is required")]
    [StringLength(100)]
    [Display(Name = "Subhead Name")]
    public string SubheadName { get; set; } = string.Empty;

    // Navigation Properties
    public virtual Head? Head { get; set; }
    public virtual ICollection<ChartOfAccount> ChartOfAccounts { get; set; } = new List<ChartOfAccount>();
}
