using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Tenancy;

namespace PharmaCare.Infrastructure.Implementations;

/// <summary>
/// Unit of Work implementation for transaction management
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    // sp_getapplock with @LockOwner='Transaction' releases automatically on commit/rollback.
    // The return value is < 0 on timeout/failure and does NOT raise an error by itself,
    // so it must be checked explicitly.
    private const string AcquireLockSql = @"
DECLARE @res INT;
EXEC @res = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @res < 0
    THROW 51000, N'Timed out waiting for a concurrent transaction on the same record(s). Please try again.', 1;";

    private readonly PharmaCareDBContext _context;
    private readonly ICurrentTenant _currentTenant;
    private IDbContextTransaction? _transaction;

    public UnitOfWork(PharmaCareDBContext context, ICurrentTenant currentTenant)
    {
        _context = context;
        _currentTenant = currentTenant;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public bool HasActiveTransaction => _transaction != null;

    public async Task AcquireResourceLockAsync(string resource)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("Resource locks require an active transaction (call BeginTransactionAsync first).");
        }

        await AcquireLockCoreAsync(resource);
    }

    public async Task AcquireResourceLocksAsync(string resourcePrefix, IEnumerable<int> resourceIds)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("Resource locks require an active transaction (call BeginTransactionAsync first).");
        }

        // Deduped and sorted: every caller acquires locks in the same global order,
        // which prevents lock-ordering deadlocks between concurrent transactions.
        foreach (var id in resourceIds.Distinct().OrderBy(i => i))
        {
            await AcquireLockCoreAsync($"{resourcePrefix}:{id}");
        }
    }

    private async Task AcquireLockCoreAsync(string resource)
    {
        var tenant = _currentTenant.TenantId?.ToString() ?? "none";
        var parameter = new SqlParameter("@resource", $"{tenant}:{resource}");
        await _context.Database.ExecuteSqlRawAsync(AcquireLockSql, parameter);
    }

    public void Dispose()
    {
        _transaction?.Dispose();
    }
}
