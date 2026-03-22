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
}

/// <summary>
/// Exception thrown when Scoutnet API operations fail.
/// </summary>
public class ScoutnetApiException : Exception
{
    public ScoutnetApiException(string message) : base(message) { }
    public ScoutnetApiException(string message, Exception innerException) : base(message, innerException) { }
}
