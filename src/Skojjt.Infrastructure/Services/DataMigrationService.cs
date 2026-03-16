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
    }

	/// <summary>
	/// Import all data from the specified directory.
	/// </summary>
	public async Task ImportAllAsync(string importDirectory, CancellationToken cancellationToken = default)
	{
		_logger.LogInformation("Starting data import from {Directory}", importDirectory);

		var stats = new Dictionary<string, int>();
		var totalSw = System.Diagnostics.Stopwatch.StartNew();

		// Import in dependency order
		stats["semesters"] = await ImportStepAsync("semesters", () => ImportSemestersAsync(Path.Combine(importDirectory, "semesters.json"), cancellationToken));
		stats["scout_groups"] = await ImportStepAsync("scout_groups", () => ImportScoutGroupsAsync(Path.Combine(importDirectory, "scout_groups.json"), cancellationToken));
		stats["persons"] = await ImportStepAsync("persons", () => ImportPersonsAsync(Path.Combine(importDirectory, "persons.json"), cancellationToken));
		stats["troops"] = await ImportStepAsync("troops", () => ImportTroopsAsync(Path.Combine(importDirectory, "troops.json"), cancellationToken));
		stats["troop_persons"] = await ImportStepAsync("troop_persons", () => ImportTroopPersonsAsync(Path.Combine(importDirectory, "troop_persons.json"), cancellationToken));
		stats["meetings"] = await ImportStepAsync("meetings", () => ImportMeetingsAsync(Path.Combine(importDirectory, "meetings.json"), cancellationToken));
		stats["meeting_attendances"] = await ImportStepAsync("meeting_attendances", () => ImportMeetingAttendancesAsync(Path.Combine(importDirectory, "meeting_attendances.json"), cancellationToken));
		stats["users"] = await ImportStepAsync("users", () => ImportUsersAsync(Path.Combine(importDirectory, "users.json"), cancellationToken));
		stats["badge_templates"] = await ImportStepAsync("badge_templates", () => ImportBadgeTemplatesAsync(Path.Combine(importDirectory, "badge_templates.json"), cancellationToken));
		stats["badges"] = await ImportStepAsync("badges", () => ImportBadgesAsync(Path.Combine(importDirectory, "badges.json"), cancellationToken));
		stats["troop_badges"] = await ImportStepAsync("troop_badges", () => ImportTroopBadgesAsync(Path.Combine(importDirectory, "troop_badges.json"), cancellationToken));
		stats["badge_parts_done"] = await ImportStepAsync("badge_parts_done", () => ImportBadgePartsDoneAsync(Path.Combine(importDirectory, "badge_parts_done.json"), cancellationToken));
		stats["badges_completed"] = await ImportStepAsync("badges_completed", () => ImportBadgesCompletedAsync(Path.Combine(importDirectory, "badges_completed.json"), cancellationToken));

		totalSw.Stop();
		_logger.LogInformation("Data import complete in {Elapsed}!", totalSw.Elapsed);
		foreach (var (table, count) in stats)
		{
			_logger.LogInformation("  {Table}: {Count} records", table, count);
		}
	}

	private async Task<int> ImportStepAsync(string stepName, Func<Task<int>> importFunc)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();
		_logger.LogInformation("Starting import step: {Step}...", stepName);
		var count = await importFunc();
		sw.Stop();
		_logger.LogInformation("Completed {Step}: {Count} records in {Elapsed}", stepName, count, sw.Elapsed);
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
        var count = 0;

        foreach (var item in items)
        {
            if (await _context.Semesters.AnyAsync(s => s.Id == item.Id, cancellationToken))
                continue;

            await _context.Semesters.AddAsync(new Semester
            {
                Id = item.Id,
                Year = item.Year,
                IsAutumn = item.IsAutumn
            });
            count++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} semesters", count);
        return count;
    }

    private async Task<int> ImportScoutGroupsAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<ScoutGroupImport>(filePath, cancellationToken);
        var count = 0;

        foreach (var item in items)
        {
            if (item.Id <= 0)
                continue;

            if (await _context.ScoutGroups.AnyAsync(s => s.Id == item.Id, cancellationToken))
                continue;

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

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} scout groups", count);
        return count;
    }

    private async Task<int> ImportPersonsAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<PersonImport>(filePath, cancellationToken);
        var count = 0;
        var validScoutGroups = await _context.ScoutGroups.Select(s => s.Id).ToListAsync(cancellationToken);
		var validPersons = await _context.Persons.Select(s => s.Id).ToListAsync(cancellationToken);

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

			if (item.ScoutGroupId != 0 && !validScoutGroups.Contains(item.ScoutGroupId))
			{
				scoutGroupPersonsToAdd.Add(new ScoutGroupPerson
				{
					PersonId = personId,
					ScoutGroupId = item.ScoutGroupId
				});
			}

			if (validPersons.Contains(personId))
				continue;

            if (await _context.Persons.AnyAsync(p => p.Id == personId, cancellationToken))
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
            _context.ScoutGroupPersons.Add(new ScoutGroupPerson
            {
                PersonId = personId,
                ScoutGroupId = item.ScoutGroupId,
				GroupRoles = item.GroupRoles != null ? string.Join(", ", item.GroupRoles.Select(r => r.Trim())) : null,
				NotInScoutnet = item.NotInScoutnet ?? false
			});

            count++;

            // Batch save to avoid memory issues
            if (count % 1000 == 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("  Imported {Count} persons so far...", count);
            }
        }
		await _context.SaveChangesAsync(cancellationToken);
		_logger.LogInformation("Imported {Count} persons", count);

		_logger.LogInformation("Importing scout group persons");
		count = 0;
		foreach (var scoutGroupPerson in scoutGroupPersonsToAdd)
		{
			if (await _context.ScoutGroupPersons.AnyAsync(
				sgp => sgp.PersonId == scoutGroupPerson.PersonId && sgp.ScoutGroupId == scoutGroupPerson.ScoutGroupId, cancellationToken))
			{
				continue;
			}
			_context.ScoutGroupPersons.Add(scoutGroupPerson);
			count++;
			if (count % 1000 == 0)
			{
				await _context.SaveChangesAsync(cancellationToken);
				_logger.LogInformation("  Imported {Count} scout group persons so far...", count);
			}
		}
		await _context.SaveChangesAsync(cancellationToken);
		_logger.LogInformation("Imported {Count} scout group persons", count);

		return count;
    }

    private async Task<int> ImportTroopsAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<TroopImport>(filePath, cancellationToken);
        var count = 0;
        var validScoutGroups = await _context.ScoutGroups.Select(s => s.Id).ToListAsync(cancellationToken);
        var validSemesters = await _context.Semesters.Select(s => s.Id).ToListAsync(cancellationToken);

		foreach (var item in items)
        {
			if (await _context.Troops.AnyAsync(p => p.ScoutnetId == item.ScoutnetId && p.ScoutGroupId == item.ScoutGroupId && p.SemesterId == item.SemesterId, cancellationToken))
					continue;

			// Validate foreign keys
			if (item.ScoutnetId == null)
			{
				_logger.LogWarning($"Skipping troop - no scoutnet_id. Scout group: {item.ScoutGroupId}, import record {item}");
				continue;
			}

			if (item.ScoutGroupId == null || !validScoutGroups.Contains(item.ScoutGroupId.Value))
            {
                _logger.LogWarning($"Skipping troop {item.ScoutnetId} - invalid scout_group_id: {item.ScoutGroupId}");
                continue;
            }

            if (item.SemesterId == null || !validSemesters.Contains(item.SemesterId.Value))
            {
                _logger.LogWarning($"Skipping troop {item.ScoutnetId} - invalid semester_id: {item.SemesterId}");
                continue;
            }

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
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("  Imported {Count} troops so far...", count);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} troops", count);
        return count;
    }

    private async Task<int> ImportTroopPersonsAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<TroopPersonImport>(filePath, cancellationToken);
        var count = 0;
        var validTroops = await _context.Troops.Select(t => t.Id).ToListAsync(cancellationToken);
        var validPersons = await _context.Persons.Select(p => p.Id).ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            // Skip invalid person IDs
            if (item.PersonId <= 0 || item.PersonId > int.MaxValue)
                continue;

            var personId = (int)item.PersonId;

			if (!validPersons.Contains(personId))
			{
				_logger.LogWarning("Skipping troop person - person not found {item}", item);
				continue;
			}

			var troop = await _context.Troops.FirstOrDefaultAsync(tp => tp.ScoutnetId == item.ScoutnetTroopId && tp.SemesterId == item.SemesterId, cancellationToken);
			if (troop == null)
			{
				_logger.LogWarning($"Skipping troop person - no troop found. Import record {item}");
				continue;
			}

            if (await _context.Set<TroopPerson>().AnyAsync(
                tp => tp.PersonId == personId && tp.TroopId == troop.Id, cancellationToken))
                continue;

            _context.Set<TroopPerson>().Add(new TroopPerson
            {
                TroopId = troop.Id,
                PersonId = personId,
                IsLeader = item.IsLeader ?? false
            });
            count++;

            if (count % 1000 == 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("  Imported {Count} troop persons so far...", count);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} troop persons", count);
        return count;
    }

    private async Task<int> ImportMeetingsAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<MeetingImport>(filePath, cancellationToken);
        var count = 0;
        var skipped = 0;
		var validTroops = await _context.Troops.Select(t => t.Id).ToListAsync(cancellationToken);
		var meetingSet = new HashSet<string>();

		foreach (var item in items)
        {
			var troop = await _context.Troops.Where(t => t.ScoutnetId == item.ScoutnetTroopId && t.SemesterId == item.SemesterId).FirstOrDefaultAsync(cancellationToken);
			if (troop == null)
			{
				_logger.LogWarning($"Skipping meeting - no troop found. Import record {item}");
				skipped++;
				continue;
			}

			if (item.MeetingDate == null)
			{
				_logger.LogWarning($"Skipping meeting - no meeting_date. Import record {item}");
				skipped++;
				continue;
			}

			DateOnly meetingDate = (DateOnly)ParseDate(item.MeetingDate)!;
			TimeOnly startTime = (TimeOnly)ParseTime(item.StartTime)!;

			var meetingId = $"{troop.Id}+{item.MeetingDate}";
			if (meetingSet.Contains(meetingId))
			{
				_logger.LogWarning("Duplicate meeting {item}", item);
				skipped++;
				continue;
			}
			meetingSet.Add(meetingId);

			if (await _context.Meetings.AnyAsync(
				m => m.TroopId == troop.Id && m.MeetingDate == meetingDate, cancellationToken))
			{
				skipped++;
				continue;
			}

			var meetingName = item.Name;
			if (meetingName.Length > 50)
			{
				meetingName = meetingName.Substring(0, 50);
			}

			_context.Meetings.Add(new Meeting
            {
                TroopId = troop.Id,
                MeetingDate = meetingDate,
                StartTime = startTime,
                Name = meetingName,
                DurationMinutes = item.DurationMinutes,
                IsHike = item.IsHike ?? false
            });
            count++;

            if (count % 1000 == 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("  Imported {Count} meetings so far...", count);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} meetings (skipped {Skipped} invalid)", count, skipped);
        return count;
    }

    private async Task<int> ImportMeetingAttendancesAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<MeetingAttendanceImport>(filePath, cancellationToken);
        var count = 0;
        var skipped = 0;
        var validMeetings = await _context.Meetings.Select(m => m.Id).ToListAsync(cancellationToken);
        var validPersons = await _context.Persons.Select(p => p.Id).ToListAsync(cancellationToken);

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
			var troop = await _context.Troops.Where(t => t.ScoutnetId == item.TroopScoutnetId && t.SemesterId == item.SemesterId).FirstOrDefaultAsync(cancellationToken);
			if (troop == null)
			{
				skipped++;
				continue;
			}
			var meetingDate = DateOnly.Parse(item.MeetingDate);
			var meeting = await _context.Meetings.Where(m => m.TroopId == troop.Id && m.MeetingDate == meetingDate).FirstOrDefaultAsync(cancellationToken);
            if (meeting == null)
            {
                skipped++;
                continue;
			}
			_context.Set<MeetingAttendance>().Add(new MeetingAttendance
            {
                MeetingId = meeting.Id,
                PersonId = personId
            });
            count++;

            if (count % 5000 == 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("  Imported {Count} meeting attendances so far...", count);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} meeting attendances", count);
        return count;
    }

    private async Task<int> ImportUsersAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<UserImport>(filePath, cancellationToken);
        var count = 0;
        //var validScoutGroups = await _context.ScoutGroups.Select(s => s.Id).ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item.Id) || string.IsNullOrEmpty(item.Email))
                continue;

            if (await _context.Users.AnyAsync(u => u.Email == item.Email, cancellationToken))
                continue;

            _context.Users.Add(new User
            {
                Id = item.Id,
                Email = item.Email,
                Name = item.Name,
                //ScoutGroupId = item.ScoutGroupId.HasValue && validScoutGroups.Contains(item.ScoutGroupId.Value) ? item.ScoutGroupId : null,
                //ActiveSemesterId = null, // Will be set by user
                //HasAccess = item.HasAccess ?? false,
                //IsAdmin = item.IsAdmin ?? false
            });
            count++;
        }

        await _context.SaveChangesAsync(cancellationToken);
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

        foreach (var item in items)
        {
            if (await _context.BadgeTemplates.AnyAsync(bt => bt.Name == item.Name, cancellationToken))
                continue;

            _context.BadgeTemplates.Add(new BadgeTemplate
            {
                Name = item.Name ?? "",
                Description = item.Description,
                PartsScoutShort = item.PartsScoutShort?.ToArray() ?? Array.Empty<string>(),
                PartsScoutLong = item.PartsScoutLong?.ToArray() ?? Array.Empty<string>(),
                PartsAdminShort = item.PartsAdminShort?.ToArray() ?? Array.Empty<string>(),
                PartsAdminLong = item.PartsAdminLong?.ToArray() ?? Array.Empty<string>(),
                ImageUrl = item.ImageUrl
            });
            count++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} badge templates", count);
        return count;
    }

    private async Task<int> ImportBadgesAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<BadgeImport>(filePath, cancellationToken);
        var count = 0;
        var validScoutGroups = await _context.ScoutGroups.Select(s => s.Id).ToListAsync(cancellationToken);

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

            if (await _context.Badges.AnyAsync(b => b.Id == item.Id, cancellationToken))
                continue;

            // Use raw SQL to insert with explicit ID
            await _context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO badges (id, scout_group_id, name, description, parts_scout_short, parts_scout_long, parts_admin_short, parts_admin_long, image_url)
                  VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8})
                  ON CONFLICT (id) DO NOTHING",
                item.Id,
                item.ScoutGroupId,
                item.Name,
                item.Description,
                item.PartsScoutShort?.ToArray() ?? Array.Empty<string>(),
                item.PartsScoutLong?.ToArray() ?? Array.Empty<string>(),
                item.PartsAdminShort?.ToArray() ?? Array.Empty<string>(),
                item.PartsAdminLong?.ToArray() ?? Array.Empty<string>(),
                (object?)item.ImageUrl ?? DBNull.Value);
            count++;
        }

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
		var validTroops = await _context.Troops.Select(t => t.Id).ToListAsync(cancellationToken);
        var validBadges = await _context.Badges.Select(b => b.Id).ToListAsync(cancellationToken);

        foreach (var item in items)
        {
			//if (string.IsNullOrEmpty(item.TroopId) || !validTroops.Contains(item.TroopId) || !validBadges.Contains(item.BadgeId))
			//    continue;

			//if (await _context.Set<TroopBadge>().AnyAsync(
			//    tb => tb.TroopId == item.TroopId && tb.BadgeId == item.BadgeId, cancellationToken))
			//    continue;

			var troop = await _context.Troops.Where(t => t.ScoutnetId == item.ScoutnetTroopId && t.SemesterId == item.SemesterId).FirstOrDefaultAsync(cancellationToken);
			if (troop == null)
			{
				skipped++;
				continue;
			}

			_context.Set<TroopBadge>().Add(new TroopBadge
            {
                Troop = troop,
                BadgeId = item.BadgeId,
                SortOrder = item.SortOrder
            });
            count++;
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} troop badges", count);
        return count;
    }

    private async Task<int> ImportBadgePartsDoneAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<BadgePartDoneImport>(filePath, cancellationToken);
        var count = 0;
        var validPersons = await _context.Persons.Select(p => p.Id).ToListAsync(cancellationToken);
        var validBadges = await _context.Badges.Select(b => b.Id).ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            // Skip invalid person IDs
            if (item.PersonId <= 0 || item.PersonId > int.MaxValue)
                continue;

            var personId = (int)item.PersonId;

            if (!validPersons.Contains(personId) || !validBadges.Contains(item.BadgeId))
                continue;

            if (await _context.Set<BadgePartDone>().AnyAsync(
                bp => bp.PersonId == personId && bp.BadgeId == item.BadgeId && 
                      bp.PartIndex == item.PartIndex && bp.IsScoutPart == item.IsScoutPart, cancellationToken))
                continue;

            _context.Set<BadgePartDone>().Add(new BadgePartDone
            {
                PersonId = personId,
                BadgeId = item.BadgeId,
                PartIndex = item.PartIndex,
                IsScoutPart = item.IsScoutPart ?? true,
                ExaminerName = item.ExaminerName,
                CompletedDate = ParseDate(item.CompletedDate) ?? DateOnly.FromDateTime(DateTime.Now)
            });
            count++;

            if (count % 1000 == 0)
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Imported {Count} badge parts done", count);
        return count;
    }

    private async Task<int> ImportBadgesCompletedAsync(string filePath, CancellationToken cancellationToken)
    {
        var items = await LoadJsonFileAsync<BadgeCompletedImport>(filePath, cancellationToken);
        var count = 0;
        var validPersons = await _context.Persons.Select(p => p.Id).ToListAsync(cancellationToken);
        var validBadges = await _context.Badges.Select(b => b.Id).ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            // Skip invalid person IDs
            if (item.PersonId <= 0 || item.PersonId > int.MaxValue)
                continue;

            var personId = (int)item.PersonId;

            if (!validPersons.Contains(personId) || !validBadges.Contains(item.BadgeId))
                continue;

            if (await _context.Set<BadgeCompleted>().AnyAsync(
                bc => bc.PersonId == personId && bc.BadgeId == item.BadgeId, cancellationToken))
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

        await _context.SaveChangesAsync(cancellationToken);
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
}

#region Import DTOs

public record SemesterImport(
    [property: JsonPropertyName("id")] int Id, 
    [property: JsonPropertyName("year")] int Year, 
    [property: JsonPropertyName("is_autumn")] bool IsAutumn);

public record ScoutGroupImport(
    int Id,
    string? Name,
    string? OrganisationNumber,
    string? AssociationId,
    string? MunicipalityId,
    string? ApiKeyWaitinglist,
    string? ApiKeyAllMembers,
    string? BankAccount,
    string? Address,
    string? PostalAddress,
    string? Email,
    string? Phone,
    string? DefaultLocation,
    string? Signatory,
    string? SignatoryPhone,
    string? SignatoryEmail,
    int? AttendanceMinYear,
    bool? AttendanceInclHike
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
