using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.DTOs;
using PharmaCare.Application.DTOs.Transactions;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Interfaces.Transactions;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Transactions;

public class JournalVoucherService : IJournalVoucherService
{
    private readonly IRepository<Voucher> _voucherRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFinancialPeriodService _financialPeriodService;

    public JournalVoucherService(
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IUnitOfWork unitOfWork,
        IFinancialPeriodService financialPeriodService)
    {
        _voucherRepository = voucherRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _unitOfWork = unitOfWork;
        _financialPeriodService = financialPeriodService;
    }

    /// <summary>
    /// A manual journal is the most direct route into the general ledger there is — and the one an
    /// operator reaches for precisely BECAUSE the transaction screens refuse a closed period. It
    /// must honour the lock like every other posting path, or closing a period means nothing.
    /// </summary>
    private async Task ValidatePeriodAsync(DateTime date)
    {
        if (await _financialPeriodService.IsPeriodLockedAsync(date))
        {
            throw new InvalidOperationException(
                $"The date {date:dd/MM/yyyy} falls within a closed financial period. Transactions are locked.");
        }
    }

    /// <summary>
    /// Re-checks the posting date immediately before commit, holding the same lock
    /// <c>ClosePeriodAsync</c> takes. The check above runs before the work starts, so on its own it
    /// would still let a period closed mid-flight accept the voucher. Must be called inside a
    /// transaction — the lock is transaction-scoped.
    /// </summary>
    private async Task RecheckPeriodBeforeCommitAsync(DateTime date)
    {
        await _unitOfWork.AcquireResourceLockAsync(AccountingConstants.PeriodCloseLockResource);

        if (await _financialPeriodService.IsPeriodLockedAsync(date))
        {
            throw new InvalidOperationException(
                $"The financial period covering {date:dd/MM/yyyy} was closed while this transaction was being saved. It has not been posted.");
        }
    }

    public async Task<IEnumerable<Voucher>> GetAllJournalVouchersAsync()
    {
        // "JV" is the voucher type for manual journals; every other type is machine-generated
        // by a transaction service. The null-forgiving operator is safe inside the expression
        // tree — it is translated to a SQL join, never dereferenced in memory.
        return await _voucherRepository.Query()
            .Include(v => v.VoucherType)
            .Where(v => v.VoucherType!.Code == "JV")
            .OrderByDescending(v => v.VoucherID)
            .ToListAsync();
    }

    public async Task<PagedResult<Voucher>> GetPagedJournalVouchersAsync(string? search, string? status, int page, int pageSize)
    {
        var query = _voucherRepository.Query()
            .AsNoTracking()
            .Include(v => v.VoucherType)
            .Where(v => v.VoucherType!.Code == "JV");

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(v => v.VoucherNo.Contains(term) || (v.Narration != null && v.Narration.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            // Reversal is tracked by IsReversed, not Status — a reversed JV keeps Status "Posted"
            // so ledger reports still net it against its reversal.
            query = status == "Reversed"
                ? query.Where(v => v.IsReversed)
                : query.Where(v => v.Status == status && !v.IsReversed);
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(v => v.VoucherID)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<Voucher>
        {
            Items = items,
            TotalCount = totalCount,
            CurrentPage = page,
            PageSize = pageSize
        };
    }

    public async Task<Voucher?> GetByIdAsync(int id)
    {
        return await _voucherRepository.Query()
            .Include(v => v.VoucherDetails)
                .ThenInclude(vd => vd.Account)
            .Include(v => v.VoucherType)
            .FirstOrDefaultAsync(v => v.VoucherID == id);
    }

    public async Task<Voucher> CreateJournalVoucherAsync(JournalVoucherDto model, int userId)
    {
        // 1. Get JV Type
        var jvType = await _voucherTypeRepository.GetByIdAsync(model.VoucherType_ID);
        if (jvType == null)
            throw new Exception("Voucher Type not found.");
        
        if (jvType.IsAutoGenerated)
             throw new Exception("Cannot manually create an auto-generated voucher type.");

        await ValidatePeriodAsync(model.VoucherDate);

        // A negative credit is really a debit. Such a voucher still balances arithmetically, so the
        // debit == credit check below waves it through, but it leaves the ledger in a state where
        // summing a column and summing a signed net disagree. Reject it at the line level.
        if (model.VoucherDetails.Any(d => d.DebitAmount < 0 || d.CreditAmount < 0))
             throw new InvalidOperationException("Voucher lines cannot carry negative amounts. Use the opposite column instead.");

        // Normalize every line to 2dp BEFORE the balance check. The columns are decimal(18,2), so
        // amounts finer than a cent would be rounded per-line by the database — a set of lines
        // that balances at 4dp can store unbalanced at 2dp.
        foreach (var det in model.VoucherDetails)
        {
            det.DebitAmount = Math.Round(det.DebitAmount, 2);
            det.CreditAmount = Math.Round(det.CreditAmount, 2);
        }

        // SECURITY: Recalculate totals from details instead of trusting model scalars
        decimal calculatedTotalDebit = model.VoucherDetails.Sum(d => d.DebitAmount);
        decimal calculatedTotalCredit = model.VoucherDetails.Sum(d => d.CreditAmount);

        if (calculatedTotalDebit != calculatedTotalCredit)
             throw new InvalidOperationException("Voucher must be balanced. Total Debit must equal Total Credit.");

        if (calculatedTotalDebit <= 0)
             throw new InvalidOperationException("Voucher amount must be greater than zero.");

        // SANITY CHECK: Prevent nonsensical amounts
        if (calculatedTotalDebit > AccountingConstants.MaxTransactionAmount)
             throw new InvalidOperationException("Voucher amount exceeds sanity limit (100 Million).");

        // 2. Map ViewModel to Entity
        var voucher = new Voucher
        {
            VoucherType_ID = jvType.VoucherTypeID,
            VoucherNo = await GenerateVoucherNoAsync(),
            VoucherDate = model.VoucherDate,
            Narration = model.Narration,
            Status = "Posted",
            TotalDebit = calculatedTotalDebit,
            TotalCredit = calculatedTotalCredit,
            IsReversed = false,
            CreatedAt = AppTime.Now,
            CreatedBy = userId
        };

        // 3. Add Details
        foreach (var det in model.VoucherDetails)
        {
            voucher.VoucherDetails.Add(new VoucherDetail
            {
                Account_ID = det.Account_ID,
                DebitAmount = det.DebitAmount,
                CreditAmount = det.CreditAmount,
                Description = det.Description
            });
        }

        // 4. Save. A transaction rather than a bare SaveChanges, so the period re-check below can
        // hold the close lock until the voucher is actually committed.
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            await _voucherRepository.AddAsync(voucher);
            await _unitOfWork.SaveChangesAsync();

            await RecheckPeriodBeforeCommitAsync(model.VoucherDate);

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }

        return voucher;
    }

    public async Task<bool> VoidVoucherAsync(int voucherId, string reason, int userId)
    {
        var original = await GetByIdAsync(voucherId);
        if (original == null) return false;
        if (original.IsReversed || original.ReversesVoucher_ID != null)
            throw new InvalidOperationException("Reversal not allowed. This voucher is already reversed or is itself a reversal entry.");

        // Only manual JVs may be reversed here. A voucher generated by a source document (sale,
        // GRN, payment...) must be unwound by voiding THAT document — reversing it in the GL alone
        // leaves the stock and subledger claiming the transaction still happened.
        if (original.VoucherType?.Code != "JV" || !string.IsNullOrEmpty(original.SourceTable))
            throw new InvalidOperationException(
                "Only manual journal vouchers can be reversed here. Void the source transaction instead.");

        // Both dates matter: unwinding the original touches its period, and the reversal below is
        // dated NOW, so it posts into today's period. Either being closed blocks the reversal.
        await ValidatePeriodAsync(original.VoucherDate);
        await ValidatePeriodAsync(AppTime.Now);

        // Marking the original, posting the reversal and linking the two must all land or none of
        // them. It takes two SaveChanges because the reversal's id does not exist until the first
        // one completes, and a failure in between would leave a posted reversal that the original
        // does not point back to. The transaction also brings this under the audit-log deferral,
        // so a rollback leaves no activity-log rows describing a reversal that never happened.
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // The IsReversed check above ran before this transaction opened, so two concurrent
            // reversals could both pass it. Serialize on the voucher and re-read committed state:
            // the reversal is minted with a fresh voucher number, so no unique index would catch a
            // duplicate — both would post and leave every touched account double-credited.
            await _unitOfWork.AcquireResourceLockAsync($"voucher:{voucherId}");

            var alreadyReversed = await _voucherRepository.Query()
                .AsNoTracking()
                .AnyAsync(v => v.VoucherID == voucherId && v.IsReversed);

            if (alreadyReversed)
                throw new InvalidOperationException("This voucher has already been reversed.");

            // 1. Mark Original as Reversed. Status stays "Posted": the trial balance and general
            // ledger include only "Posted" vouchers, so flipping the status here would count the
            // reversal but not the original, leaving every touched account with a one-sided
            // residue. IsReversed is the void marker, same as every other void path.
            original.IsReversed = true;
            original.VoidReason = reason;

            // 2. Create Reversal Voucher. Header totals are swapped to mirror the original.
            var reversal = new Voucher
            {
                VoucherType_ID = original.VoucherType_ID,
                VoucherNo = await GenerateVoucherNoAsync(),
                VoucherDate = AppTime.Now, // Reversal date is NOW
                Narration = $"Reversal of {original.VoucherNo} - {reason}",
                Status = "Posted", // The reversal itself is a valid posted transaction
                TotalDebit = original.TotalCredit,
                TotalCredit = original.TotalDebit,
                IsReversed = false,
                ReversesVoucher_ID = original.VoucherID,
                CreatedAt = AppTime.Now,
                CreatedBy = userId
            };

            // 3. Create Reversal Details (Swap Debit/Credit)
            foreach (var det in original.VoucherDetails)
            {
                reversal.VoucherDetails.Add(new VoucherDetail
                {
                    Account_ID = det.Account_ID,
                    DebitAmount = det.CreditAmount, // Swap
                    CreditAmount = det.DebitAmount, // Swap
                    Description = $"Reversal: {det.Description}"
                });
            }

            await _voucherRepository.AddAsync(reversal);
            await _unitOfWork.SaveChangesAsync(); // Save first so the reversal has an id to link to

            original.ReversedByVoucher_ID = reversal.VoucherID;
            _voucherRepository.Update(original);
            await _unitOfWork.SaveChangesAsync();

            // The reversal posts at AppTime.Now, so that is the date that must still be open.
            await RecheckPeriodBeforeCommitAsync(AppTime.Now);

            await _unitOfWork.CommitTransactionAsync();
            return true;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<string> GenerateVoucherNoAsync()
    {
        // Shared generator: JV-yyyyMMdd-XXXX (serialized when called inside a transaction)
        return await _voucherRepository.GenerateVoucherNoAsync("JV", _unitOfWork);
    }
}
