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

    public async Task<User?> FindByEmailAsync(string email)
    {
        return await _userManager.FindByEmailAsync(email);
    }

    public async Task<(bool Succeeded, IEnumerable<string> Errors)> SetEmailAndUserNameAsync(User user, string newEmail)
    {
        // Compare against the NORMALIZED column, not user.Email: callers hand us the tracked
        // entity, whose Email property they may already have overwritten with the new address.
        // The normalized columns are the only ones still holding what is actually persisted —
        // and they are what login resolves against.
        var normalizedEmail = _userManager.NormalizeEmail(newEmail);
        if (string.Equals(user.NormalizedEmail, normalizedEmail, StringComparison.Ordinal))
        {
            return (true, Array.Empty<string>());
        }

        // Both setters go through Identity so the normalized columns are recomputed, and each
        // runs the configured validators (including the unique-email rule).
        var userNameResult = await _userManager.SetUserNameAsync(user, newEmail);
        if (!userNameResult.Succeeded)
        {
            return (false, userNameResult.Errors.Select(e => e.Description));
        }

        var emailResult = await _userManager.SetEmailAsync(user, newEmail);
        return (emailResult.Succeeded, emailResult.Errors.Select(e => e.Description));
    }

    public async Task UpdateSecurityStampAsync(User user)
    {
        await _userManager.UpdateSecurityStampAsync(user);
    }
}
