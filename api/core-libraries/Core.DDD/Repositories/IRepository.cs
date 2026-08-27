using Core.DDD.Entities;
using Core.DDD.Models;
using System.Linq.Expressions;

namespace Core.DDD.Repositories;

public interface IRepository<T> where T : class, IEntity
{
    #region GET Methods
    Task<List<T>?> GetAllAsync(CancellationToken cancellationToken, Func<IQueryable<T>, IQueryable<T>>? include = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, bool asNoTracking = false);
    Task<T?> GetByIdAsync(Guid id, CancellationToken cancellationToken, Func<IQueryable<T>, IQueryable<T>>? include = null, bool asNoTracking = false);
    #endregion

    #region ADD Methods
    Task AddAsync(T entity, CancellationToken cancellationToken);
    Task AddRangeAsync(List<T> entities, CancellationToken cancellationToken);
    #endregion

    #region UPDATE Methods
    void Update(T entity);
    void UpdateRange(List<T> entities);
    #endregion

    #region DELETE Methods
    void Delete(T entity);
    void DeleteRange(List<T> entities);
    #endregion

    #region FIND Methods
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken, Func<IQueryable<T>, IQueryable<T>>? include = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, bool asNoTracking = false);
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken, Func<IQueryable<T>, IQueryable<T>>? include = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, bool asNoTracking = false);
    Task<PageData<T>> FindPaginatedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken, Expression<Func<T, bool>>? filter = null, Func<IQueryable<T>, IQueryable<T>>? include = null, Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, bool asNoTracking = false);
    #endregion

    Task<int> CountAsync(CancellationToken cancellationToken, Expression<Func<T, bool>>? predicate = null);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
