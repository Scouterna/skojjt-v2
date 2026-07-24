using Skojjt.Core.Exports;

namespace Skojjt.Core.Services;

/// <summary>
/// Service for incrementally importing a DAK XML file (exported from Skojjt or any other
/// system that produces the DAK format) into an existing troop + semester in Skojjt.
/// Meetings and attendance are merged;
/// meetings that conflict with existing data are surfaced for the user to resolve per meeting.
/// </summary>
public interface IDakImportService
{
    /// <summary>
    /// Parse a DAK file and compare it against the meetings currently stored for the given
    /// troop + semester, without modifying anything. Returns a preview describing which
    /// meetings would be added, which are unchanged, which conflict, and which persons
    /// would be skipped because they are not Skojjt members.
    /// </summary>
    Task<DakImportPreview> BuildPreviewAsync(
        int scoutGroupId,
        int troopId,
        int semesterId,
        byte[] fileContent,
        string fileName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Apply an incremental DAK import, merging meetings and attendance into Skojjt and
    /// resolving conflicts according to the supplied per-meeting decisions.
    /// </summary>
    Task<DakImportResult> ApplyAsync(DakImportApplyRequest request, CancellationToken cancellationToken = default);
}
