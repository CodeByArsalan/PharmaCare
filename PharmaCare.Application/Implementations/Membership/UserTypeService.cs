using Microsoft.EntityFrameworkCore;
using PharmaCare.Application.Interfaces.Membership;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Application.Implementations.Membership;

public class UserTypeService(IRepository<UserTypes> _repository, DbContext _dbContext) : IUserTypeService
{
    public async Task<List<UserTypes>> GetAllAsync()
    {
        var result = await _repository.GetAll(isTracking: false);
        return result.OrderBy(ut => ut.UserType).ToList();
    }

    public Task<UserTypes?> GetByIdAsync(int id)
    {
        return Task.FromResult(_repository.FindByCondition(ut => ut.UserTypeID == id).FirstOrDefault());
    }

    public async Task<bool> CreateAsync(UserTypes userType)
    {
        return await _repository.Insert(userType);
    }

    public async Task<bool> UpdateAsync(UserTypes userType)
    {
        return await _repository.Update(userType);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        // Check if type can be deleted
        bool isInUse = await IsTypeInUseAsync(id);
        if (isInUse)
            return false;

        var entity = _repository.GetById(id);
        if (entity == null)
            return false;

        return await _repository.Delete(entity);
    }

    public async Task<bool> CanDeleteAsync(int id)
    {
        return !await IsTypeInUseAsync(id);
    }

    private async Task<bool> IsTypeInUseAsync(int id)
    {
        return await _dbContext.Set<SystemUser>().AnyAsync(u => u.UserType_ID == id);
    }
}
