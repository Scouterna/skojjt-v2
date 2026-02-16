using Skojjt.Core.Entities;

namespace Skojjt.Core.Interfaces;

/// <summary>
/// Repository interface for Semester operations.
/// </summary>
public interface ISemesterRepository : IRepository<Semester, int>
{
    Task<Semester?> GetByYearAndTermAsync(int year, bool isAutumn, CancellationToken cancellationToken = default);
    Task<Semester?> GetCurrentSemesterAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets the current semester based on today's date, or creates it if it doesn't exist.
    /// VT (spring): January 1 - June 30
    /// HT (autumn): July 1 - December 31
    /// </summary>
    Task<Semester> GetOrCreateCurrentSemesterAsync(CancellationToken cancellationToken = default);
    
    Task<IReadOnlyList<Semester>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Semester>> GetByScoutGroupAsync(int scoutGroupId, CancellationToken cancellationToken = default);
}
