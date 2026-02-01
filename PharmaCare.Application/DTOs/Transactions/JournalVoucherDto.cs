namespace PharmaCare.Application.DTOs.Transactions;

public class JournalVoucherDto
{
    public int VoucherType_ID { get; set; }
    public DateTime VoucherDate { get; set; }
    public string Narration { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<JournalVoucherDetailDto> VoucherDetails { get; set; } = new List<JournalVoucherDetailDto>();
}

public class JournalVoucherDetailDto
{
    public int Account_ID { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Description { get; set; }
}
