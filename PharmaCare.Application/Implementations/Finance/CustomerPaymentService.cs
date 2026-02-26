using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs.Finance;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.Implementations.Finance;

/// <summary>
/// Service for managing customer receipts with double-entry accounting.
/// </summary>
public class CustomerPaymentService : ICustomerPaymentService
{
    private const int CashAccountTypeId = 1;
    private const int BankAccountTypeId = 2;

    private readonly IRepository<Payment> _paymentRepository;
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IRepository<TransactionType> _transactionTypeRepository;
    private readonly IRepository<Voucher> _voucherRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Party> _partyRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<CreditNote> _creditNoteRepository;
    private readonly IRepository<PaymentAllocation> _paymentAllocationRepository;
    private readonly IUnitOfWork _unitOfWork;

    private const string SALE_TRANSACTION_TYPE_CODE = "SALE";
    private const string SALE_RETURN_TRANSACTION_TYPE_CODE = "SRTN";
    private const string PREFIX = "REC";
    private const string VOUCHER_TYPE_CODE = "RV"; // Receipt Voucher
    private static readonly string CustomerReceiptPaymentType = PaymentType.RECEIPT.ToString();
    private static readonly string RefundPaymentType = PaymentType.REFUND.ToString();

    public CustomerPaymentService(
        IRepository<Payment> paymentRepository,
        IRepository<StockMain> stockMainRepository,
        IRepository<TransactionType> transactionTypeRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Party> partyRepository,
        IRepository<Account> accountRepository,
        IRepository<CreditNote> creditNoteRepository,
        IRepository<PaymentAllocation> paymentAllocationRepository,
        IUnitOfWork unitOfWork)
    {
        _paymentRepository = paymentRepository;
        _stockMainRepository = stockMainRepository;
        _transactionTypeRepository = transactionTypeRepository;
        _voucherRepository = voucherRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _partyRepository = partyRepository;
        _accountRepository = accountRepository;
        _creditNoteRepository = creditNoteRepository;
        _paymentAllocationRepository = paymentAllocationRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<Payment>> GetAllCustomerReceiptsAsync()
    {
        return await _paymentRepository.Query()
            .Include(p => p.Party)
            .Include(p => p.StockMain)
            .Include(p => p.Account)
            .Where(p => p.PaymentType == CustomerReceiptPaymentType)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .ToListAsync();
    }

    public async Task<Payment?> GetByIdAsync(int id)
    {
        return await _paymentRepository.Query()
            .Include(p => p.Party)
            .Include(p => p.StockMain)
            .Include(p => p.Account)
            .Include(p => p.Voucher)
            .FirstOrDefaultAsync(p => p.PaymentID == id);
    }

    public async Task<IEnumerable<Payment>> GetReceiptsByTransactionAsync(int stockMainId)
    {
        return await _paymentRepository.Query()
            .Include(p => p.Account)
            .Where(p => p.StockMain_ID == stockMainId && p.PaymentType == CustomerReceiptPaymentType)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .ToListAsync();
    }

    public async Task<IEnumerable<StockMain>> GetPendingSalesAsync(int? customerId = null)
    {
        var query = _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Include(s => s.Party)
            .Where(s => s.TransactionType!.Code == SALE_TRANSACTION_TYPE_CODE
                     && s.Status == "Approved"
                     && s.Party_ID.HasValue);

        if (customerId.HasValue)
        {
            query = query.Where(s => s.Party_ID == customerId.Value);
        }

        var sales = await query
            .OrderByDescending(s => s.TransactionDate)
            .ToListAsync();

        if (!sales.Any())
            return sales;

        var saleIds = sales.Select(s => s.StockMainID).ToList();
        var returnSums = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == SALE_RETURN_TRANSACTION_TYPE_CODE
                     && s.ReferenceStockMain_ID.HasValue
                     && saleIds.Contains(s.ReferenceStockMain_ID.Value)
                     && s.Status != "Void")
            .GroupBy(s => s.ReferenceStockMain_ID!.Value)
            .Select(g => new
            {
                SaleId = g.Key,
                TotalReturns = g.Sum(x => x.TotalAmount)
            })
            .ToListAsync();

        var returnMap = returnSums.ToDictionary(x => x.SaleId, x => x.TotalReturns);

        foreach (var sale in sales)
        {
            var totalReturns = returnMap.TryGetValue(sale.StockMainID, out var amount) ? amount : 0;
            var outstanding = sale.TotalAmount - sale.PaidAmount - totalReturns;
            sale.BalanceAmount = Math.Max(0, outstanding);
            sale.PaymentStatus = sale.BalanceAmount <= 0
                ? PharmaCare.Domain.Enums.PaymentStatus.Paid.ToString()
                : (sale.PaidAmount > 0
                    ? PharmaCare.Domain.Enums.PaymentStatus.Partial.ToString()
                    : PharmaCare.Domain.Enums.PaymentStatus.Unpaid.ToString());
        }

        return sales.Where(s => s.BalanceAmount > 0).ToList();
    }

    public async Task<Payment> CreateReceiptAsync(Payment payment, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            // Validate the transaction exists (include Party and their Account)
            var stockMain = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .Include(s => s.Party)
                    .ThenInclude(p => p!.Account)
                .FirstOrDefaultAsync(s => s.StockMainID == payment.StockMain_ID);

            if (stockMain == null)
                throw new InvalidOperationException("Transaction not found.");

            if (!string.Equals(stockMain.TransactionType?.Code, SALE_TRANSACTION_TYPE_CODE, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Receipts can only be recorded against sales.");

            if (!string.Equals(stockMain.Status, "Approved", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Receipts can only be recorded against approved sales.");

            if (payment.Amount <= 0)
                throw new InvalidOperationException("Receipt amount must be greater than zero.");

            var outstandingBeforeReceipt = await GetNetOutstandingAmountAsync(stockMain.StockMainID, stockMain.TotalAmount, stockMain.PaidAmount);
            if (outstandingBeforeReceipt <= 0)
                throw new InvalidOperationException("This sale has no outstanding receivable after considering sale returns.");

            if (payment.Amount > outstandingBeforeReceipt)
                throw new InvalidOperationException($"Receipt amount ({payment.Amount:N2}) exceeds balance ({outstandingBeforeReceipt:N2}).");

            // Get the receipt account (Cash/Bank)
            var receiptAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == payment.Account_ID);

            if (receiptAccount == null)
                throw new InvalidOperationException("Receipt account not found.");

            if (receiptAccount.AccountType_ID != CashAccountTypeId && receiptAccount.AccountType_ID != BankAccountTypeId)
                throw new InvalidOperationException("Receipt account must be a Cash or Bank account.");

            // Get customer's linked account directly
            var customerAccount = stockMain.Party?.Account;
            if (customerAccount == null)
                throw new InvalidOperationException($"Customer '{stockMain.Party?.Name}' does not have a linked account. Please update the party record.");

            if (!stockMain.Party_ID.HasValue || stockMain.Party_ID.Value <= 0)
                throw new InvalidOperationException("Sale is not linked to a customer.");

            // Generate reference number
            payment.Reference = await GenerateReferenceNoAsync();
            payment.PaymentType = CustomerReceiptPaymentType; // Customer receipt
            payment.Party_ID = stockMain.Party_ID.Value;
            payment.PaymentMethod = NormalizePaymentMethod(payment.PaymentMethod, receiptAccount.AccountType_ID);
            payment.IsVoided = false;
            payment.CreatedAt = DateTime.Now;
            payment.CreatedBy = userId;

            // Create accounting voucher
            var voucher = await CreateReceiptVoucherAsync(
                payment,
                customerAccount,
                receiptAccount,
                stockMain.Party!.Name,
                userId);

            payment.Voucher = voucher;

            // Update transaction balance
            stockMain.PaidAmount += payment.Amount;
            await RecalculateSaleBalanceIncludingReturnsAsync(stockMain, userId);

            await _paymentRepository.AddAsync(payment);
            await _paymentAllocationRepository.AddAsync(new PaymentAllocation
            {
                Payment = payment,
                StockMain_ID = stockMain.StockMainID,
                Amount = payment.Amount,
                SourceType = "Receipt",
                AllocationDate = payment.PaymentDate,
                CreatedAt = DateTime.Now,
                CreatedBy = userId
            });
            _stockMainRepository.Update(stockMain);
            await _unitOfWork.SaveChangesAsync();

            return payment;
        });
    }

    private async Task<decimal> GetNetOutstandingAmountAsync(int saleId, decimal totalAmount, decimal paidAmount)
    {
        var totalReturns = await _stockMainRepository.Query()
            .Include(s => s.TransactionType)
            .Where(s => s.TransactionType!.Code == SALE_RETURN_TRANSACTION_TYPE_CODE
                     && s.ReferenceStockMain_ID == saleId
                     && s.Status != "Void")
            .SumAsync(s => (decimal?)s.TotalAmount) ?? 0;

        return Math.Max(0, totalAmount - paidAmount - totalReturns);
    }

    private async Task RecalculateSaleBalanceIncludingReturnsAsync(StockMain sale, int userId)
    {
        sale.BalanceAmount = await GetNetOutstandingAmountAsync(sale.StockMainID, sale.TotalAmount, sale.PaidAmount);
        sale.PaymentStatus = sale.BalanceAmount <= 0
            ? PharmaCare.Domain.Enums.PaymentStatus.Paid.ToString()
            : (sale.PaidAmount > 0
                ? PharmaCare.Domain.Enums.PaymentStatus.Partial.ToString()
                : PharmaCare.Domain.Enums.PaymentStatus.Unpaid.ToString());
        sale.UpdatedAt = DateTime.Now;
        sale.UpdatedBy = userId;
    }

    /// <summary>
    /// Creates a receipt voucher with double-entry accounting.
    /// Debit: Cash/Bank Account - increases asset
    /// Credit: Customer Account (Accounts Receivable) - reduces asset (what they owe us)
    /// </summary>
    private async Task<Voucher> CreateReceiptVoucherAsync(
        Payment payment, 
        Account customerAccount, 
        Account cashBankAccount,
        string customerName,
        int userId)
    {
        // Get Receipt Voucher type (fallback to JV if RV not found)
        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == VOUCHER_TYPE_CODE || vt.Code == "JV");

        if (voucherType == null)
            throw new InvalidOperationException($"Voucher type '{VOUCHER_TYPE_CODE}' or 'JV' not found. Please ensure it exists in the database.");

        // Determine voucher prefix based on account type (CR for Cash, BR for Bank)
        string voucherPrefix = "RV";
        if (cashBankAccount.AccountType_ID == 1) // Cash
        {
            voucherPrefix = "CR";
        }
        else if (cashBankAccount.AccountType_ID == 2) // Bank
        {
            voucherPrefix = "BR";
        }

        var voucherNo = await GenerateVoucherNoAsync(voucherPrefix);

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = voucherNo,
            VoucherDate = payment.PaymentDate,
            TotalDebit = payment.Amount,
            TotalCredit = payment.Amount,
            Status = "Posted",
            SourceTable = "StockMain",
            SourceID = payment.StockMain_ID,
            Narration = $"Receipt from customer: {customerName}. Ref: {payment.Reference}",
            CreatedAt = DateTime.Now,
            CreatedBy = userId,
            VoucherDetails = new List<VoucherDetail>
            {
                // Debit: Cash/Bank Account - increases asset
                new VoucherDetail
                {
                    Account_ID = cashBankAccount.AccountID,
                    DebitAmount = payment.Amount,
                    CreditAmount = 0,
                    Description = $"Receipt via {payment.PaymentMethod}"
                },
                // Credit: Customer Account (Accounts Receivable) - reduces what they owe us
                new VoucherDetail
                {
                    Account_ID = customerAccount.AccountID,
                    DebitAmount = 0,
                    CreditAmount = payment.Amount,
                    Description = $"Receipt from {customerName}",
                    Party_ID = payment.Party_ID
                }
            }
        };

        await _voucherRepository.AddAsync(voucher);

        return voucher;
    }

    private async Task<string> GenerateReferenceNoAsync()
    {
        var datePrefix = $"{PREFIX}-{DateTime.Now:yyyyMMdd}-";

        var lastPayment = await _paymentRepository.Query()
            .Where(p => p.Reference != null && p.Reference.StartsWith(datePrefix))
            .OrderByDescending(p => p.Reference)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (lastPayment != null && lastPayment.Reference != null)
        {
            var parts = lastPayment.Reference.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
    }

    private async Task<string> GenerateVoucherNoAsync(string prefix = "RV")
    {
        var datePrefix = $"{prefix}-{DateTime.Now:yyyyMMdd}-";

        var lastVoucher = await _voucherRepository.Query()
            .Where(v => v.VoucherNo.StartsWith(datePrefix))
            .OrderByDescending(v => v.VoucherNo)
            .FirstOrDefaultAsync();

        int nextNum = 1;
        if (lastVoucher != null)
        {
            var parts = lastVoucher.VoucherNo.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
    }

    /// <summary>
    /// Creates a refund to a customer.
    /// DR: Customer Account (A/R) - restores receivable / reduces credit balance
    /// CR: Cash/Bank Account - cash goes out
    /// </summary>
    public async Task<Payment> CreateRefundAsync(Payment payment, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            if (payment.Amount <= 0)
                throw new InvalidOperationException("Refund amount must be greater than zero.");

            if (payment.Party_ID <= 0)
                throw new InvalidOperationException("Customer is required for a refund.");

            var customer = await _partyRepository.Query()
                .Include(p => p.Account)
                .FirstOrDefaultAsync(p => p.PartyID == payment.Party_ID);

            if (customer == null)
                throw new InvalidOperationException("Customer not found.");

            if (customer.Account_ID == null || customer.Account == null)
                throw new InvalidOperationException($"Customer '{customer.Name}' does not have a linked account.");

            var customerAccount = customer.Account;

            var refundAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == payment.Account_ID);

            if (refundAccount == null)
                throw new InvalidOperationException("Refund account not found.");

            if (refundAccount.AccountType_ID != CashAccountTypeId && refundAccount.AccountType_ID != BankAccountTypeId)
                throw new InvalidOperationException("Refund account must be a Cash or Bank account.");

            payment.Reference = await GenerateReferenceNoAsync();
            payment.PaymentType = RefundPaymentType;
            payment.StockMain_ID = null;
            payment.PaymentMethod = NormalizePaymentMethod(payment.PaymentMethod, refundAccount.AccountType_ID);
            payment.IsVoided = false;
            payment.Remarks = string.IsNullOrWhiteSpace(payment.Remarks)
                ? $"Refund to {customer.Name}"
                : payment.Remarks;
            payment.CreatedAt = DateTime.Now;
            payment.CreatedBy = userId;

            var voucherType = await _voucherTypeRepository.Query()
                .FirstOrDefaultAsync(vt => vt.Code == VOUCHER_TYPE_CODE || vt.Code == "JV");

            if (voucherType == null)
                throw new InvalidOperationException("Voucher type not found.");

            string voucherPrefix = refundAccount.AccountType_ID == 1 ? "CP" : "BP";
            var voucherNo = await GenerateVoucherNoAsync(voucherPrefix);

            var voucher = new Voucher
            {
                VoucherType_ID = voucherType.VoucherTypeID,
                VoucherNo = voucherNo,
                VoucherDate = payment.PaymentDate,
                TotalDebit = payment.Amount,
                TotalCredit = payment.Amount,
                Status = "Posted",
                SourceTable = "Payment",
                Narration = $"Customer refund to {customer.Name}. Ref: {payment.Reference}",
                CreatedAt = DateTime.Now,
                CreatedBy = userId,
                VoucherDetails = new List<VoucherDetail>
                {
                    new VoucherDetail
                    {
                        Account_ID = customerAccount.AccountID,
                        DebitAmount = payment.Amount,
                        CreditAmount = 0,
                        Description = $"Refund to {customer.Name}",
                        Party_ID = payment.Party_ID
                    },
                    new VoucherDetail
                    {
                        Account_ID = refundAccount.AccountID,
                        DebitAmount = 0,
                        CreditAmount = payment.Amount,
                        Description = $"Refund via {payment.PaymentMethod}"
                    }
                }
            };

            await _voucherRepository.AddAsync(voucher);

            payment.Voucher = voucher;
            await _paymentRepository.AddAsync(payment);
            await _unitOfWork.SaveChangesAsync();

            return payment;
        });
    }

    /// <summary>
    /// Gets all customer refunds.
    /// </summary>
    public async Task<IEnumerable<Payment>> GetAllRefundsAsync()
    {
        return await _paymentRepository.Query()
            .Include(p => p.Party)
            .Include(p => p.Account)
            .Where(p => p.PaymentType == RefundPaymentType)
            .OrderByDescending(p => p.PaymentDate)
            .ThenByDescending(p => p.PaymentID)
            .ToListAsync();
    }

    public async Task<IEnumerable<CreditNote>> GetOpenCreditNotesAsync(int? customerId = null)
    {
        var query = _creditNoteRepository.Query()
            .Include(c => c.Party)
            .Include(c => c.SourceStockMain)
            .Where(c => c.Status == "Open" && c.BalanceAmount > 0);

        if (customerId.HasValue && customerId.Value > 0)
        {
            query = query.Where(c => c.Party_ID == customerId.Value);
        }

        return await query
            .OrderBy(c => c.CreditDate)
            .ThenBy(c => c.CreditNoteNo)
            .ToListAsync();
    }

    public async Task<CustomerReconciliationVM> GetCustomerReconciliationAsync(int? customerId = null)
    {
        var outstandingSales = (await GetPendingSalesAsync(customerId)).ToList();
        var openCredits = (await GetOpenCreditNotesAsync(customerId)).ToList();

        var result = new CustomerReconciliationVM
        {
            CustomerId = customerId
        };

        result.OutstandingSales = outstandingSales
            .Select(s => new OutstandingSaleVM
            {
                SaleId = s.StockMainID,
                TransactionNo = s.TransactionNo,
                TransactionDate = s.TransactionDate,
                CustomerName = s.Party?.Name ?? string.Empty,
                TotalAmount = s.TotalAmount,
                PaidAmount = s.PaidAmount,
                ReturnedAmount = Math.Max(0, s.TotalAmount - s.PaidAmount - s.BalanceAmount),
                BalanceAmount = s.BalanceAmount
            })
            .OrderByDescending(s => s.TransactionDate)
            .ToList();

        result.OpenCreditNotes = openCredits
            .Select(c => new OpenCreditNoteVM
            {
                CreditNoteId = c.CreditNoteID,
                CreditNoteNo = c.CreditNoteNo,
                CreditDate = c.CreditDate,
                TotalAmount = c.TotalAmount,
                AppliedAmount = c.AppliedAmount,
                BalanceAmount = c.BalanceAmount,
                SourceTransactionNo = c.SourceStockMain?.TransactionNo,
                CustomerName = c.Party?.Name ?? string.Empty
            })
            .OrderBy(c => c.CreditDate)
            .ToList();

        result.CustomerName = result.OutstandingSales.FirstOrDefault()?.CustomerName
                              ?? result.OpenCreditNotes.FirstOrDefault()?.CustomerName;

        return result;
    }

    public async Task ApplyCreditNoteAsync(int creditNoteId, int saleId, decimal amount, int userId)
    {
        await ExecuteInTransactionAsync(async () =>
        {
            if (amount <= 0)
            {
                throw new InvalidOperationException("Applied amount must be greater than zero.");
            }

            var creditNote = await _creditNoteRepository.Query()
                .FirstOrDefaultAsync(c => c.CreditNoteID == creditNoteId);

            if (creditNote == null)
            {
                throw new InvalidOperationException("Credit note not found.");
            }

            if (creditNote.Status != "Open" || creditNote.BalanceAmount <= 0)
            {
                throw new InvalidOperationException("Credit note is not open for allocation.");
            }

            if (amount > creditNote.BalanceAmount)
            {
                throw new InvalidOperationException("Applied amount exceeds available credit.");
            }

            var sale = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .FirstOrDefaultAsync(s => s.StockMainID == saleId);

            if (sale == null || !string.Equals(sale.TransactionType?.Code, SALE_TRANSACTION_TYPE_CODE, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Target sale not found.");
            }

            if (!string.Equals(sale.Status, "Approved", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Credit notes can only be applied to approved sales.");
            }

            if (!sale.Party_ID.HasValue || sale.Party_ID.Value != creditNote.Party_ID)
            {
                throw new InvalidOperationException("Credit note customer does not match the selected sale.");
            }

            var saleOutstanding = await GetNetOutstandingAmountAsync(sale.StockMainID, sale.TotalAmount, sale.PaidAmount);
            if (saleOutstanding <= 0)
            {
                throw new InvalidOperationException("Selected sale has no outstanding balance.");
            }

            if (amount > saleOutstanding)
            {
                throw new InvalidOperationException("Applied amount exceeds sale outstanding balance.");
            }

            sale.PaidAmount += amount;
            await RecalculateSaleBalanceIncludingReturnsAsync(sale, userId);
            _stockMainRepository.Update(sale);

            creditNote.AppliedAmount = Math.Round(creditNote.AppliedAmount + amount, 2);
            creditNote.BalanceAmount = Math.Round(Math.Max(0, creditNote.TotalAmount - creditNote.AppliedAmount), 2);
            creditNote.Status = creditNote.BalanceAmount <= 0 ? "Applied" : "Open";
            creditNote.UpdatedAt = DateTime.Now;
            creditNote.UpdatedBy = userId;
            _creditNoteRepository.Update(creditNote);

            await _paymentAllocationRepository.AddAsync(new PaymentAllocation
            {
                CreditNote_ID = creditNote.CreditNoteID,
                StockMain_ID = sale.StockMainID,
                Amount = amount,
                SourceType = "CreditNote",
                AllocationDate = DateTime.Now,
                Remarks = $"Applied {creditNote.CreditNoteNo} against {sale.TransactionNo}",
                CreatedAt = DateTime.Now,
                CreatedBy = userId
            });

            await _unitOfWork.SaveChangesAsync();
            return true;
        });
    }

    public async Task<bool> VoidReceiptAsync(int paymentId, string reason, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException("Void reason is required.");
            }

            var receipt = await _paymentRepository.Query()
                .Include(p => p.StockMain)
                    .ThenInclude(s => s!.TransactionType)
                .FirstOrDefaultAsync(p => p.PaymentID == paymentId && p.PaymentType == CustomerReceiptPaymentType);

            if (receipt == null || receipt.IsVoided)
            {
                return false;
            }

            receipt.IsVoided = true;
            receipt.VoidReason = reason.Trim();
            receipt.VoidedAt = DateTime.Now;
            receipt.VoidedBy = userId;
            receipt.UpdatedAt = DateTime.Now;
            receipt.UpdatedBy = userId;
            _paymentRepository.Update(receipt);

            var allocations = await _paymentAllocationRepository.Query()
                .Where(a => a.Payment_ID == receipt.PaymentID)
                .ToListAsync();
            if (allocations.Count > 0)
            {
                _paymentAllocationRepository.RemoveRange(allocations);
            }

            if (receipt.StockMain != null && string.Equals(receipt.StockMain.TransactionType?.Code, SALE_TRANSACTION_TYPE_CODE, StringComparison.OrdinalIgnoreCase))
            {
                receipt.StockMain.PaidAmount = Math.Max(0, receipt.StockMain.PaidAmount - receipt.Amount);
                await RecalculateSaleBalanceIncludingReturnsAsync(receipt.StockMain, userId);
                _stockMainRepository.Update(receipt.StockMain);
            }

            if (receipt.Voucher_ID.HasValue)
            {
                await CreateVoucherReversalAsync(receipt.Voucher_ID.Value, userId, reason);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        });
    }

    public async Task<bool> VoidRefundAsync(int paymentId, string reason, int userId)
    {
        return await ExecuteInTransactionAsync(async () =>
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new InvalidOperationException("Void reason is required.");
            }

            var refund = await _paymentRepository.Query()
                .FirstOrDefaultAsync(p => p.PaymentID == paymentId && p.PaymentType == RefundPaymentType);

            if (refund == null || refund.IsVoided)
            {
                return false;
            }

            refund.IsVoided = true;
            refund.VoidReason = reason.Trim();
            refund.VoidedAt = DateTime.Now;
            refund.VoidedBy = userId;
            refund.UpdatedAt = DateTime.Now;
            refund.UpdatedBy = userId;
            _paymentRepository.Update(refund);

            if (refund.Voucher_ID.HasValue)
            {
                await CreateVoucherReversalAsync(refund.Voucher_ID.Value, userId, reason);
            }

            await _unitOfWork.SaveChangesAsync();
            return true;
        });
    }

    private static string NormalizePaymentMethod(string? paymentMethod, int accountTypeId)
    {
        if (!Enum.TryParse<PaymentMethod>(paymentMethod, true, out var parsedMethod))
        {
            parsedMethod = accountTypeId == BankAccountTypeId ? PaymentMethod.Bank : PaymentMethod.Cash;
        }

        if (parsedMethod == PaymentMethod.Adjustment)
        {
            throw new InvalidOperationException("Adjustment is not a valid customer payment method.");
        }

        if (parsedMethod == PaymentMethod.Cash && accountTypeId != CashAccountTypeId)
        {
            throw new InvalidOperationException("Cash method requires a cash account.");
        }

        if ((parsedMethod == PaymentMethod.Bank || parsedMethod == PaymentMethod.Cheque) && accountTypeId != BankAccountTypeId)
        {
            throw new InvalidOperationException("Bank or Cheque method requires a bank account.");
        }

        return parsedMethod.ToString();
    }

    private async Task<Voucher?> CreateVoucherReversalAsync(int originalVoucherId, int userId, string reason)
    {
        var originalVoucher = await _voucherRepository.Query()
            .Include(v => v.VoucherDetails)
            .FirstOrDefaultAsync(v => v.VoucherID == originalVoucherId);

        if (originalVoucher == null || originalVoucher.IsReversed)
        {
            return null;
        }

        var reversalVoucher = new Voucher
        {
            VoucherType_ID = originalVoucher.VoucherType_ID,
            VoucherNo = $"REV-{originalVoucher.VoucherNo}",
            VoucherDate = DateTime.Now,
            TotalDebit = originalVoucher.TotalCredit,
            TotalCredit = originalVoucher.TotalDebit,
            Status = "Posted",
            SourceTable = originalVoucher.SourceTable,
            SourceID = originalVoucher.SourceID,
            Narration = $"Reversal of {originalVoucher.VoucherNo} - Void: {reason}",
            ReversesVoucher_ID = originalVoucher.VoucherID,
            CreatedAt = DateTime.Now,
            CreatedBy = userId
        };

        foreach (var detail in originalVoucher.VoucherDetails)
        {
            reversalVoucher.VoucherDetails.Add(new VoucherDetail
            {
                Account_ID = detail.Account_ID,
                DebitAmount = detail.CreditAmount,
                CreditAmount = detail.DebitAmount,
                Description = $"Reversal - {detail.Description}",
                Party_ID = detail.Party_ID,
                Product_ID = detail.Product_ID
            });
        }

        await _voucherRepository.AddAsync(reversalVoucher);

        originalVoucher.IsReversed = true;
        originalVoucher.ReversedByVoucher = reversalVoucher;
        _voucherRepository.Update(originalVoucher);

        return reversalVoucher;
    }

    private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var result = await operation();
            await _unitOfWork.CommitTransactionAsync();
            return result;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}
