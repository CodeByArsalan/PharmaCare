using PharmaCare.Domain.Entities.Accounting;

namespace PharmaCare.Application.Interfaces.Accounting;

public interface IAccountSubHeadService
{
    Task<IEnumerable<AccountSubhead>> GetAllAsync();
    Task<AccountSubhead?> GetByIdAsync(int id);
    Task<AccountSubhead> CreateAsync(AccountSubhead accountSubhead);
    Task<bool> UpdateAsync(AccountSubhead accountSubhead);
    Task<bool> DeleteAsync(int id);
    Task<IEnumerable<AccountHead>> GetHeadsForDropdownAsync();
}
