using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Entities;
using Skojjt.Infrastructure.Data;
using Skojjt.Core.Utilities;

namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// Service for importing member data from Scoutnet into the database.
/// </summary>
public class ScoutnetImportService : IScoutnetImportService
{
    private readonly SkojjtDbContext _context;
    private readonly IScoutnetApiClient _apiClient;
    private readonly ILogger<ScoutnetImportService> _logger;

    /// <summary>
    /// Scoutnet role IDs that indicate a leader position.
    /// 2 = Avdelningsledare, 3 = Ledare, 4 = Vice avdelningsledare, 5 = Assisterande ledare
    /// </summary>
    private static readonly HashSet<int> LeaderRoleIds = [2, 3, 4, 5];

    // Database column length limits
    private const int MaxFirstNameLength = 50;
    private const int MaxLastNameLength = 50;
    private const int MaxPersonalNumberLength = 15;
    private const int MaxEmailLength = 100;
    private const int MaxPhoneLength = 25;
    private const int MaxMobileLength = 50;
    private const int MaxMumNameLength = 60;
    private const int MaxDadNameLength = 50;
    private const int MaxStreetLength = 100;
    private const int MaxZipCodeLength = 20;
    private const int MaxZipNameLength = 50;

    public ScoutnetImportService(
        SkojjtDbContext context,
        IScoutnetApiClient apiClient,
        ILogger<ScoutnetImportService> logger)
    {
        _context = context;
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// Truncates a string to the specified maximum length.
    /// </summary>
    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    /// <summary>
    /// Truncates a nullable string to the specified maximum length.
    /// </summary>
    private static string? TruncateNullable(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public async Task<ScoutnetImportResult> ImportMembersAsync(
        int scoutGroupId,
        int semesterId,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ScoutnetImportResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Get the scout group and its API key
            var scoutGroup = await _context.ScoutGroups
                .FirstOrDefaultAsync(sg => sg.Id == scoutGroupId, cancellationToken);

            if (scoutGroup == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Scout group {scoutGroupId} not found";
                return result;
            }

            if (string.IsNullOrEmpty(scoutGroup.ApiKeyAllMembers))
            {
                result.Success = false;
                result.ErrorMessage = "Scout group does not have an API key configured";
                return result;
            }

            progress?.Report($"Fetching members from Scoutnet for {scoutGroup.Name}...");

            var response = await _apiClient.GetMemberListAsync(
                scoutGroupId,
                scoutGroup.ApiKeyAllMembers,
                cancellationToken);

            return await ImportFromResponseAsync(
                scoutGroupId,
                semesterId,
                response,
                progress,
                cancellationToken);
        }
        catch (ScoutnetApiException ex)
        {
            result.Success = false;
            result.ErrorMessage = ex.Message;
            result.Duration = stopwatch.Elapsed;
            _logger.LogError(ex, "Scoutnet API error during import for group {GroupId}", scoutGroupId);
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Unexpected error: {ex.Message}";
            result.Duration = stopwatch.Elapsed;
            _logger.LogError(ex, "Unexpected error during import for group {GroupId}", scoutGroupId);
            return result;
        }
    }

    public async Task<ScoutnetImportResult> ImportFromResponseAsync(
        int scoutGroupId,
        int semesterId,
        ScoutnetMemberListResponse response,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ScoutnetImportResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Verify semester exists
            var semester = await _context.Semesters
                .FirstOrDefaultAsync(s => s.Id == semesterId, cancellationToken);

            if (semester == null)
            {
                result.Success = false;
                result.ErrorMessage = $"Semester {semesterId} not found";
                return result;
            }

            progress?.Report($"Processing {response.Data.Count} members...");
            _logger.LogInformation("Starting import of {Count} members for group {GroupId}, semester {SemesterId}",
                response.Data.Count, scoutGroupId, semesterId);

            // Collect all person IDs from the import to fetch existing persons.
            // Use the dictionary key as fallback when member_no is missing from the member data.
            var importPersonIds = response.Data
                .Select(kvp =>
                {
                    var id = kvp.Value.GetMemberNo();
                    if (id == 0) int.TryParse(kvp.Key, out id);
                    return id;
                })
                .Where(id => id != 0)
                .ToHashSet();

            // Get existing persons globally (they may belong to other scout groups too)
            var existingPersons = await _context.Persons
                .Where(p => importPersonIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            var existingScoutGroupPersons = await _context.ScoutGroupPersons
                .Where(sgp => sgp.ScoutGroupId == scoutGroupId)
                .ToDictionaryAsync(sgp => sgp.PersonId, cancellationToken);

            var existingTroops = await _context.Troops
                .Where(t => t.ScoutGroupId == scoutGroupId && t.SemesterId == semesterId)
                .ToDictionaryAsync(t => t.ScoutnetId, cancellationToken);

            var existingTroopPersons = await _context.TroopPersons
                .Where(tp => tp.Troop.ScoutGroupId == scoutGroupId && tp.Troop.SemesterId == semesterId)
                .ToListAsync(cancellationToken);

            // Track which persons we've seen in this import
            var seenPersonIds = new HashSet<int>();
            var troopsToCreate = new Dictionary<int, Troop>();
            var leadersForTroops = new List<(int TroopScoutnetId, int PersonId)>();
            var processedCount = 0;

            foreach (var (memberId, member) in response.Data)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var personId = member.GetMemberNo();
                if (personId == 0) int.TryParse(memberId, out personId);
                if (personId == 0) continue;

                seenPersonIds.Add(personId);

                var group = member.Group;
                if (group == null || group.RawValue == null || group.Value == null)
                {
                    _logger.LogError(result.ErrorMessage = $"Invalid group for member {memberId}");
                    return result;
                }

                int groupId = int.Parse(group.RawValue!);
                if (groupId != scoutGroupId)
                {
                    _logger.LogError(result.ErrorMessage = $"Invalid group {groupId} for member {memberId} than what was imported for {scoutGroupId}");
                    return result;
                }

                ScoutGroup? scoutGroup = await _context.ScoutGroups.FirstOrDefaultAsync(sg => sg.Id == groupId, cancellationToken);
                if (scoutGroup == null)
                {
                    var groupName = group.Value;
                    progress?.Report($"Creating ScoutGroup {groupName}({groupId})...");
                    _logger.LogInformation($"Creating ScoutGroup {groupName}({groupId})...");
                    scoutGroup = new ScoutGroup
                    {
                        Id = groupId,
                        Name = groupName
                    };
                    _context.ScoutGroups.Add(scoutGroup);
                    result.ScoutGroupsCreated++;
                    result.CreatedScoutGroups.Add(new ImportedScoutGroup(groupId, groupName));
                }

                // Process person
                Person person;
                string? personTroopName = member.GetUnitName();
                if (existingPersons.TryGetValue(personId, out var existingPerson))
                {
                    // Update existing person
                    UpdatePersonFromScoutnet(existingPerson, member);
                    existingPerson.Removed = false;
                    person = existingPerson;
                    result.PersonsUpdated++;
                }
                else
                {
                    // Create new person
                    var newPerson = CreatePersonFromScoutnet(member);
                    _context.Persons.Add(newPerson);
                    existingPersons[personId] = newPerson;
                    person = newPerson;
                    result.PersonsCreated++;
                    result.CreatedPersons.Add(new ImportedPerson(personId, newPerson.FullName, personTroopName));
                }

                // Ensure the semester year is recorded in MemberYears
                if (!person.MemberYears.Contains(semester.Year))
                {
                    person.MemberYears = [.. person.MemberYears, semester.Year];
                }

                // Process ScoutGroupPerson (for multi-group support)
                if (existingScoutGroupPersons.TryGetValue(personId, out var scoutGroupPerson))
                {
                    // Update existing membership
                    scoutGroupPerson.NotInScoutnet = false;
                    scoutGroupPerson.GroupRoles = member.GetGroupRole();
                    scoutGroupPerson.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new membership
                    var newScoutGroupPerson = new ScoutGroupPerson
                    {
                        PersonId = personId,
                        ScoutGroupId = scoutGroupId,
                        NotInScoutnet = false,
                        GroupRoles = member.GetGroupRole()
                    };
                    _context.ScoutGroupPersons.Add(newScoutGroupPerson);
                    existingScoutGroupPersons[personId] = newScoutGroupPerson;
                }

                // Process troop assignment
                var unitId = member.GetUnitId();
                if (unitId.HasValue && unitId.Value > 0)
                {
                    var unitName = member.GetUnitName() ?? $"Troop {unitId}";

                    // Get or create troop
                    Troop troop;
                    if (!existingTroops.TryGetValue(unitId.Value, out troop!))
                    {
                        if (!troopsToCreate.TryGetValue(unitId.Value, out troop!))
                        {
                            troop = new Troop
                            {
                                ScoutnetId = unitId.Value,
                                ScoutGroupId = scoutGroupId,
                                SemesterId = semesterId,
                                Name = unitName,
                                UnitTypeId = member.GetUnitTypeId()
                            };
                            troopsToCreate[unitId.Value] = troop;
                            _context.Troops.Add(troop);
                            result.TroopsProcessed++;
                            result.LogMessages.Add($"Created troop: {unitName} (ID: {unitId.Value})");
                            result.CreatedTroops.Add(new ImportedTroop(unitId.Value, unitName, 0, 0));
                        }
                    }
                    else
                    {
                        // Update troop name and unit type if changed
                        if (troop.Name != unitName)
                        {
                            troop.Name = unitName;
                        }
                        var unitTypeId = member.GetUnitTypeId();
                        if (troop.UnitTypeId != unitTypeId)
                        {
                            troop.UnitTypeId = unitTypeId;
                        }
                    }

                    // Ensure troop person membership
                    var existingMembership = existingTroopPersons
                        .FirstOrDefault(tp => tp.PersonId == personId &&
                            (tp.Troop == troop || tp.Troop?.ScoutnetId == unitId.Value));

                    if (existingMembership == null)
                    {
                        var troopPerson = new TroopPerson
                        {
                            Troop = troop,
                            PersonId = personId,
                            IsLeader = member.IsLeader(),
                            Patrol = member.GetPatrol(),
                            PatrolId = member.GetPatrolId()
                        };
                        _context.TroopPersons.Add(troopPerson);
                        existingTroopPersons.Add(troopPerson);
                        result.TroopMembershipsCreated++;
                    }
                    else
                    {
                        // Update existing membership
                        existingMembership.IsLeader = member.IsLeader();
                        existingMembership.Patrol = member.GetPatrol();
                        existingMembership.PatrolId = member.GetPatrolId();
                    }
                }

                // Collect leader assignments from roles structure
                var leaderTroopIds = GetLeaderTroopIds(member);
                foreach (var leaderTroopId in leaderTroopIds)
                {
                    leadersForTroops.Add((leaderTroopId, personId));
                }

                processedCount++;
                if (processedCount % 50 == 0)
                {
                    progress?.Report($"Processed {processedCount}/{response.Data.Count} members...");
                }
            }

            // Process leader assignments from roles structure
            foreach (var (troopScoutnetId, personId) in leadersForTroops)
            {
                // Find the troop
                Troop? troop = null;
                if (existingTroops.TryGetValue(troopScoutnetId, out troop) ||
                    troopsToCreate.TryGetValue(troopScoutnetId, out troop))
                {
                    // Find or create TroopPerson
                    var existingMembership = existingTroopPersons
                        .FirstOrDefault(tp => tp.PersonId == personId &&
                            (tp.Troop == troop || tp.Troop?.ScoutnetId == troopScoutnetId));

                    if (existingMembership != null)
                    {
                        existingMembership.IsLeader = true;
                    }
                    else
                    {
                        // Leader not already assigned to this troop - add them
                        var troopPerson = new TroopPerson
                        {
                            Troop = troop,
                            PersonId = personId,
                            IsLeader = true
                        };
                        _context.TroopPersons.Add(troopPerson);
                        existingTroopPersons.Add(troopPerson);
                        result.TroopMembershipsCreated++;

                        if (existingPersons.TryGetValue(personId, out var leaderPerson))
                        {
                            result.LogMessages.Add($"Added leader {leaderPerson.FullName} to {troop.Name}");
                        }
                    }
                }
            }

            // Mark persons as not in scoutnet if they weren't in the import
            progress?.Report("Checking for removed members...");
            foreach (var scoutGroupPerson in existingScoutGroupPersons.Values)
            {
                if (!seenPersonIds.Contains(scoutGroupPerson.PersonId) && !scoutGroupPerson.NotInScoutnet)
                {
                    scoutGroupPerson.NotInScoutnet = true;
                    scoutGroupPerson.UpdatedAt = DateTime.UtcNow;
                    result.PersonsRemoved++;

                    if (existingPersons.TryGetValue(scoutGroupPerson.PersonId, out var removedPerson))
                    {
                        result.LogMessages.Add($"Marked {removedPerson.FullName} (#{removedPerson.Id}) as not in Scoutnet");
                        result.RemovedPersons.Add(new ImportedPerson(removedPerson.Id, removedPerson.FullName, null));
                    }
                }
            }

            // Update troop member/leader counts for created troops
            foreach (var createdTroop in result.CreatedTroops.ToList())
            {
                var troopMembers = existingTroopPersons
                    .Where(tp => tp.Troop?.ScoutnetId == createdTroop.ScoutnetId ||
                                 (troopsToCreate.TryGetValue(createdTroop.ScoutnetId, out var t) && tp.Troop == t))
                    .ToList();

                var memberCount = troopMembers.Count;
                var leaderCount = troopMembers.Count(tp => tp.IsLeader);

                // Replace with updated counts
                var index = result.CreatedTroops.FindIndex(t => t.ScoutnetId == createdTroop.ScoutnetId);
                if (index >= 0)
                {
                    result.CreatedTroops[index] = createdTroop with { MemberCount = memberCount, LeaderCount = leaderCount };
                }
            }

            progress?.Report("Saving changes to database...");
            await _context.SaveChangesAsync(cancellationToken);

            result.Success = true;
            result.Duration = stopwatch.Elapsed;

            _logger.LogInformation(
                "Import completed: {Created} created, {Updated} updated, {Removed} removed in {Duration:F1}s",
                result.PersonsCreated, result.PersonsUpdated, result.PersonsRemoved, result.Duration.TotalSeconds);

            progress?.Report(result.ToString());
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Database error: {ex.Message}";
            result.Duration = stopwatch.Elapsed;
            _logger.LogError(ex, "Database error during import for group {GroupId}", scoutGroupId);
            return result;
        }
    }

    /// <summary>
    /// Gets the troop IDs where the member has a leader role from the roles structure.
    /// </summary>
    private static List<int> GetLeaderTroopIds(ScoutnetMember member)
    {
        var result = new List<int>();
        var roles = member.Roles?.Value;
        if (roles?.Troop == null) return result;

        foreach (var (troopIdStr, troopRoles) in roles.Troop)
        {
            if (!int.TryParse(troopIdStr, out var troopId)) continue;

            foreach (var (_, role) in troopRoles)
            {
                if (LeaderRoleIds.Contains(role.RoleId))
                {
                    result.Add(troopId);
                    break; // Only need to add the troop once
                }
            }
        }

        return result;
    }

    private static Person CreatePersonFromScoutnet(ScoutnetMember member)
    {
        return new Person
        {
            Id = member.GetMemberNo(),
            FirstName = Truncate(member.GetFirstName(), MaxFirstNameLength),
            LastName = Truncate(member.GetLastName(), MaxLastNameLength),
            BirthDate = member.GetBirthDate(),
            PersonalNumber = member.GetPersonalNumber().GetNullablePersonnummer(),
            Email = TruncateNullable(member.GetEmail(), MaxEmailLength),
            AltEmail = TruncateNullable(member.GetAltEmail(), MaxEmailLength),
            Mobile = TruncateNullable(member.GetMobile(), MaxMobileLength),
            Phone = TruncateNullable(member.GetPhone(), MaxPhoneLength),
            Street = TruncateNullable(member.GetStreet(), MaxStreetLength),
            ZipCode = TruncateNullable(member.GetZipCode(), MaxZipCodeLength),
            ZipName = TruncateNullable(member.GetZipName(), MaxZipNameLength),
            MumName = TruncateNullable(member.GetMumName(), MaxMumNameLength),
            MumEmail = TruncateNullable(member.GetMumEmail(), MaxEmailLength),
            MumMobile = TruncateNullable(member.GetMumMobile(), MaxMobileLength),
            DadName = TruncateNullable(member.GetDadName(), MaxDadNameLength),
            DadEmail = TruncateNullable(member.GetDadEmail(), MaxEmailLength),
            DadMobile = TruncateNullable(member.GetDadMobile(), MaxMobileLength),
            Removed = false
        };
    }

    private static void UpdatePersonFromScoutnet(Person person, ScoutnetMember member)
    {
        person.FirstName = Truncate(member.GetFirstName(), MaxFirstNameLength);
        person.LastName = Truncate(member.GetLastName(), MaxLastNameLength);
        person.BirthDate = member.GetBirthDate();
        person.PersonalNumber = member.GetPersonalNumber().GetNullablePersonnummer();
        person.Email = TruncateNullable(member.GetEmail(), MaxEmailLength);
        person.AltEmail = TruncateNullable(member.GetAltEmail(), MaxEmailLength);
        person.Mobile = TruncateNullable(member.GetMobile(), MaxMobileLength);
        person.Phone = TruncateNullable(member.GetPhone(), MaxPhoneLength);
        person.Street = TruncateNullable(member.GetStreet(), MaxStreetLength);
        person.ZipCode = TruncateNullable(member.GetZipCode(), MaxZipCodeLength);
        person.ZipName = TruncateNullable(member.GetZipName(), MaxZipNameLength);
        person.MumName = TruncateNullable(member.GetMumName(), MaxMumNameLength);
        person.MumEmail = TruncateNullable(member.GetMumEmail(), MaxEmailLength);
        person.MumMobile = TruncateNullable(member.GetMumMobile(), MaxMobileLength);
        person.DadName = TruncateNullable(member.GetDadName(), MaxDadNameLength);
        person.DadEmail = TruncateNullable(member.GetDadEmail(), MaxEmailLength);
        person.DadMobile = TruncateNullable(member.GetDadMobile(), MaxMobileLength);
        person.UpdatedAt = DateTime.UtcNow;
    }
}
