using System.Net.Http.Json;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Skojjt.Infrastructure.Scoutnet;

/// <summary>
/// HTTP client for the Scoutnet API.
/// </summary>
public class ScoutnetApiClient : IScoutnetApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ScoutnetApiClient> _logger;

    public ScoutnetApiClient(
        HttpClient httpClient, 
        IOptions<ScoutnetOptions> options,
        ILogger<ScoutnetApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        var baseUrl = options.Value.BaseUrl;
        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += '/';
        }
        
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        
        _logger.LogInformation("ScoutnetApiClient initialized with base URL: {BaseUrl}", baseUrl);
    }

    public async Task<ScoutnetMemberListResponse> GetMemberListAsync(
        int groupId, 
        string apiKey, 
        CancellationToken cancellationToken = default)
    {
        var url = $"api/group/memberlist?id={groupId}&key={apiKey}";
        
        _logger.LogInformation("Fetching member list for group {GroupId} from Scoutnet", groupId);

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var result = await response.Content.ReadFromJsonAsync<ScoutnetMemberListResponse>(options, cancellationToken);
            
            if (result == null)
            {
                throw new ScoutnetApiException("Empty response from Scoutnet API");
            }

            _logger.LogInformation("Successfully fetched {Count} members from Scoutnet for group {GroupId}", 
                result.Data.Count, groupId);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching member list for group {GroupId}", groupId);
            throw new ScoutnetApiException($"Failed to fetch member list from Scoutnet: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while fetching member list for group {GroupId}", groupId);
            throw new ScoutnetApiException($"Failed to parse Scoutnet response: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout while fetching member list for group {GroupId}", groupId);
            throw new ScoutnetApiException("Request to Scoutnet API timed out", ex);
        }
    }

    public async Task<WaitinglistRegistrationResult> RegisterMemberAsync(
        int groupId,
        string apiKeyWaitinglist,
        Dictionary<string, string> formData,
        CancellationToken cancellationToken = default)
    {
        var content = new FormUrlEncodedContent(formData);
        var formString = await content.ReadAsStringAsync(cancellationToken);
        var url = $"api/organisation/register/member?id={groupId}&key={HttpUtility.UrlEncode(apiKeyWaitinglist)}&{formString}";

        _logger.LogInformation("Registering new member for group {GroupId} on Scoutnet waiting list", groupId);

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMessage = ParseScoutnetErrorMessage(responseBody)
                    ?? $"Scoutnet returned HTTP {(int)response.StatusCode}";
                _logger.LogError("Failed to register member for group {GroupId}: {Error}", groupId, errorMessage);
                return new WaitinglistRegistrationResult
                {
                    Success = false,
                    ErrorMessage = errorMessage
                };
            }

            var memberNo = ParseMemberNoFromResponse(responseBody);
            _logger.LogInformation("Successfully registered member with member_no {MemberNo} for group {GroupId}",
                memberNo, groupId);

            return new WaitinglistRegistrationResult
            {
                Success = true,
                MemberNo = memberNo
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while registering member for group {GroupId}", groupId);
            throw new ScoutnetApiException($"Failed to register member on Scoutnet: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout while registering member for group {GroupId}", groupId);
            throw new ScoutnetApiException("Request to Scoutnet API timed out", ex);
        }
    }

    /// <summary>
    /// Parses the member_no from the Scoutnet registration response JSON.
    /// The response has top-level objects (e.g., "profile") containing a "member_no" field.
    /// </summary>
    private static int ParseMemberNoFromResponse(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return 0;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Object &&
                    prop.Value.TryGetProperty("member_no", out var memberNoProp))
                {
                    return memberNoProp.ValueKind == JsonValueKind.Number
                        ? memberNoProp.GetInt32()
                        : int.TryParse(memberNoProp.GetString(), out var parsed) ? parsed : 0;
                }
            }
        }
        catch (JsonException)
        {
            // Response may not be valid JSON
        }

        return 0;
    }

    /// <summary>
    /// Parses error messages from a Scoutnet error response.
    /// Error responses contain arrays with objects having a "msg" field.
    /// </summary>
    private static string? ParseScoutnetErrorMessage(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.TryGetProperty("msg", out var msgProp))
                        {
                            return msgProp.GetString();
                        }
                    }
                }
            }
        }
        catch (JsonException)
        {
            return responseBody;
        }

        return null;
    }

    public async Task<MembershipUpdateResult> UpdateMembershipAsync(
        int groupId,
        string apiKey,
        Dictionary<int, MembershipUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (updates.Count == 0)
        {
            throw new ArgumentException("At least one membership update is required.", nameof(updates));
        }

        // Build the JSON body: { "memberNo": { "status": "...", "troop_id": ..., "patrol_id": ... }, ... }
        var payload = new Dictionary<string, Dictionary<string, object>>();
        foreach (var (memberNo, update) in updates)
        {
            var fields = new Dictionary<string, object>();
            if (update.Status is not null)
            {
                fields["status"] = update.Status;
            }
            if (update.TroopId is not null)
            {
                fields["troop_id"] = update.TroopId.Value;
            }
            if (update.PatrolId is not null)
            {
                fields["patrol_id"] = update.PatrolId.Value;
            }

            payload[memberNo.ToString()] = fields;
        }

        var url = $"api/organisation/update/membership?id={groupId}&key={Uri.EscapeDataString(apiKey)}";

        _logger.LogInformation(
            "Updating membership for {Count} member(s) in group {GroupId} via Scoutnet",
            updates.Count, groupId);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("Scoutnet UpdateMembership response ({StatusCode}): {Body}",
                (int)response.StatusCode, responseBody);

            if (response.IsSuccessStatusCode)
            {
                return ParseUpdateMembershipSuccess(responseBody);
            }

            return ParseUpdateMembershipError(responseBody);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while updating membership for group {GroupId}", groupId);
            throw new ScoutnetApiException($"Failed to update membership on Scoutnet: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout while updating membership for group {GroupId}", groupId);
            throw new ScoutnetApiException("Request to Scoutnet API timed out", ex);
        }
    }

    public async Task<ScoutnetProjectParticipantsResponse> GetProjectParticipantsAsync(
        int projectId,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var url = $"api/project/get/participants?id={projectId}&key={Uri.EscapeDataString(apiKey)}";

        _logger.LogInformation("Fetching participants for project {ProjectId} from Scoutnet", projectId);

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = await response.Content.ReadFromJsonAsync<ScoutnetProjectParticipantsResponse>(options, cancellationToken);

            if (result == null)
            {
                throw new ScoutnetApiException("Empty response from Scoutnet project API");
            }

            _logger.LogInformation("Successfully fetched {Count} participants for project {ProjectId}",
                result.Participants.Count, projectId);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching participants for project {ProjectId}", projectId);
            throw new ScoutnetApiException($"Failed to fetch project participants from Scoutnet: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for project {ProjectId}", projectId);
            throw new ScoutnetApiException($"Failed to parse Scoutnet project response: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout while fetching participants for project {ProjectId}", projectId);
            throw new ScoutnetApiException("Request to Scoutnet API timed out", ex);
        }
    }

    public async Task<ProjectCheckinResult> CheckinParticipantsAsync(
        int projectId,
        string apiKey,
        Dictionary<int, bool> memberCheckins,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        if (memberCheckins.Count == 0)
        {
            return new ProjectCheckinResult { Success = true };
        }

        // Build payload: { "memberNo": { "checked_in": 1 }, ... }
        var payload = new Dictionary<string, Dictionary<string, int>>();
        foreach (var (memberNo, checkedIn) in memberCheckins)
        {
            payload[memberNo.ToString()] = new Dictionary<string, int>
            {
                ["checked_in"] = checkedIn ? 1 : 0
            };
        }

        var url = $"api/project/checkin?id={projectId}&key={Uri.EscapeDataString(apiKey)}";

        _logger.LogInformation(
            "Sending checkin for {Count} participant(s) on project {ProjectId}",
            memberCheckins.Count, projectId);

        try
        {
            var response = await _httpClient.PutAsJsonAsync(url, payload, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("Scoutnet checkin response ({StatusCode}): {Body}",
                (int)response.StatusCode, responseBody);

            if (!response.IsSuccessStatusCode)
            {
                return new ProjectCheckinResult
                {
                    Success = false,
                    ErrorMessage = ParseScoutnetErrorMessage(responseBody)
                        ?? $"Scoutnet returned HTTP {(int)response.StatusCode}"
                };
            }

            return ParseCheckinResponse(responseBody);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error during checkin for project {ProjectId}", projectId);
            throw new ScoutnetApiException($"Failed to checkin on Scoutnet: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout during checkin for project {ProjectId}", projectId);
            throw new ScoutnetApiException("Request to Scoutnet API timed out", ex);
        }
    }

    private static ProjectCheckinResult ParseCheckinResponse(string responseBody)
    {
        var result = new ProjectCheckinResult { Success = true };

        if (string.IsNullOrWhiteSpace(responseBody))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            result.CheckedIn = ParseIntArray(root, "checked_in");
            result.CheckedOutAttended = ParseIntArray(root, "checked_out_attended");
            result.CheckedOutNotAttended = ParseIntArray(root, "checked_out_not_attended");
            result.Unchanged = ParseIntArray(root, "unchanged");
            result.NotFound = ParseIntArray(root, "not_found");
            result.NoMember = ParseIntArray(root, "no_member");

            if (root.TryGetProperty("total", out var totalProp) && totalProp.TryGetInt32(out var total))
            {
                result.Total = total;
            }
        }
        catch (JsonException)
        {
            // Best-effort parsing
        }

        return result;
    }

    private static List<int> ParseIntArray(JsonElement root, string propertyName)
    {
        var list = new List<int>();
        if (root.TryGetProperty(propertyName, out var array) && array.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in array.EnumerateArray())
            {
                if (item.TryGetInt32(out var value))
                {
                    list.Add(value);
                }
            }
        }
        return list;
    }

    private static MembershipUpdateResult ParseUpdateMembershipSuccess(string responseBody)
    {
        var result = new MembershipUpdateResult { Success = true };

        if (string.IsNullOrWhiteSpace(responseBody))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("updated", out var updatedArray) &&
                updatedArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in updatedArray.EnumerateArray())
                {
                    if (item.TryGetInt32(out var memberNo))
                    {
                        result.UpdatedMemberNumbers.Add(memberNo);
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Best-effort parsing; success is already set
        }

        return result;
    }

    private static MembershipUpdateResult ParseUpdateMembershipError(string responseBody)
    {
        var result = new MembershipUpdateResult { Success = false };

        if (string.IsNullOrWhiteSpace(responseBody))
            return result;

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("errors", out var errorsElement) &&
                errorsElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var memberProp in errorsElement.EnumerateObject())
                {
                    var fieldErrors = new Dictionary<string, string>();
                    if (memberProp.Value.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var fieldProp in memberProp.Value.EnumerateObject())
                        {
                            fieldErrors[fieldProp.Name] = fieldProp.Value.GetString() ?? string.Empty;
                        }
                    }
                    result.Errors[memberProp.Name] = fieldErrors;
                }
            }
        }
        catch (JsonException)
        {
            // Best-effort parsing
        }

        return result;
    }

    public async Task<List<ScoutnetGroupProject>> GetGroupProjectsAsync(
        int groupId,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var url = $"api/organisation/project?id={groupId}&key={Uri.EscapeDataString(apiKey)}";

        _logger.LogInformation("Fetching projects for group {GroupId} from Scoutnet", groupId);

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            _logger.LogDebug("Scoutnet viewGroupProjects response: {Body}", responseBody);

            return ParseGroupProjectsResponse(responseBody);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching projects for group {GroupId}", groupId);
            throw new ScoutnetApiException($"Failed to fetch projects from Scoutnet: {ex.Message}", ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error while fetching projects for group {GroupId}", groupId);
            throw new ScoutnetApiException($"Failed to parse Scoutnet projects response: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "Timeout while fetching projects for group {GroupId}", groupId);
            throw new ScoutnetApiException("Request to Scoutnet API timed out", ex);
        }
    }

    /// <summary>
    /// Parses the viewGroupProjects response.
    /// Expected format: [{"Project":{"name":"...", "starts":"...", ...}}, ...]
    /// Also handles empty/malformed items (e.g. [[], [], []] seen in some instances).
    /// </summary>
    private static List<ScoutnetGroupProject> ParseGroupProjectsResponse(string responseBody)
    {
        var projects = new List<ScoutnetGroupProject>();

        if (string.IsNullOrWhiteSpace(responseBody))
            return projects;

        using var doc = JsonDocument.Parse(responseBody);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            return projects;

        foreach (var item in root.EnumerateArray())
        {
            // Skip empty arrays or non-object items (malformed response)
            if (item.ValueKind == JsonValueKind.Array || item.ValueKind != JsonValueKind.Object)
                continue;

            // Expected structure: {"Project": {...}} or {"id": ..., "Project": {...}}
            JsonElement projectElement;

            if (item.TryGetProperty("Project", out var projectProp) && projectProp.ValueKind == JsonValueKind.Object)
            {
                projectElement = projectProp;
            }
            else
            {
                // Try treating the item itself as the project data
                projectElement = item;
            }

            var project = new ScoutnetGroupProject();

            if (projectElement.TryGetProperty("id", out var idProp))
            {
                if (idProp.TryGetInt32(out var id))
                    project.Id = id;
                else if (int.TryParse(idProp.GetString(), out var parsedId))
                    project.Id = parsedId;
            }

            // Also check outer item for ID (some API versions put it there)
            if (project.Id == null && item.TryGetProperty("id", out var outerIdProp))
            {
                if (outerIdProp.TryGetInt32(out var outerId))
                    project.Id = outerId;
                else if (int.TryParse(outerIdProp.GetString(), out var parsedOuterId))
                    project.Id = parsedOuterId;
            }

            if (projectElement.TryGetProperty("name", out var nameProp))
                project.Name = nameProp.GetString() ?? string.Empty;

            if (projectElement.TryGetProperty("starts", out var startsProp))
            {
                if (DateTime.TryParse(startsProp.GetString(), out var starts))
                    project.Starts = starts;
            }

            if (projectElement.TryGetProperty("ends", out var endsProp))
            {
                if (DateTime.TryParse(endsProp.GetString(), out var ends))
                    project.Ends = ends;
            }

            if (projectElement.TryGetProperty("description", out var descProp))
                project.Description = descProp.GetString();

            if (projectElement.TryGetProperty("min_age", out var minAgeProp) && minAgeProp.TryGetInt32(out var minAge))
                project.MinAge = minAge;

            if (projectElement.TryGetProperty("max_age", out var maxAgeProp) && maxAgeProp.TryGetInt32(out var maxAge))
                project.MaxAge = maxAge;

            // Only add if we have at least a name
            if (!string.IsNullOrWhiteSpace(project.Name))
                projects.Add(project);
        }

        return projects;
    }
}

/// <summary>
/// Exception thrown when Scoutnet API operations fail.
/// </summary>
public class ScoutnetApiException : Exception
{
    public ScoutnetApiException(string message) : base(message) { }
    public ScoutnetApiException(string message, Exception innerException) : base(message, innerException) { }
}
