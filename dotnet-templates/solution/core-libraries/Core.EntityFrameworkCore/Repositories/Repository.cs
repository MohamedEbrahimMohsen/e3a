using Core.DDD.Entities;
using Core.DDD.Models;
using Core.DDD.Repositories;
using Core.EntityFrameworkCore.Exceptions;
using Core.Identity.Tokens.CurrentUser;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Core.EntityFrameworkCore.Repositories;

public class Repository<T>(DbContext context, ICurrentUserService? currentUserService = null) : IRepository<T> where T : class, IEntity
{
    protected readonly DbContext _context = context;
    protected readonly DbSet<T> _dbSet = context.Set<T>();
    protected readonly ICurrentUserService? _currentUserService = currentUserService;

    #region GET Methods
    public virtual async Task<List<T>?> GetAllAsync(CancellationToken cancellationToken, 
                                                    Func<IQueryable<T>, IQueryable<T>>? include = null, 
                                                    Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, 
                                                    bool asNoTracking = false)
    {
        IQueryable<T> query = _dbSet;

        if(asNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (include != null)
        {
            query = include(query);
        }

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<T?> GetByIdAsync(Guid id, 
                                               CancellationToken cancellationToken, 
                                               Func<IQueryable<T>, IQueryable<T>>? include = null, 
                                               bool asNoTracking = false)
    {
        IQueryable<T> query = _dbSet;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (include != null)
        {
            query = include(query);
        }

        return await query.FirstOrDefaultAsync(t => t.Id == id, cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region ADD Methods
    public virtual async Task AddAsync(T entity, CancellationToken cancellationToken)
    {
        if (entity == null)
        {
            throw new InfrastructureCoreException(ErrorCodes.RepositoryAddEntityNull);
        }

        await _dbSet.AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task AddRangeAsync(List<T> entities, CancellationToken cancellationToken)
    {
        if (entities == null)
        {
            throw new InfrastructureCoreException(ErrorCodes.RepositoryAddRangeEntitiesNull);
        }

        foreach (var entity in entities)
        {
            await AddAsync(entity, cancellationToken).ConfigureAwait(false);
        }
    }
    #endregion

    #region UPDATE Methods
    public virtual void Update(T entity)
    {
        if (entity == null)
        {
            throw new InfrastructureCoreException(ErrorCodes.RepositoryUpdateEntityNull);
        }

        _context.Update(entity);
    }

    public virtual void UpdateRange(List<T> entities)
    {
        if (entities == null)
        {
            throw new InfrastructureCoreException(ErrorCodes.RepositoryUpdateRangeEntitiesNull);
        }

        _context.UpdateRange(entities);
    }
    #endregion

    #region DELETE Methods
    public virtual void Delete(T entity)
    {
        if (entity == null)
        {
            throw new InfrastructureCoreException(ErrorCodes.RepositoryDeleteEntityNull);
        }

        _context.Remove(entity);
    }

    public virtual void DeleteRange(List<T> entities)
    {
        if (entities == null)
        {
            throw new InfrastructureCoreException(ErrorCodes.RepositoryDeleteRangeEntitiesNull);
        }

        _context.RemoveRange(entities);
    }
    #endregion

    #region FIND Methods
    public virtual async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, 
                                                 CancellationToken cancellationToken, 
                                                 Func<IQueryable<T>, IQueryable<T>>? include = null, 
                                                 Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, 
                                                 bool asNoTracking = false)
    {
        IQueryable<T> query = _dbSet;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (include != null)
        {
            query = include(query);
        }

        query = query.Where(predicate);

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate,
                                             CancellationToken cancellationToken,
                                             Func<IQueryable<T>, IQueryable<T>>? include = null,
                                             Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null,
                                             bool asNoTracking = false)
    {
        IQueryable<T> query = _dbSet;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (include != null)
        {
            query = include(query);
        }

        query = query.Where(predicate);

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        return await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<PageData<T>> FindPaginatedAsync(int pageNumber, int pageSize, 
                                                     CancellationToken cancellationToken, 
                                                     Expression<Func<T, bool>>? filter = null, 
                                                     Func<IQueryable<T>, IQueryable<T>>? include = null, 
                                                     Func<IQueryable<T>, IOrderedQueryable<T>>? orderBy = null, 
                                                     bool asNoTracking = false)
    {
        IQueryable<T> query = _dbSet;

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }

        if (filter != null)
        {
            query = query.Where(filter);
        }

        if (include != null)
        {
            query = include(query);
        }

        var totalItems = await query.CountAsync(cancellationToken)
                                    .ConfigureAwait(false);

        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        var data = await query.Skip((pageNumber - 1) * pageSize)
                                     .Take(pageSize)
                                     .ToListAsync(cancellationToken)
                                     .ConfigureAwait(false);

        return new PageData<T>()
        {
            Items = data,
            TotalItems = totalItems,
            TotalPages = totalPages,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
    #endregion

    public async Task<int> CountAsync(CancellationToken cancellationToken, Expression<Func<T, bool>>? predicate = null)
    {
        if (predicate != null)
        {
            return await _dbSet.CountAsync(predicate, cancellationToken).ConfigureAwait(false);
        }

        return await _dbSet.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    //public virtual async Task SaveChangesAsync(CancellationToken cancellationToken)
    //{
    //    await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    //}

    public virtual async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        var events = _context.ChangeTracker.Entries<AuditEntity>()
            .Select(e => e.Entity);

        foreach (var entry in _context.ChangeTracker.Entries<AuditEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreationDate = DateTimeOffset.Now;
                //entry.Entity.CreatedBy = _currentUserService?.UserId;
            }

            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                entry.Entity.UpdationDate = DateTimeOffset.Now;
                //entry.Entity.UpdatedBy = _currentUserService?.UserId;
            }
        }

        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}