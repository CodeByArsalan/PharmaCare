using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Finance;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Finance;
using PharmaCare.Domain.Entities.Transactions;
using PharmaCare.Domain.Enums;

namespace PharmaCare.Application.Implementations.Finance;

/// <summary>
/// Service for supplier credit notes — financial credits reducing supplier payable.
/// No stock movement occurs.
/// </summary>
public class SupplierCreditNoteService : ISupplierCreditNoteService
{
    private const string PREFIX = "SCN";
    private const string JV_VOUCHER_CODE = "JV"; // Journal Voucher for the entry

    private readonly IRepository<SupplierCreditNote> _repository;
    private readonly IRepository<Party> _partyRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<Voucher> _voucherRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<StockMain> _stockMainRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFinancialPeriodService _financialPeriodService;

    public SupplierCreditNoteService(
        IRepository<SupplierCreditNote> repository,
        IRepository<Party> partyRepository,
        IRepository<Account> accountRepository,
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<StockMain> stockMainRepository,
        IUnitOfWork unitOfWork,
        IFinancialPeriodService financialPeriodService)
    {
        _repository = repository;
        _partyRepository = partyRepository;
        _accountRepository = accountRepository;
        _voucherRepository = voucherRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _stockMainRepository = stockMainRepository;
        _unitOfWork = unitOfWork;
        _financialPeriodService = financialPeriodService;
    }

    public async Task<IEnumerable<SupplierCreditNote>> GetAllAsync(int? supplierId = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var query = _repository.Query()
            .Include(c => c.Party)
            .Include(c => c.SourceStockMain)
            .Include(c => c.Voucher)
            .AsQueryable();

        if (supplierId.HasValue && supplierId.Value > 0)
            query = query.Where(c => c.Party_ID == supplierId.Value);

        if (fromDate.HasValue)
            query = query.Where(c => c.CreditDate >= fromDate.Value.Date);

        if (toDate.HasValue)
            query = query.Where(c => c.CreditDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));

        return await query
            .OrderByDescending(c => c.CreditDate)
            .ThenByDescending(c => c.SupplierCreditNoteID)
            .ToListAsync();
    }

    public async Task<IEnumerable<SupplierCreditNote>> GetOpenAsync(int? supplierId = null)
    {
        var query = _repository.Query()
            .Include(c => c.Party)
            .Include(c => c.SourceStockMain)
            .Where(c => c.Status == "Open" && c.BalanceAmount > 0);

        if (supplierId.HasValue && supplierId.Value > 0)
            query = query.Where(c => c.Party_ID == supplierId.Value);

        return await query
            .OrderBy(c => c.CreditDate)
            .ThenBy(c => c.CreditNoteNo)
            .ToListAsync();
    }

    public async Task<SupplierCreditNote?> GetByIdAsync(int id)
    {
        return await _repository.Query()
            .Include(c => c.Party)
            .Include(c => c.SourceStockMain)
            .Include(c => c.Voucher)
                .ThenInclude(v => v!.VoucherDetails)
            .FirstOrDefaultAsync(c => c.SupplierCreditNoteID == id);
    }

    /// <summary>
    /// Creates a supplier credit note.
    /// DR: Supplier Account (Accounts Payable) — reduces what we owe
    /// CR: Supplier Credit Note Liability (or directly credit supplier account)
    /// In practice we debit Supplier Payable and credit Purchases Return / Expense.
    /// </summary>
    public async Task<SupplierCreditNote> CreateAsync(SupplierCreditNote creditNote, int userId)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (creditNote.TotalAmount <= 0)
                throw new InvalidOperationException("Credit note amount must be greater than zero.");

            if (creditNote.Party_ID <= 0)
                throw new InvalidOperationException("Supplier is required.");

            if (await _financialPeriodService.IsPeriodLockedAsync(creditNote.CreditDate))
                throw new InvalidOperationException("The financial period for this credit note date is closed.");

            var supplier = await _partyRepository.Query()
                .Include(p => p.Account)
                .FirstOrDefaultAsync(p => p.PartyID == creditNote.Party_ID);

            if (supplier == null)
                throw new InvalidOperationException("Supplier not found.");

            if (supplier.Account == null)
                throw new InvalidOperationException($"Supplier '{supplier.Name}' does not have a linked account.");

            if (!creditNote.AdjustmentAccount_ID.HasValue || creditNote.AdjustmentAccount_ID.Value <= 0)
                throw new InvalidOperationException("An adjustment (contra) account is required for the credit note.");

            var adjustmentAccount = await _accountRepository.Query()
                .FirstOrDefaultAsync(a => a.AccountID == creditNote.AdjustmentAccount_ID.Value && a.IsActive);

            if (adjustmentAccount == null)
                throw new InvalidOperationException("The selected adjustment account was not found or is inactive.");

            if (adjustmentAccount.AccountID == supplier.Account.AccountID)
                throw new InvalidOperationException("The adjustment account must be different from the supplier account.");

            creditNote.CreditNoteNo = await GenerateCreditNoteNoAsync();
            creditNote.AppliedAmount = 0;
            creditNote.BalanceAmount = creditNote.TotalAmount;
            creditNote.Status = "Open";
            creditNote.CreatedAt = DateTime.Now;
            creditNote.CreatedBy = userId;

            // Journal Entry: DR Supplier Account (reduce payable), CR the chosen adjustment/contra
            // account (Purchase Returns & Allowances, rebate income, etc.) so the voucher produces
            // real ledger movement rather than a self-cancelling supplier DR/CR.
            var voucherType = await _voucherTypeRepository.Query()
                .FirstOrDefaultAsync(vt => vt.Code == JV_VOUCHER_CODE);

            if (voucherType != null)
            {
                var voucherNo = await GenerateVoucherNoAsync();
                var voucher = new Voucher
                {
                    VoucherType_ID = voucherType.VoucherTypeID,
                    VoucherNo = voucherNo,
                    VoucherDate = creditNote.CreditDate,
                    TotalDebit = creditNote.TotalAmount,
                    TotalCredit = creditNote.TotalAmount,
                    Status = "Posted",
                    SourceTable = "SupplierCreditNote",
                    Narration = $"Supplier credit note {creditNote.CreditNoteNo} from {supplier.Name}",
                    CreatedAt = DateTime.Now,
                    CreatedBy = userId,
                    VoucherDetails = new List<VoucherDetail>
                    {
                        // DR: Supplier Account — reduces what we owe
                        new VoucherDetail
                        {
                            Account_ID = supplier.Account.AccountID,
                            DebitAmount = creditNote.TotalAmount,
                            CreditAmount = 0,
                            Description = $"Supplier CN {creditNote.CreditNoteNo} — reduces payable",
                            Party_ID = supplier.PartyID
                        },
                        // CR: Adjustment / contra account (real ledger movement)
                        new VoucherDetail
                        {
                            Account_ID = adjustmentAccount.AccountID,
                            DebitAmount = 0,
                            CreditAmount = creditNote.TotalAmount,
                            Description = $"Supplier CN {creditNote.CreditNoteNo} — {adjustmentAccount.Name}"
                        }
                    }
                };

                await _voucherRepository.AddAsync(voucher);
                creditNote.Voucher = voucher;
            }

            await _repository.AddAsync(creditNote);
            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return creditNote;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<bool> VoidAsync(int id, string reason, int userId)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new InvalidOperationException("Void reason is required.");

            var cn = await _repository.Query()
                .Include(c => c.Voucher)
                    .ThenInclude(v => v!.VoucherDetails)
                .FirstOrDefaultAsync(c => c.SupplierCreditNoteID == id);

            if (cn == null || cn.Status == "Void")
                return false;

            if (await _financialPeriodService.IsPeriodLockedAsync(cn.CreditDate))
                throw new InvalidOperationException("The financial period for this credit note date is closed.");

            if (cn.AppliedAmount > 0)
                throw new InvalidOperationException("Cannot void a credit note that has been partially applied.");

            cn.Status = "Void";
            cn.VoidReason = reason.Trim();
            cn.VoidedAt = DateTime.Now;
            cn.VoidedBy = userId;
            cn.UpdatedAt = DateTime.Now;
            cn.UpdatedBy = userId;
            _repository.Update(cn);

            // Reverse accounting voucher
            if (cn.Voucher_ID.HasValue && cn.Voucher != null && !cn.Voucher.IsReversed)
            {
                var reversal = new Voucher
                {
                    VoucherType_ID = cn.Voucher.VoucherType_ID,
                    VoucherNo = $"REV-{cn.Voucher.VoucherNo}",
                    VoucherDate = DateTime.Now,
                    TotalDebit = cn.Voucher.TotalCredit,
                    TotalCredit = cn.Voucher.TotalDebit,
                    Status = "Posted",
                    SourceTable = "SupplierCreditNote",
                    Narration = $"Reversal of {cn.Voucher.VoucherNo} — Void: {reason}",
                    ReversesVoucher_ID = cn.Voucher.VoucherID,
                    CreatedAt = DateTime.Now,
                    CreatedBy = userId
                };

                foreach (var detail in cn.Voucher.VoucherDetails)
                {
                    reversal.VoucherDetails.Add(new VoucherDetail
                    {
                        Account_ID = detail.Account_ID,
                        DebitAmount = detail.CreditAmount,
                        CreditAmount = detail.DebitAmount,
                        Description = $"Reversal — {detail.Description}",
                        Party_ID = detail.Party_ID
                    });
                }

                await _voucherRepository.AddAsync(reversal);
                cn.Voucher.IsReversed = true;
                _voucherRepository.Update(cn.Voucher);
            }

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return true;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    private async Task<string> GenerateCreditNoteNoAsync()
    {
        var datePrefix = $"{PREFIX}-{DateTime.Now:yyyyMMdd}-";
        var last = await _repository.Query()
            .Where(c => c.CreditNoteNo.StartsWith(datePrefix))
            .OrderByDescending(c => c.CreditNoteNo)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last != null)
        {
            var parts = last.CreditNoteNo.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int n))
                next = n + 1;
        }
        return $"{datePrefix}{next:D4}";
    }

    private async Task<string> GenerateVoucherNoAsync()
    {
        var datePrefix = $"JV-{DateTime.Now:yyyyMMdd}-";
        var last = await _voucherRepository.Query()
            .Where(v => v.VoucherNo.StartsWith(datePrefix))
            .OrderByDescending(v => v.VoucherNo)
            .FirstOrDefaultAsync();

        int next = 1;
        if (last != null)
        {
            var parts = last.VoucherNo.Split('-');
            if (parts.Length > 2 && int.TryParse(parts.Last(), out int n))
                next = n + 1;
        }
        return $"{datePrefix}{next:D4}";
    }

    public async Task<bool> ApplyToGrnAsync(int cnId, int grnId, decimal amount, int userId)
    {
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (amount <= 0)
                throw new InvalidOperationException("Amount must be greater than zero.");

            var cn = await _repository.Query()
                .FirstOrDefaultAsync(c => c.SupplierCreditNoteID == cnId && c.Status == "Open");

            if (cn == null)
                throw new InvalidOperationException("Valid open credit note not found.");

            if (amount > cn.BalanceAmount)
                throw new InvalidOperationException($"Cannot apply. Available credit note balance is only {cn.BalanceAmount:N2}");

            var grn = await _stockMainRepository.Query()
                .Include(s => s.TransactionType)
                .FirstOrDefaultAsync(s => s.StockMainID == grnId 
                    && s.TransactionType!.Code == "GRN" 
                    && s.Status == "Approved");

            if (grn == null)
                throw new InvalidOperationException("Approved GRN not found.");

            if (cn.Party_ID != grn.Party_ID)
                throw new InvalidOperationException("Credit Note and GRN do not belong to the same supplier.");

            // Calculate outstanding using GRN logic
            // (Similar to PaymentService but simpler calculation based on known balance logic)
            // Just trust grn.BalanceAmount, as it's maintained by previous actions
            if (amount > grn.BalanceAmount)
                throw new InvalidOperationException($"Cannot apply. GRN outstanding balance is only {grn.BalanceAmount:N2}");

            // Update CN balances
            cn.AppliedAmount += amount;
            cn.BalanceAmount -= amount;
            cn.Status = cn.BalanceAmount <= 0 ? "Applied" : "Open";
            cn.UpdatedAt = DateTime.Now;
            cn.UpdatedBy = userId;

            _repository.Update(cn);

            // Update GRN balances
            grn.PaidAmount += amount;
            grn.BalanceAmount -= amount;
            grn.PaymentStatus = grn.BalanceAmount <= 0
                ? PaymentStatus.Paid.ToString()
                : (grn.PaidAmount <= 0 ? PaymentStatus.Unpaid.ToString() : PaymentStatus.Partial.ToString());
            
            grn.UpdatedAt = DateTime.Now;
            grn.UpdatedBy = userId;

            _stockMainRepository.Update(grn);

            await _unitOfWork.SaveChangesAsync();
            await _unitOfWork.CommitTransactionAsync();
            return true;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }
}
