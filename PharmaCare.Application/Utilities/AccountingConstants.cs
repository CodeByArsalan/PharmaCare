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
}
