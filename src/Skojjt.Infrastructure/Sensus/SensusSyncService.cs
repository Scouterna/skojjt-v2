using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Authentication;
using Skojjt.Core.Interfaces;
using Skojjt.Core.Services;

namespace Skojjt.Infrastructure.Sensus;

/// <summary>
/// Syncs attendance data from Skojjt to Sensus e-tjänst via server-side HTTP calls.
/// Each call creates a fresh authenticated session using the provided credentials.
/// Uses IHttpClientFactory to avoid socket exhaustion.
/// </summary>
public class SensusSyncService : ISensusSyncService
{
    /// <summary>
    /// Named HttpClient identifier used for DI registration.
    /// </summary>
    public const string HttpClientName = "Sensus";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMeetingRepository _meetingRepository;
    private readonly ITroopRepository _troopRepository;
    private readonly ILogger<SensusSyncService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public SensusSyncService(
        IHttpClientFactory httpClientFactory,
        ICurrentUserService currentUserService,
        IMeetingRepository meetingRepository,
        ITroopRepository troopRepository,
        ILogger<SensusSyncService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _currentUserService = currentUserService;
        _meetingRepository = meetingRepository;
        _troopRepository = troopRepository;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SensusArrangemangDto>> GetArrangemangAsync(
        SensusCredentials credentials,
        CancellationToken ct = default)
    {
        var (client, sessionCookies) = await CreateAuthenticatedSessionAsync(credentials, ct);

        _logger.LogInformation("Fetching Sensus arrangemang list");
        var arrangemang = await SensusGetAsync<SensusPagedResponse<SensusArrangemang>>(
            client,
            "/api/arrangemangs?size=100&page=1&view=4&verksar=0&listtype=0&arrtypfilters=0&narvarofilter=1&sorttype=9&getProgress=true",
            sessionCookies,
            ct);

        var items = arrangemang?.Result ?? [];
        _logger.LogInformation("Fetched {Count} arrangemang from Sensus", items.Count);
        foreach (var a in items)
        {
            _logger.LogDebug("  Arrangemang {Id}: {Name} ({SchemaCount} schemas)",
                a.Id, a.Namn ?? a.Name, a.AntalSchema);
        }

        return items.Select(a => new SensusArrangemangDto(
            a.Id,
            a.Namn ?? a.Name ?? string.Empty,
            a.AntalSchema)).ToList();
    }

    public async Task<SensusSyncResult> SyncAttendanceAsync(
        SensusCredentials credentials,
        int troopId,
        int arrangemangId,
        CancellationToken ct = default)
    {
        var log = new List<string>();

        // Verify the current user has access to this troop
        var troop = await _troopRepository.GetWithMembersAsync(troopId, ct)
            ?? throw new InvalidOperationException($"Avdelning {troopId} hittades inte.");

        if (!_currentUserService.HasTroopAccess(troop.ScoutGroupId, troop.ScoutnetId))
        {
            throw new UnauthorizedAccessException(
                $"Du har inte behörighet till avdelning {troop.Name}.");
        }

        var (client, sessionCookies) = await CreateAuthenticatedSessionAsync(credentials, ct);

        // 1. Load Skojjt data
        _logger.LogInformation("Starting Sensus sync for troop {TroopId} ({TroopName}) → arrangemang {ArrangemangId}",
            troopId, troop.Name, arrangemangId);

        var meetings = await _meetingRepository.GetByTroopWithAttendanceAsync(troopId, ct);
        _logger.LogInformation("Loaded {MemberCount} members and {MeetingCount} meetings from Skojjt",
            troop.TroopPersons.Count, meetings.Count);
        log.Add($"Skojjt: {troop.TroopPersons.Count} medlemmar, {meetings.Count} sammankomster");

        // 2. Fetch Sensus data
        var deltagareResponse = await SensusGetAsync<SensusPagedResponse<SensusDeltagare>>(
            client,
            $"/api/arrangemangs/{arrangemangId}/arrdeltagares/&roll=0&dolda=false",
            sessionCookies,
            ct);
        var sensusDeltagare = deltagareResponse?.Result ?? [];

        var schemasResponse = await SensusGetAsync<JsonElement>(
            client,
            $"/api/arrangemangs/{arrangemangId}/schema",
            sessionCookies,
            ct);
        var sensusSchemas = ExtractArray<SensusSchema>(schemasResponse);

        _logger.LogInformation("Fetched {DeltagareCount} deltagare and {SchemaCount} schemas from Sensus",
            sensusDeltagare.Count, sensusSchemas.Count);
        log.Add($"Sensus: {sensusDeltagare.Count} deltagare, {sensusSchemas.Count} sammankomster");

        if (sensusDeltagare.Count == 0)
        {
            log.Add("Inga deltagare i Sensus-arrangemanget.");
            return new SensusSyncResult(0, 0, 0, 0, 0, 0, log);
        }

        // 3. Build Sensus name → person ID mapping
        var sensusPersonMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in sensusDeltagare)
        {
            var name = d.Person != null
                ? $"{d.Person.Fornamn} {d.Person.Efternamn}"
                : d.Namn ?? string.Empty;
            var pid = d.Person?.Id ?? d.Id;
            var normalized = NormalizeName(name);
            sensusPersonMap.TryAdd(normalized, pid);
        }

        // 4. Match Skojjt members to Sensus persons
        var skojjtToSensus = new Dictionary<int, int>();
        var usedSensusNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var tp in troop.TroopPersons)
        {
            var person = tp.Person;
            if (person == null) continue;

            var fullName = NormalizeName($"{person.FirstName} {person.LastName}");
            int? sensusId = null;
            string? matchedName = null;

            // Direct name match
            if (sensusPersonMap.TryGetValue(fullName, out var sid1))
            {
                sensusId = sid1;
                matchedName = fullName;
            }
            // Reversed name
            else
            {
                var reversed = NormalizeName($"{person.LastName} {person.FirstName}");
                if (sensusPersonMap.TryGetValue(reversed, out var sid2))
                {
                    sensusId = sid2;
                    matchedName = reversed;
                }
                else
                {
                    // Partial match
                    foreach (var (sName, sId) in sensusPersonMap)
                    {
                        if (usedSensusNames.Contains(sName)) continue;
                        if (sName.Contains(fullName, StringComparison.OrdinalIgnoreCase) ||
                            fullName.Contains(sName, StringComparison.OrdinalIgnoreCase))
                        {
                            sensusId = sId;
                            matchedName = sName;
                            break;
                        }
                    }
                }
            }

            if (sensusId.HasValue && matchedName != null)
            {
                skojjtToSensus[person.Id] = sensusId.Value;
                usedSensusNames.Add(matchedName);
            }
        }

        _logger.LogInformation("Matched {Matched}/{Total} Skojjt members to Sensus deltagare",
            skojjtToSensus.Count, troop.TroopPersons.Count);
        log.Add($"Matchade {skojjtToSensus.Count} av {troop.TroopPersons.Count} personer");

        // 5. Build Sensus schema date index
        var schemaByDate = new Dictionary<DateOnly, SensusSchema>();
        foreach (var schema in sensusSchemas)
        {
            var date = ParseDate(schema.Datum);
            if (date.HasValue)
            {
                schemaByDate.TryAdd(date.Value, schema);
            }
        }

        // 6. Sync attendance per date
        var syncedCount = 0;
        var skippedCount = 0;
        var noMatchCount = 0;
        var errorCount = 0;

        foreach (var meeting in meetings)
        {
            if (!schemaByDate.TryGetValue(meeting.MeetingDate, out var schema))
            {
                log.Add($"{meeting.MeetingDate:yyyy-MM-dd}: inget matchande Sensus-schema");
                noMatchCount++;
                continue;
            }

            if (schema.Signerad)
            {
                log.Add($"{meeting.MeetingDate:yyyy-MM-dd}: redan signerad, hoppar över");
                skippedCount++;
                continue;
            }

            if (schema.Redigerbar == false)
            {
                log.Add($"{meeting.MeetingDate:yyyy-MM-dd}: inte redigerbar, hoppar över");
                skippedCount++;
                continue;
            }

            // Build narvaros list
            var narvaros = new List<int>();
            var unmatchedAttendees = 0;
            foreach (var attendance in meeting.Attendances)
            {
                if (skojjtToSensus.TryGetValue(attendance.PersonId, out var sensusPersonId))
                {
                    narvaros.Add(sensusPersonId);
                }
                else
                {
                    unmatchedAttendees++;
                }
            }

            try
            {
                await SensusPutSchemaAsync(client, arrangemangId, schema.Id, schema, narvaros, sessionCookies, ct);
                var suffix = unmatchedAttendees > 0 ? $" ({unmatchedAttendees} omatchade)" : string.Empty;
                log.Add($"✓ {meeting.MeetingDate:yyyy-MM-dd}: {narvaros.Count} närvarande{suffix}");
                syncedCount++;
            }
            catch (HttpRequestException ex)
            {
                log.Add($"✗ {meeting.MeetingDate:yyyy-MM-dd}: {ex.Message}");
                errorCount++;
            }
        }

        var parts = new List<string>();
        if (syncedCount > 0) parts.Add($"{syncedCount} synkade");
        if (skippedCount > 0) parts.Add($"{skippedCount} hoppade över");
        if (noMatchCount > 0) parts.Add($"{noMatchCount} utan matchande datum");
        if (errorCount > 0) parts.Add($"{errorCount} fel");
        log.Add($"Synk klar! {string.Join(", ", parts)}");

        return new SensusSyncResult(
            syncedCount, skippedCount, noMatchCount, errorCount,
            skojjtToSensus.Count, troop.TroopPersons.Count, log);
    }

    // =========================================================================
    // Sensus HTTP helpers
    // =========================================================================

    /// <summary>
    /// Creates an authenticated session by logging in to Sensus and capturing session cookies.
    /// Returns the HttpClient (from IHttpClientFactory) and the session cookie string.
    /// </summary>
    private async Task<(HttpClient Client, string SessionCookies)> CreateAuthenticatedSessionAsync(
        SensusCredentials credentials, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);

        _logger.LogInformation("Sensus login attempt for user {Username} (length={Length})",
            credentials.Username, credentials.Username.Length);

        // Login: POST JSON to /api/account/login
        var loginPayload = new { username = credentials.Username, password = credentials.Password };
        var loginResponse = await client.PostAsJsonAsync("/api/account/login", loginPayload, ct);

        _logger.LogDebug("Sensus login response: HTTP {StatusCode}", (int)loginResponse.StatusCode);

        if (!loginResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning("Sensus login failed with HTTP {StatusCode} for user {Username}",
                (int)loginResponse.StatusCode, credentials.Username);
            throw new InvalidOperationException(
                $"Sensus-inloggning misslyckades (HTTP {(int)loginResponse.StatusCode}).");
        }

        var rawBody = await loginResponse.Content.ReadAsStringAsync(ct);
        _logger.LogDebug("Sensus login response body (truncated): {Body}",
            MaskSensitiveFields(rawBody.Length > 500 ? rawBody[..500] + "..." : rawBody));

        var loginResult = JsonSerializer.Deserialize<SensusLoginResponse>(rawBody, JsonOptions);
        if (loginResult?.Errormessage != null)
        {
            // Strip "[support]" marker that the Sensus SPA uses to toggle a support link
            var cleanError = loginResult.Errormessage.Replace("[support]", string.Empty).Trim();
            _logger.LogWarning("Sensus login rejected for user {Username}: {Error}",
                credentials.Username, cleanError);
            throw new InvalidOperationException(
                $"Sensus-inloggning misslyckades: {cleanError}");
        }

        // Extract session cookies from the login response
        var sessionCookies = ExtractSessionCookies(loginResponse);
        _logger.LogDebug("Sensus session cookies: {Cookies}",
            string.IsNullOrEmpty(sessionCookies) ? "(none)" : sessionCookies);

        _logger.LogInformation("Sensus login successful for user {Username}", credentials.Username);
        return (client, sessionCookies);
    }

    private static string ExtractSessionCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
        {
            return string.Empty;
        }

        // Extract "name=value" from each Set-Cookie header (before the first ';')
        var cookies = setCookieHeaders
            .Select(h => h.Split(';', 2)[0].Trim())
            .Where(c => !string.IsNullOrEmpty(c));
        return string.Join("; ", cookies);
    }

    private async Task<T?> SensusGetAsync<T>(
        HttpClient client, string path, string sessionCookies, CancellationToken ct)
    {
        _logger.LogDebug("Sensus GET {Path}", path);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("application/json");
        if (!string.IsNullOrEmpty(sessionCookies))
        {
            request.Headers.Add("Cookie", sessionCookies);
        }

        var response = await client.SendAsync(request, ct);
        _logger.LogDebug("Sensus GET {Path} → HTTP {StatusCode}", path, (int)response.StatusCode);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new InvalidOperationException("Sensus-sessionen har gått ut.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
    }

    private async Task SensusPutSchemaAsync(
        HttpClient client,
        int arrangemangId,
        int schemaId,
        SensusSchema originalSchema,
        List<int> narvaros,
        string sessionCookies,
        CancellationToken ct)
    {
        // Sensus expects FormData with a "data" field containing JSON
        var updatedSchema = originalSchema with { Narvaros = narvaros, SigneratAntalStudieTimmar = 1 };
        var json = JsonSerializer.Serialize(updatedSchema, JsonOptions);
        _logger.LogDebug("Sensus PUT schema {SchemaId} payload: {Json}", schemaId, json);

        using var formContent = new MultipartFormDataContent
        {
            { new StringContent(json), "data" }
        };

        using var request = new HttpRequestMessage(HttpMethod.Put,
            $"/api/arrangemangs/{arrangemangId}/schemas/{schemaId}")
        {
            Content = formContent,
        };
        if (!string.IsNullOrEmpty(sessionCookies))
        {
            request.Headers.Add("Cookie", sessionCookies);
        }

        var response = await client.SendAsync(request, ct);
        _logger.LogDebug("Sensus PUT schema {SchemaId} → HTTP {StatusCode}", schemaId, (int)response.StatusCode);
        response.EnsureSuccessStatusCode();
    }

    // =========================================================================
    // Name matching utilities
    // =========================================================================

    private static string NormalizeName(string name) =>
        name.Trim().ToLowerInvariant().Replace("  ", " ");

    private static readonly System.Text.RegularExpressions.Regex PasswordRegex =
        new(@"""password""\s*:\s*""[^""]*""", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Compiled);

    private static string MaskSensitiveFields(string json) =>
        PasswordRegex.Replace(json, @"""password"":""***""");

    private static DateOnly? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr)) return null;
        if (DateOnly.TryParse(dateStr, out var d)) return d;
        if (DateTime.TryParse(dateStr, out var dt)) return DateOnly.FromDateTime(dt);
        return null;
    }

    private static List<T> ExtractArray<T>(JsonElement? element)
    {
        if (element == null) return [];
        var el = element.Value;

        // Plain array
        if (el.ValueKind == JsonValueKind.Array)
        {
            return el.Deserialize<List<T>>(JsonOptions) ?? [];
        }

        // { result: [...] } wrapper
        if (el.ValueKind == JsonValueKind.Object)
        {
            if (el.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
            {
                return result.Deserialize<List<T>>(JsonOptions) ?? [];
            }
            if (el.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                return items.Deserialize<List<T>>(JsonOptions) ?? [];
            }
        }

        return [];
    }

    // =========================================================================
    // Sensus API DTOs (internal)
    // =========================================================================

    private record SensusLoginResponse(string? User, string? Errormessage);

    private record SensusPagedResponse<T>(List<T> Result, int TotalCount, int TotalPages);

    private record SensusArrangemang(
        int Id,
        string? Namn,
        string? Name,
        int AntalSchema);

    private record SensusDeltagarePerson(int Id, string? Fornamn, string? Efternamn);

    private record SensusDeltagare(int Id, string? Namn, SensusDeltagarePerson? Person);

    private record SensusSchema
    {
        public int Id { get; init; }
        public string? Datum { get; init; }
        public bool Signerad { get; init; }
        public bool? Redigerbar { get; init; }
        public List<int> Narvaros { get; init; } = [];

        [JsonConverter(typeof(LenientNullableIntConverter))]
        public int? SigneratAntalStudieTimmar { get; init; }
    }

    /// <summary>
    /// Reads a JSON number (including decimals like 1.0) or null into an int?.
    /// Sensus returns studietimmar as a JSON decimal even though values are logically integers.
    /// </summary>
    private sealed class LenientNullableIntConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt32(out var intVal)) return intVal;
                if (reader.TryGetDecimal(out var decVal)) return (int)decVal;
            }
            if (reader.TokenType == JsonTokenType.String)
            {
                var str = reader.GetString();
                if (int.TryParse(str, out var parsed)) return parsed;
            }
            return null;
        }

        public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        {
            if (value.HasValue) writer.WriteNumberValue(value.Value);
            else writer.WriteNullValue();
        }
    }
}
