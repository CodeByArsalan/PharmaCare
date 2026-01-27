using PharmaCare.Domain.Models.Membership;

namespace PharmaCare.Application.Interfaces.Membership
{
    public interface IUserTypeService
    {
        Task<List<UserTypes>> GetAllAsync();
        Task<UserTypes?> GetByIdAsync(int id);
        Task<bool> CreateAsync(UserTypes userType);
        Task<bool> UpdateAsync(UserTypes userType);
        Task<bool> DeleteAsync(int id);
        Task<bool> CanDeleteAsync(int id);
    }
}
