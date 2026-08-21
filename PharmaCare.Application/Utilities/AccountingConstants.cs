namespace PharmaCare.Application.Utilities;

/// <summary>
/// Well-known ids, names and limits shared across services. The AccountType ids are stable
/// because DbInitializer seeds the GLOBAL AccountType rows in a fixed order — see the seeding
/// comment in DbInitializer.SeedAccountTypesAsync. PriceTypes, by contrast, are PER-TENANT rows
/// with identity ids (each pharmacy gets its own pair at provisioning), so they can never be
/// referenced by a constant id — resolve them by name via IProductService instead.
/// </summary>
public static class AccountingConstants
{
    /// <summary>AccountType id for Cash accounts (seeded code "CASH").</summary>
    public const int CashAccountTypeId = 1;

    /// <summary>AccountType id for Bank accounts (seeded code "BANK").</summary>
    public const int BankAccountTypeId = 2;

    /// <summary>Per-tenant PriceType name for the retail (per-unit) price tier.</summary>
    public const string RetailPriceTypeName = "Retail";

    /// <summary>Per-tenant PriceType name for the wholesale (per-box) price tier.</summary>
    public const string WholesalePriceTypeName = "Wholesale";

    /// <summary>Sanity cap on a single transaction/voucher amount (100 million).</summary>
    public const decimal MaxTransactionAmount = 100_000_000m;

    /// <summary>
    /// Lock resource shared by period closing and the pre-commit period re-check every posting
    /// performs. Closing waits for postings that are already committing, and a posting that starts
    /// its re-check after a close has committed sees the period as closed and rolls back.
    /// </summary>
    public const string PeriodCloseLockResource = "financial-period-close";
}
