using Microsoft.AspNetCore.DataProtection;

namespace PharmaCare.Web.Utilities;

/// <summary>
/// Helper for producing tamper-proof, opaque tokens for entity IDs placed in URLs.
///
/// Backed by ASP.NET Core Data Protection (authenticated encryption). Unlike the
/// previous implementation — which merely Base64-encoded the raw ID and was trivially
/// reversible — values produced here cannot be decoded or forged without the
/// application's protection key, which closes the IDOR/enumeration vector via URL tampering.
///
/// Authorization is still the responsibility of each controller/service: a valid token
/// only proves the value was issued by this app, not that the current user may access it.
/// </summary>
public static class Utility
{
    private static IDataProtector? _protector;

    /// <summary>
    /// Must be called once at application startup (see Program.cs) before any
    /// Encrypt/Decrypt call. Wires the static helper to the configured
    /// Data Protection provider.
    /// </summary>
    public static void Initialize(IDataProtectionProvider provider)
    {
        // A stable, versioned purpose string. Changing it invalidates previously issued tokens.
        _protector = provider.CreateProtector("PharmaCare.Web.Utilities.Utility.UrlId.v1");
    }

    private static IDataProtector Protector =>
        _protector ?? throw new InvalidOperationException(
            "Utility.Initialize(IDataProtectionProvider) must be called at startup before encrypting/decrypting IDs.");

    public static string EncryptURL(string strData)
    {
        if (string.IsNullOrEmpty(strData))
        {
            return string.Empty;
        }

        try
        {
            // Protect returns a URL-safe (base64url) string.
            return Protector.Protect(strData);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    public static string DecryptURL(string strData)
    {
        if (string.IsNullOrEmpty(strData))
        {
            return string.Empty;
        }

        try
        {
            // Unprotect throws CryptographicException if the token was tampered with,
            // forged, or issued under a different key/purpose. We swallow it and return
            // empty so callers fall through to their existing "not found" handling.
            return Protector.Unprotect(strData);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Extension method to encrypt an integer ID for use in URLs.
    /// </summary>
    public static string EncryptId(this int id)
    {
        return EncryptURL(id.ToString());
    }

    /// <summary>
    /// Decrypt an encrypted ID string back to integer. Returns 0 when the token is
    /// missing, malformed, tampered with, or forged.
    /// </summary>
    public static int DecryptId(string encryptedId)
    {
        var decrypted = DecryptURL(encryptedId);
        if (int.TryParse(decrypted, out int id))
            return id;
        return 0;
    }

    /// <summary>
    /// Extension method to encrypt a long ID for use in URLs.
    /// </summary>
    public static string EncryptId(this long id)
    {
        return EncryptURL(id.ToString());
    }

    /// <summary>
    /// Decrypt an encrypted ID string back to long. Returns 0 when the token is
    /// missing, malformed, tampered with, or forged.
    /// </summary>
    public static long DecryptIdLong(string encryptedId)
    {
        var decrypted = DecryptURL(encryptedId);
        if (long.TryParse(decrypted, out long id))
            return id;
        return 0;
    }
}
