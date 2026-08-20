using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Accounting;

public class AccountHeadService : IAccountHeadService
{
    private readonly IRepository<AccountHead> _repository;
    private readonly IRepository<AccountFamily> _familyRepository;
    private readonly IRepository<AccountSubhead> _subheadRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<VoucherDetail> _voucherDetailRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountHeadService(
        IRepository<AccountHead> repository,
        IRepository<AccountFamily> familyRepository,
        IRepository<AccountSubhead> subheadRepository,
        IRepository<Account> accountRepository,
        IRepository<VoucherDetail> voucherDetailRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _familyRepository = familyRepository;
        _subheadRepository = subheadRepository;
        _accountRepository = accountRepository;
        _voucherDetailRepository = voucherDetailRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<AccountHead>> GetAllAsync()
    {
        return await _repository.Query()
            .Include(h => h.AccountFamily)
            .OrderBy(h => h.AccountHeadID)
            .ToListAsync();
    }

    public async Task<AccountHead?> GetByIdAsync(int id)
    {
        return await _repository.Query()
            .Include(h => h.AccountFamily)
            .FirstOrDefaultAsync(h => h.AccountHeadID == id);
    }

    public async Task<AccountHead> CreateAsync(AccountHead accountHead)
    {
        await EnsureFamilyIsOursAsync(accountHead.AccountFamily_ID);

        await _repository.AddAsync(accountHead);
        await _unitOfWork.SaveChangesAsync();
        return accountHead;
    }

    public async Task<bool> UpdateAsync(AccountHead accountHead)
    {
        var existing = await _repository.FirstOrDefaultAsync(h => h.AccountHeadID == accountHead.AccountHeadID);
        if (existing == null) return false;

        // Read the STORED values as a projection, never off `existing`: a caller passing the
        // tracked instance it already mutated makes the two sides of the comparison one object.
        var stored = await _repository.Query()
            .AsNoTracking()
            .Where(h => h.AccountHeadID == accountHead.AccountHeadID)
            .Select(h => new { h.AccountFamily_ID, h.Code })
            .FirstOrDefaultAsync();
        if (stored == null) return false;

        if (accountHead.AccountFamily_ID != stored.AccountFamily_ID)
        {
            // The family decides which SECTION of the balance sheet / P&L every account under
            // this head reports in. Re-parenting a head silently moves all of them at once — no
            // account row changes, so nothing on the chart-of-accounts screen shows what moved.
            if (stored.Code != null)
            {
                throw new InvalidOperationException(
                    "This account head is part of the pharmacy's provisioned chart of accounts " +
                    "and cannot be moved to a different family.");
            }

            if (await HasPostedEntriesAsync(accountHead.AccountHeadID))
            {
                throw new InvalidOperationException(
                    "Accounts under this head carry posted ledger entries, so its family can no " +
                    "longer be changed — doing so would reclassify those balances with no " +
                    "posting to show for it.");
            }

            await EnsureFamilyIsOursAsync(accountHead.AccountFamily_ID);
        }

        existing.HeadName = accountHead.HeadName;
        existing.AccountFamily_ID = accountHead.AccountFamily_ID;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var existing = await _repository.FirstOrDefaultAsync(h => h.AccountHeadID == id);
        if (existing == null) return false;

        // Pre-check the children the foreign keys would reject anyway, so the user gets a message
        // naming the blocker instead of the generic line SafeErrorMessage substitutes for a raw
        // DbUpdateException.
        var subheads = await _subheadRepository.Query().CountAsync(s => s.AccountHead_ID == id);
        if (subheads > 0)
        {
            throw new InvalidOperationException(
                $"This account head still owns {subheads} subhead(s). Move or delete them first.");
        }

        var accounts = await _accountRepository.Query().CountAsync(a => a.AccountHead_ID == id);
        if (accounts > 0)
        {
            throw new InvalidOperationException(
                $"This account head still owns {accounts} account(s). Move or delete them first.");
        }

        _repository.Remove(existing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AccountFamily>> GetFamiliesForDropdownAsync()
    {
        return await _familyRepository.Query()
            .OrderBy(f => f.AccountFamilyID) // OR Name if preferable, but ID is standard sort for families (100, 200 etc)
            .ToListAsync();
    }

    /// <summary>
    /// The family id arrives from a form. The tenant filter hides other pharmacies' families from
    /// queries but the database accepts the reference anyway (the FK carries no Pharmacy_ID), so
    /// an unchecked id can parent this pharmacy's head under another pharmacy's family — a branch
    /// whose parent its own owner can never see.
    /// </summary>
    private async Task EnsureFamilyIsOursAsync(int familyId)
    {
        var visible = await _familyRepository.Query().AnyAsync(f => f.AccountFamilyID == familyId);
        if (!visible)
        {
            throw new InvalidOperationException("The selected account family was not found.");
        }
    }

    /// <summary>Any posted voucher line against any account under this head.</summary>
    private async Task<bool> HasPostedEntriesAsync(int headId)
    {
        return await _voucherDetailRepository.Query()
            .AnyAsync(d => d.Voucher!.Status == "Posted" && d.Account!.AccountHead_ID == headId);
    }
}
