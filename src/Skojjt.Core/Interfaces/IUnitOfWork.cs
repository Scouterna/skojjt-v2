namespace Skojjt.Core.Interfaces;

/// <summary>
/// Unit of Work interface for managing transactions across multiple repositories.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    ISemesterRepository Semesters { get; }
    IScoutGroupRepository ScoutGroups { get; }
    IPersonRepository Persons { get; }
    ITroopRepository Troops { get; }
    IMeetingRepository Meetings { get; }
    IUserRepository Users { get; }
    IBadgeRepository Badges { get; }
    IBadgeTemplateRepository BadgeTemplates { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
