namespace PharmaCare.Domain.Models.Membership;

/// <summary>
/// Represents the result of an authentication operation
/// </summary>
public class AuthenticationResult
{
    public bool Succeeded { get; set; }
    public bool RequiresTwoFactor { get; set; }
    public bool IsLockedOut { get; set; }
    public IEnumerable<string> Errors { get; set; } = new List<string>();

    public static AuthenticationResult Success() => new() { Succeeded = true };
    public static AuthenticationResult Failure(IEnumerable<string> errors) => new() { Succeeded = false, Errors = errors };
    public static AuthenticationResult TwoFactorRequired() => new() { Succeeded = false, RequiresTwoFactor = true };
    public static AuthenticationResult LockedOut() => new() { Succeeded = false, IsLockedOut = true };
}
