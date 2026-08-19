using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
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
    /// Resolves an entity by primary key, honouring global query filters.
    /// <para>
    /// Deliberately NOT <c>DbSet.Find</c>. Find performs a keyed lookup that BYPASSES global query
    /// filters, so on a tenant-scoped entity it returns another pharmacy's row. Services across the
    /// app pass a client-supplied id straight into this method, which made that a cross-tenant read
    /// (and, where the loaded entity is then written back, a cross-tenant write).
    /// </para>
    /// </summary>
    public virtual async Task<T?> GetByIdAsync(int id)
    {
        var primaryKey = _context.Model.FindEntityType(typeof(T))?.FindPrimaryKey();
        var keyProperty = primaryKey?.Properties.Count == 1 ? primaryKey.Properties[0] : null;

        // Composite or non-int keys have no single int column to match on; nothing tenant-scoped
        // uses one, so falling back to Find here changes no isolation behavior.
        if (keyProperty is null || keyProperty.ClrType != typeof(int))
        {
            return await _dbSet.FindAsync(id);
        }

        var keyName = keyProperty.Name;
        return await _dbSet.FirstOrDefaultAsync(e => EF.Property<int>(e, keyName) == id);
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
