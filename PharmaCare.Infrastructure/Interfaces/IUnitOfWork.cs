namespace PharmaCare.Infrastructure.Interfaces;

/// <summary>
/// Unit of Work interface for managing database transactions atomically.
/// All business operations that require multiple database changes must use this.
/// 
/// CRITICAL RULES:
/// 1. NO repository should call SaveChangesAsync() directly
/// 2. All commits happen ONLY through UnitOfWork.CommitAsync()
/// 3. Any failure = automatic rollback
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Begins a new database transaction.
    /// Call this at the start of a business operation that requires atomicity.
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves all pending changes to the database within the current transaction.
    /// Use this for intermediate saves before final commit.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits all changes and ends the transaction.
    /// Call this only after ALL operations in the business transaction succeed.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if no active transaction exists</exception>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back all changes and ends the transaction.
    /// Called automatically on failure, or manually if needed.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true if there is an active transaction.
    /// </summary>
    bool HasActiveTransaction { get; }
}
