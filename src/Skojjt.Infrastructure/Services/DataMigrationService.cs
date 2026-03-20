using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Entities;
using Skojjt.Infrastructure.Data;
using Skojjt.Core.Utilities;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Converter that handles JSON values that can be either a string or an array of strings.
/// </summary>
public class StringOrArrayConverter : JsonConverter<List<string>?>
{
    public override List<string>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return null;
            return [value];
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<string>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;
                if (reader.TokenType == JsonTokenType.String)
                {
                    var item = reader.GetString();
                    if (!string.IsNullOrWhiteSpace(item))
                        list.Add(item);
                }
            }
            return list.Count > 0 ? list : null;
        }

        throw new JsonException($"Unexpected token type: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, List<string>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        JsonSerializer.Serialize(writer, value, options);
    }
}

/// <summary>
/// Converter that handles JSON values that can be either a single int or an array of ints.
/// </summary>
public class IntOrArrayConverter : JsonConverter<List<int>?>
{
    public override List<int>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.Number)
        {
            return [reader.GetInt32()];
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString();
            if (string.IsNullOrWhiteSpace(value))
                return null;
            if (int.TryParse(value, out var intVal))
                return [intVal];
            return null;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            var list = new List<int>();
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                    break;
                if (reader.TokenType == JsonTokenType.Number)
                {
                    list.Add(reader.GetInt32());
                }
                else if (reader.TokenType == JsonTokenType.String)
                {
                    var strVal = reader.GetString();
                    if (int.TryParse(strVal, out var intVal))
                        list.Add(intVal);
                }
            }
            return list.Count > 0 ? list : null;
        }

        throw new JsonException($"Unexpected token type for int array: {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, List<int>? value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
        JsonSerializer.Serialize(writer, value, options);
    }
}

/// <summary>
/// Service for importing migrated data from JSON files into PostgreSQL.
/// </summary>
public class DataMigrationService
{
    private readonly SkojjtDbContext _context;
    private readonly ILogger<DataMigrationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    // Pre-loaded lookup caches shared across import steps.
    // Not thread-safe — this service must be scoped (one instance per import operation).
    private Dictionary<(int ScoutnetId, int ScoutGroupId, int SemesterId), int> _troopLookup = [];
    private Dictionary<(int TroopId, DateOnly MeetingDate), int> _meetingLookup = [];

    public DataMigrationService(SkojjtDbContext context, ILogger<DataMigrationService> logger)
    {
        _context = context;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new StringOrArrayConverter(), new IntOrArrayConverter() }
        };

        // Use a generous command timeout for large migration operations (10 minutes)
        _context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));

        // Disable auto-detect changes for bulk import performance
        _context.ChangeTracker.AutoDetectChangesEnabled = false;
    }

    /// <summary>
    /// Resolves a troop's database ID from its ScoutnetId, ScoutGroupId, and SemesterId using the cached lookup.
    /// </summary>
    private int? ResolveTroopId(int scoutnetId, int scoutGroupId, int semesterId)
    {
        return _troopLookup.TryGetValue((scoutnetId, scoutGroupId, semesterId), out var troopId) ? troopId : null;
    }

    /// <summary>
    /// Resolves a meeting's database ID from its TroopId and MeetingDate using the cached lookup.
    /// </summary>
    private int? ResolveMeetingId(int troopId, DateOnly meetingDate)
    {
        return _meetingLookup.TryGetValue((troopId, meetingDate), out var meetingId) ? meetingId : null;
    }

    /// <summary>
    /// Saves changes, clears the change tracker, and logs progress.
    /// Clearing the tracker prevents slowdown as entities accumulate during bulk imports.
    /// </summary>
    private async Task SaveAndClearAsync(CancellationToken cancellationToken)
    {
        _context.ChangeTracker.DetectChanges();
        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();
    }

    /// <summary>
    /// Import all data from the specified directory.
    /// </summary>
    public async Task ImportAllAsync(string importDirectory, CancellationToken cancellationToken = default, Func<MigrationProgress, Task>? progress = null)
    {
        _logger.LogInformation("Starting data import from {Directory}", importDirectory);

        var stats = new Dictionary<string, int>();
        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        var totalSteps = 13;
        var currentStep = 0;

        // Import in dependency order
        stats["semesters"] = await ImportStepAsync("semesters", ++currentStep, totalSteps, progress, () => ImportSemestersAsync(Path.Combine(importDirectory, "semesters.json"), cancellationToken));
        stats["scout_groups"] = await ImportStepAsync("scout_groups", ++currentStep, totalSteps, progress, () => ImportScoutGroupsAsync(Path.Combine(importDirectory, "scout_groups.json"), cancellationToken));
        stats["persons"] = await ImportStepAsync("persons", ++currentStep, totalSteps, progress, () => ImportPersonsAsync(Path.Combine(importDirectory, "persons.json"), cancellationToken));
        stats["troops"] = await ImportStepAsync("troops", ++currentStep, totalSteps, progress, () => ImportTroopsAsync(Path.Combine(importDirectory, "troops.json"), cancellationToken));
        await UpdateNextLocalTroopIdsAsync(cancellationToken);
        stats["troop_persons"] = await ImportStepAsync("troop_persons", ++currentStep, totalSteps, progress, () => ImportTroopPersonsAsync(Path.Combine(importDirectory, "troop_persons.json"), cancellationToken));
        stats["meetings"] = await ImportStepAsync("meetings", ++currentStep, totalSteps, progress, () => ImportMeetingsAsync(Path.Combine(importDirectory, "meetings.json"), cancellationToken));
        stats["meeting_attendances"] = await ImportStepAsync("meeting_attendances", ++currentStep, totalSteps, progress, () => ImportMeetingAttendancesAsync(Path.Combine(importDirectory, "meeting_attendances.json"), cancellationToken));
        stats["users"] = await ImportStepAsync("users", ++currentStep, totalSteps, progress, () => ImportUsersAsync(Path.Combine(importDirectory, "users.json"), cancellationToken));
        stats["badge_templates"] = await ImportStepAsync("badge_templates", ++currentStep, totalSteps, progress, () => ImportBadgeTemplatesAsync(Path.Combine(importDirectory, "badge_templates.json"), cancellationToken));
        stats["badges"] = await ImportStepAsync("badges", ++currentStep, totalSteps, progress, () => ImportBadgesAsync(Path.Combine(importDirectory, "badges.json"), cancellationToken));
        stats["troop_badges"] = await ImportStepAsync("troop_badges", ++currentStep, totalSteps, progress, () => ImportTroopBadgesAsync(Path.Combine(importDirectory, "troop_badges.json"), cancellationToken));
        stats["badge_parts_done"] = await ImportStepAsync("badge_parts_done", ++currentStep, totalSteps, progress, () => ImportBadgePartsDoneAsync(Path.Combine(importDirectory, "badge_parts_done.json"), cancellationToken));
        stats["badges_completed"] = await ImportStepAsync("badges_completed", ++currentStep, totalSteps, progress, () => ImportBadgesCompletedAsync(Path.Combine(importDirectory, "badges_completed.json"), cancellationToken));

        totalSw.Stop();
        _logger.LogInformation("Data import complete in {Elapsed}!", totalSw.Elapsed);
        foreach (var (table, count) in stats)
        {
            _logger.LogInformation("  {Table}: {Count} records", table, count);
        }

        if (progress != null)
            await progress(new MigrationProgress("done", totalSteps, totalSteps, 0, totalSw.Elapsed));
    }

    private async Task<int> ImportStepAsync(string stepName, int step, int totalSteps, Func<MigrationProgress, Task>? progress, Func<Task<int>> importFunc)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _logger.LogInformation("Starting import step: {Step}...", stepName);
        if (progress != null)
            await progress(new MigrationProgress(stepName, step, totalSteps, 0, null));
        var count = await importFunc();
        sw.Stop();
        _logger.LogInformation("Completed {Step}: {Count} records in {Elapsed}", stepName, count, sw.Elapsed);
        if (progress != null)
            await progress(new MigrationProgress(stepName, step, totalSteps, count, sw.Elapsed));
        return count;
    }

    private async Task<List<T>> LoadJsonFileAsync<T>(string filePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("File not found: {FilePath}", filePath);
            return new List<T>();
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<List<T>>(json, _jsonOptions) ?? new List<T>();
    }

    private async Task<int> ImportSemestersAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<SemesterImport>(filePath, cancellationToken);
        var existingIds = (await _context.Semesters.Select(s => s.Id).ToListAsync(cancellationToken)).ToHashSet();
        var count = 0;

        foreach (var item in items)
        {
            if (!existingIds.Add(item.Id))
                continue;

            _context.Semesters.Add(new Semester(item.Id, item.Year, item.IsAutumn));
            count++;
        }

        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} semesters", count);
        return count;
    }

    private async Task<int> ImportScoutGroupsAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<ScoutGroupImport>(filePath, cancellationToken);
        var existingGroups = await _context.ScoutGroups.ToDictionaryAsync(s => s.Id, cancellationToken);
        var count = 0;
        var updated = 0;

        foreach (var item in items)
        {
            if (item.Id <= 0)
                continue;

            if (existingGroups.TryGetValue(item.Id, out var existing))
            {
                // Update fields that may have been missing from a previous import
                existing.OrganisationNumber ??= item.OrganisationNumber;
                existing.AssociationId ??= item.AssociationId;
                existing.MunicipalityId ??= item.MunicipalityId ?? "1480";
                existing.ApiKeyWaitinglist ??= item.ApiKeyWaitinglist;
                existing.ApiKeyAllMembers ??= item.ApiKeyAllMembers;
                existing.BankAccount ??= item.BankAccount;
                existing.PostalAddress ??= item.PostalAddress;
                existing.DefaultCampLocation ??= item.DefaultLocation;
                existing.SignatoryPhone ??= item.SignatoryPhone;
                existing.SignatoryEmail ??= item.SignatoryEmail;
                updated++;
                continue;
            }

            _context.ScoutGroups.Add(new ScoutGroup
            {
                Id = item.Id,
                Name = item.Name ?? "",
                OrganisationNumber = item.OrganisationNumber,
                AssociationId = item.AssociationId,
                MunicipalityId = item.MunicipalityId ?? "1480",
                ApiKeyWaitinglist = item.ApiKeyWaitinglist,
                ApiKeyAllMembers = item.ApiKeyAllMembers,
                BankAccount = item.BankAccount,
                Address = item.Address,
                PostalAddress = item.PostalAddress,
                Email = item.Email,
                Phone = item.Phone,
                DefaultCampLocation = item.DefaultLocation,
                Signatory = item.Signatory,
                SignatoryPhone = item.SignatoryPhone,
                SignatoryEmail = item.SignatoryEmail,
                AttendanceMinYear = item.AttendanceMinYear ?? 10,
                AttendanceInclHike = item.AttendanceInclHike ?? true
            });
            count++;
        }

        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} scout groups, updated {Updated} existing", count, updated);
        return count + updated;
    }

    private async Task<int> ImportPersonsAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<PersonImport>(filePath, cancellationToken);
        var count = 0;
        var validScoutGroups = (await _context.ScoutGroups.Select(s => s.Id).ToListAsync(cancellationToken)).ToHashSet();
        var existingPersonIds = (await _context.Persons.Select(s => s.Id).ToListAsync(cancellationToken)).ToHashSet();
        var existingScoutGroupPersons = (await _context.ScoutGroupPersons
            .Select(sgp => new { sgp.PersonId, sgp.ScoutGroupId })
            .ToListAsync(cancellationToken))
            .Select(sgp => (sgp.PersonId, sgp.ScoutGroupId))
            .ToHashSet();

        var scoutGroupPersonsToAdd = new List<ScoutGroupPerson>();

        foreach (var item in items)
        {
            // Skip invalid or out-of-range IDs
            if (item.Id <= 0 || item.Id > int.MaxValue)
            {
                _logger.LogWarning("Skipping person with invalid ID: {Id}", item.Id);
                continue;
            }

            var personId = (int)item.Id;

            // Collect additional ScoutGroupPerson entries for persons that already exist
            if (item.ScoutGroupId > 0 && validScoutGroups.Contains(item.ScoutGroupId)
                && existingPersonIds.Contains(personId)
                && !existingScoutGroupPersons.Contains((personId, item.ScoutGroupId)))
            {
                scoutGroupPersonsToAdd.Add(new ScoutGroupPerson
                {
                    PersonId = personId,
                    ScoutGroupId = item.ScoutGroupId
                });
            }

            if (!existingPersonIds.Add(personId))
                continue;

            _context.Persons.Add(new Person
            {
                Id = personId,
                FirstName = item.FirstName.Substring(0, Math.Min(item.FirstName.Length, 50)),
                LastName = item.LastName.Substring(0, Math.Min(item.LastName.Length, 50)),
                BirthDate = ParseDate(item.BirthDate),
                PersonalNumber = item.PersonalNumber.GetNullablePersonnummer(),
                Email = item.Email?.Substring(0, Math.Min(item.Email.Length, 100)),
                Phone = item.Phone?.Substring(0, Math.Min(item.Phone.Length, 50)),
                Mobile = item.Mobile?.Substring(0, Math.Min(item.Mobile.Length, 50)),
                AltEmail = item.AltEmail?.Substring(0, Math.Min(item.AltEmail.Length, 100)),
                MumName = item.MumName?.Substring(0, Math.Min(item.MumName.Length, 50)),
                MumEmail = item.MumEmail?.Substring(0, Math.Min(item.MumEmail.Length, 100)),
                MumMobile = item.MumMobile?.Substring(0, Math.Min(item.MumMobile.Length, 50)),
                DadName = item.DadName?.Substring(0, Math.Min(item.DadName.Length, 50)),
                DadEmail = item.DadEmail?.Substring(0, Math.Min(item.DadEmail.Length, 100)),
                DadMobile = item.DadMobile?.Substring(0, Math.Min(item.DadMobile.Length, 50)),
                Street = item.Street?.Substring(0, Math.Min(item.Street.Length, 100)),
                ZipCode = item.ZipCode?.Substring(0, Math.Min(item.ZipCode.Length, 20)),
                ZipName = item.ZipName?.Substring(0, Math.Min(item.ZipName.Length, 50)),
                MemberYears = item.MemberYears?.ToArray() ?? Array.Empty<int>(),
                Removed = item.Removed ?? false
            });

            // Add scout group membership via junction table
            if (item.ScoutGroupId > 0 && validScoutGroups.Contains(item.ScoutGroupId))
            {
                _context.ScoutGroupPersons.Add(new ScoutGroupPerson
                {
                    PersonId = personId,
                    ScoutGroupId = item.ScoutGroupId,
                    GroupRoles = item.GroupRoles != null ? string.Join(", ", item.GroupRoles.Select(r => r.Trim())) : null,
                    NotInScoutnet = item.NotInScoutnet ?? false
                });
                existingScoutGroupPersons.Add((personId, item.ScoutGroupId));
            }
            else
            {
                _logger.LogWarning("Skipping ScoutGroupPerson for person {PersonId} - invalid scout_group_id: {ScoutGroupId}", personId, item.ScoutGroupId);
            }

            count++;

            // Batch save to avoid memory issues
            if (count % 1000 == 0)
            {
                await SaveAndClearAsync(cancellationToken);
                _logger.LogInformation("  Imported {Count} persons so far...", count);
            }
        }
        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} persons", count);

        _logger.LogInformation("Importing scout group persons");
        var sgpCount = 0;
        foreach (var scoutGroupPerson in scoutGroupPersonsToAdd)
        {
            _context.ScoutGroupPersons.Add(scoutGroupPerson);
            sgpCount++;
            if (sgpCount % 1000 == 0)
            {
                await SaveAndClearAsync(cancellationToken);
                _logger.LogInformation("  Imported {Count} scout group persons so far...", sgpCount);
            }
        }
        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} scout group persons", sgpCount);

        return count;
    }

    private async Task<int> ImportTroopsAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<TroopImport>(filePath, cancellationToken);
        var count = 0;
        var validScoutGroups = (await _context.ScoutGroups.Select(s => s.Id).ToListAsync(cancellationToken)).ToHashSet();
        var validSemesters = (await _context.Semesters.Select(s => s.Id).ToListAsync(cancellationToken)).ToHashSet();
        var existingTroops = (await _context.Troops
            .Select(t => new { t.ScoutnetId, t.ScoutGroupId, t.SemesterId })
            .ToListAsync(cancellationToken))
            .Select(t => (t.ScoutnetId, t.ScoutGroupId, t.SemesterId))
            .ToHashSet();

        foreach (var item in items)
        {
            // Validate foreign keys
            if (item.ScoutnetId == null)
            {
                _logger.LogDebug("Skipping troop - no scoutnet_id. ScoutGroupId: {ScoutGroupId}", item.ScoutGroupId);
                continue;
            }

            if (item.ScoutGroupId == null || !validScoutGroups.Contains(item.ScoutGroupId.Value))
            {
                _logger.LogDebug("Skipping troop {ScoutnetId} - invalid ScoutGroupId: {ScoutGroupId}", item.ScoutnetId, item.ScoutGroupId);
                continue;
            }

            if (item.SemesterId == null || !validSemesters.Contains(item.SemesterId.Value))
            {
                _logger.LogDebug("Skipping troop {ScoutnetId} - invalid SemesterId: {SemesterId}", item.ScoutnetId, item.SemesterId);
                continue;
            }

            if (!existingTroops.Add((item.ScoutnetId.Value, item.ScoutGroupId.Value, item.SemesterId.Value)))
                continue;

            _context.Troops.Add(new Troop
            {
                ScoutnetId = (int)item.ScoutnetId,
                ScoutGroupId = item.ScoutGroupId.Value,
                SemesterId = item.SemesterId.Value,
                Name = item.Name ?? "",
                DefaultStartTime = ParseTime(item.DefaultStartTime) ?? new TimeOnly(18, 30),
                DefaultDurationMinutes = item.DefaultDurationMinutes ?? 90
            });
            count++;

            if (count % 1000 == 0)
            {
                await SaveAndClearAsync(cancellationToken);
                _logger.LogInformation("  Imported {Count} troops so far...", count);
            }
        }

        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} troops", count);

        // Build the shared troop lookup cache for subsequent import steps
        _troopLookup = (await _context.Troops
            .Select(t => new { t.Id, t.ScoutnetId, t.ScoutGroupId, t.SemesterId })
            .ToListAsync(cancellationToken))
            .ToDictionary(t => (t.ScoutnetId, t.ScoutGroupId, t.SemesterId), t => t.Id);
        _logger.LogInformation("Built troop lookup cache with {Count} entries", _troopLookup.Count);

        return count;
    }

    /// <summary>
    /// After importing troops, set each scout group's NextLocalTroopId to one past
    /// the highest ScoutnetId in the reserved range (250-1000) used by that group.
    /// </summary>
    private async Task UpdateNextLocalTroopIdsAsync(CancellationToken cancellationToken)
    {
        const int rangeStart = 250;
        const int rangeEnd = 1000;

        var maxByGroup = await _context.Troops
            .Where(t => t.ScoutnetId >= rangeStart && t.ScoutnetId <= rangeEnd)
            .GroupBy(t => t.ScoutGroupId)
            .Select(g => new { ScoutGroupId = g.Key, MaxId = g.Max(t => t.ScoutnetId) })
            .ToListAsync(cancellationToken);

        foreach (var entry in maxByGroup)
        {
            var scoutGroup = await _context.ScoutGroups.FindAsync([entry.ScoutGroupId], cancellationToken);
            if (scoutGroup != null)
            {
                scoutGroup.NextLocalTroopId = entry.MaxId + 1;
                _logger.LogInformation(
                    "Set NextLocalTroopId={NextId} for scout group {GroupId}",
                    scoutGroup.NextLocalTroopId, entry.ScoutGroupId);
            }
        }

        await SaveAndClearAsync(cancellationToken);
    }

    private async Task<int> ImportTroopPersonsAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<TroopPersonImport>(filePath, cancellationToken);
        var count = 0;
        var validPersons = (await _context.Persons.Select(p => p.Id).ToListAsync(cancellationToken)).ToHashSet();
        var existingMemberships = (await _context.TroopPersons
            .Select(tp => new { tp.TroopId, tp.PersonId })
            .ToListAsync(cancellationToken))
            .Select(tp => (tp.TroopId, tp.PersonId))
            .ToHashSet();

        foreach (var item in items)
        {
            // Skip invalid person IDs
            if (item.PersonId <= 0 || item.PersonId > int.MaxValue)
                continue;

            var personId = (int)item.PersonId;

            if (!validPersons.Contains(personId))
                continue;

            var troopId = ResolveTroopId(item.ScoutnetTroopId, item.ScoutGroupId, item.SemesterId);
            if (troopId == null)
                continue;

            if (!existingMemberships.Add((troopId.Value, personId)))
                continue;

            _context.TroopPersons.Add(new TroopPerson
            {
                TroopId = troopId.Value,
                PersonId = personId,
                IsLeader = item.IsLeader ?? false
            });
            count++;

            if (count % 1000 == 0)
            {
                await SaveAndClearAsync(cancellationToken);
                _logger.LogInformation("  Imported {Count} troop persons so far...", count);
            }
        }

        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} troop persons", count);
        return count;
    }

    private async Task<int> ImportMeetingsAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<MeetingImport>(filePath, cancellationToken);
        var count = 0;
        var skipped = 0;
        var duplicates = 0;
        var existingMeetings = (await _context.Meetings
            .Select(m => new { m.TroopId, m.MeetingDate })
            .ToListAsync(cancellationToken))
            .Select(m => (m.TroopId, m.MeetingDate))
            .ToHashSet();

        foreach (var item in items)
        {
            var troopId = ResolveTroopId(item.ScoutnetTroopId, item.GroupId, item.SemesterId);
            if (troopId == null)
            {
                skipped++;
                continue;
            }

            if (item.MeetingDate == null)
            {
                skipped++;
                continue;
            }

            DateOnly meetingDate = (DateOnly)ParseDate(item.MeetingDate)!;
            TimeOnly startTime = (TimeOnly)ParseTime(item.StartTime)!;

            if (!existingMeetings.Add((troopId.Value, meetingDate)))
            {
                duplicates++;
                continue;
            }

            var meetingName = item.Name;
            if (meetingName.Length > 50)
            {
                meetingName = meetingName.Substring(0, 50);
            }

            _context.Meetings.Add(new Meeting
            {
                TroopId = troopId.Value,
                MeetingDate = meetingDate,
                StartTime = startTime,
                Name = meetingName,
                DurationMinutes = item.DurationMinutes,
                IsHike = item.IsHike ?? false
            });
            count++;

            if (count % 1000 == 0)
            {
                await SaveAndClearAsync(cancellationToken);
                _logger.LogInformation("  Imported {Count} meetings so far...", count);
            }
        }

        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} meetings (skipped {Skipped} invalid, {Duplicates} duplicates)", count, skipped, duplicates);

        // Build the shared meeting lookup cache for attendance import
        _meetingLookup = (await _context.Meetings
            .Select(m => new { m.Id, m.TroopId, m.MeetingDate })
            .ToListAsync(cancellationToken))
            .ToDictionary(m => (m.TroopId, m.MeetingDate), m => m.Id);
        _logger.LogInformation("Built meeting lookup cache with {Count} entries", _meetingLookup.Count);

        return count;
    }

    private async Task<int> ImportMeetingAttendancesAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<MeetingAttendanceImport>(filePath, cancellationToken);
        var count = 0;
        var skipped = 0;
        var validPersons = (await _context.Persons.Select(p => p.Id).ToListAsync(cancellationToken)).ToHashSet();
        var existingAttendances = (await _context.Set<MeetingAttendance>()
            .Select(ma => new { ma.MeetingId, ma.PersonId })
            .ToListAsync(cancellationToken))
            .Select(ma => (ma.MeetingId, ma.PersonId))
            .ToHashSet();

        foreach (var item in items)
        {
            // Skip invalid person IDs
            if (item.PersonId <= 0 || item.PersonId > int.MaxValue)
            {
                skipped++;
                continue;
            }

            var personId = (int)item.PersonId;

            if (!validPersons.Contains(personId))
            {
                skipped++;
                continue;
            }

            var troopId = ResolveTroopId((int)item.TroopScoutnetId, (int)item.GroupId, item.SemesterId);
            if (troopId == null)
            {
                skipped++;
                continue;
            }

            var meetingDate = DateOnly.Parse(item.MeetingDate);
            var meetingId = ResolveMeetingId(troopId.Value, meetingDate);
            if (meetingId == null)
            {
                skipped++;
                continue;
            }

            if (!existingAttendances.Add((meetingId.Value, personId)))
            {
                skipped++;
                continue;
            }

            _context.Set<MeetingAttendance>().Add(new MeetingAttendance
            {
                MeetingId = meetingId.Value,
                PersonId = personId
            });
            count++;

            if (count % 5000 == 0)
            {
                await SaveAndClearAsync(cancellationToken);
                _logger.LogInformation("  Imported {Count} meeting attendances so far...", count);
            }
        }

        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} meeting attendances (skipped {Skipped})", count, skipped);
        return count;
    }

    private async Task<int> ImportUsersAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<UserImport>(filePath, cancellationToken);
        var count = 0;
        var existingEmails = (await _context.Users.Select(u => u.Email).ToListAsync(cancellationToken)).ToHashSet();

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Email))
                continue;

            if (!existingEmails.Add(item.Email))
                continue;

            _context.Users.Add(new User
            {
                Id = item.Id,
                Email = item.Email,
                Name = item.Name,
            });
            count++;
        }

        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} users", count);
        return count;
    }

    private async Task<int> ImportBadgeTemplatesAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<BadgeTemplateImport>(filePath, cancellationToken);
        var count = 0;

        // Reset sequence if table is empty
        if (!await _context.BadgeTemplates.AnyAsync(cancellationToken))
        {
            await _context.Database.ExecuteSqlRawAsync(
                "ALTER SEQUENCE badge_templates_id_seq RESTART WITH 1", cancellationToken);
        }

        var existingNames = (await _context.BadgeTemplates.Select(bt => bt.Name).ToListAsync(cancellationToken)).ToHashSet();

        foreach (var item in items)
        {
            if (!existingNames.Add(item.Name ?? ""))
                continue;

            var scoutShort = item.PartsScoutShort?.ToArray() ?? Array.Empty<string>();
            var scoutLong = item.PartsScoutLong?.ToArray() ?? Array.Empty<string>();
            var adminShort = item.PartsAdminShort?.ToArray() ?? Array.Empty<string>();
            var adminLong = item.PartsAdminLong?.ToArray() ?? Array.Empty<string>();

            var template = new BadgeTemplate
            {
                Name = item.Name ?? "",
                Description = item.Description,
                PartsScoutShort = scoutShort,
                PartsScoutLong = scoutLong,
                PartsAdminShort = adminShort,
                PartsAdminLong = adminLong,
                ImageUrl = item.ImageUrl
            };

            // Generate normalized BadgePart entities from legacy arrays
            foreach (var part in CreatePartsFromLegacyArrays(scoutShort, scoutLong, adminShort, adminLong))
            {
                template.Parts.Add(part);
            }

            _context.BadgeTemplates.Add(template);
            count++;
        }

        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} badge templates", count);
        return count;
    }

    private async Task<int> ImportBadgesAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<BadgeImport>(filePath, cancellationToken);
        var count = 0;
        var existingBadgeIds = (await _context.Badges.Select(b => b.Id).ToListAsync(cancellationToken)).ToHashSet();

        // Use explicit IDs from the import to maintain mapping
        foreach (var item in items)
        {
            if (item.Id <= 0)
                continue;

            if (item.ScoutGroupId == 0)
            {
                _logger.LogWarning("Skipping badge {Id} - invalid scout_group_id: {ScoutGroupId}", item.Id, item.ScoutGroupId);
                continue;
            }

            if (!existingBadgeIds.Add(item.Id))
                continue;

            var scoutShort = item.PartsScoutShort?.ToArray() ?? Array.Empty<string>();
            var scoutLong = item.PartsScoutLong?.ToArray() ?? Array.Empty<string>();
            var adminShort = item.PartsAdminShort?.ToArray() ?? Array.Empty<string>();
            var adminLong = item.PartsAdminLong?.ToArray() ?? Array.Empty<string>();

            // Use raw SQL to insert with explicit ID
            await _context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO badges (id, scout_group_id, name, description, parts_scout_short, parts_scout_long, parts_admin_short, parts_admin_long, image_url)
                  VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})
                  ON CONFLICT (id) DO NOTHING",
                item.Id,
                item.ScoutGroupId,
                item.Name,
                item.Description,
                scoutShort,
                scoutLong,
                adminShort,
                adminLong,
                (object?)item.ImageUrl ?? DBNull.Value);

            // Generate normalized BadgePart entities from legacy arrays
            foreach (var part in CreatePartsFromLegacyArrays(scoutShort, scoutLong, adminShort, adminLong))
            {
                part.BadgeId = item.Id;
                _context.BadgeParts.Add(part);
            }

            count++;

            if (count % 500 == 0)
            {
                await SaveAndClearAsync(cancellationToken);
            }
        }

        await SaveAndClearAsync(cancellationToken);

        // Update the sequence to be after the max ID
        if (items.Count > 0)
        {
            var maxId = items.Max(b => b.Id);
            await _context.Database.ExecuteSqlAsync(
                $"SELECT setval('badges_id_seq', {maxId + 1}, false)", cancellationToken);
        }

        _logger.LogInformation("Imported {Count} badges", count);
        return count;
    }

    private async Task<int> ImportTroopBadgesAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<TroopBadgeImport>(filePath, cancellationToken);
        var count = 0;
        var skipped = 0;
        var validBadges = (await _context.Badges.Select(b => b.Id).ToListAsync(cancellationToken)).ToHashSet();
        var existingTroopBadges = (await _context.Set<TroopBadge>()
            .Select(tb => new { tb.TroopId, tb.BadgeId })
            .ToListAsync(cancellationToken))
            .Select(tb => (tb.TroopId, tb.BadgeId))
            .ToHashSet();

        foreach (var item in items)
        {
            var troopId = ResolveTroopId(item.ScoutnetTroopId, item.ScoutGroupId, item.SemesterId);
            if (troopId == null)
            {
                skipped++;
                continue;
            }

            if (!validBadges.Contains(item.BadgeId))
            {
                skipped++;
                continue;
            }

            if (!existingTroopBadges.Add((troopId.Value, item.BadgeId)))
            {
                skipped++;
                continue;
            }

            _context.Set<TroopBadge>().Add(new TroopBadge
            {
                TroopId = troopId.Value,
                BadgeId = item.BadgeId,
                SortOrder = item.SortOrder
            });
            count++;

            if (count % 1000 == 0)
            {
                await SaveAndClearAsync(cancellationToken);
            }
        }

        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} troop badges (skipped {Skipped})", count, skipped);
        return count;
    }

    private async Task<int> ImportBadgePartsDoneAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<BadgePartDoneImport>(filePath, cancellationToken);
        var count = 0;
        var validPersons = (await _context.Persons.Select(p => p.Id).ToListAsync(cancellationToken)).ToHashSet();
        var validBadges = (await _context.Badges.Select(b => b.Id).ToListAsync(cancellationToken)).ToHashSet();
        var existingParts = (await _context.Set<BadgePartDone>()
            .Select(bp => new { bp.PersonId, bp.BadgeId, bp.PartIndex, bp.IsScoutPart })
            .ToListAsync(cancellationToken))
            .Select(bp => (bp.PersonId, bp.BadgeId, bp.PartIndex, bp.IsScoutPart))
            .ToHashSet();

        foreach (var item in items)
        {
            // Skip invalid person IDs
            if (item.PersonId <= 0 || item.PersonId > int.MaxValue)
                continue;

            var personId = (int)item.PersonId;

            if (!validPersons.Contains(personId) || !validBadges.Contains(item.BadgeId))
                continue;

            var isScoutPart = item.IsScoutPart ?? true;
            if (!existingParts.Add((personId, item.BadgeId, item.PartIndex, isScoutPart)))
                continue;

            _context.Set<BadgePartDone>().Add(new BadgePartDone
            {
                PersonId = personId,
                BadgeId = item.BadgeId,
                PartIndex = item.PartIndex,
                IsScoutPart = isScoutPart,
                ExaminerName = item.ExaminerName,
                CompletedDate = ParseDate(item.CompletedDate) ?? DateOnly.FromDateTime(DateTime.Now)
            });
            count++;

            if (count % 1000 == 0)
            {
                await SaveAndClearAsync(cancellationToken);
            }
        }

        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} badge parts done", count);
        return count;
    }

    private async Task<int> ImportBadgesCompletedAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<BadgeCompletedImport>(filePath, cancellationToken);
        var count = 0;
        var validPersons = (await _context.Persons.Select(p => p.Id).ToListAsync(cancellationToken)).ToHashSet();
        var validBadges = (await _context.Badges.Select(b => b.Id).ToListAsync(cancellationToken)).ToHashSet();
        var existingCompleted = (await _context.Set<BadgeCompleted>()
            .Select(bc => new { bc.PersonId, bc.BadgeId })
            .ToListAsync(cancellationToken))
            .Select(bc => (bc.PersonId, bc.BadgeId))
            .ToHashSet();

        foreach (var item in items)
        {
            // Skip invalid person IDs
            if (item.PersonId <= 0 || item.PersonId > int.MaxValue)
                continue;

            var personId = (int)item.PersonId;

            if (!validPersons.Contains(personId) || !validBadges.Contains(item.BadgeId))
                continue;

            if (!existingCompleted.Add((personId, item.BadgeId)))
                continue;

            _context.Set<BadgeCompleted>().Add(new BadgeCompleted
            {
                PersonId = personId,
                BadgeId = item.BadgeId,
                Examiner = item.Examiner,
                CompletedDate = ParseDate(item.CompletedDate) ?? DateOnly.FromDateTime(DateTime.Now)
            });
            count++;
        }

        await SaveAndClearAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} badges completed", count);
        return count;
    }

    private static DateOnly? ParseDate(string? dateStr)
    {
        if (string.IsNullOrEmpty(dateStr))
            return null;
        
        if (DateOnly.TryParse(dateStr, out var date))
            return date;
        
        // Try parsing just the date part
        if (dateStr.Length >= 10 && DateOnly.TryParse(dateStr[..10], out date))
            return date;
        
        return null;
    }

    private static TimeOnly? ParseTime(string? timeStr)
    {
        if (string.IsNullOrEmpty(timeStr))
            return null;

        if (TimeOnly.TryParse(timeStr, out var time))
            return time;

        return null;
    }

    /// <summary>
    /// Creates normalized BadgePart entities from legacy PartsScout*/PartsAdmin* arrays.
    /// Scout parts use SortOrder starting at 0, admin parts start at 100.
    /// </summary>
    private static List<BadgePart> CreatePartsFromLegacyArrays(
        string[] partsScoutShort, string[] partsScoutLong,
        string[] partsAdminShort, string[] partsAdminLong)
    {
        const int maxShortLength = 255;
        var parts = new List<BadgePart>();

        for (var i = 0; i < partsScoutShort.Length; i++)
        {
            var shortDesc = partsScoutShort[i];
            var longDesc = i < partsScoutLong.Length ? partsScoutLong[i] : null;

            // If short description exceeds column limit, move full text to long description
            if (shortDesc.Length > maxShortLength)
            {
                longDesc ??= shortDesc;
                shortDesc = shortDesc[..maxShortLength];
            }

            parts.Add(new BadgePart
            {
                SortOrder = i,
                IsAdminPart = false,
                ShortDescription = shortDesc,
                LongDescription = longDesc
            });
        }

        for (var i = 0; i < partsAdminShort.Length; i++)
        {
            var shortDesc = partsAdminShort[i];
            var longDesc = i < partsAdminLong.Length ? partsAdminLong[i] : null;

            if (shortDesc.Length > maxShortLength)
            {
                longDesc ??= shortDesc;
                shortDesc = shortDesc[..maxShortLength];
            }

            parts.Add(new BadgePart
            {
                SortOrder = 100 + i,
                IsAdminPart = true,
                ShortDescription = shortDesc,
                LongDescription = longDesc
            });
        }

        return parts;
    }
}

/// <summary>
/// Progress report from a migration step.
/// </summary>
/// <param name="Step">Current step name (e.g. "persons"), or "done" when complete.</param>
/// <param name="Current">Current step number (1-based).</param>
/// <param name="Total">Total number of steps.</param>
/// <param name="Records">Number of records imported in this step (0 while in progress).</param>
/// <param name="Elapsed">Time taken for this step (null while in progress).</param>
public record MigrationProgress(string Step, int Current, int Total, int Records, TimeSpan? Elapsed);

#region Import DTOs

public record SemesterImport(
    [property: JsonPropertyName("id")] int Id, 
    [property: JsonPropertyName("year")] int Year, 
    [property: JsonPropertyName("is_autumn")] bool IsAutumn);

public record ScoutGroupImport(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("organisation_number")] string? OrganisationNumber,
    [property: JsonPropertyName("association_id")] string? AssociationId,
    [property: JsonPropertyName("municipality_id")] string? MunicipalityId,
    [property: JsonPropertyName("api_key_waitinglist")] string? ApiKeyWaitinglist,
    [property: JsonPropertyName("api_key_all_members")] string? ApiKeyAllMembers,
    [property: JsonPropertyName("bank_account")] string? BankAccount,
    [property: JsonPropertyName("address")] string? Address,
    [property: JsonPropertyName("postal_address")] string? PostalAddress,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("default_location")] string? DefaultLocation,
    [property: JsonPropertyName("signatory")] string? Signatory,
    [property: JsonPropertyName("signatory_phone")] string? SignatoryPhone,
    [property: JsonPropertyName("signatory_email")] string? SignatoryEmail,
    [property: JsonPropertyName("attendance_min_year")] int? AttendanceMinYear,
    [property: JsonPropertyName("attendance_incl_hike")] bool? AttendanceInclHike
);

public record PersonImport(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("scout_group_id")] int ScoutGroupId,
    [property: JsonPropertyName("first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string LastName,
    [property: JsonPropertyName("birth_date")] string BirthDate,
    [property: JsonPropertyName("personal_number")] string? PersonalNumber,
    [property: JsonPropertyName("email")] string? Email,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("mobile")] string? Mobile,
    [property: JsonPropertyName("alt_email")] string? AltEmail,
    [property: JsonPropertyName("mum_name")] string? MumName,
    [property: JsonPropertyName("mum_email")] string? MumEmail,
    [property: JsonPropertyName("mum_mobile")] string? MumMobile,
    [property: JsonPropertyName("dad_name")] string? DadName,
    [property: JsonPropertyName("dad_email")] string? DadEmail,
    [property: JsonPropertyName("dad_mobile")] string? DadMobile,
    [property: JsonPropertyName("street")] string? Street,
    [property: JsonPropertyName("zip_code")] string? ZipCode,
    [property: JsonPropertyName("zip_name")] string? ZipName,
    [property: JsonConverter(typeof(StringOrArrayConverter))] List<string>? GroupRoles,
    [property: JsonConverter(typeof(IntOrArrayConverter))] List<int>? MemberYears,
    [property: JsonPropertyName("not_in_scoutnet")] bool? NotInScoutnet,
    [property: JsonPropertyName("removed")] bool? Removed
);

public record TroopImport(
    [property: JsonPropertyName("scoutnet_id")] int? ScoutnetId,
    [property: JsonPropertyName("scout_group_id")] int? ScoutGroupId,
    [property: JsonPropertyName("semester_id")] int? SemesterId,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("default_start_time")] string? DefaultStartTime,
    [property: JsonPropertyName("default_duration_minutes")] int? DefaultDurationMinutes
);

public record TroopPersonImport(
    [property: JsonPropertyName("troop_id")] string TroopId,
    [property: JsonPropertyName("scoutnet_id")] int ScoutnetTroopId,
    [property: JsonPropertyName("scout_group_id")] int ScoutGroupId,
    [property: JsonPropertyName("semester_id")] int SemesterId,
    [property: JsonPropertyName("person_id")] int PersonId,
    [property: JsonPropertyName("is_leader")] bool? IsLeader);


public record MeetingImport(
    int ScoutnetTroopId,
    int GroupId,
    int SemesterId,
    string MeetingDate,
    string StartTime,
    string Name,
    int DurationMinutes,
    bool? IsHike
);

public record MeetingAttendanceImport(long TroopScoutnetId, long GroupId, int SemesterId, long PersonId, string MeetingDate);

public record UserImport(
    string Id,
    string Email,
    string? Name,
    int? ScoutGroupId,
    int? ActiveSemesterId,
    bool? HasAccess,
    bool? IsAdmin
);

public record BadgeTemplateImport(
    int Id,
    string Name,
    string? Description,
    List<string>? PartsScoutShort,
    List<string>? PartsScoutLong,
    List<string>? PartsAdminShort,
    List<string>? PartsAdminLong,
    string? ImageUrl
);

public record BadgeImport(
    int Id,
    int ScoutGroupId,
    string Name,
    string Description,
    List<string>? PartsScoutShort,
    List<string>? PartsScoutLong,
    List<string>? PartsAdminShort,
    List<string>? PartsAdminLong,
    string? ImageUrl
);

public record TroopBadgeImport(
    int ScoutnetTroopId, 
    int ScoutGroupId, 
    int SemesterId, 
    int BadgeId, 
    int? SortOrder
    );

public record BadgePartDoneImport(
    int PersonId,
    int BadgeId,
    int PartIndex,
    bool? IsScoutPart,
    string? ExaminerName,
    string? CompletedDate
);

public record BadgeCompletedImport(
    int PersonId,
    int BadgeId,
    string? Examiner,
    string? CompletedDate
);

#endregion
