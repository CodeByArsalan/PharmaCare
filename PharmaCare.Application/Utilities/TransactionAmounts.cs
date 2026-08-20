namespace PharmaCare.Application.Utilities;

/// <summary>
/// The two rules every trading document shares about the numbers on it: what a quantity may be,
/// and how large a document may get.
/// </summary>
public static class TransactionAmounts
{
    /// <summary>
    /// Decimal places <c>StockDetail.Quantity</c> is stored with — see its
    /// <c>[Column(TypeName = "decimal(18,4)")]</c>.
    /// </summary>
    public const int QuantityScale = 4;

    /// <summary>
    /// Brings a quantity onto the precision the database will actually keep.
    ///
    /// <para>
    /// Line money is computed in C# from the quantity as supplied, but SQL Server then rounds the
    /// quantity to four decimals on the way in. For anything finer the two disagree, and below
    /// 0.00005 the disagreement is total: the quantity stores as zero while the revenue computed
    /// from it does not, so the books show a sale that moved no stock. Rounding FIRST — before the
    /// "greater than zero" check and before any money is derived — makes the arithmetic and the
    /// stored row describe the same transaction, and turns a sub-precision quantity into a plain
    /// validation failure.
    /// </para>
    /// </summary>
    public static decimal NormalizeQuantity(decimal quantity)
        => Math.Round(quantity, QuantityScale, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Rejects a document larger than <see cref="AccountingConstants.MaxTransactionAmount"/>.
    ///
    /// <para>
    /// This ceiling existed only on expenses and journal vouchers, leaving the documents that carry
    /// the most value — sales, receipts, purchases, returns — with none. Beyond catching the
    /// mistyped extra zero, it keeps line amounts inside <c>decimal(18,2)</c>: without it an
    /// out-of-range total reaches SQL Server and comes back as a raw overflow, which the caller can
    /// neither explain nor act on.
    /// </para>
    /// </summary>
    public static void EnsureWithinSanityCap(decimal amount, string documentLabel)
    {
        if (Math.Abs(amount) > AccountingConstants.MaxTransactionAmount)
        {
            throw new InvalidOperationException(
                $"{documentLabel} total of {amount:N2} exceeds the maximum permitted transaction " +
                $"amount of {AccountingConstants.MaxTransactionAmount:N2}. " +
                "Check the quantities and prices entered, or split the document.");
        }
    }
}
