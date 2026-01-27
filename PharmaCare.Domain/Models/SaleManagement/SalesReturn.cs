using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Models.Base;
using PharmaCare.Domain.Models.Configuration;
using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Domain.Models.SaleManagement;

/// <summary>
/// Represents a sales return/refund transaction
/// </summary>
public class SalesReturn : BaseModel
{
    [Key]
    public int SalesReturnID { get; set; }

    [Required]
    [StringLength(50)]
    public string ReturnNumber { get; set; } = string.Empty;

    [ForeignKey("Sale")]
    public int Sale_ID { get; set; }

    [ForeignKey("Party")]
    public int? Party_ID { get; set; }

    [ForeignKey("Store")]
    public int Store_ID { get; set; }

    [Required]
    public DateTime ReturnDate { get; set; }

    /// <summary>
    /// Reason for return: Defective, WrongItem, Expired, ChangeOfMind, Other
    /// </summary>
    [StringLength(50)]
    public string ReturnReason { get; set; } = "Other";

    [StringLength(500)]
    public string? ReturnNotes { get; set; }

    /// <summary>
    /// Total refund amount
    /// </summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// Refund method: Cash, Credit (store credit), Exchange
    /// </summary>
    [StringLength(20)]
    public string RefundMethod { get; set; } = "Cash";

    /// <summary>
    /// Status: Pending, Completed, Cancelled
    /// </summary>
    [StringLength(20)]
    public string Status { get; set; } = "Completed";

    /// <summary>
    /// Journal entry for accounting reversal
    /// </summary>
    [ForeignKey("JournalEntry")]
    public int? JournalEntry_ID { get; set; }

    // Navigation properties
    public Sale? Sale { get; set; }
    public Party? Party { get; set; }
    public Store? Store { get; set; }
    public JournalEntry? JournalEntry { get; set; }
    public ICollection<SalesReturnLine> ReturnLines { get; set; } = new List<SalesReturnLine>();
}
