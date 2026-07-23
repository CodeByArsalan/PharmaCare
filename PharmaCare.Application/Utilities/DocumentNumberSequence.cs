using PharmaCare.Application.Interfaces;

namespace PharmaCare.Application.Utilities;

/// <summary>
/// Single source of truth for sequential document numbers (PREFIX-yyyyMMdd-XXXX):
/// transaction numbers, voucher numbers, credit-note numbers, payment references.
/// The sequence is derived by reading the current day's max and adding one, which is a
/// read-then-insert race under concurrency — callers running inside a transaction must
/// call <see cref="SerializeAsync"/> first so competing generators queue up instead of
/// colliding on the (per-tenant unique) document-number index.
/// </summary>
public static class DocumentNumberSequence
{
    public static string DatePrefix(string prefix) => $"{prefix}-{DateTime.Now:yyyyMMdd}-";

    /// <summary>
    /// Serializes number generation for one date-prefix (per tenant) via a transaction-scoped
    /// app lock. No-op when no transaction is active (e.g. form-prefill previews) — in that
    /// case the caller gets today's best-effort next number, same as before.
    /// </summary>
    public static async Task SerializeAsync(IUnitOfWork unitOfWork, string datePrefix)
    {
        if (unitOfWork.HasActiveTransaction)
        {
            await unitOfWork.AcquireResourceLockAsync($"doc-no:{datePrefix}");
        }
    }

    /// <summary>
    /// Returns the next number in the sequence given the highest existing number for the
    /// same date-prefix (null when none exists yet).
    /// </summary>
    public static string Next(string datePrefix, string? lastNumber)
    {
        int nextNum = 1;
        if (!string.IsNullOrEmpty(lastNumber))
        {
            var parts = lastNumber.Split('-');
            if (parts.Length > 2 && int.TryParse(parts[^1], out int lastNum))
            {
                nextNum = lastNum + 1;
            }
        }

        return $"{datePrefix}{nextNum:D4}";
    }
}
