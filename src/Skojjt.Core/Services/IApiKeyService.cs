using Skojjt.Core.Entities;

namespace Skojjt.Core.Services;

/// <summary>
/// Service for managing API keys used to authenticate external tool access.
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Generates a new API key. Returns the raw key string — this is the only time it is available.
    /// </summary>
    /// <param name="name">Human-readable name/description for the key.</param>
    /// <param name="createdByUserId">ScoutID UID of the admin creating the key.</param>
    /// <param name="expiresAt">Optional expiration date. Null means no expiration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A result containing the raw key (show once) and the stored entity.</returns>
    Task<ApiKeyCreateResult> GenerateKeyAsync(string name, string createdByUserId, DateTime? expiresAt = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a raw API key. Returns the key entity if valid, null otherwise.
    /// Also updates LastUsedAt on successful validation.
    /// </summary>
    Task<ApiKey?> ValidateKeyAsync(string rawKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all API keys (for admin display). Does not include the raw key.
    /// </summary>
    Task<List<ApiKey>> GetAllKeysAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an API key by ID.
    /// </summary>
    Task<bool> RevokeKeyAsync(int keyId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of creating a new API key. Contains the raw key that must be shown to the user exactly once.
/// </summary>
public class ApiKeyCreateResult
{
    /// <summary>
    /// The raw API key string. Only available at creation time — store it securely.
    /// </summary>
    public string RawKey { get; set; } = string.Empty;

    /// <summary>
    /// The stored API key entity (without the raw key).
    /// </summary>
    public ApiKey ApiKey { get; set; } = null!;
}
