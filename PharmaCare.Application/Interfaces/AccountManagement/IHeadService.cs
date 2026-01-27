using PharmaCare.Domain.Models.AccountManagement;

namespace PharmaCare.Application.Interfaces.AccountManagement;

public interface IHeadService
{
    Task<List<Head>> GetHeads();
    Task<Head?> GetHeadById(int id);
    Task<bool> CreateHead(Head head);
    Task<bool> UpdateHead(Head head);
    Task<bool> DeleteHead(int id);
}
