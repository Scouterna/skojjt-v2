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
}

/// <summary>
/// Exception thrown when Scoutnet API operations fail.
/// </summary>
public class ScoutnetApiException : Exception
{
    public ScoutnetApiException(string message) : base(message) { }
    public ScoutnetApiException(string message, Exception innerException) : base(message, innerException) { }
}
