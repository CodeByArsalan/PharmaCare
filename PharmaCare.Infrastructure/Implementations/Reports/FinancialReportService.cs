using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Reports;
using PharmaCare.Application.ViewModels.Report;
using PharmaCare.Domain.Enums;
using PharmaCare.Infrastructure;

namespace PharmaCare.Infrastructure.Implementations.Reports;

public class FinancialReportService : IFinancialReportService
{
    private readonly PharmaCareDBContext _db;
    
    // Transaction-type codes
    private static readonly string[] SaleCodes = { "SALE" };
    private static readonly string[] SaleReturnCodes = { "SRTN" };
    private static readonly string[] PurchaseCodes = { "GRN" };
    private static readonly string[] PurchaseReturnCodes = { "PRTN" };

    public FinancialReportService(PharmaCareDBContext db)
    {
        _db = db;
    }

    public async Task<ProfitLossVM> GetProfitLossAsync(DateRangeFilter filter)
    {
        var fromDate = filter.FromDate;
        var toDate = filter.ToDate.AddDays(1);

        var revenueByType = await _db.StockMains
            .AsNoTracking()
            .Where(s => s.TransactionDate >= fromDate && s.TransactionDate < toDate
                        && s.Status != "Void"
                        && (SaleCodes.Contains(s.TransactionType!.Code) || SaleReturnCodes.Contains(s.TransactionType!.Code)))
            .GroupBy(s => s.TransactionType!.Code)
            .Select(g => new
            {
                Code = g.Key,
                Amount = g.Sum(s => s.TotalAmount)
            })
            .ToListAsync();

        var totalRevenue = revenueByType
            .Where(x => SaleCodes.Contains(x.Code))
            .Sum(x => x.Amount);
        var salesReturns = revenueByType
            .Where(x => SaleReturnCodes.Contains(x.Code))
            .Sum(x => x.Amount);
        var netRevenue = totalRevenue - salesReturns;

        var cogs = await _db.StockDetails
            .AsNoTracking()
            .Where(d => d.StockMain!.TransactionDate >= fromDate
                        && d.StockMain.TransactionDate < toDate
                        && d.StockMain.Status != "Void"
                        && SaleCodes.Contains(d.StockMain.TransactionType!.Code))
            .SumAsync(d => (decimal?)d.LineCost) ?? 0;

        var grossProfit = netRevenue - cogs;

        var expensesByCategory = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate < toDate)
            .GroupBy(e => e.ExpenseCategory != null ? e.ExpenseCategory.Name : "Uncategorized")
            .Select(g => new ExpenseCategoryTotal
            {
                CategoryName = g.Key,
                Amount = g.Sum(e => e.Amount)
            })
            .OrderByDescending(e => e.Amount)
            .ToList();

        var totalExpenses = expensesByCategory.Sum(e => e.Amount);

        return new ProfitLossVM
        {
            Filter = filter,
            TotalRevenue = totalRevenue,
            SalesReturns = salesReturns,
            NetRevenue = netRevenue,
            COGS = cogs,
            GrossProfit = grossProfit,
            GrossProfitMargin = netRevenue == 0 ? 0 : Math.Round(grossProfit / netRevenue * 100, 2),
            ExpensesByCategory = expensesByCategory,
            TotalExpenses = totalExpenses,
            NetProfit = grossProfit - totalExpenses,
            NetProfitMargin = netRevenue == 0 ? 0 : Math.Round((grossProfit - totalExpenses) / netRevenue * 100, 2)
        };
    }

    public async Task<CashFlowReportVM> GetCashFlowReportAsync(DateRangeFilter filter)
    {
        var fromDate = filter.FromDate;
        var toDate = filter.ToDate.AddDays(1);

        // Pre-aggregate payments once by day and type
        var dailyPaymentTotals = await _db.Payments
            .AsNoTracking()
            .Where(p => p.PaymentDate >= fromDate
                        && p.PaymentDate < toDate
                        && p.PaymentMethod.ToUpper() != PaymentMethod.Adjustment.ToString().ToUpper()
                        && (p.PaymentType == PaymentType.RECEIPT.ToString() || p.PaymentType == PaymentType.PAYMENT.ToString()))
            .GroupBy(p => new { Date = p.PaymentDate.Date, p.PaymentType })
            .Select(g => new
            {
                g.Key.Date,
                g.Key.PaymentType,
                Amount = g.Sum(p => p.Amount)
            })
            .ToListAsync();

        // Pre-aggregate expenses once by day
        var dailyExpenseTotals = await _db.Expenses
            .AsNoTracking()
            .Where(e => e.ExpenseDate >= fromDate && e.ExpenseDate < toDate)
            .GroupBy(e => e.ExpenseDate.Date)
            .Select(g => new
            {
                Date = g.Key,
                Amount = g.Sum(e => e.Amount)
            })
            .ToListAsync();

        var receiptsByDate = dailyPaymentTotals
            .Where(x => x.PaymentType == PaymentType.RECEIPT.ToString())
            .ToDictionary(x => x.Date, x => x.Amount);

        var paymentsByDate = dailyPaymentTotals
            .Where(x => x.PaymentType == PaymentType.PAYMENT.ToString())
            .ToDictionary(x => x.Date, x => x.Amount);

        var expensesByDate = dailyExpenseTotals
            .ToDictionary(x => x.Date, x => x.Amount);

        var totalCashIn = receiptsByDate.Values.Sum();
        var purchasePayments = paymentsByDate.Values.Sum();
        var expensePayments = expensesByDate.Values.Sum();
        var totalCashOut = purchasePayments + expensePayments;

        // Daily data for chart
        var allDays = Enumerable.Range(0, (int)(filter.ToDate - filter.FromDate).TotalDays + 1)
            .Select(i => filter.FromDate.AddDays(i))
            .ToList();

        var dailyData = new List<DailyCashFlowData>(allDays.Count);
        foreach (var day in allDays)
        {
            var dayKey = day.Date;
            var dayIn = receiptsByDate.TryGetValue(dayKey, out var receiptAmt) ? receiptAmt : 0;
            var dayPaymentOut = paymentsByDate.TryGetValue(dayKey, out var paymentAmt) ? paymentAmt : 0;
            var dayExpenseOut = expensesByDate.TryGetValue(dayKey, out var expenseAmt) ? expenseAmt : 0;
            var dayOut = dayPaymentOut + dayExpenseOut;

            dailyData.Add(new DailyCashFlowData
            {
                Date = day,
                CashIn = dayIn,
                CashOut = dayOut,
                NetFlow = dayIn - dayOut
            });
        }

        return new CashFlowReportVM
        {
            Filter = filter,
            SalesReceipts = totalCashIn, // Assuming mostly Sales Receipts
            CustomerPayments = 0, // Merged
            TotalCashIn = totalCashIn,
            PurchasePayments = purchasePayments,
            SupplierPayments = 0,
            ExpensePayments = expensePayments,
            TotalCashOut = totalCashOut,
            ClosingBalance = totalCashIn - totalCashOut,
            DailyData = dailyData
        };
    }

    public async Task<ReceivablesAgingVM> GetReceivablesAgingAsync(DateTime asOfDate)
    {
        var rows = await _db.StockMains
            .AsNoTracking()
            .Where(s => SaleCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate <= asOfDate
                        && s.BalanceAmount > 0
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .GroupBy(s => new { PartyId = s.Party_ID!.Value, PartyName = s.Party != null ? s.Party.Name : "Unknown" })
            .Select(g => new AgingRow
            {
                PartyId = g.Key.PartyId,
                PartyName = g.Key.PartyName,
                Current = g.Sum(s => EF.Functions.DateDiffDay(s.TransactionDate, asOfDate) <= 30 ? s.BalanceAmount : 0),
                Days31_60 = g.Sum(s =>
                    EF.Functions.DateDiffDay(s.TransactionDate, asOfDate) > 30
                    && EF.Functions.DateDiffDay(s.TransactionDate, asOfDate) <= 60
                        ? s.BalanceAmount
                        : 0),
                Days61_90 = g.Sum(s =>
                    EF.Functions.DateDiffDay(s.TransactionDate, asOfDate) > 60
                    && EF.Functions.DateDiffDay(s.TransactionDate, asOfDate) <= 90
                        ? s.BalanceAmount
                        : 0),
                Days90Plus = g.Sum(s => EF.Functions.DateDiffDay(s.TransactionDate, asOfDate) > 90 ? s.BalanceAmount : 0),
                Total = g.Sum(s => s.BalanceAmount)
            })
            .OrderByDescending(r => r.Total)
            .ToListAsync();

        return new ReceivablesAgingVM
        {
            AsOfDate = asOfDate,
            Rows = rows,
            TotalCurrent = rows.Sum(r => r.Current),
            Total31_60 = rows.Sum(r => r.Days31_60),
            Total61_90 = rows.Sum(r => r.Days61_90),
            Total90Plus = rows.Sum(r => r.Days90Plus),
            GrandTotal = rows.Sum(r => r.Total)
        };
    }

    public async Task<PayablesAgingVM> GetPayablesAgingAsync(DateTime asOfDate)
    {
        var purchases = await _db.StockMains
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => PurchaseCodes.Contains(s.TransactionType!.Code)
                        && s.TransactionDate <= asOfDate
                        && s.BalanceAmount > 0
                        && s.Status != "Void"
                        && s.Party_ID != null)
            .ToListAsync();

        var rows = purchases
            .GroupBy(s => s.Party_ID)
            .Select(g =>
            {
                var row = new AgingRow
                {
                    PartyId = g.Key ?? 0,
                    PartyName = g.First().Party?.Name ?? "Unknown"
                };

                foreach (var s in g)
                {
                    var daysOld = (int)(asOfDate - s.TransactionDate).TotalDays;
                    if (daysOld <= 30) row.Current += s.BalanceAmount;
                    else if (daysOld <= 60) row.Days31_60 += s.BalanceAmount;
                    else if (daysOld <= 90) row.Days61_90 += s.BalanceAmount;
                    else row.Days90Plus += s.BalanceAmount;
                }

                row.Total = row.Current + row.Days31_60 + row.Days61_90 + row.Days90Plus;
                return row;
            })
            .OrderByDescending(r => r.Total)
            .ToList();

        return new PayablesAgingVM
        {
            AsOfDate = asOfDate,
            Rows = rows,
            TotalCurrent = rows.Sum(r => r.Current),
            Total31_60 = rows.Sum(r => r.Days31_60),
            Total61_90 = rows.Sum(r => r.Days61_90),
            Total90Plus = rows.Sum(r => r.Days90Plus),
            GrandTotal = rows.Sum(r => r.Total)
        };
    }

    public async Task<ExpenseReportVM> GetExpenseReportAsync(DateRangeFilter filter)
    {
        var query = _db.Expenses
            .Include(e => e.ExpenseCategory)
            .Include(e => e.SourceAccount)
            .Where(e => e.ExpenseDate >= filter.FromDate
                        && e.ExpenseDate < filter.ToDate.AddDays(1));

        if (filter.ExpenseCategoryId.HasValue)
            query = query.Where(e => e.ExpenseCategory_ID == filter.ExpenseCategoryId.Value);

        var expenses = await query.OrderByDescending(e => e.ExpenseDate).ToListAsync();

        var rows = expenses.Select(e => new ExpenseReportRow
        {
            ExpenseId = e.ExpenseID,
            ExpenseDate = e.ExpenseDate,
            CategoryName = e.ExpenseCategory?.Name ?? "",
            Description = e.Description ?? "",
            Amount = e.Amount,
            SourceAccountName = e.SourceAccount?.Name ?? "",
            Reference = e.Reference
        }).ToList();

        var categoryTotals = rows
            .GroupBy(r => r.CategoryName)
            .Select(g => new ExpenseCategoryTotal
            {
                CategoryName = g.Key,
                Amount = g.Sum(r => r.Amount)
            })
            .OrderByDescending(c => c.Amount)
            .ToList();

        return new ExpenseReportVM
        {
            Filter = filter,
            Rows = rows,
            CategoryTotals = categoryTotals,
            GrandTotal = rows.Sum(r => r.Amount)
        };
    }

    public async Task<TrialBalanceVM> GetTrialBalanceAsync(DateTime asOfDate)
    {
        var accountTotals = await _db.VoucherDetails
            .AsNoTracking()
            .Where(vd => vd.Voucher!.VoucherDate <= asOfDate
                         && vd.Voucher.Status == "Posted")
            .GroupBy(vd => new
            {
                vd.Account_ID,
                AccountName = vd.Account != null ? vd.Account.Name : "",
                AccountHeadName = vd.Account != null && vd.Account.AccountHead != null ? vd.Account.AccountHead.HeadName : "",
                AccountTypeName = vd.Account != null && vd.Account.AccountType != null ? vd.Account.AccountType.Name : ""
            })
            .Select(g => new
            {
                g.Key.Account_ID,
                g.Key.AccountName,
                g.Key.AccountHeadName,
                g.Key.AccountTypeName,
                DebitTotal = g.Sum(vd => vd.DebitAmount),
                CreditTotal = g.Sum(vd => vd.CreditAmount)
            })
            .Where(x => x.DebitTotal != 0 || x.CreditTotal != 0)
            .OrderBy(x => x.AccountHeadName).ThenBy(x => x.AccountName)
            .ToListAsync();

        var rows = accountTotals.Select(x =>
        {
            var balance = x.DebitTotal - x.CreditTotal;
            return new TrialBalanceRow
            {
                AccountId = x.Account_ID,
                AccountName = x.AccountName,
                AccountHeadName = x.AccountHeadName,
                AccountTypeName = x.AccountTypeName,
                DebitTotal = x.DebitTotal,
                CreditTotal = x.CreditTotal,
                Balance = Math.Abs(balance),
                BalanceType = balance >= 0 ? "Dr" : "Cr"
            };
        }).ToList();

        var totalDebit = rows.Sum(r => r.DebitTotal);
        var totalCredit = rows.Sum(r => r.CreditTotal);

        return new TrialBalanceVM
        {
            AsOfDate = asOfDate,
            Rows = rows,
            TotalDebit = totalDebit,
            TotalCredit = totalCredit,
            IsBalanced = totalDebit == totalCredit
        };
    }

    public async Task<GeneralLedgerVM> GetGeneralLedgerAsync(DateRangeFilter filter)
    {
        if (!filter.AccountId.HasValue)
            return new GeneralLedgerVM { Filter = filter };

        var account = await _db.Accounts.FindAsync(filter.AccountId.Value);
        if (account == null)
            return new GeneralLedgerVM { Filter = filter };

        // Opening balance (all voucher details before FromDate)
        var priorEntries = await _db.VoucherDetails
            .Include(vd => vd.Voucher)
            .Where(vd => vd.Account_ID == filter.AccountId.Value
                         && vd.Voucher!.VoucherDate < filter.FromDate
                         && vd.Voucher.Status == "Posted")
            .ToListAsync();

        decimal openingBalance = priorEntries.Sum(vd => vd.DebitAmount - vd.CreditAmount);

        // Period entries
        var entries = await _db.VoucherDetails
            .Include(vd => vd.Voucher)
            .Where(vd => vd.Account_ID == filter.AccountId.Value
                         && vd.Voucher!.VoucherDate >= filter.FromDate
                         && vd.Voucher.VoucherDate < filter.ToDate.AddDays(1)
                         && vd.Voucher.Status == "Posted")
            .OrderBy(vd => vd.Voucher!.VoucherDate)
            .ThenBy(vd => vd.VoucherDetailID)
            .ToListAsync();

        var runningBalance = openingBalance;
        var rows = entries.Select(vd =>
        {
            runningBalance += vd.DebitAmount - vd.CreditAmount;
            return new GeneralLedgerRow
            {
                Date = vd.Voucher!.VoucherDate,
                VoucherNo = vd.Voucher.VoucherNo,
                Narration = vd.Description ?? vd.Voucher.Narration ?? "",
                Debit = vd.DebitAmount,
                Credit = vd.CreditAmount,
                RunningBalance = runningBalance
            };
        }).ToList();

        return new GeneralLedgerVM
        {
            Filter = filter,
            AccountName = account.Name,
            Rows = rows,
            OpeningBalance = openingBalance,
            TotalDebit = rows.Sum(r => r.Debit),
            TotalCredit = rows.Sum(r => r.Credit),
            ClosingBalance = runningBalance
        };
    }

    public async Task<PartyLedgerVM> GetPartyLedgerAsync(DateRangeFilter filter, string partyType)
    {
        if (!filter.PartyId.HasValue)
            return new PartyLedgerVM { Filter = filter, PartyType = partyType };

        var party = await _db.Parties.FindAsync(filter.PartyId.Value);
        if (party == null)
            return new PartyLedgerVM { Filter = filter, PartyType = partyType };

        var isCustomer = partyType == "Customer";
        var relevantSaleCodes = isCustomer ? SaleCodes : PurchaseCodes;
        var relevantReturnCodes = isCustomer ? SaleReturnCodes : PurchaseReturnCodes;
        var paymentType = isCustomer ? PaymentType.RECEIPT.ToString() : PaymentType.PAYMENT.ToString();

        // Opening balance: transactions before FromDate
        var priorTransactions = await _db.StockMains
            .Include(s => s.TransactionType)
            .Where(s => s.Party_ID == filter.PartyId.Value
                        && s.TransactionDate < filter.FromDate
                        && s.Status != "Void"
                        && (relevantSaleCodes.Contains(s.TransactionType!.Code) || relevantReturnCodes.Contains(s.TransactionType!.Code)))
            .ToListAsync();

        var priorPayments = await _db.Payments
            .Where(p => p.Party_ID == filter.PartyId.Value
                        && p.PaymentDate < filter.FromDate
                        && p.PaymentType == paymentType)
            .ToListAsync();

        // For customer: Sale = Debit (adds to receivable), Receipt = Credit (reduces receivable)
        // For supplier: Purchase = Credit (adds to payable), Payment/Return = Debit (reduces payable)
        decimal openingBalance = party.OpeningBalance;
        if (isCustomer)
        {
            openingBalance += priorTransactions.Where(s => SaleCodes.Contains(s.TransactionType!.Code)).Sum(s => s.TotalAmount);
            openingBalance -= priorTransactions.Where(s => SaleReturnCodes.Contains(s.TransactionType!.Code)).Sum(s => s.TotalAmount);
            openingBalance -= priorPayments.Sum(p => p.Amount);
        }
        else
        {
            openingBalance += priorTransactions.Where(s => PurchaseCodes.Contains(s.TransactionType!.Code)).Sum(s => s.TotalAmount);
            openingBalance -= priorTransactions.Where(s => PurchaseReturnCodes.Contains(s.TransactionType!.Code)).Sum(s => s.TotalAmount);
            openingBalance -= priorPayments.Sum(p => p.Amount);
        }

        // Period transactions
        var periodTransactions = await _db.StockMains
            .Include(s => s.TransactionType)
            .Where(s => s.Party_ID == filter.PartyId.Value
                        && s.TransactionDate >= filter.FromDate
                        && s.TransactionDate < filter.ToDate.AddDays(1)
                        && s.Status != "Void"
                        && (relevantSaleCodes.Contains(s.TransactionType!.Code) || relevantReturnCodes.Contains(s.TransactionType!.Code)))
            .ToListAsync();

        var periodPayments = await _db.Payments
            .Where(p => p.Party_ID == filter.PartyId.Value
                        && p.PaymentDate >= filter.FromDate
                        && p.PaymentDate < filter.ToDate.AddDays(1)
                        && p.PaymentType == paymentType)
            .ToListAsync();

        // Merge and sort by date
        var ledgerEntries = new List<(DateTime Date, string Ref, string Desc, decimal Debit, decimal Credit)>();

        foreach (var s in periodTransactions)
        {
            if (isCustomer)
            {
                if (SaleCodes.Contains(s.TransactionType!.Code))
                    ledgerEntries.Add((s.TransactionDate, s.TransactionNo, "Sale", s.TotalAmount, 0));
                else
                    ledgerEntries.Add((s.TransactionDate, s.TransactionNo, "Sale Return", 0, s.TotalAmount));
            }
            else
            {
                if (PurchaseCodes.Contains(s.TransactionType!.Code))
                    ledgerEntries.Add((s.TransactionDate, s.TransactionNo, "Purchase", 0, s.TotalAmount));
                else
                    ledgerEntries.Add((s.TransactionDate, s.TransactionNo, "Purchase Return", s.TotalAmount, 0));
            }
        }

        foreach (var p in periodPayments)
        {
            if (isCustomer)
                ledgerEntries.Add((p.PaymentDate, p.Reference ?? $"PMT-{p.PaymentID}", "Payment Received", 0, p.Amount));
            else
                ledgerEntries.Add((p.PaymentDate, p.Reference ?? $"PMT-{p.PaymentID}", "Payment Made", p.Amount, 0));
        }

        ledgerEntries = ledgerEntries.OrderBy(e => e.Date).ToList();

        var runningBalance = openingBalance;
        var rows = ledgerEntries.Select(e =>
        {
            // Customer balance follows Dr-Cr; supplier payable balance follows Cr-Dr.
            runningBalance += isCustomer ? (e.Debit - e.Credit) : (e.Credit - e.Debit);
            return new PartyLedgerRow
            {
                Date = e.Date,
                Reference = e.Ref,
                Description = e.Desc,
                Debit = e.Debit,
                Credit = e.Credit,
                RunningBalance = runningBalance
            };
        }).ToList();

        return new PartyLedgerVM
        {
            Filter = filter,
            PartyName = party.Name,
            PartyType = partyType,
            Rows = rows,
            OpeningBalance = openingBalance,
            TotalDebit = rows.Sum(r => r.Debit),
            TotalCredit = rows.Sum(r => r.Credit),
            ClosingBalance = runningBalance
        };
    }
}
