using System.ComponentModel.DataAnnotations;

namespace PharmaCare.Web.ViewModels.Accounting;

public class JournalVoucherViewModel
{
    public int VoucherID { get; set; }

    [Display(Name = "Voucher No")]
    public string VoucherNo { get; set; } = "Auto-Generated";

    [Required]
    [Display(Name = "Voucher Type")]
    public int VoucherType_ID { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Voucher Date")]
    public DateTime VoucherDate { get; set; } = DateTime.Today;

    [Required]
    [StringLength(500)]
    public string Narration { get; set; } = string.Empty;

    public List<JournalVoucherDetailViewModel> VoucherDetails { get; set; } = new List<JournalVoucherDetailViewModel>();
    
    // Read-only totals
    public decimal TotalDebit => VoucherDetails.Sum(d => d.DebitAmount);
    public decimal TotalCredit => VoucherDetails.Sum(d => d.CreditAmount);
}

public class JournalVoucherDetailViewModel
{
    public int VoucherDetailID { get; set; }
    
    [Required(ErrorMessage = "Account is required")]
    public int Account_ID { get; set; }
    
    public string AccountName { get; set; } = string.Empty; // For display

    [Range(0, double.MaxValue)]
    public decimal DebitAmount { get; set; }

    [Range(0, double.MaxValue)]
    public decimal CreditAmount { get; set; }

    [StringLength(200)]
    public string? Description { get; set; }
}
