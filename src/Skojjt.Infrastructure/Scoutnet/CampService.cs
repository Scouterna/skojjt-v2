using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Entities;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Service for creating camp troops and importing participants from Scoutnet projects.
/// </summary>
public class CampService : ICampService
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private readonly IScoutnetApiClient _apiClient;
    private readonly ILogger<CampService> _logger;

    public CampService(
        IDbContextFactory<SkojjtDbContext> contextFactory,
        IScoutnetApiClient apiClient,
        ILogger<CampService> logger)
    {
        _contextFactory = contextFactory;
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<CampPreviewResult> PreviewParticipantsAsync(
        int projectId,
        string projectApiKey,
        CancellationToken cancellationToken = default)
    {
        ScoutnetProjectParticipantsResponse response;
        try
        {
            response = await _apiClient.GetProjectParticipantsAsync(
                projectId, projectApiKey, cancellationToken);
        }
        catch (ScoutnetApiException ex)
        {
            _logger.LogError(ex, "Failed to fetch participant preview for project {ProjectId}", projectId);
            return new CampPreviewResult
            {
                Success = false,
                ErrorMessage = $"Kunde inte hämta deltagare från Scoutnet: {ex.Message}"
            };
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var memberNos = response.Participants.Values.Select(p => p.MemberNo).ToList();
        var existingIds = (await context.Persons
            .Where(p => memberNos.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var previews = response.Participants.Values
            .OrderBy(p => p.Cancelled)
            .ThenBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Select(p => new CampParticipantPreview(
                p.MemberNo,
                p.FullName,
                p.Cancelled,
                existingIds.Contains(p.MemberNo)))
            .ToList();

        return new CampPreviewResult
        {
            Success = true,
            Participants = previews
        };
    }

    public async Task<CampCreationResult> CreateCampAsync(
        int scoutGroupId,
        int semesterId,
        string name,
        string location,
        DateOnly startDate,
        DateOnly endDate,
        int? scoutnetProjectId = null,
        CancellationToken cancellationToken = default)
    {
        if (endDate < startDate)
        {
            return new CampCreationResult
            {
                Success = false,
                ErrorMessage = "Slutdatum kan inte vara före startdatum."
            };
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            var scoutGroup = await context.ScoutGroups
                .FirstOrDefaultAsync(g => g.Id == scoutGroupId, ct);

            if (scoutGroup == null)
            {
                return new CampCreationResult
                {
                    Success = false,
                    ErrorMessage = "Scoutkåren hittades inte."
                };
            }

            // Check for duplicate import
            if (scoutnetProjectId.HasValue)
            {
                var existing = await context.Troops
                    .FirstOrDefaultAsync(t => t.ScoutGroupId == scoutGroupId
                                              && t.SemesterId == semesterId
                                              && t.ScoutnetProjectId == scoutnetProjectId.Value, ct);
                if (existing != null)
                {
                    return new CampCreationResult
                    {
                        Success = false,
                        ErrorMessage = $"Projekt {scoutnetProjectId.Value} har redan importerats som \"{existing.Name}\"."
                    };
                }
            }

            // Allocate local troop ID
            var localId = scoutGroup.NextLocalTroopId;
            scoutGroup.NextLocalTroopId = localId + 1;

            var troop = new Troop
            {
                ScoutnetId = localId,
                ScoutGroupId = scoutGroupId,
                SemesterId = semesterId,
                Name = name,
                TroopType = TroopType.Camp,
                CampStartDate = startDate,
                CampEndDate = endDate,
                ScoutnetProjectId = scoutnetProjectId,
                DefaultMeetingLocation = location,
                DefaultStartTime = new TimeOnly(8, 0),
                DefaultDurationMinutes = 1440
            };

            context.Troops.Add(troop);
            await context.SaveChangesAsync(ct);

            // Auto-generate one meeting per day
            var meetingsCreated = 0;
            var totalDays = endDate.DayNumber - startDate.DayNumber + 1;

            for (var day = 0; day < totalDays; day++)
            {
                var meetingDate = startDate.AddDays(day);
                context.Set<Meeting>().Add(new Meeting
                {
                    TroopId = troop.Id,
                    Name = totalDays > 1 ? $"{name} dag {day + 1}" : name,
                    MeetingDate = meetingDate,
                    StartTime = new TimeOnly(8, 0),
                    DurationMinutes = 1440,
                    Location = location,
                    IsHike = true
                });
                meetingsCreated++;
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Created camp troop {TroopId} ({Name}) with {Days} days for group {GroupId}",
                troop.Id, name, meetingsCreated, scoutGroupId);

            return new CampCreationResult
            {
                Success = true,
                Troop = troop,
                MeetingsCreated = meetingsCreated
            };
        }, cancellationToken);
    }

    public async Task<CampImportResult> ImportFromScoutnetAsync(
        int scoutGroupId,
        int semesterId,
        int projectId,
        string projectApiKey,
        string campName,
        string location,
        DateOnly startDate,
        DateOnly endDate,
        string? checkinApiKey = null,
        CancellationToken cancellationToken = default)
    {
        // Fetch participants from Scoutnet
        ScoutnetProjectParticipantsResponse participantResponse;
        try
        {
            participantResponse = await _apiClient.GetProjectParticipantsAsync(
                projectId, projectApiKey, cancellationToken);
        }
        catch (ScoutnetApiException ex)
        {
            _logger.LogError(ex, "Failed to fetch participants for import from project {ProjectId}", projectId);
            return new CampImportResult
            {
                Success = false,
                ErrorMessage = $"Kunde inte hämta deltagare från Scoutnet: {ex.Message}"
            };
        }

        // Create the camp troop
        var campResult = await CreateCampAsync(
            scoutGroupId, semesterId, campName, location, startDate, endDate, projectId, cancellationToken);

        if (!campResult.Success)
        {
            return new CampImportResult
            {
                Success = false,
                ErrorMessage = campResult.ErrorMessage
            };
        }

        // Add participants
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        // Store checkin API key if provided
        if (!string.IsNullOrWhiteSpace(checkinApiKey))
        {
            var troop = await context.Troops.FindAsync([campResult.Troop!.Id], cancellationToken);
            if (troop != null)
            {
                troop.ScoutnetCheckinApiKey = checkinApiKey;
                await context.SaveChangesAsync(cancellationToken);
            }
        }

        // Get confirmed, non-cancelled participants
        var activeParticipants = participantResponse.Participants.Values
            .Where(p => p.Confirmed && !p.Cancelled)
            .ToList();

        // Find which participants exist as Person records in our DB
        var memberNos = activeParticipants.Select(p => p.MemberNo).ToList();
        var existingPersonIds = await context.Persons
            .Where(p => memberNos.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var existingSet = existingPersonIds.ToHashSet();

        var imported = 0;
        var skippedNames = new List<string>();

        foreach (var participant in activeParticipants)
        {
            if (existingSet.Contains(participant.MemberNo))
            {
                context.Set<TroopPerson>().Add(new TroopPerson
                {
                    TroopId = campResult.Troop!.Id,
                    PersonId = participant.MemberNo,
                    IsLeader = false
                });
                imported++;
            }
            else
            {
                skippedNames.Add(participant.FullName);
            }
        }

        if (imported > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "Imported {Imported} participants from Scoutnet project {ProjectId} into camp {TroopId}. {Skipped} skipped (not in DB).",
            imported, projectId, campResult.Troop!.Id, skippedNames.Count);

        return new CampImportResult
        {
            Success = true,
            CampResult = campResult,
            ParticipantsImported = imported,
            ParticipantsSkipped = skippedNames.Count,
            SkippedNames = skippedNames
        };
    }

    public async Task<CampCheckinResult> PushCheckinAsync(
        int troopId,
        IReadOnlyList<(int PersonId, bool Attended)> attendanceState,
        CancellationToken cancellationToken = default)
    {
        if (attendanceState.Count == 0)
        {
            return new CampCheckinResult { Success = true };
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var troop = await context.Troops
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == troopId, cancellationToken);

        if (troop == null)
        {
            return new CampCheckinResult
            {
                Success = false,
                ErrorMessage = "Avdelningen hittades inte."
            };
        }

        if (!troop.ScoutnetProjectId.HasValue || string.IsNullOrWhiteSpace(troop.ScoutnetCheckinApiKey))
        {
            return new CampCheckinResult
            {
                Success = false,
                ErrorMessage = "Lägret saknar Scoutnet projekt-ID eller checkin API-nyckel."
            };
        }

        var checkins = attendanceState.ToDictionary(a => a.PersonId, a => a.Attended);

        try
        {
            var result = await _apiClient.CheckinParticipantsAsync(
                troop.ScoutnetProjectId.Value,
                troop.ScoutnetCheckinApiKey,
                checkins,
                cancellationToken);

            if (!result.Success)
            {
                return new CampCheckinResult
                {
                    Success = false,
                    ErrorMessage = result.ErrorMessage ?? "Scoutnet avvisade checkin-begäran."
                };
            }

            _logger.LogInformation(
                "Pushed checkin for project {ProjectId}: {In} checked in, {Out} checked out, {Unchanged} unchanged",
                troop.ScoutnetProjectId.Value,
                result.CheckedIn.Count,
                result.CheckedOutAttended.Count + result.CheckedOutNotAttended.Count,
                result.Unchanged.Count);

            return new CampCheckinResult
            {
                Success = true,
                CheckedInCount = result.CheckedIn.Count,
                CheckedOutCount = result.CheckedOutAttended.Count + result.CheckedOutNotAttended.Count,
                UnchangedCount = result.Unchanged.Count
            };
        }
        catch (ScoutnetApiException ex)
        {
            _logger.LogError(ex, "Failed to push checkin for project {ProjectId}", troop.ScoutnetProjectId.Value);
            return new CampCheckinResult
            {
                Success = false,
                ErrorMessage = $"Kommunikationsfel med Scoutnet: {ex.Message}"
            };
        }
    }
}
