using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Skojjt.Core.Interfaces;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Repositories;

/// <summary>
/// Generic repository implementation using Entity Framework Core with DbContextFactory.
/// Uses factory pattern to create short-lived DbContext instances, which is essential for Blazor Server.
/// </summary>
public class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    protected readonly IDbContextFactory<SkojjtDbContext> ContextFactory;

    public Repository(IDbContextFactory<SkojjtDbContext> contextFactory)
    {
        ContextFactory = contextFactory;
    }

    /// <summary>
    /// Creates a new DbContext instance. Caller is responsible for disposing.
    /// </summary>
    protected SkojjtDbContext CreateContext() => ContextFactory.CreateDbContext();

    public virtual async Task<TEntity?> GetByIdAsync<TId>(TId id, CancellationToken cancellationToken = default) where TId : notnull
    {
        await using var context = CreateContext();
        return await context.Set<TEntity>().FindAsync([id], cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<TEntity>().ToListAsync(cancellationToken);
    }

    public virtual async Task<IReadOnlyList<TEntity>> FindAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<TEntity>().Where(predicate).ToListAsync(cancellationToken);
    }

    public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await context.Set<TEntity>().AddAsync(entity, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await context.Set<TEntity>().AddRangeAsync(entities, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.Set<TEntity>().Update(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task DeleteAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.Set<TEntity>().Attach(entity);
        context.Set<TEntity>().Remove(entity);
        await context.SaveChangesAsync(cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync<TId>(TId id, CancellationToken cancellationToken = default) where TId : notnull
    {
        await using var context = CreateContext();
        var entity = await context.Set<TEntity>().FindAsync([id], cancellationToken);
        return entity != null;
    }

    public virtual async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<TEntity>().CountAsync(cancellationToken);
    }

    public virtual async Task<int> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<TEntity>().CountAsync(predicate, cancellationToken);
    }
}

/// <summary>
/// Generic repository implementation with strongly-typed ID.
/// </summary>
public class Repository<TEntity, TId> : Repository<TEntity>, IRepository<TEntity, TId>
    where TEntity : class
    where TId : notnull
{
    public Repository(IDbContextFactory<SkojjtDbContext> contextFactory) : base(contextFactory)
    {
    }

    public virtual async Task<TEntity?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Set<TEntity>().FindAsync([id], cancellationToken);
    }

    public virtual async Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var entity = await context.Set<TEntity>().FindAsync([id], cancellationToken);
        return entity != null;
    }
}
