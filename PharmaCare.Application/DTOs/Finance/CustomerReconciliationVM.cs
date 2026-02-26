namespace PharmaCare.Application.DTOs.Finance;

public class CustomerReconciliationVM
{
    public int? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public List<OutstandingSaleVM> OutstandingSales { get; set; } = new();
    public List<OpenCreditNoteVM> OpenCreditNotes { get; set; } = new();
    public decimal TotalOutstanding => OutstandingSales.Sum(s => s.BalanceAmount);
    public decimal TotalAvailableCredit => OpenCreditNotes.Sum(c => c.BalanceAmount);
}

public class OutstandingSaleVM
{
    public int SaleId { get; set; }
    public string TransactionNo { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal ReturnedAmount { get; set; }
    public decimal BalanceAmount { get; set; }
}

public class OpenCreditNoteVM
{
    public int CreditNoteId { get; set; }
    public string CreditNoteNo { get; set; } = string.Empty;
    public DateTime CreditDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public string? SourceTransactionNo { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}
