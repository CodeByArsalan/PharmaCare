using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Domain.Models.Inventory;

/// <summary>
/// Lookup table for invoice/transaction types
/// </summary>
public class InvoiceType
{
    [Key]
    public int TypeID { get; set; }

    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Category: PURCHASE, SALE, INVENTORY
    /// </summary>
    [MaxLength(20)]
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// +1 = Inbound (increases stock), -1 = Outbound (decreases stock), 0 = No impact
    /// </summary>
    public int Direction { get; set; }

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
