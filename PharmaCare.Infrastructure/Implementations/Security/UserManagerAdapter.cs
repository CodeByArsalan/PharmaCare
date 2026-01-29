using Microsoft.AspNetCore.Identity;
using PharmaCare.Application.Interfaces.Security;
using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Infrastructure.Implementations.Security;

/// <summary>
/// Adapter that wraps ASP.NET Identity UserManager for use in Application layer.
/// </summary>
public class UserManagerAdapter : IUserManager
{
    private readonly UserManager<User> _userManager;

    public UserManagerAdapter(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> CreateAsync(User user, string password)
    {
        var result = await _userManager.CreateAsync(user, password);
        return (result.Succeeded, result.Errors.Select(e => e.Description));
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> ResetPasswordAsync(User user, string newPassword)
    {
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        return (result.Succeeded, result.Errors.Select(e => e.Description));
    }

    public async Task<User?> FindByIdAsync(int id)
    {
        return await _userManager.FindByIdAsync(id.ToString());
    }
}
