namespace PharmaCare.Application.DTOs.Finance;

/// <summary>
/// DTO for outstanding GRN information
/// </summary>
public class GrnOutstandingDto
{
    public int StockMainID { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public int SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ReturnedAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}

/// <summary>
/// DTO for supplier payment summary dashboard
/// </summary>
public class SupplierPaymentSummaryDto
{
    public decimal TotalOutstanding { get; set; }
    public decimal TotalPaidToday { get; set; }
    public decimal TotalPaidThisMonth { get; set; }
    public int UnpaidGrnCount { get; set; }
    public int PartiallyPaidGrnCount { get; set; }
    public List<SupplierOutstandingDto> TopSuppliersByOutstanding { get; set; } = new();
}

/// <summary>
/// DTO for supplier outstanding summary
/// </summary>
public class SupplierOutstandingDto
{
    public int PartyID { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal TotalOutstanding { get; set; }
    public int UnpaidGrnCount { get; set; }
}
