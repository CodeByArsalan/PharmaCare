using Microsoft.EntityFrameworkCore.Storage;
using PharmaCare.Infrastructure.Interfaces;

namespace PharmaCare.Infrastructure.Persistence;

/// <summary>
/// Unit of Work implementation that wraps EF Core database transactions.
/// Ensures all database operations within a business transaction are atomic.
/// </summary>
public sealed class UnitOfWork : IUnitOfWork
{
    private readonly PharmaCareDBContext _context;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public UnitOfWork(PharmaCareDBContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public bool HasActiveTransaction => _transaction != null;

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            // Already in a transaction - nested transactions not supported
            // This is fine, we just continue with the existing transaction
            return;
        }

        _transaction = await _context.Database
            .BeginTransactionAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException(
                "Cannot commit: No active transaction. Call BeginTransactionAsync first.");
        }

        try
        {
            // Save any pending changes
            await _context.SaveChangesAsync(cancellationToken);

            // Commit the transaction
            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            // If commit fails, rollback
            await RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    private async Task DisposeTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _transaction?.Dispose();
        _context.Dispose();
        _disposed = true;
    }
}
