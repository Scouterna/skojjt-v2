using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Entities;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Service for creating and managing troops that do not originate from Scoutnet.
/// Uses IDbContextFactory for Blazor Server compatibility.
/// </summary>
public class TroopManagementService : ITroopManagementService
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private readonly ILogger<TroopManagementService> _logger;

    public TroopManagementService(
        IDbContextFactory<SkojjtDbContext> contextFactory,
        ILogger<TroopManagementService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<LocalTroopCreationResult> CreateLocalTroopAsync(
        int scoutGroupId,
        int semesterId,
        string name,
        int? unitTypeId = null,
        TimeOnly? defaultStartTime = null,
        int? defaultDurationMinutes = null,
        string? defaultMeetingLocation = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new LocalTroopCreationResult
            {
                Success = false,
                ErrorMessage = "Avdelningen måste ha ett namn."
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
                return new LocalTroopCreationResult
                {
                    Success = false,
                    ErrorMessage = "Scoutkåren hittades inte."
                };
            }

            if (!scoutGroup.TryAllocateNextLocalTroopId(out var localId))
            {
                return new LocalTroopCreationResult
                {
                    Success = false,
                    ErrorMessage = $"Det lokala avdelnings-ID-intervallet (250–{ScoutGroup.MaxLocalTroopId}) är slut för denna scoutkår."
                };
            }

            var troop = new Troop
            {
                ScoutnetId = localId,
                ScoutGroupId = scoutGroupId,
                SemesterId = semesterId,
                Name = name.Trim(),
                TroopType = TroopType.Regular,
                UnitTypeId = unitTypeId,
                DefaultStartTime = defaultStartTime ?? new TimeOnly(18, 30),
                DefaultDurationMinutes = defaultDurationMinutes ?? 90,
                DefaultMeetingLocation = defaultMeetingLocation
            };

            context.Troops.Add(troop);
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Created local troop {TroopId} ({Name}) with ScoutnetId {ScoutnetId} for group {GroupId}",
                troop.Id, troop.Name, troop.ScoutnetId, scoutGroupId);

            return new LocalTroopCreationResult
            {
                Success = true,
                Troop = troop
            };
        }, cancellationToken);
    }
}
