using PharmaCare.Application.ViewModels.Report;

namespace PharmaCare.Application.Interfaces.Reports;

public interface IFinancialReportService
{
    Task<ProfitLossVM> GetProfitLossAsync(DateRangeFilter filter);
    Task<CashFlowReportVM> GetCashFlowReportAsync(DateRangeFilter filter);
    Task<ReceivablesAgingVM> GetReceivablesAgingAsync(DateTime asOfDate);
    Task<PayablesAgingVM> GetPayablesAgingAsync(DateTime asOfDate);
    Task<ExpenseReportVM> GetExpenseReportAsync(DateRangeFilter filter);
    Task<TrialBalanceVM> GetTrialBalanceAsync(DateTime asOfDate);
    Task<GeneralLedgerVM> GetGeneralLedgerAsync(DateRangeFilter filter);
    Task<PartyLedgerVM> GetPartyLedgerAsync(DateRangeFilter filter, string partyType);
}
