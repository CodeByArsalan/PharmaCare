using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Finance;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Application.Interfaces.AccountManagement;
using PharmaCare.Domain.Models.Finance;
using PharmaCare.Domain.Models.Inventory;
using PharmaCare.Domain.Models.AccountManagement;
using PharmaCare.Infrastructure.Interfaces;
using PharmaCare.Infrastructure.Interfaces.Accounting;

namespace PharmaCare.Application.Implementations.Finance;

/// <summary>
/// Service for managing customer payment collection (receivables)
/// Now uses StockMain (InvoiceType_ID=1 for Sales) instead of deprecated Sale model
/// </summary>
public class CustomerPaymentService : ICustomerPaymentService
{
    private readonly IRepository<CustomerPayment> _paymentRepo;
    private readonly IRepository<StockMain> _stockMainRepo;
    private readonly IAccountingService _accountingService;
    private readonly IVoucherService _voucherService;

    // Account Type IDs
    private const int CASH_ACCOUNT_TYPE = 1;
    private const int BANK_ACCOUNT_TYPE = 2;
    private const int CUSTOMER_ACCOUNT_TYPE = 3; // Customer/Receivables

    // Voucher Type IDs
    private const int BANK_RECEIPT_VOUCHER = 3;
    private const int CASH_RECEIPT_VOUCHER = 5;

    public CustomerPaymentService(
        IRepository<CustomerPayment> paymentRepo,
        IRepository<StockMain> stockMainRepo,
        IAccountingService accountingService,
        IVoucherService voucherService)
    {
        _paymentRepo = paymentRepo;
        _stockMainRepo = stockMainRepo;
        _accountingService = accountingService;
        _voucherService = voucherService;
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
            .Include(p => p.StockMain)
            .Include(p => p.AccountVoucher)
            .OrderByDescending(p => p.PaymentDate)
            .ToListAsync();

        return payments.Select(p => new CustomerPaymentListDto
        {
            CustomerPaymentID = p.CustomerPaymentID,
            PaymentNumber = p.PaymentNumber,
            PaymentDate = p.PaymentDate,
            CustomerName = p.Party?.PartyName ?? "Unknown",
            CustomerID = p.Party_ID,
            SaleNumber = p.StockMain?.InvoiceNo,
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
            .Include(pay => pay.StockMain)
            .Include(pay => pay.AccountVoucher)
            .FirstOrDefaultAsync();

        if (p == null) return null;

        return new CustomerPaymentListDto
        {
            CustomerPaymentID = p.CustomerPaymentID,
            PaymentNumber = p.PaymentNumber,
            PaymentDate = p.PaymentDate,
            CustomerName = p.Party?.PartyName ?? "Unknown",
            CustomerID = p.Party_ID,
            SaleNumber = p.StockMain?.InvoiceNo,
            Amount = p.Amount,
            PaymentMethod = p.PaymentMethod,
            ReferenceNumber = p.ReferenceNumber,
            Status = p.Status
        };
    }

    public async Task<List<OutstandingSaleDto>> GetOutstandingSales(int? customerId = null)
    {
        // Query StockMain with InvoiceType_ID=1 (SALE) and balance > 0
        var query = _stockMainRepo.FindByCondition(s =>
            s.InvoiceType_ID == 1 && // SALE
            s.BalanceAmount > 0 &&
            (s.PaymentStatus == "Partial" || s.PaymentStatus == "Credit") &&
            s.Status != "Voided");

        if (customerId.HasValue)
            query = query.Where(s => s.Party_ID == customerId.Value);

        var sales = await query
            .Include(s => s.Party)
            .OrderByDescending(s => s.InvoiceDate)
            .ToListAsync();

        return sales.Select(s => new OutstandingSaleDto
        {
            StockMainID = s.StockMainID,
            SaleNumber = s.InvoiceNo,
            SaleDate = s.InvoiceDate,
            CustomerName = s.Party?.PartyName ?? "Walk-in Customer",
            CustomerID = s.Party_ID ?? 0,
            Total = s.TotalAmount,
            AmountPaid = s.PaidAmount,
            BalanceAmount = s.BalanceAmount,
            PaymentStatus = s.PaymentStatus
        }).ToList();
    }

    public async Task<decimal> GetTotalOutstandingForCustomer(int customerId)
    {
        return await _stockMainRepo.FindByCondition(s =>
            s.InvoiceType_ID == 1 && // SALE
            s.Party_ID == customerId &&
            s.BalanceAmount > 0 &&
            s.Status != "Voided")
            .SumAsync(s => s.BalanceAmount);
    }

    public async Task<List<CustomerOutstandingDto>> GetCustomersWithOutstanding()
    {
        var sales = await _stockMainRepo.FindByCondition(s =>
            s.InvoiceType_ID == 1 && // SALE
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

        // If linked to a specific sale (StockMain), validate it
        StockMain? sale = null;
        if (dto.SaleID.HasValue)
        {
            sale = await _stockMainRepo.FindByCondition(s => s.StockMainID == dto.SaleID.Value && s.InvoiceType_ID == 1)
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
            StockMain_ID = dto.SaleID,
            Amount = dto.Amount,
            PaymentMethod = dto.PaymentMethod,
            ReferenceNumber = dto.ReferenceNumber,
            Notes = dto.Notes,
            Status = "Active",
            CreatedBy = userId,
            CreatedDate = DateTime.Now,
            IsActive = true
        };

        // Create Voucher (Replaces JournalEntry)
        // DR: Cash/Bank Account (increases asset)
        // CR: Customer Account (reduces receivable)
        var paymentAccountTypeId = dto.PaymentMethod == "Bank" ? BANK_ACCOUNT_TYPE : CASH_ACCOUNT_TYPE;
        var voucherTypeId = dto.PaymentMethod == "Bank" ? BANK_RECEIPT_VOUCHER : CASH_RECEIPT_VOUCHER;

        var customerAccount = await _accountingService.GetFirstAccountByTypeId(CUSTOMER_ACCOUNT_TYPE);
        var paymentAccount = await _accountingService.GetFirstAccountByTypeId(paymentAccountTypeId);

        if (customerAccount == null)
            throw new InvalidOperationException("Customer/Receivables account not found in Chart of Accounts");

        if (paymentAccount == null)
            throw new InvalidOperationException($"{dto.PaymentMethod} account not found in Chart of Accounts");

        var saleRef = sale != null ? $" - {sale.InvoiceNo}" : "";
        var storeId = sale?.Store_ID; // Should probably default to login user store if sale not present, but for now safely null

        var voucherRequest = new CreateVoucherRequest
        {
            VoucherTypeId = voucherTypeId,
            VoucherDate = payment.PaymentDate,
            SourceTable = "CustomerPayments",
            SourceId = 0, // Will update after insert
            StoreId = storeId,
            Narration = $"Customer Payment - {paymentNumber}{saleRef}",
            CreatedBy = userId,
            Lines = new List<CreateVoucherLineRequest>
            {
                // DR: Cash/Bank (Increases Asset)
                new CreateVoucherLineRequest
                {
                    AccountId = paymentAccount.AccountID,
                    Dr = dto.Amount,
                    Cr = 0,
                    Particulars = $"Payment received from customer{saleRef}",
                    StoreId = storeId
                },
                // CR: Customer (Reduces Receivable)
                new CreateVoucherLineRequest
                {
                    AccountId = customerAccount.AccountID,
                    Dr = 0,
                    Cr = dto.Amount,
                    Particulars = $"Customer payment received via {dto.PaymentMethod}{saleRef}",
                    StoreId = storeId
                }
            }
        };

        var voucher = await _voucherService.CreateVoucherAsync(voucherRequest);
        payment.Voucher_ID = voucher.VoucherID;

        // Insert payment
        var result = await _paymentRepo.Insert(payment);

        if (result)
        {
            // Update sale payment tracking if linked to a specific sale
            if (sale != null)
            {
                sale.PaidAmount += dto.Amount;
                sale.BalanceAmount = sale.TotalAmount - sale.PaidAmount;
                sale.PaymentStatus = sale.BalanceAmount <= 0 ? "Paid" : "Partial";

                await _stockMainRepo.Update(sale);
            }
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

        // Reverse the voucher
        if (payment.Voucher_ID.HasValue)
        {
            await _voucherService.ReverseVoucherAsync(payment.Voucher_ID.Value, $"Cancelled: {reason}", userId);
        }


        // Update payment status
        payment.Status = "Cancelled";
        payment.Notes = (payment.Notes ?? "") + $"\n[Cancelled: {reason}]";
        payment.UpdatedBy = userId;
        payment.UpdatedDate = DateTime.Now;

        var result = await _paymentRepo.Update(payment);

        if (result && payment.StockMain_ID.HasValue)
        {
            // Reverse sale payment tracking
            var sale = await _stockMainRepo.FindByCondition(s => s.StockMainID == payment.StockMain_ID)
                .FirstOrDefaultAsync();

            if (sale != null)
            {
                sale.PaidAmount -= payment.Amount;
                sale.BalanceAmount = sale.TotalAmount - sale.PaidAmount;
                sale.PaymentStatus = sale.PaidAmount <= 0 ? "Credit" : (sale.BalanceAmount <= 0 ? "Paid" : "Partial");

                await _stockMainRepo.Update(sale);
            }
        }

        return result;
    }

    #endregion
}
