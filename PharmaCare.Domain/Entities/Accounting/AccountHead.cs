using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PharmaCare.Domain.Entities.Accounting;

/// <summary>
/// Level 2: Account Head
/// Examples: Assets, Liabilities, Equity, Revenue, Expenses
/// </summary>
public class AccountHead
{
    [Key]
    public int AccountHeadID { get; set; }

    [Required]
    [StringLength(100)]
    public string HeadName { get; set; } = string.Empty;

    [ForeignKey("AccountFamily")]
    public int AccountFamily_ID { get; set; }
    public AccountFamily? AccountFamily { get; set; }

    // Navigation
    public ICollection<AccountSubhead> AccountSubheads { get; set; } = new List<AccountSubhead>();
}
