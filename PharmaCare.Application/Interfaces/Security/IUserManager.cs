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

    /// <summary>
    /// Find a user by email address, using the same normalized lookup login uses.
    /// </summary>
    Task<User?> FindByEmailAsync(string email);

    /// <summary>
    /// Changes the user's email and login name through Identity so the normalized lookup columns
    /// stay in step. Assigning the properties directly leaves NormalizedEmail/NormalizedUserName
    /// holding the old address: the new one can never sign in and the old one still can.
    /// </summary>
    Task<(bool Succeeded, IEnumerable<string> Errors)> SetEmailAndUserNameAsync(User user, string newEmail);

    /// <summary>
    /// Rotates the user's security stamp, invalidating their existing auth cookies at the
    /// next security-stamp validation. Call whenever access must be revoked mid-session
    /// (deactivation, pharmacy suspension, role removal).
    /// </summary>
    Task UpdateSecurityStampAsync(User user);
}
