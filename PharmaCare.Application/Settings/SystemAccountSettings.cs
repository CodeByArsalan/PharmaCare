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
}
