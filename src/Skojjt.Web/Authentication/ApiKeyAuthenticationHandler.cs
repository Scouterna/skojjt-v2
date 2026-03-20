using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Skojjt.Core.Services;

namespace Skojjt.Web.Authentication;

/// <summary>
/// Authentication handler that validates API keys from the X-Api-Key header.
/// Valid keys are granted an Admin claims principal.
/// </summary>
public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    private readonly IApiKeyService _apiKeyService;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<ApiKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeyService)
        : base(options, logger, encoder)
    {
        _apiKeyService = apiKeyService;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValue))
        {
            return AuthenticateResult.NoResult();
        }

        var rawKey = headerValue.ToString();
        if (string.IsNullOrWhiteSpace(rawKey))
        {
            return AuthenticateResult.NoResult();
        }

        var apiKey = await _apiKeyService.ValidateKeyAsync(rawKey, Context.RequestAborted);
        if (apiKey == null)
        {
            return AuthenticateResult.Fail("Invalid or expired API key.");
        }

        // Build claims principal with Admin role
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, $"apikey:{apiKey.Id}"),
            new(ClaimTypes.Name, $"API Key: {apiKey.Name}"),
            new(ClaimTypes.Role, "Admin"),
            new("apikey_id", apiKey.Id.ToString()),
            new("apikey_name", apiKey.Name),
            new("apikey_created_by", apiKey.CreatedByUserId)
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}

/// <summary>
/// Options for API key authentication (currently empty, extensible for future settings).
/// </summary>
public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}
