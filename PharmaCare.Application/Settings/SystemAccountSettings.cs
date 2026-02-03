namespace PharmaCare.Application.Settings;

/// <summary>
/// Configuration for system-wide account references.
/// </summary>
public class SystemAccountSettings
{
    public const string SectionName = "SystemAccounts";
    
    /// <summary>
    /// Account ID used for walking customer transactions.
    /// </summary>
    public int WalkingCustomerAccountId { get; set; }

    /// <summary>
    /// Account ID for Sales Revenue (Income).
    /// </summary>
    public int SalesRevenueAccountId { get; set; }

    /// <summary>
    /// Account ID for Cash (for immediate payments).
    /// </summary>
    public int CashAccountId { get; set; }
}
