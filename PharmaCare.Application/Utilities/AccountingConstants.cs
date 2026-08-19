namespace PharmaCare.Application.Utilities;

/// <summary>
/// Well-known ids and limits shared across services. These ids are stable because
/// DbInitializer seeds the global AccountType/PriceType rows in a fixed order —
/// see the seeding comment in DbInitializer.SeedAccountTypesAsync.
/// </summary>
public static class AccountingConstants
{
    /// <summary>AccountType id for Cash accounts (seeded code "CASH").</summary>
    public const int CashAccountTypeId = 1;

    /// <summary>AccountType id for Bank accounts (seeded code "BANK").</summary>
    public const int BankAccountTypeId = 2;

    /// <summary>PriceType id for the wholesale price tier.</summary>
    public const int WholesalePriceTypeId = 2;

    /// <summary>Sanity cap on a single transaction/voucher amount (100 million).</summary>
    public const decimal MaxTransactionAmount = 100_000_000m;

    /// <summary>
    /// Lock resource shared by period closing and the pre-commit period re-check every posting
    /// performs. Closing waits for postings that are already committing, and a posting that starts
    /// its re-check after a close has committed sees the period as closed and rolls back.
    /// </summary>
    public const string PeriodCloseLockResource = "financial-period-close";
}
