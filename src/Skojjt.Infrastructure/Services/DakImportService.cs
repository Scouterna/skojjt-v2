using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Entities;
using Skojjt.Core.Exports;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Incrementally imports a DAK XML file (exported from Skojjt or any other system that
/// produces the DAK format) into an existing troop + semester, merging meetings and
/// attendance and surfacing conflicts for per-meeting resolution.
/// </summary>
public class DakImportService : IDakImportService
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private readonly IDakAnalysisService _dakAnalysis;
    private readonly ILogger<DakImportService> _logger;

    public DakImportService(
        IDbContextFactory<SkojjtDbContext> contextFactory,
        IDakAnalysisService dakAnalysis,
        ILogger<DakImportService> logger)
    {
        _contextFactory = contextFactory;
        _dakAnalysis = dakAnalysis;
        _logger = logger;
    }

    public async Task<DakImportPreview> BuildPreviewAsync(
        int scoutGroupId,
        int troopId,
        int semesterId,
        byte[] fileContent,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var parse = _dakAnalysis.Parse(fileContent, fileName);

        if (parse.Data is null)
        {
            return new DakImportPreview
            {
                CanImport = false,
                Errors = parse.Issues
                    .Where(i => i.Severity == DakIssueSeverity.Error)
                    .Select(i => i.ToString())
                    .DefaultIfEmpty("DAK-filen kunde inte läsas.")
                    .ToList()
            };
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var troop = await context.Troops
            .FirstOrDefaultAsync(t => t.Id == troopId, cancellationToken);

        var validationError = ValidateTroop(troop, scoutGroupId, semesterId);
        if (validationError is not null)
        {
            return new DakImportPreview { CanImport = false, Errors = [validationError] };
        }

        var existing = await LoadExistingMeetingsAsync(context, troopId, cancellationToken);
        var memberIds = await LoadMemberIdsAsync(context, scoutGroupId, cancellationToken);

        var semesterRange = new Semester(semesterId).GetStartAndEndDates();
        var preview = DakImportPlanner.Plan(parse.Data, existing, memberIds, semesterRange);

        var warnings = parse.Issues
            .Where(i => i.Severity == DakIssueSeverity.Warning)
            .Select(i => i.ToString())
            .ToList();

        if (preview.OutOfRangeMeetings.Count > 0)
        {
            warnings.Add(
                $"{preview.OutOfRangeMeetings.Count} möten ligger utanför terminen och hoppas över " +
                $"(t.ex. {preview.OutOfRangeMeetings[0].Date:yyyy-MM-dd}). Kontrollera att du valt rätt termin.");
        }

        return new DakImportPreview
        {
            CanImport = true,
            Warnings = warnings,
            NewMeetings = preview.NewMeetings,
            UnchangedMeetings = preview.UnchangedMeetings,
            Conflicts = preview.Conflicts,
            SkippedPersons = preview.SkippedPersons,
            OutOfRangeMeetings = preview.OutOfRangeMeetings
        };
    }

    public async Task<DakImportResult> ApplyAsync(DakImportApplyRequest request, CancellationToken cancellationToken = default)
    {
        var parse = _dakAnalysis.Parse(request.FileContent, request.FileName);
        if (parse.Data is null)
        {
            return new DakImportResult
            {
                Success = false,
                Errors = ["DAK-filen kunde inte läsas."]
            };
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var troop = await context.Troops
            .FirstOrDefaultAsync(t => t.Id == request.TroopId, cancellationToken);

        var validationError = ValidateTroop(troop, request.ScoutGroupId, request.SemesterId);
        if (validationError is not null)
        {
            return new DakImportResult { Success = false, Errors = [validationError] };
        }

        var existing = await LoadExistingMeetingsAsync(context, request.TroopId, cancellationToken);
        var memberIds = await LoadMemberIdsAsync(context, request.ScoutGroupId, cancellationToken);
        var semesterRange = new Semester(request.SemesterId).GetStartAndEndDates();
        var preview = DakImportPlanner.Plan(parse.Data, existing, memberIds, semesterRange);

        var added = 0;
        var updated = 0;

        await context.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            // Add meetings that don't exist yet.
            foreach (var m in preview.NewMeetings)
            {
                var meeting = new Meeting
                {
                    TroopId = request.TroopId,
                    MeetingDate = m.Date,
                    StartTime = m.StartTime,
                    DurationMinutes = m.DurationMinutes,
                    Name = m.Name,
                    Location = m.Location,
                    IsHike = m.IsHike
                };
                context.Meetings.Add(meeting);
                await context.SaveChangesAsync(ct);

                foreach (var personId in m.AttendingPersonIds)
                {
                    context.MeetingAttendances.Add(new MeetingAttendance
                    {
                        MeetingId = meeting.Id,
                        PersonId = personId
                    });
                }
                added++;
            }

            // Resolve conflicts according to the user's decisions.
            foreach (var conflict in preview.Conflicts)
            {
                var choice = request.Decisions.TryGetValue(conflict.Imported.Date, out var c)
                    ? c
                    : MeetingImportChoice.KeepExisting;

                if (choice != MeetingImportChoice.UseImported)
                    continue;

                var meeting = await context.Meetings
                    .FirstOrDefaultAsync(x => x.Id == conflict.Existing.MeetingId, ct);
                if (meeting is null)
                    continue;

                meeting.StartTime = conflict.Imported.StartTime;
                meeting.DurationMinutes = conflict.Imported.DurationMinutes;
                meeting.Name = conflict.Imported.Name;
                meeting.Location = conflict.Imported.Location;
                meeting.IsHike = conflict.Imported.IsHike;

                var currentAttendance = await context.MeetingAttendances
                    .Where(a => a.MeetingId == meeting.Id)
                    .ToListAsync(ct);
                context.MeetingAttendances.RemoveRange(currentAttendance);

                foreach (var personId in conflict.Imported.AttendingPersonIds)
                {
                    context.MeetingAttendances.Add(new MeetingAttendance
                    {
                        MeetingId = meeting.Id,
                        PersonId = personId
                    });
                }
                updated++;
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);

        _logger.LogInformation(
            "DAK import for troop {TroopId}: {Added} added, {Updated} updated, {Unchanged} unchanged, {Skipped} skipped persons",
            request.TroopId, added, updated, preview.UnchangedMeetings.Count, preview.SkippedPersons.Count);

        return new DakImportResult
        {
            Success = true,
            AddedMeetings = added,
            UpdatedMeetings = updated,
            UnchangedMeetings = preview.UnchangedMeetings.Count,
            SkippedPersons = preview.SkippedPersons.Count
        };
    }

    private static string? ValidateTroop(Troop? troop, int scoutGroupId, int semesterId)
    {
        if (troop is null)
            return "Avdelningen hittades inte.";
        if (troop.ScoutGroupId != scoutGroupId)
            return "Avdelningen tillhör inte den valda scoutkåren.";
        if (troop.SemesterId != semesterId)
            return "Avdelningen tillhör inte den valda terminen.";
        return null;
    }

    private static async Task<List<DakExistingMeeting>> LoadExistingMeetingsAsync(
        SkojjtDbContext context, int troopId, CancellationToken ct)
    {
        var meetings = await context.Meetings
            .Where(m => m.TroopId == troopId)
            .Select(m => new
            {
                m.Id,
                m.MeetingDate,
                m.StartTime,
                m.DurationMinutes,
                m.Name,
                m.Location,
                m.IsHike,
                AttendingIds = m.Attendances.Select(a => a.PersonId).ToList()
            })
            .ToListAsync(ct);

        return meetings
            .Select(m => new DakExistingMeeting
            {
                MeetingId = m.Id,
                Date = m.MeetingDate,
                StartTime = m.StartTime,
                DurationMinutes = m.DurationMinutes,
                Name = m.Name,
                Location = m.Location,
                IsHike = m.IsHike,
                AttendingPersonIds = m.AttendingIds
            })
            .ToList();
    }

    private static async Task<HashSet<int>> LoadMemberIdsAsync(
        SkojjtDbContext context, int scoutGroupId, CancellationToken ct)
    {
        var ids = await context.ScoutGroupPersons
            .Where(sgp => sgp.ScoutGroupId == scoutGroupId)
            .Select(sgp => sgp.PersonId)
            .ToListAsync(ct);

        return ids.ToHashSet();
    }
}
