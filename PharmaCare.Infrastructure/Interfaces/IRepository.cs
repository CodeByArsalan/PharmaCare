using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace PharmaCare.Infrastructure.Interfaces;

public interface IRepository<T> where T : class
{
    // ========== QUERY METHODS ==========
    Task<IEnumerable<T>> GetAll(bool isTracking = true);
    int TotalCount(bool isTracking = true);
    IQueryable<T> FindByCondition(Expression<Func<T, bool>> expression);
    IQueryable<T> FindByConditionWithInclude(Expression<Func<T, bool>> expression, Expression<Func<T, object>>[] expressionInclude);
    T GetById(int Id);
    Task<T?> GetByIdAsync(int id, bool isTracking = true);
    IQueryable<T> GetAllWithInclude(params Expression<Func<T, object>>[] expression);

    // ========== LEGACY METHODS (auto-save - for backward compatibility) ==========
    // DEPRECATED: These will be removed after full UoW migration
    Task<bool> Delete(T entity);
    Task<bool> Insert(T entity);
    Task<T> InsertAndReturn(T entity);
    Task<bool> Update(T entity, params string[] changeFields);

    // ========== UNIT OF WORK METHODS (no auto-save) ==========
    // Use these with IUnitOfWork for atomic transactions

    /// <summary>
    /// Adds an entity to the context without saving. 
    /// Call IUnitOfWork.CommitAsync() to persist.
    /// </summary>
    void Add(T entity);

    /// <summary>
    /// Adds multiple entities to the context without saving.
    /// Call IUnitOfWork.CommitAsync() to persist.
    /// </summary>
    void AddRange(IEnumerable<T> entities);

    /// <summary>
    /// Marks an entity for removal without saving.
    /// Call IUnitOfWork.CommitAsync() to persist.
    /// </summary>
    void Remove(T entity);

    /// <summary>
    /// Marks an entity as modified without saving.
    /// Call IUnitOfWork.CommitAsync() to persist.
    /// </summary>
    void MarkModified(T entity);
}
