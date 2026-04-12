using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Syncs troop and patrol assignments from Skojjt back to Scoutnet
/// via the UpdateGroupMembership API.
/// </summary>
public class ScoutnetMembershipSyncService : IMembershipSyncService
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private readonly IScoutnetApiClient _apiClient;
    private readonly ILogger<ScoutnetMembershipSyncService> _logger;

    public ScoutnetMembershipSyncService(
        IDbContextFactory<SkojjtDbContext> contextFactory,
        IScoutnetApiClient apiClient,
        ILogger<ScoutnetMembershipSyncService> logger)
    {
        _contextFactory = contextFactory;
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<MembershipSyncPreview> PreviewChangesAsync(
        int scoutGroupId,
        int semesterId,
        CancellationToken cancellationToken = default)
    {
        var preview = new MembershipSyncPreview();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var scoutGroup = await context.ScoutGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == scoutGroupId, cancellationToken);

        if (scoutGroup == null || string.IsNullOrWhiteSpace(scoutGroup.ApiKeyAllMembers))
        {
            _logger.LogWarning("Scout group {GroupId} not found or missing API key", scoutGroupId);
            return preview;
        }

        // Fetch current Scoutnet data
        var scoutnetData = await _apiClient.GetMemberListAsync(
            scoutGroupId, scoutGroup.ApiKeyAllMembers, cancellationToken);

        // Build lookup: memberNo → (scoutnet troop ID, scoutnet patrol ID, troop name, patrol name)
        var scoutnetAssignments = new Dictionary<int, (int? TroopId, int? PatrolId, string? TroopName, string? PatrolName)>();
        foreach (var (_, member) in scoutnetData.Data)
        {
            var memberNo = member.GetMemberNo();
            if (memberNo == 0) continue;
            scoutnetAssignments[memberNo] = (
                member.GetUnitId(),
                member.GetPatrolId(),
                member.GetUnitName(),
                member.GetPatrol()
            );
        }

        // Collect all troop IDs that exist in Scoutnet — any troop ScoutnetId NOT in this set
        // is a locally created troop that cannot be synced back.
        var scoutnetTroopIds = scoutnetAssignments.Values
            .Where(a => a.TroopId.HasValue)
            .Select(a => a.TroopId!.Value)
            .ToHashSet();

        // Load Skojjt troop assignments for this semester
        var troops = await context.Troops
            .AsNoTracking()
            .Where(t => t.ScoutGroupId == scoutGroupId && t.SemesterId == semesterId)
            .Include(t => t.TroopPersons)
            .ThenInclude(tp => tp.Person)
            .ToListAsync(cancellationToken);

        foreach (var troop in troops)
        {
            // Skip locally created troops — they don't exist in Scoutnet and can't be synced
            if (!scoutnetTroopIds.Contains(troop.ScoutnetId))
            {
                foreach (var tp in troop.TroopPersons.Where(tp => !tp.IsLeader))
                {
                    preview.SkippedLocalTroopMembers.Add(
                        $"{tp.Person.FullName} ({troop.Name})");
                }
                continue;
            }

            foreach (var tp in troop.TroopPersons)
            {
                if (!scoutnetAssignments.TryGetValue(tp.PersonId, out var scoutnet))
                    continue;

                var memberName = tp.Person.FullName;

                // Check troop change (only for non-leaders — leaders may have multiple troop roles)
                if (!tp.IsLeader && scoutnet.TroopId.HasValue && troop.ScoutnetId != scoutnet.TroopId.Value)
                {
                    preview.TroopChanges.Add(new MembershipChange(
                        tp.PersonId,
                        memberName,
                        troop.ScoutnetId,
                        troop.Name,
                        null,
                        null,
                        scoutnet.TroopName,
                        null));
                }

                // Check patrol change
                if (tp.PatrolId.HasValue && tp.PatrolId != scoutnet.PatrolId)
                {
                    // Always include troop_id with patrol changes — Scoutnet validates that
                    // patrol_id belongs to the specified troop. Without troop_id, Scoutnet
                    // validates against the member's current Scoutnet troop, which may differ.
                    preview.PatrolChanges.Add(new MembershipChange(
                        tp.PersonId,
                        memberName,
                        troop.ScoutnetId,
                        troop.Name,
                        tp.PatrolId,
                        tp.Patrol,
                        scoutnet.TroopName,
                        scoutnet.PatrolName));
                }
                else if (tp.Patrol != null && !tp.PatrolId.HasValue && tp.Patrol != scoutnet.PatrolName)
                {
                    // Patrol name changed but we don't have the ID — can't push
                    preview.UnmappedPatrolWarnings.Add(
                        $"{memberName}: patrull \"{tp.Patrol}\" saknar Scoutnet-ID (kör Scoutnet-import först)");
                }
            }
        }

        _logger.LogInformation(
            "Membership sync preview for group {GroupId}: {TroopChanges} troop changes, {PatrolChanges} patrol changes",
            scoutGroupId, preview.TroopChanges.Count, preview.PatrolChanges.Count);

        return preview;
    }

    public async Task<MembershipSyncResult> PushChangesAsync(
        int scoutGroupId,
        IReadOnlyList<MembershipChange> changes,
        CancellationToken cancellationToken = default)
    {
        if (changes.Count == 0)
        {
            return new MembershipSyncResult { Success = true, UpdatedCount = 0 };
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var scoutGroup = await context.ScoutGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == scoutGroupId, cancellationToken);

        if (scoutGroup == null)
        {
            return new MembershipSyncResult
            {
                Success = false,
                ErrorMessage = "Scoutkåren hittades inte."
            };
        }

        if (string.IsNullOrWhiteSpace(scoutGroup.ApiKeyUpdateMembership))
        {
            return new MembershipSyncResult
            {
                Success = false,
                ErrorMessage = "API-nyckeln för \"Uppdatera medlemskap\" saknas. Konfigurera den under kårinställningar."
            };
        }

        // Build the update payload
        var updates = new Dictionary<int, MembershipUpdate>();
        foreach (var change in changes)
        {
            if (!updates.TryGetValue(change.MemberNo, out var update))
            {
                update = new MembershipUpdate();
                updates[change.MemberNo] = update;
            }

            if (change.NewTroopId.HasValue)
            {
                update.TroopId = change.NewTroopId.Value;
                // Scoutnet requires status alongside troop_id for the move to take effect.
                // Without status, the API returns success but doesn't apply the change.
                update.Status ??= ScoutnetMembershipStatus.Confirmed;
            }

            if (change.NewPatrolId.HasValue)
            {
                update.PatrolId = change.NewPatrolId.Value;
            }
        }

        _logger.LogInformation(
            "Pushing {Count} membership updates for group {GroupId} to Scoutnet",
            updates.Count, scoutGroupId);

        try
        {
            var result = await _apiClient.UpdateMembershipAsync(
                scoutGroupId,
                scoutGroup.ApiKeyUpdateMembership,
                updates,
                cancellationToken);

            var syncResult = new MembershipSyncResult
            {
                Success = result.Success,
                UpdatedCount = result.UpdatedMemberNumbers.Count,
            };

            if (result.Success)
            {
                foreach (var change in changes)
                {
                    // Troop-only changes (patrol changes also carry troop_id, skip those here)
                    if (change.NewTroopId.HasValue && !change.NewPatrolId.HasValue)
                    {
                        syncResult.Details.Add(
                            $"{change.MemberName}: avdelning → {change.NewTroopName}");
                    }

                    if (change.NewPatrolId.HasValue)
                    {
                        syncResult.Details.Add(
                            $"{change.MemberName}: patrull → {change.NewPatrolName}");
                    }
                }
            }
            else
            {
                syncResult.ErrorMessage = "Scoutnet avvisade uppdateringen.";
                foreach (var (memberNo, errors) in result.Errors)
                {
                    foreach (var (field, msg) in errors)
                    {
                        syncResult.Details.Add($"Medlem {memberNo}: {field} — {msg}");
                    }
                }

                _logger.LogWarning(
                    "Scoutnet rejected membership update for group {GroupId}. Errors: {Errors}",
                    scoutGroupId,
                    string.Join("; ", syncResult.Details));
            }

            return syncResult;
        }
        catch (ScoutnetApiException ex)
        {
            _logger.LogError(ex, "Failed to push membership updates for group {GroupId}", scoutGroupId);
            return new MembershipSyncResult
            {
                Success = false,
                ErrorMessage = $"Kommunikationsfel med Scoutnet: {ex.Message}"
            };
        }
    }
}
