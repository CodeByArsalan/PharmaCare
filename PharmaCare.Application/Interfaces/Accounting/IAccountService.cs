using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Application.Interfaces.Accounting;

public interface IAccountService
{
    Task<IEnumerable<Account>> GetAllAsync();
    Task<Account?> GetByIdAsync(int id);
    Task<Account> CreateAsync(Account account, int userId);
    Task<bool> UpdateAsync(Account account, int userId);
    Task<bool> ToggleStatusAsync(int id, int userId);
    Task<IEnumerable<AccountSubhead>> GetSubHeadsForDropdownAsync();
    Task<IEnumerable<AccountHead>> GetAccountHeadsForDropdownAsync();
    Task<IEnumerable<AccountType>> GetAccountTypesForDropdownAsync();
    Task<IEnumerable<AccountSubhead>> GetSubHeadsByHeadIdAsync(int headId);
    
    /// <summary>
    /// Gets accounts with AccountType_ID 1 (Cash) or 2 (Bank) for payment selection.
    /// </summary>
    Task<IEnumerable<Account>> GetCashBankAccountsAsync();
}
