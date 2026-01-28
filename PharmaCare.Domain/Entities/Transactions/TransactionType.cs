using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Domain.Entities.Transactions;

/// <summary>
/// Transaction Type lookup - defines behavior for stock transactions.
/// </summary>
public class TransactionType
{
    [Key]
    public int TransactionTypeID { get; set; }

    /// <summary>
    /// Short code: PO, GRN, PRTN, SALE, SRTN, TFRO, TFRI, SADJ+, SADJ-
    /// </summary>
    [Required]
    [StringLength(10)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Category: PURCHASE, SALE, INVENTORY
    /// </summary>
    [Required]
    [StringLength(20)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Stock direction: +1 = Inbound, -1 = Outbound, 0 = No impact
    /// </summary>
    public int StockDirection { get; set; }

    /// <summary>
    /// Whether this transaction type affects physical stock
    /// </summary>
    public bool AffectsStock { get; set; } = true;

    /// <summary>
    /// Whether this transaction type creates accounting vouchers
    /// </summary>
    public bool CreatesVoucher { get; set; } = true;

    public bool IsActive { get; set; } = true;
}
