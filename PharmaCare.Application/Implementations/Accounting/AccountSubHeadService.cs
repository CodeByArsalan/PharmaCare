using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Accounting;

public class AccountSubHeadService : IAccountSubHeadService
{
    private readonly IRepository<AccountSubhead> _repository;
    private readonly IRepository<AccountHead> _headRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<VoucherDetail> _voucherDetailRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountSubHeadService(
        IRepository<AccountSubhead> repository,
        IRepository<AccountHead> headRepository,
        IRepository<Account> accountRepository,
        IRepository<VoucherDetail> voucherDetailRepository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _headRepository = headRepository;
        _accountRepository = accountRepository;
        _voucherDetailRepository = voucherDetailRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IEnumerable<AccountSubhead>> GetAllAsync()
    {
        return await _repository.Query()
            .Include(s => s.AccountHead)
            .OrderBy(s => s.AccountSubheadID)
            .ToListAsync();
    }

    public async Task<AccountSubhead?> GetByIdAsync(int id)
    {
        return await _repository.Query()
            .Include(s => s.AccountHead)
            .FirstOrDefaultAsync(s => s.AccountSubheadID == id);
    }

    public async Task<AccountSubhead> CreateAsync(AccountSubhead accountSubhead)
    {
        await EnsureHeadIsOursAsync(accountSubhead.AccountHead_ID);

        await _repository.AddAsync(accountSubhead);
        await _unitOfWork.SaveChangesAsync();
        return accountSubhead;
    }

    public async Task<bool> UpdateAsync(AccountSubhead accountSubhead)
    {
        var existing = await _repository.FirstOrDefaultAsync(s => s.AccountSubheadID == accountSubhead.AccountSubheadID);
        if (existing == null) return false;

        // Stored values as a projection, never off `existing` — see AccountHeadService.UpdateAsync.
        var stored = await _repository.Query()
            .AsNoTracking()
            .Where(s => s.AccountSubheadID == accountSubhead.AccountSubheadID)
            .Select(s => new { s.AccountHead_ID, s.Code })
            .FirstOrDefaultAsync();
        if (stored == null) return false;

        if (accountSubhead.AccountHead_ID != stored.AccountHead_ID)
        {
            // Same rule as one level up: re-parenting a subhead reclassifies every account under
            // it — the probe that forced this guard moved Accounts Receivable under Current
            // Liabilities while it held posted customer balances.
            if (stored.Code != null)
            {
                throw new InvalidOperationException(
                    "This account subhead is part of the pharmacy's provisioned chart of " +
                    "accounts and cannot be moved to a different head.");
            }

            if (await HasPostedEntriesAsync(accountSubhead.AccountSubheadID))
            {
                throw new InvalidOperationException(
                    "Accounts under this subhead carry posted ledger entries, so its head can no " +
                    "longer be changed — doing so would reclassify those balances with no " +
                    "posting to show for it.");
            }

            await EnsureHeadIsOursAsync(accountSubhead.AccountHead_ID);
        }

        existing.SubheadName = accountSubhead.SubheadName;
        existing.AccountHead_ID = accountSubhead.AccountHead_ID;

        _repository.Update(existing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var existing = await _repository.FirstOrDefaultAsync(s => s.AccountSubheadID == id);
        if (existing == null) return false;

        // Named error instead of a raw FK violation — see AccountHeadService.DeleteAsync.
        var accounts = await _accountRepository.Query().CountAsync(a => a.AccountSubhead_ID == id);
        if (accounts > 0)
        {
            throw new InvalidOperationException(
                $"This account subhead still owns {accounts} account(s). Move or delete them first.");
        }

        _repository.Remove(existing);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<AccountHead>> GetHeadsForDropdownAsync()
    {
        return await _headRepository.Query()
            .OrderBy(h => h.HeadName)
            .ToListAsync();
    }

    /// <summary>Same cross-tenant parent guard as AccountHeadService.EnsureFamilyIsOursAsync.</summary>
    private async Task EnsureHeadIsOursAsync(int headId)
    {
        var visible = await _headRepository.Query().AnyAsync(h => h.AccountHeadID == headId);
        if (!visible)
        {
            throw new InvalidOperationException("The selected account head was not found.");
        }
    }

    private async Task<bool> HasPostedEntriesAsync(int subheadId)
    {
        return await _voucherDetailRepository.Query()
            .AnyAsync(d => d.Voucher!.Status == "Posted" && d.Account!.AccountSubhead_ID == subheadId);
    }
}
