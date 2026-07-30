using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PharmaCare.Application.Interfaces;
using PharmaCare.Application.Interfaces.Tenancy;
using PharmaCare.Infrastructure.Interceptors;

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
    private readonly AuditSaveChangesInterceptor? _auditInterceptor;
    private IDbContextTransaction? _transaction;

    /// <param name="auditInterceptor">
    /// Optional ONLY to support hosts that deliberately run without auditing (the AuditTests
    /// console harness registers no interceptor and has no HTTP context). It cannot silently
    /// disable auditing in the web app: Program.cs resolves this same scoped instance with
    /// GetRequiredService while building the DbContext, so a missing registration fails at
    /// startup long before this constructor runs. When present it is that same instance, so the
    /// buffer it fills during this transaction is the one flushed or discarded below.
    /// </param>
    public UnitOfWork(
        PharmaCareDBContext context,
        ICurrentTenant currentTenant,
        AuditSaveChangesInterceptor? auditInterceptor = null)
    {
        _context = context;
        _currentTenant = currentTenant;
        _auditInterceptor = auditInterceptor;
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public async Task BeginTransactionAsync()
    {
        _transaction = await _context.Database.BeginTransactionAsync();

        // Hold audit rows back until this transaction commits. The activity log is a separate
        // database and cannot join this transaction, so writing per-SaveChanges would leave the
        // log describing work that a later rollback undid.
        _auditInterceptor?.BeginDeferral();
    }

    public async Task CommitTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;

            // Only now are the changes real, so the audit rows can be written.
            if (_auditInterceptor != null) await _auditInterceptor.FlushDeferredAsync();
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

        // Unconditional: if BeginTransactionAsync succeeded but the transaction handle is already
        // gone, any buffered rows still describe work that did not happen.
        _auditInterceptor?.DiscardDeferred();
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
        // A transaction still open at dispose was never committed, so it is being abandoned —
        // drop any buffered audit rows rather than let them describe uncommitted work.
        if (_transaction != null)
        {
            _auditInterceptor?.DiscardDeferred();
        }

        _transaction?.Dispose();
    }
}
