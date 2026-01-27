using Microsoft.EntityFrameworkCore;
using PharmaCare.Infrastructure.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace PharmaCare.Infrastructure.Implementations;

public class Repository<T> : IRepository<T> where T : class
{
    private readonly DbContext _context;
    private readonly DbSet<T> _dbSet;
    public Repository(DbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }
    public async Task<IEnumerable<T>> GetAll(bool isTracking = true)
    {
        try
        {
            if (!isTracking)
            {
                return await _dbSet.AsNoTracking().ToListAsync();
            }
            return await _dbSet.ToListAsync();
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while retrieving entities.", ex);
        }
    }
    public int TotalCount(bool isTracking = true)
    {
        try
        {
            if (!isTracking)
            {
                return _dbSet.AsNoTracking().Count();
            }
            return _dbSet.Count();
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while retrieving entities.", ex);
        }
    }
    public IQueryable<T> FindByCondition(Expression<Func<T, bool>> expression)
    {
        try
        {
            return _dbSet.Where(expression).AsNoTracking();
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while retrieving entities.", ex);
        }
    }
    public IQueryable<T> FindByConditionWithInclude(Expression<Func<T, bool>> expression, Expression<Func<T, object>>[] expressionInclude)
    {
        try
        {
            IQueryable<T> retrival = _dbSet;
            retrival = retrival.Where(expression);
            foreach (var item in expressionInclude)
            {
                retrival = retrival.Include(item);
            }
            return retrival;

        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while retrieving entities.", ex);
        }
    }
    public T GetById(int Id)
    {
        try
        {
            return _dbSet.Find(Id);
        }
        catch (Exception ex)
        {
            throw new ApplicationException($"An error occurred while retrieving the entity with ID {Id}.", ex);
        }
    }
    public async Task<T?> GetByIdAsync(int id, bool isTracking = true)
    {
        try
        {
            if (isTracking)
            {
                return await _dbSet.FindAsync(id);
            }
            // For no-tracking, we use FirstOrDefaultAsync since FindAsync always tracks
            return await _dbSet.AsNoTracking().FirstOrDefaultAsync(e => EF.Property<int>(e, typeof(T).Name + "ID") == id);
        }
        catch (Exception ex)
        {
            // Fallback for generic key name if property trick fails
            if (!isTracking) return await _dbSet.AsNoTracking().FirstOrDefaultAsync();
            throw new ApplicationException($"An error occurred while retrieving the entity with ID {id}.", ex);
        }
    }
    public IQueryable<T> GetAllWithInclude(params Expression<Func<T, object>>[] expression)
    {
        IQueryable<T> retrival = _dbSet;
        foreach (var item in expression)
        {
            retrival = retrival.Include(item);
        }
        return retrival;
    }
    public async Task<bool> Delete(T entity)
    {
        try
        {
            _context.Set<T>().Remove(entity);
            await Save();
            return true;
        }
        catch (Exception ex)
        {
            // Log the exception or handle it as appropriate for your application
            throw new ApplicationException($"An error occurred while deleting the entity with ID.", ex);
        }
    }
    public async Task<bool> Insert(T entity)
    {
        try
        {
            _dbSet.Add(entity);
            await Save();
            return true;
        }
        catch (Exception ex)
        {
            // Log the exception or handle it as appropriate for your application
            throw new ApplicationException("An error occurred while inserting the entity.", ex);
        }
    }
    public async Task<T> InsertAndReturn(T entity)
    {
        try
        {
            _dbSet.Add(entity);
            await Save();
            return entity;
        }
        catch (Exception ex)
        {
            // Log the exception or handle it as appropriate for your application

            throw new ApplicationException("An error occurred while inserting the entity.", ex);
        }
    }
    public async Task<bool> Update(T entity, params string[] propertyNames)
    {
        try
        {
            if (propertyNames != null && propertyNames.Length > 0)
            {
                _dbSet.Attach(entity);
                foreach (var propertyName in propertyNames)
                {
                    _context.Entry(entity).Property(propertyName).IsModified = true;
                }
            }
            else
            {
                _context.Entry(entity).State = EntityState.Modified;
            }

            await Save();
            return true;
        }
        catch (Exception ex)
        {
            // Log the exception or handle it as appropriate for your application
            throw new ApplicationException("An error occurred while updating the entity.", ex);
        }
    }
    public async Task Save()
    {
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Log the exception or handle it as appropriate for your application
            throw new ApplicationException("An error occurred while saving changes to the database.", ex);
        }
    }

    // ========== UNIT OF WORK METHODS (no auto-save) ==========
    // Use these with IUnitOfWork for atomic transactions

    /// <inheritdoc />
    public void Add(T entity)
    {
        _dbSet.Add(entity);
    }

    /// <inheritdoc />
    public void AddRange(IEnumerable<T> entities)
    {
        _dbSet.AddRange(entities);
    }

    /// <inheritdoc />
    public void Remove(T entity)
    {
        _dbSet.Remove(entity);
    }

    /// <inheritdoc />
    public void MarkModified(T entity)
    {
        _context.Entry(entity).State = EntityState.Modified;
    }
}
