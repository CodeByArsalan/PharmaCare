using Microsoft.AspNetCore.Identity;
using PharmaCare.Domain.Models.Membership;
using PharmaCare.Domain.ViewModels;

namespace PharmaCare.Infrastructure.Interfaces.Membership;

public interface ISystemUserRepository
{
    List<MenuItemDto> GetUserPages(int userID);
    UserWithPagesDto? GetUserWithPages(int userID);
    Task<List<WebPages>> GetAllWebPagesAsync();
    Task SaveUserPagesAsync(int userId, List<int> pageIds);
}

