namespace PharmaCare.Application.Interfaces;

/// <summary>
/// Unit of Work interface for managing transactions
/// </summary>
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();

    /// <summary>True while an explicit transaction begun via BeginTransactionAsync is open.</summary>
    bool HasActiveTransaction { get; }

    /// <summary>
    /// Takes a single transaction-scoped exclusive application lock on a named resource
    /// (scoped to the current tenant). See <see cref="AcquireResourceLocksAsync"/> for semantics.
    /// </summary>
    Task AcquireResourceLockAsync(string resource);

    /// <summary>
    /// Takes transaction-scoped exclusive application locks on the given resources
    /// (deduped, sorted to avoid deadlocks, scoped to the current tenant). Concurrent
    /// transactions locking any of the same resources block until this transaction
    /// commits or rolls back — use before read-then-write checks (e.g. computed
    /// stock-on-hand) that must not be raced past. Requires an active transaction.
    /// </summary>
    Task AcquireResourceLocksAsync(string resourcePrefix, IEnumerable<int> resourceIds);
}
