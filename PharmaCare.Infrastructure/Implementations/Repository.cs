using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PharmaCare.Domain.Entities.Base;
using PharmaCare.Application.Interfaces;

namespace PharmaCare.Infrastructure.Implementations;

/// <summary>
/// Generic repository implementation
/// </summary>
public class Repository<T> : IRepository<T> where T : class
{
    protected readonly PharmaCareDBContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(PharmaCareDBContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    /// <summary>
    /// Resolves an entity by primary key, honouring the tenant boundary that global query filters
    /// enforce everywhere else.
    /// <para>
    /// <c>Find</c> is kept for its fast path — it returns an already-tracked entity without going to
    /// the database at all — but it BYPASSES global query filters by design. On a tenant-scoped
    /// entity that made this a cross-tenant read (and a cross-tenant write wherever the caller saved
    /// the entity back), on every one of the many call sites that pass a client-supplied id. The
    /// ownership check below reproduces exactly what the query filter would have decided, including
    /// returning nothing when there is no ambient tenant.
    /// </para>
    /// </summary>
    public virtual async Task<T?> GetByIdAsync(int id)
    {
        var entity = await _dbSet.FindAsync(id);

        if (entity is ITenantEntity tenantEntity)
        {
            var tenantId = _context.CurrentTenantId;

            // Matches the filter expression (Pharmacy_ID == TenantId): with no ambient tenant the
            // comparison is never true in SQL, so a filtered query returns nothing and so must this.
            if (tenantId is null || tenantEntity.Pharmacy_ID != tenantId.Value)
            {
                // Find has already pulled the foreign row into the change tracker. Detach it so it
                // cannot be reached through the identity map or swept up by a later SaveChanges.
                _context.Entry(entity).State = EntityState.Detached;
                return null;
            }
        }

        return entity;
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        return await _dbSet.ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.Where(predicate).ToListAsync();
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.FirstOrDefaultAsync(predicate);
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        return entity;
    }

    public virtual async Task AddRangeAsync(IEnumerable<T> entities)
    {
        await _dbSet.AddRangeAsync(entities);
    }

    public virtual void Update(T entity)
    {
        _dbSet.Update(entity);
    }

    public virtual void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    public virtual void RemoveRange(IEnumerable<T> entities)
    {
        _dbSet.RemoveRange(entities);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        if (predicate == null)
            return await _dbSet.CountAsync();
        return await _dbSet.CountAsync(predicate);
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        return await _dbSet.AnyAsync(predicate);
    }

    public virtual IQueryable<T> Query()
    {
        return _dbSet.AsQueryable();
    }


}
