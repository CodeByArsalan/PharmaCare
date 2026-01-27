using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Application.Interfaces.AccountManagement;

public interface ISubheadService
{
    Task<List<Subhead>> GetSubheads();
    Task<List<Subhead>> GetSubheadsByHeadId(int headId);
    Task<Subhead?> GetSubheadById(int id);
    Task<bool> CreateSubhead(Subhead subhead);
    Task<bool> UpdateSubhead(Subhead subhead);
    Task<bool> DeleteSubhead(int id);
}
