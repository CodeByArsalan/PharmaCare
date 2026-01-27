using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Finance;
using PharmaCare.Application.Utilities;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.Finance;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.PurchaseManagement;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Accounting;
using PharmaCare.Infrastructure;

namespace PharmaCare.Application.Implementations.Finance;

public class SupplierPaymentService : ISupplierPaymentService
{
    private readonly IRepository<SupplierPayment> _paymentRepo;
    private readonly IRepository<Grn> _grnRepo;
    private readonly IAccountingService _accountingService;
    private readonly IJournalPostingEngine _postingEngine;
    private readonly PharmaCareDBContext _context;

    // Account Type IDs
    private const int CASH_ACCOUNT_TYPE = 1;
    private const int BANK_ACCOUNT_TYPE = 2;
    private const int SUPPLIER_ACCOUNT_TYPE = 4;

    public SupplierPaymentService(
        IRepository<SupplierPayment> paymentRepo,
        IRepository<Grn> grnRepo,
        IAccountingService accountingService,
        IJournalPostingEngine postingEngine,
        PharmaCareDBContext context)
    {
        _paymentRepo = paymentRepo;
        _grnRepo = grnRepo;
        _accountingService = accountingService;
        _postingEngine = postingEngine;
        _context = context;
    }

    #region CRUD Operations

    public async Task<List<SupplierPayment>> GetAllPayments(int? supplierId = null, string? status = null)
    {
        var query = _paymentRepo.FindByCondition(p => p.IsActive);

        if (supplierId.HasValue)
            query = query.Where(p => p.Party_ID == supplierId.Value);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(p => p.Status == status);

        return await query
            .Include(p => p.Party)
            .Include(p => p.Grn)
            .Include(p => p.Store)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public async Task<SupplierPayment?> GetPaymentById(int id)
    {
        return await _paymentRepo.FindByCondition(p => p.SupplierPaymentID == id)
            .Include(p => p.Party)
            .Include(p => p.Grn)
            .Include(p => p.Store)
            .Include(p => p.JournalEntry)
            .FirstOrDefaultAsync();
    }

    public async Task<List<SupplierPayment>> GetPaymentsByGrn(int grnId)
    {
        return await _paymentRepo.FindByCondition(p => p.Grn_ID == grnId && p.IsActive)
            .Include(p => p.Party)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();
    }

    public Task<string> GeneratePaymentNumber()
    {
        return Task.FromResult(UniqueIdGenerator.Generate("SP"));
    }

    #endregion

    #region Outstanding GRNs

    public async Task<List<GrnOutstandingDto>> GetOutstandingGrns(int? supplierId = null)
    {
        var query = _grnRepo.FindByCondition(g => g.PaymentStatus != "Paid");

        if (supplierId.HasValue)
            query = query.Where(g => g.Party_ID == supplierId.Value);

        var grns = await query
            .Include(g => g.Party)
            .Include(g => g.GrnItems)
            .OrderByDescending(g => g.CreatedDate)
            .ToListAsync();

        return grns.Select(g => new GrnOutstandingDto
        {
            GrnID = g.GrnID,
            GrnNumber = g.GrnNumber,
            CreatedDate = g.CreatedDate,
            SupplierId = g.Party_ID ?? 0,
            SupplierName = g.Party?.PartyName ?? "Unknown",
            TotalAmount = g.TotalAmount > 0 ? g.TotalAmount : g.GrnItems.Sum(i => i.QuantityReceived * i.CostPrice),
            AmountPaid = g.AmountPaid,
            ReturnedAmount = g.ReturnedAmount,
            BalanceAmount = g.TotalAmount - g.AmountPaid - g.ReturnedAmount,
            PaymentStatus = g.PaymentStatus
        }).ToList();
    }

    public async Task<decimal> GetTotalOutstandingForSupplier(int supplierId)
    {
        var grns = await _grnRepo.FindByCondition(g => g.Party_ID == supplierId && g.PaymentStatus != "Paid")
            .Include(g => g.GrnItems)
            .ToListAsync();

        return grns.Sum(g => g.TotalAmount - g.AmountPaid - g.ReturnedAmount);
    }

    #endregion

    #region Payment Processing

    public async Task<bool> CreatePayment(SupplierPayment payment, int loginUserId)
    {
        // 1. Validate GRN exists and has balance
        var grn = await _grnRepo.FindByCondition(g => g.GrnID == payment.Grn_ID)
            .Include(g => g.GrnItems)
            .FirstOrDefaultAsync();

        if (grn == null)
            throw new InvalidOperationException("GRN not found");

        // Calculate GRN total if not set
        if (grn.TotalAmount == 0)
        {
            grn.TotalAmount = grn.GrnItems.Sum(i => i.QuantityReceived * i.CostPrice);
            grn.BalanceAmount = grn.TotalAmount - grn.AmountPaid - grn.ReturnedAmount;
        }

        var currentBalance = grn.TotalAmount - grn.AmountPaid - grn.ReturnedAmount;

        if (currentBalance <= 0)
            throw new InvalidOperationException("This GRN is already fully paid");

        if (payment.AmountPaid <= 0)
            throw new InvalidOperationException("Payment amount must be greater than zero");

        if (payment.AmountPaid > currentBalance)
            throw new InvalidOperationException($"Payment amount ({payment.AmountPaid:C}) exceeds balance ({currentBalance:C})");

        // 2. Generate payment number
        payment.PaymentNumber = await GeneratePaymentNumber();
        payment.GrnAmount = grn.TotalAmount;
        payment.BalanceAfter = currentBalance - payment.AmountPaid;
        payment.PaymentType = payment.AmountPaid >= currentBalance ? "Full" : "Partial";
        payment.Status = "Paid";
        payment.CreatedBy = loginUserId;
        payment.CreatedDate = DateTime.Now;
        payment.IsActive = true;

        // 3. Create Journal Entry
        // DR: Supplier Account (reduces liability)
        // CR: Cash/Bank Account (reduces asset)
        var paymentAccountTypeId = payment.PaymentMethod == "Bank" ? BANK_ACCOUNT_TYPE : CASH_ACCOUNT_TYPE;

        var supplierAccount = await _accountingService.GetFirstAccountByTypeId(SUPPLIER_ACCOUNT_TYPE);
        var paymentAccount = await _accountingService.GetFirstAccountByTypeId(paymentAccountTypeId);

        if (supplierAccount == null)
            throw new InvalidOperationException("Supplier account not found in Chart of Accounts");

        if (paymentAccount == null)
            throw new InvalidOperationException($"{payment.PaymentMethod} account not found in Chart of Accounts");

        var journalLines = new List<JournalEntryLine>
        {
            new JournalEntryLine
            {
                Account_ID = supplierAccount.AccountID,
                DebitAmount = payment.AmountPaid,
                CreditAmount = 0,
                Description = $"Payment to supplier - {grn.GrnNumber}",
                Store_ID = payment.Store_ID
            },
            new JournalEntryLine
            {
                Account_ID = paymentAccount.AccountID,
                DebitAmount = 0,
                CreditAmount = payment.AmountPaid,
                Description = $"Supplier payment via {payment.PaymentMethod} - {grn.GrnNumber}",
                Store_ID = payment.Store_ID
            }
        };

        // Use IJournalPostingEngine to create and post entry
        var journal = await _postingEngine.CreateAndPostAsync(
            entryType: "SupplierPayment",
            description: $"Supplier Payment - {payment.PaymentNumber} for {grn.GrnNumber}",
            lines: journalLines,
            sourceTable: "SupplierPayments",
            sourceId: 0, // Will update after payment insert
            storeId: payment.Store_ID,
            userId: loginUserId,
            isSystemEntry: true,
            reference: payment.PaymentNumber);

        if (journal == null)
            throw new InvalidOperationException("Failed to create journal entry for payment");

        payment.JournalEntry_ID = journal.JournalEntryID;

        // 4. Insert Payment
        var result = await _paymentRepo.Insert(payment);

        if (result)
        {
            // Update journal entry Source_ID to the actual payment ID
            journal.Source_ID = payment.SupplierPaymentID;
            await _context.SaveChangesAsync();

            // 5. Update GRN payment tracking
            grn.AmountPaid += payment.AmountPaid;
            grn.BalanceAmount = grn.TotalAmount - grn.AmountPaid - grn.ReturnedAmount;
            grn.PaymentStatus = grn.BalanceAmount <= 0 ? "Paid" : "Partial";

            await _grnRepo.Update(grn);
        }

        return result;
    }

    public async Task<bool> CancelPayment(int paymentId, int userId)
    {
        var payment = await _paymentRepo.FindByCondition(p => p.SupplierPaymentID == paymentId)
            .FirstOrDefaultAsync();

        if (payment == null)
            throw new InvalidOperationException("Payment not found");

        if (payment.Status == "Cancelled")
            throw new InvalidOperationException("Payment is already cancelled");

        // Void the journal entry
        if (payment.JournalEntry_ID.HasValue)
        {
            await _accountingService.VoidJournalEntry(payment.JournalEntry_ID.Value, userId);
        }

        // Update payment status
        payment.Status = "Cancelled";
        payment.UpdatedBy = userId;
        payment.UpdatedDate = DateTime.Now;

        var result = await _paymentRepo.Update(payment);

        if (result)
        {
            // Reverse GRN payment tracking
            var grn = await _grnRepo.FindByCondition(g => g.GrnID == payment.Grn_ID)
                .FirstOrDefaultAsync();

            if (grn != null)
            {
                grn.AmountPaid -= payment.AmountPaid;
                grn.BalanceAmount = grn.TotalAmount - grn.AmountPaid - grn.ReturnedAmount;
                grn.PaymentStatus = grn.AmountPaid <= 0 ? "Unpaid" : (grn.BalanceAmount <= 0 ? "Paid" : "Partial");

                await _grnRepo.Update(grn);
            }
        }

        return result;
    }

    #endregion

    #region Reports

    public async Task<SupplierPaymentSummaryDto> GetPaymentSummary(int? storeId = null)
    {
        var today = DateTime.Today;
        var firstOfMonth = new DateTime(today.Year, today.Month, 1);

        var paymentsQuery = _paymentRepo.FindByCondition(p => p.IsActive && p.Status == "Paid");
        var grnsQuery = _grnRepo.FindByCondition(g => true);

        if (storeId.HasValue)
        {
            paymentsQuery = paymentsQuery.Where(p => p.Store_ID == storeId.Value);
            grnsQuery = grnsQuery.Where(g => g.Store_ID == storeId.Value);
        }

        var todayPayments = await paymentsQuery
            .Where(p => p.PaymentDate.Date == today)
            .SumAsync(p => p.AmountPaid);

        var monthPayments = await paymentsQuery
            .Where(p => p.PaymentDate >= firstOfMonth)
            .SumAsync(p => p.AmountPaid);

        var unpaidGrns = await grnsQuery
            .Where(g => g.PaymentStatus == "Unpaid")
            .CountAsync();

        var partialGrns = await grnsQuery
            .Where(g => g.PaymentStatus == "Partial")
            .CountAsync();

        var outstandingGrns = await grnsQuery
            .Where(g => g.PaymentStatus != "Paid")
            .Include(g => g.GrnItems)
            .Include(g => g.Party)
            .ToListAsync();

        var totalOutstanding = outstandingGrns.Sum(g =>
            g.TotalAmount - g.AmountPaid - g.ReturnedAmount);

        // Top suppliers by outstanding
        var topSuppliers = outstandingGrns
            .Where(g => g.Party_ID.HasValue)
            .GroupBy(g => new { g.Party_ID, SupplierName = g.Party?.PartyName ?? "Unknown" })
            .Select(g => new SupplierOutstandingDto
            {
                PartyID = g.Key.Party_ID ?? 0,
                SupplierName = g.Key.SupplierName,
                TotalOutstanding = g.Sum(x => x.BalanceAmount > 0 ? x.BalanceAmount : x.TotalAmount - x.AmountPaid),
                UnpaidGrnCount = g.Count()
            })
            .OrderByDescending(s => s.TotalOutstanding)
            .Take(5)
            .ToList();

        return new SupplierPaymentSummaryDto
        {
            TotalOutstanding = totalOutstanding,
            TotalPaidToday = todayPayments,
            TotalPaidThisMonth = monthPayments,
            UnpaidGrnCount = unpaidGrns,
            PartiallyPaidGrnCount = partialGrns,
            TopSuppliersByOutstanding = topSuppliers
        };
    }

    #endregion
}
