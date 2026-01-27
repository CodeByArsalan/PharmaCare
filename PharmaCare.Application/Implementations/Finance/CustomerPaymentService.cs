using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Finance;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.Finance;
using PharmaCare.Domain.Models.SaleManagement;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Accounting;

namespace PharmaCare.Application.Implementations.Finance;

/// <summary>
/// Service for managing customer payment collection (receivables)
/// </summary>
public class CustomerPaymentService : ICustomerPaymentService
{
    private readonly IRepository<CustomerPayment> _paymentRepo;
    private readonly IRepository<Sale> _saleRepo;
    private readonly IAccountingService _accountingService;
    private readonly IJournalPostingEngine _postingEngine;

    // Account Type IDs
    private const int CASH_ACCOUNT_TYPE = 1;
    private const int BANK_ACCOUNT_TYPE = 2;
    private const int CUSTOMER_ACCOUNT_TYPE = 3; // Customer/Receivables

    public CustomerPaymentService(
        IRepository<CustomerPayment> paymentRepo,
        IRepository<Sale> saleRepo,
        IAccountingService accountingService,
        IJournalPostingEngine postingEngine)
    {
        _paymentRepo = paymentRepo;
        _saleRepo = saleRepo;
        _accountingService = accountingService;
        _postingEngine = postingEngine;
    }

    #region Query Operations

    public async Task<List<CustomerPaymentListDto>> GetAllPayments(
        int? customerId = null,
        DateTime? startDate = null,
        DateTime? endDate = null)
    {
        var query = _paymentRepo.FindByCondition(p => p.IsActive);

        if (customerId.HasValue)
            query = query.Where(p => p.Party_ID == customerId.Value);

        if (startDate.HasValue)
            query = query.Where(p => p.PaymentDate >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(p => p.PaymentDate <= endDate.Value.AddDays(1));

        var payments = await query
            .Include(p => p.Party)
            .Include(p => p.Sale)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        return payments.Select(p => new CustomerPaymentListDto
        {
            CustomerPaymentID = p.CustomerPaymentID,
            PaymentNumber = p.PaymentNumber,
            PaymentDate = p.PaymentDate,
            CustomerName = p.Party?.PartyName ?? "Unknown",
            CustomerID = p.Party_ID,
            SaleNumber = p.Sale?.SaleNumber,
            Amount = p.Amount,
            PaymentMethod = p.PaymentMethod,
            ReferenceNumber = p.ReferenceNumber,
            Status = p.Status
        }).ToList();
    }

    public async Task<CustomerPaymentListDto?> GetPaymentById(int paymentId)
    {
        var p = await _paymentRepo.FindByCondition(pay => pay.CustomerPaymentID == paymentId)
            .Include(pay => pay.Party)
            .Include(pay => pay.Sale)
            .FirstOrDefaultAsync();

        if (p == null) return null;

        return new CustomerPaymentListDto
        {
            CustomerPaymentID = p.CustomerPaymentID,
            PaymentNumber = p.PaymentNumber,
            PaymentDate = p.PaymentDate,
            CustomerName = p.Party?.PartyName ?? "Unknown",
            CustomerID = p.Party_ID,
            SaleNumber = p.Sale?.SaleNumber,
            Amount = p.Amount,
            PaymentMethod = p.PaymentMethod,
            ReferenceNumber = p.ReferenceNumber,
            Status = p.Status
        };
    }

    public async Task<List<OutstandingSaleDto>> GetOutstandingSales(int? customerId = null)
    {
        var query = _saleRepo.FindByCondition(s =>
            s.BalanceAmount > 0 &&
            (s.PaymentStatus == "Partial" || s.PaymentStatus == "Credit") &&
            s.Status != "Voided");

        if (customerId.HasValue)
            query = query.Where(s => s.Party_ID == customerId.Value);

        var sales = await query
            .Include(s => s.Party)
            .OrderByDescending(s => s.SaleDate)
            .ToListAsync();

        return sales.Select(s => new OutstandingSaleDto
        {
            SaleID = s.SaleID,
            SaleNumber = s.SaleNumber,
            SaleDate = s.SaleDate,
            CustomerName = s.Party?.PartyName ?? "Walk-in Customer",
            CustomerID = s.Party_ID ?? 0,
            Total = s.Total,
            AmountPaid = s.AmountPaid,
            BalanceAmount = s.BalanceAmount,
            PaymentStatus = s.PaymentStatus
        }).ToList();
    }

    public async Task<decimal> GetTotalOutstandingForCustomer(int customerId)
    {
        return await _saleRepo.FindByCondition(s =>
            s.Party_ID == customerId &&
            s.BalanceAmount > 0 &&
            s.Status != "Voided")
            .SumAsync(s => s.BalanceAmount);
    }

    public async Task<List<CustomerOutstandingDto>> GetCustomersWithOutstanding()
    {
        var sales = await _saleRepo.FindByCondition(s =>
            s.Party_ID != null &&
            s.BalanceAmount > 0 &&
            s.Status != "Voided")
            .Include(s => s.Party)
            .ToListAsync();

        return sales
            .GroupBy(s => new { s.Party_ID, Name = s.Party?.PartyName ?? "Unknown", Phone = s.Party?.ContactNumber })
            .Select(g => new CustomerOutstandingDto
            {
                CustomerID = g.Key.Party_ID ?? 0,
                CustomerName = g.Key.Name,
                Phone = g.Key.Phone,
                OutstandingSalesCount = g.Count(),
                TotalOutstanding = g.Sum(s => s.BalanceAmount)
            })
            .OrderByDescending(c => c.TotalOutstanding)
            .ToList();
    }

    #endregion

    #region Payment Processing

    public async Task<int> CreatePayment(CreateCustomerPaymentDto dto, int userId)
    {
        if (dto.Amount <= 0)
            throw new InvalidOperationException("Payment amount must be greater than zero");

        // If linked to a specific sale, validate it
        Sale? sale = null;
        if (dto.SaleID.HasValue)
        {
            sale = await _saleRepo.FindByCondition(s => s.SaleID == dto.SaleID.Value)
                .FirstOrDefaultAsync();

            if (sale == null)
                throw new InvalidOperationException("Sale not found");

            if (sale.BalanceAmount <= 0)
                throw new InvalidOperationException("This sale is already fully paid");

            if (dto.Amount > sale.BalanceAmount)
                throw new InvalidOperationException($"Payment amount ({dto.Amount:C}) exceeds balance ({sale.BalanceAmount:C})");

            // Use the sale's customer if not specified
            if (!dto.CustomerID.HasValue && sale.Party_ID.HasValue)
                dto.CustomerID = sale.Party_ID;
        }

        if (!dto.CustomerID.HasValue)
            throw new InvalidOperationException("Customer is required for payment");

        // Generate payment number using timestamp
        var paymentNumber = $"CPAY-{DateTime.Now:yyyyMMddHHmmssfff}";

        // Create payment record
        var payment = new CustomerPayment
        {
            PaymentNumber = paymentNumber,
            PaymentDate = DateTime.Now,
            Party_ID = dto.CustomerID.Value,
            Sale_ID = dto.SaleID,
            Amount = dto.Amount,
            PaymentMethod = dto.PaymentMethod,
            ReferenceNumber = dto.ReferenceNumber,
            Notes = dto.Notes,
            Status = "Active",
            CreatedBy = userId,
            CreatedDate = DateTime.Now,
            IsActive = true
        };

        // Create Journal Entry
        // DR: Cash/Bank Account (increases asset)
        // CR: Customer Account (reduces receivable)
        var paymentAccountTypeId = dto.PaymentMethod == "Bank" ? BANK_ACCOUNT_TYPE : CASH_ACCOUNT_TYPE;

        var customerAccount = await _accountingService.GetFirstAccountByTypeId(CUSTOMER_ACCOUNT_TYPE);
        var paymentAccount = await _accountingService.GetFirstAccountByTypeId(paymentAccountTypeId);

        if (customerAccount == null)
            throw new InvalidOperationException("Customer/Receivables account not found in Chart of Accounts");

        if (paymentAccount == null)
            throw new InvalidOperationException($"{dto.PaymentMethod} account not found in Chart of Accounts");

        var saleRef = sale != null ? $" - {sale.SaleNumber}" : "";
        var journalLines = new List<JournalEntryLine>
        {
            new JournalEntryLine
            {
                Account_ID = paymentAccount.AccountID,
                DebitAmount = dto.Amount,
                CreditAmount = 0,
                Description = $"Payment received from customer{saleRef}"
            },
            new JournalEntryLine
            {
                Account_ID = customerAccount.AccountID,
                DebitAmount = 0,
                CreditAmount = dto.Amount,
                Description = $"Customer payment received via {dto.PaymentMethod}{saleRef}"
            }
        };

        // Create and post journal entry
        var journal = await _postingEngine.CreateAndPostAsync(
            entryType: "CustomerPayment",
            description: $"Customer Payment - {paymentNumber}{saleRef}",
            lines: journalLines,
            sourceTable: "CustomerPayments",
            sourceId: 0, // Will update after insert
            storeId: sale?.Store_ID,
            userId: userId,
            isSystemEntry: true);

        if (journal == null)
            throw new InvalidOperationException("Failed to create journal entry for payment");

        payment.JournalEntry_ID = journal.JournalEntryID;

        // Insert payment
        var result = await _paymentRepo.Insert(payment);

        if (result)
        {
            // Update sale payment tracking if linked to a specific sale
            if (sale != null)
            {
                sale.AmountPaid += dto.Amount;
                sale.BalanceAmount = sale.Total - sale.AmountPaid;
                sale.PaymentStatus = sale.BalanceAmount <= 0 ? "Paid" : "Partial";

                await _saleRepo.Update(sale);
            }
            // If not linked to a specific sale, we could potentially apply to oldest outstanding sales
            // For now, just record the general payment
        }

        return payment.CustomerPaymentID;
    }

    public async Task<bool> CancelPayment(int paymentId, string reason, int userId)
    {
        var payment = await _paymentRepo.FindByCondition(p => p.CustomerPaymentID == paymentId)
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
        payment.Notes = (payment.Notes ?? "") + $"\n[Cancelled: {reason}]";
        payment.UpdatedBy = userId;
        payment.UpdatedDate = DateTime.Now;

        var result = await _paymentRepo.Update(payment);

        if (result && payment.Sale_ID.HasValue)
        {
            // Reverse sale payment tracking
            var sale = await _saleRepo.FindByCondition(s => s.SaleID == payment.Sale_ID)
                .FirstOrDefaultAsync();

            if (sale != null)
            {
                sale.AmountPaid -= payment.Amount;
                sale.BalanceAmount = sale.Total - sale.AmountPaid;
                sale.PaymentStatus = sale.AmountPaid <= 0 ? "Credit" : (sale.BalanceAmount <= 0 ? "Paid" : "Partial");

                await _saleRepo.Update(sale);
            }
        }

        return result;
    }

    #endregion
}
