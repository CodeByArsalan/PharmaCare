namespace PharmaCare.Application.Utilities;

/// <summary>
/// Centralized utility for generating unique identifiers across the application.
/// Uses timestamp-based format for guaranteed uniqueness without database lookups.
/// </summary>
public static class UniqueIdGenerator
{
    /// <summary>
    /// Generates a unique identifier with the specified prefix.
    /// Format: PREFIX-yyyyMMddHHmmssfff (e.g., GRN-20260127102445123)
    /// </summary>
    /// <param name="prefix">The prefix for the identifier (e.g., "GRN", "PO", "S")</param>
    /// <returns>A unique identifier string</returns>
    public static string Generate(string prefix)
        => $"{prefix}-{DateTime.Now:yyyyMMddHHmmssfff}";
}
