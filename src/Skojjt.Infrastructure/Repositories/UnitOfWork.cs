using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Skojjt.Core.Interfaces;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Repositories;

/// <summary>
/// Unit of Work implementation for managing transactions across repositories.
/// Uses IDbContextFactory for Blazor Server compatibility.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private SkojjtDbContext? _transactionContext;
    private IDbContextTransaction? _transaction;

    private ISemesterRepository? _semesters;
    private IScoutGroupRepository? _scoutGroups;
    private IPersonRepository? _persons;
    private ITroopRepository? _troops;
    private IMeetingRepository? _meetings;
    private IBadgeRepository? _badges;
    private IBadgeTemplateRepository? _badgeTemplates;

    public UnitOfWork(IDbContextFactory<SkojjtDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public ISemesterRepository Semesters => _semesters ??= new SemesterRepository(_contextFactory);
    public IScoutGroupRepository ScoutGroups => _scoutGroups ??= new ScoutGroupRepository(_contextFactory);
    public IPersonRepository Persons => _persons ??= new PersonRepository(_contextFactory);
    public ITroopRepository Troops => _troops ??= new TroopRepository(_contextFactory);
    public IMeetingRepository Meetings => _meetings ??= new MeetingRepository(_contextFactory);
    public IBadgeRepository Badges => _badges ??= new BadgeRepository(_contextFactory);
    public IBadgeTemplateRepository BadgeTemplates => _badgeTemplates ??= new BadgeTemplateRepository(_contextFactory);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // If we have an active transaction context, save using that
        if (_transactionContext != null)
        {
            return await _transactionContext.SaveChangesAsync(cancellationToken);
        }
        
        // Otherwise create a temporary context for this operation
        await using var context = _contextFactory.CreateDbContext();
        return await context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transactionContext = _contextFactory.CreateDbContext();
        _transaction = await _transactionContext.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        
        if (_transactionContext != null)
        {
            await _transactionContext.DisposeAsync();
            _transactionContext = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
        
        if (_transactionContext != null)
        {
            await _transactionContext.DisposeAsync();
            _transactionContext = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _transactionContext?.Dispose();
        GC.SuppressFinalize(this);
    }
}
