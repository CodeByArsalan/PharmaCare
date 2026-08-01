using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Accounting;
using PharmaCare.Application.Utilities;
using PharmaCare.Domain.Entities.Accounting;
using PharmaCare.Domain.Entities.Configuration;
using PharmaCare.Domain.Entities.Transactions;

namespace PharmaCare.Application.Implementations.Accounting;

/// <inheritdoc cref="IOpeningBalanceService"/>
public class OpeningBalanceService : IOpeningBalanceService
{
    private const string VoucherTypeCode = "JV";
    private const string EquitySubheadName = "Opening Balances";
    private const string EquityAccountName = "Opening Balance Equity";
    private const string CapitalFamilyName = "Capital";
    private const string GeneralAccountTypeCode = "GEN";

    private readonly IRepository<Voucher> _voucherRepository;
    private readonly IRepository<VoucherType> _voucherTypeRepository;
    private readonly IRepository<Account> _accountRepository;
    private readonly IRepository<AccountHead> _accountHeadRepository;
    private readonly IRepository<AccountSubhead> _accountSubheadRepository;
    private readonly IRepository<AccountFamily> _accountFamilyRepository;
    private readonly IRepository<AccountType> _accountTypeRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFinancialPeriodService _financialPeriodService;

    public OpeningBalanceService(
        IRepository<Voucher> voucherRepository,
        IRepository<VoucherType> voucherTypeRepository,
        IRepository<Account> accountRepository,
        IRepository<AccountHead> accountHeadRepository,
        IRepository<AccountSubhead> accountSubheadRepository,
        IRepository<AccountFamily> accountFamilyRepository,
        IRepository<AccountType> accountTypeRepository,
        IUnitOfWork unitOfWork,
        IFinancialPeriodService financialPeriodService)
    {
        _financialPeriodService = financialPeriodService;
        _voucherRepository = voucherRepository;
        _voucherTypeRepository = voucherTypeRepository;
        _accountRepository = accountRepository;
        _accountHeadRepository = accountHeadRepository;
        _accountSubheadRepository = accountSubheadRepository;
        _accountFamilyRepository = accountFamilyRepository;
        _accountTypeRepository = accountTypeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task PostOpeningBalanceDeltaAsync(Party party, decimal previousBalance, int userId)
    {
        var delta = Math.Round(party.OpeningBalance - previousBalance, 2);
        if (delta == 0 || !party.Account_ID.HasValue) return;

        // The voucher below is dated NOW, so editing a party's opening balance posts into today's
        // period. That is a general-ledger write like any other and must honour the lock — the
        // no-op cases above are checked first so merely saving a party with an unchanged balance
        // is never blocked.
        if (await _financialPeriodService.IsPeriodLockedAsync(AppTime.Now))
        {
            throw new InvalidOperationException(
                "The current financial period is closed. Re-open it before changing an opening balance.");
        }

        var voucherType = await _voucherTypeRepository.Query()
            .FirstOrDefaultAsync(vt => vt.Code == VoucherTypeCode)
            ?? throw new InvalidOperationException($"Voucher type '{VoucherTypeCode}' not found.");

        var equityAccount = await ResolveOrCreateEquityAccountAsync(userId);

        // Sign convention matches how the balance reports read OpeningBalance:
        //   customer: positive = they owe us   -> DEBIT  the party's receivable account
        //   supplier: positive = we owe them   -> CREDIT the party's payable account
        // "Both" is treated as a customer, mirroring PartyService's account creation.
        var isSupplier = party.PartyType.Equals("Supplier", StringComparison.OrdinalIgnoreCase);
        var amount = Math.Abs(delta);

        // A negative delta simply reverses the direction of the same pair of entries.
        var partyIsDebit = isSupplier ? delta < 0 : delta > 0;

        var details = new List<VoucherDetail>
        {
            new()
            {
                Account_ID = party.Account_ID.Value,
                Party_ID = party.PartyID,
                DebitAmount = partyIsDebit ? amount : 0,
                CreditAmount = partyIsDebit ? 0 : amount,
                Description = $"Opening balance – {party.Name}"
            },
            new()
            {
                Account_ID = equityAccount.AccountID,
                DebitAmount = partyIsDebit ? 0 : amount,
                CreditAmount = partyIsDebit ? amount : 0,
                Description = $"Opening balance equity – {party.Name}"
            }
        };

        var voucher = new Voucher
        {
            VoucherType_ID = voucherType.VoucherTypeID,
            VoucherNo = await _voucherRepository.GenerateVoucherNoAsync(VoucherTypeCode, _unitOfWork),
            VoucherDate = AppTime.Now,
            TotalDebit = amount,
            TotalCredit = amount,
            Status = "Posted",
            SourceTable = "Party",
            SourceID = party.PartyID,
            Narration = previousBalance == 0
                ? $"Opening balance for {party.PartyType.ToLowerInvariant()} {party.Name}"
                : $"Opening balance adjustment for {party.PartyType.ToLowerInvariant()} {party.Name} " +
                  $"({previousBalance:N2} -> {party.OpeningBalance:N2})",
            CreatedAt = AppTime.Now,
            CreatedBy = userId,
            VoucherDetails = details
        };

        await _voucherRepository.AddAsync(voucher);
    }

    /// <summary>
    /// Finds the tenant's Opening Balance Equity account, creating the subhead and account on
    /// first use. Self-healing rather than a hard setup error, because pharmacies provisioned
    /// before this feature existed have no such account and would otherwise be unable to save a
    /// party with an opening balance at all.
    /// </summary>
    private async Task<Account> ResolveOrCreateEquityAccountAsync(int userId)
    {
        var existing = await _accountRepository.Query()
            .Include(a => a.AccountSubhead)
            .FirstOrDefaultAsync(a => a.AccountSubhead != null
                                   && a.AccountSubhead.SubheadName == EquitySubheadName
                                   && a.IsActive);
        if (existing != null) return existing;

        // Build whatever part of the equity chain is missing. Charts of accounts differ per tenant
        // and older pharmacies were provisioned before the current seed existed: some name the
        // family "CAPITAL", some "Capital", and some have no equity section at all. Refusing to
        // save would leave those pharmacies unable to record a party opening balance, so the
        // missing links are created instead. Matching is by name (SQL collation is
        // case-insensitive), accepting "Equity" as a synonym for "Capital".
        var capitalFamily = await _accountFamilyRepository.Query()
            .FirstOrDefaultAsync(f => f.FamilyName == CapitalFamilyName || f.FamilyName == "Equity");

        if (capitalFamily == null)
        {
            capitalFamily = new AccountFamily { FamilyName = CapitalFamilyName };
            await _accountFamilyRepository.AddAsync(capitalFamily);
            await _unitOfWork.SaveChangesAsync();
        }

        var head = await _accountHeadRepository.Query()
            .FirstOrDefaultAsync(h => h.AccountFamily_ID == capitalFamily.AccountFamilyID);

        if (head == null)
        {
            head = new AccountHead
            {
                HeadName = "Owner's Equity",
                AccountFamily_ID = capitalFamily.AccountFamilyID
            };
            await _accountHeadRepository.AddAsync(head);
            await _unitOfWork.SaveChangesAsync();
        }

        var subhead = await _accountSubheadRepository.Query()
            .FirstOrDefaultAsync(s => s.SubheadName == EquitySubheadName);

        if (subhead == null)
        {
            // AccountSubhead carries no audit/status fields — same shape TenantProvisioningService uses.
            subhead = new AccountSubhead
            {
                SubheadName = EquitySubheadName,
                AccountHead_ID = head.AccountHeadID
            };
            await _accountSubheadRepository.AddAsync(subhead);
            await _unitOfWork.SaveChangesAsync();
        }

        var generalType = await _accountTypeRepository.Query()
            .FirstOrDefaultAsync(t => t.Code == GeneralAccountTypeCode);

        var account = new Account
        {
            Name = EquityAccountName,
            AccountHead_ID = head.AccountHeadID,
            AccountSubhead_ID = subhead.AccountSubheadID,
            AccountType_ID = generalType?.AccountTypeID ?? 0,
            IsSystemAccount = true,
            IsActive = true,
            CreatedAt = AppTime.Now,
            CreatedBy = userId
        };

        await _accountRepository.AddAsync(account);
        await _unitOfWork.SaveChangesAsync();

        return account;
    }
}
