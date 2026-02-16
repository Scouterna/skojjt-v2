using System.Net.Http.Json;
using System.Text.Json;
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
}

/// <summary>
/// Exception thrown when Scoutnet API operations fail.
/// </summary>
public class ScoutnetApiException : Exception
{
    public ScoutnetApiException(string message) : base(message) { }
    public ScoutnetApiException(string message, Exception innerException) : base(message, innerException) { }
}
