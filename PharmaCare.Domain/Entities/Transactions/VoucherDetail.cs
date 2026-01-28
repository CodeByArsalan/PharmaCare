using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Domain.Entities.Transactions;

/// <summary>
/// Voucher Detail - Line items for accounting vouchers.
/// Each line debits or credits an account.
/// </summary>
public class VoucherDetail
{
    [Key]
    public int VoucherDetailID { get; set; }

    [ForeignKey("Voucher")]
    public int Voucher_ID { get; set; }
    public Voucher? Voucher { get; set; }

    [ForeignKey("Account")]
    public int Account_ID { get; set; }
    public Account? Account { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DebitAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditAmount { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }

    // Optional references for detailed tracking
    [ForeignKey("Party")]
    public int? Party_ID { get; set; }
    public Party? Party { get; set; }

    [ForeignKey("Product")]
    public int? Product_ID { get; set; }
    public Product? Product { get; set; }
}
