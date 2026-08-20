namespace PharmaCare.Application.DTOs.Security;

/// <summary>
/// Serializable DTO for storing user information in session.
/// Loaded once at login for efficient access throughout the session.
/// </summary>
public class UserSessionInfo
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>The pharmacy (tenant) this user belongs to. Null for platform super-admins.</summary>
    public int? Pharmacy_ID { get; set; }
    public string? PharmacyName { get; set; }
    public bool IsPlatformAdmin { get; set; }

    public List<int> RoleIds { get; set; } = new();
    public List<string> RoleNames { get; set; } = new();

    /// <summary>
    /// The user's <c>PermissionsStamp</c> at the moment this snapshot was built. Compared against
    /// the database value on every request; a mismatch means permissions changed after sign-in and
    /// the snapshot must be rebuilt.
    /// </summary>
    public string? PermissionsStamp { get; set; }
}
