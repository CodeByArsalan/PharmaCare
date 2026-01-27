using PharmaCare.Domain.Models.Base;
using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Domain.Models.AccountManagement;

/// <summary>
/// Financial Head - second-level grouping under Family (e.g., Cash, Bank under Assets)
/// </summary>
public class Head : BaseModelWithStatus
{
    [Key]
    public int HeadID { get; set; }

    /// <summary>
    /// Family type: Assets, Revenue, Expense, Capital, Liability
    /// </summary>
    [Required(ErrorMessage = "Family is required")]
    [StringLength(50)]
    [Display(Name = "Family")]
    public string Family { get; set; } = "Assets";

    /// <summary>
    /// Display name for the head
    /// </summary>
    [Required(ErrorMessage = "Head Name is required")]
    [StringLength(100)]
    [Display(Name = "Head Name")]
    public string HeadName { get; set; } = string.Empty;

    // Navigation Properties
    public virtual ICollection<Subhead> Subheads { get; set; } = new List<Subhead>();
    public virtual ICollection<ChartOfAccount> ChartOfAccounts { get; set; } = new List<ChartOfAccount>();
}
