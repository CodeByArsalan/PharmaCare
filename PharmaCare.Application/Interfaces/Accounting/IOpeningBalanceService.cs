using PharmaCare.Domain.Entities.Configuration;

namespace PharmaCare.Application.Interfaces.Accounting;

/// <summary>
/// Posts party opening balances into the general ledger.
/// <para>
/// A party's OpeningBalance feeds the customer/supplier ledgers and the balance reports, but it
/// was never posted as a voucher — so the trial balance (which is built purely from vouchers)
/// disagreed with the AR/AP sub-ledgers by the total of those opening balances. This service
/// closes that gap by posting the balance against the party's own ledger account and an
/// Opening Balance Equity account, which is the standard way to bring historic balances onto
/// a new set of books.
/// </para>
/// The sub-ledger reports are deliberately NOT changed: they read StockMains and Payments, never
/// vouchers, so there is no double counting.
/// </summary>
public interface IOpeningBalanceService
{
    /// <summary>
    /// Posts the CHANGE in a party's opening balance as a journal voucher.
    /// No-op when the delta is zero or the party has no ledger account.
    /// Must be called inside the caller's transaction so it commits with the party itself.
    /// </summary>
    /// <param name="party">The party, with Account_ID populated.</param>
    /// <param name="previousBalance">The opening balance before this change (0 for a new party).</param>
    /// <param name="userId">User performing the change.</param>
    Task PostOpeningBalanceDeltaAsync(Party party, decimal previousBalance, int userId);
}
