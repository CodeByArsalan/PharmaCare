namespace PharmaCare.Application.DTOs.Accounting;

/// <summary>
/// DTO for creating/viewing journal entries
/// </summary>
public class JournalEntryDto
{
    public int? JournalEntryID { get; set; }
    public string? EntryNumber { get; set; }
    public DateTime EntryDate { get; set; }
    public DateTime PostingDate { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public string Status { get; set; } = "Draft";
    public string? Source_Table { get; set; }
    public int? Source_ID { get; set; }

    // Reversal tracking
    public int? ReversesEntry_ID { get; set; }
    public string? ReversesEntryNumber { get; set; }
    public int? ReversedByEntry_ID { get; set; }
    public string? ReversedByEntryNumber { get; set; }

    public List<JournalEntryLineDto> Lines { get; set; } = new List<JournalEntryLineDto>();
}

/// <summary>
/// DTO for journal entry lines
/// </summary>
public class JournalEntryLineDto
{
    public int? JournalEntryLineID { get; set; }
    public int LineNumber { get; set; }
    public int? Account_ID { get; set; }
    public string? AccountCode { get; set; } // Used for lookup when Account_ID is not provided
    public string? AccountName { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public string? Description { get; set; }
    public int? Store_ID { get; set; }
}

/// <summary>
/// DTO for chart of account display
/// </summary>
public class ChartOfAccountDto
{
    public int AccountID { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public int Head_ID { get; set; }
    public string HeadName { get; set; } = string.Empty;
    public int Subhead_ID { get; set; }
    public string SubheadName { get; set; } = string.Empty;
    public int AccountType_ID { get; set; }
    public string AccountType { get; set; } = "Cash";
    public string? AccountNo { get; set; }
    public string? IBAN { get; set; }
    public string? AccountAddress { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// DTO for trial balance report
/// </summary>
public class TrialBalanceDto
{
    public DateTime AsOfDate { get; set; }
    public int? StoreId { get; set; }
    public string? StoreName { get; set; }
    public List<TrialBalanceLineDto> Lines { get; set; } = new List<TrialBalanceLineDto>();
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public bool IsBalanced => Math.Abs(TotalDebits - TotalCredits) < 0.01m;
}

public class TrialBalanceLineDto
{
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Family { get; set; } = string.Empty;
    public decimal DebitBalance { get; set; }
    public decimal CreditBalance { get; set; }
}

/// <summary>
/// DTO for general ledger report
/// </summary>
public class GeneralLedgerDto
{
    public ChartOfAccountDto Account { get; set; } = new ChartOfAccountDto();
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public List<GeneralLedgerLineDto> Transactions { get; set; } = new List<GeneralLedgerLineDto>();
    public decimal ClosingBalance { get; set; }
}

public class GeneralLedgerLineDto
{
    public DateTime Date { get; set; }
    public string EntryNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal Balance { get; set; }
}

/// <summary>
/// DTO for balance sheet
/// </summary>
public class BalanceSheetDto
{
    public DateTime AsOfDate { get; set; }
    public List<BalanceSheetSectionDto> Assets { get; set; } = new List<BalanceSheetSectionDto>();
    public List<BalanceSheetSectionDto> Liabilities { get; set; } = new List<BalanceSheetSectionDto>();
    public List<BalanceSheetSectionDto> Equity { get; set; } = new List<BalanceSheetSectionDto>();
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }
    public bool IsBalanced => Math.Abs(TotalAssets - (TotalLiabilities + TotalEquity)) < 0.01m;
}

public class BalanceSheetSectionDto
{
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>
/// DTO for income statement
/// </summary>
public class IncomeStatementDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<IncomeStatementLineDto> Revenue { get; set; } = new List<IncomeStatementLineDto>();
    public List<IncomeStatementLineDto> CostOfGoodsSold { get; set; } = new List<IncomeStatementLineDto>();
    public List<IncomeStatementLineDto> Expenses { get; set; } = new List<IncomeStatementLineDto>();
    public decimal TotalRevenue { get; set; }
    public decimal TotalCOGS { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetIncome { get; set; }
}

public class IncomeStatementLineDto
{
    public string AccountName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
