using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Application.Interfaces.Accounting;

public interface IAccountHeadService
{
    Task<IEnumerable<AccountHead>> GetAllAsync();
    Task<AccountHead?> GetByIdAsync(int id);
    Task<AccountHead> CreateAsync(AccountHead accountHead);
    Task<bool> UpdateAsync(AccountHead accountHead);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<AccountFamily>> GetFamiliesForDropdownAsync();
}
