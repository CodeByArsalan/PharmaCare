namespace PharmaCare.Web.Utilities;

/// <summary>
/// Single source of truth for the currency shown across the UI. The symbol/code come from
/// the "Currency" configuration section (see appsettings.json); changing the currency for a
/// deployment is a config edit, not a find-and-replace across the views.
///
/// Initialized once at startup from Program.cs (same pattern as <see cref="Utility"/>).
/// </summary>
public static class CurrencyDisplay
{
    /// <summary>Symbol/abbreviation rendered next to amounts (e.g. "PKR", "Rs", "$").</summary>
    public static string Symbol { get; private set; } = "PKR";

    /// <summary>ISO 4217 code used by JavaScript Intl.NumberFormat (e.g. "PKR", "USD").</summary>
    public static string Code { get; private set; } = "PKR";

    public static void Initialize(IConfiguration configuration)
    {
        var symbol = configuration["Currency:Symbol"];
        var code = configuration["Currency:Code"];

        if (!string.IsNullOrWhiteSpace(symbol)) Symbol = symbol.Trim();
        if (!string.IsNullOrWhiteSpace(code)) Code = code.Trim();
    }

    /// <summary>Formats an amount with the currency symbol, e.g. "PKR 1,234.56".</summary>
    public static string Format(decimal value, string numberFormat = "N2")
        => $"{Symbol} {value.ToString(numberFormat)}";

    /// <summary>Formats an amount with no decimals, e.g. "PKR 1,235" (dashboard/stat style).</summary>
    public static string FormatWhole(decimal value)
        => Format(value, "N0");
}
