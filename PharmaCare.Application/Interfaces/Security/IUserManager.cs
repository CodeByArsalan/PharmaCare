using PharmaCare.Domain.Entities.Security;

namespace PharmaCare.Application.Interfaces.Security;

/// <summary>
/// Abstraction for ASP.NET Identity UserManager operations.
/// Implemented in Infrastructure layer to avoid cyclic dependencies.
/// </summary>
public interface IUserManager
{
    /// <summary>
    /// Create a new user with password.
    /// </summary>
    Task<(bool Succeeded, IEnumerable<string> Errors)> CreateAsync(User user, string password);

    /// <summary>
    /// Reset user password.
    /// </summary>
    Task<(bool Succeeded, IEnumerable<string> Errors)> ResetPasswordAsync(User user, string newPassword);

    /// <summary>
    /// Find user by ID.
    /// </summary>
    Task<User?> FindByIdAsync(int id);
}
