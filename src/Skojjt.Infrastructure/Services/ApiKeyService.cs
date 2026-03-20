using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Entities;
using Skojjt.Core.Services;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Services;

/// <summary>
/// Service for generating and validating API keys.
/// Keys are generated with cryptographic randomness, and only the SHA256 hash is stored.
/// </summary>
public class ApiKeyService : IApiKeyService
{
    private const string KeyPrefix = "skojjt_";
    private const int KeyRandomBytes = 32;

    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(
        IDbContextFactory<SkojjtDbContext> contextFactory,
        ILogger<ApiKeyService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<ApiKeyCreateResult> GenerateKeyAsync(
        string name,
        string createdByUserId,
        DateTime? expiresAt = null,
        CancellationToken cancellationToken = default)
    {
        // Generate cryptographically secure random bytes
        var randomBytes = RandomNumberGenerator.GetBytes(KeyRandomBytes);
        var rawKey = KeyPrefix + Convert.ToBase64String(randomBytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

        var hash = ComputeHash(rawKey);

        var apiKey = new ApiKey
        {
            KeyHash = hash,
            KeyPrefix = rawKey[..Math.Min(16, rawKey.Length)],
            Name = name,
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            IsRevoked = false
        };

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        context.ApiKeys.Add(apiKey);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "API key created: {KeyPrefix}... by user {UserId}, expires {ExpiresAt}",
            apiKey.KeyPrefix, createdByUserId, expiresAt?.ToString("o") ?? "never");

        return new ApiKeyCreateResult
        {
            RawKey = rawKey,
            ApiKey = apiKey
        };
    }

    public async Task<ApiKey?> ValidateKeyAsync(string rawKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey))
            return null;

        var hash = ComputeHash(rawKey);

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var apiKey = await context.ApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == hash, cancellationToken);

        if (apiKey == null)
        {
            _logger.LogWarning("API key validation failed: key not found");
            return null;
        }

        if (apiKey.IsRevoked)
        {
            _logger.LogWarning("API key validation failed: key {KeyPrefix}... is revoked", apiKey.KeyPrefix);
            return null;
        }

        if (apiKey.ExpiresAt != null && apiKey.ExpiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("API key validation failed: key {KeyPrefix}... has expired", apiKey.KeyPrefix);
            return null;
        }

        // Update last used timestamp (throttled to avoid a write on every single request)
        if (apiKey.LastUsedAt == null || apiKey.LastUsedAt < DateTime.UtcNow.AddMinutes(-1))
        {
            apiKey.LastUsedAt = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogDebug("API key validated: {KeyPrefix}...", apiKey.KeyPrefix);
        return apiKey;
    }

    public async Task<List<ApiKey>> GetAllKeysAsync(CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.ApiKeys
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RevokeKeyAsync(int keyId, CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var apiKey = await context.ApiKeys.FindAsync([keyId], cancellationToken);

        if (apiKey == null)
            return false;

        apiKey.IsRevoked = true;
        apiKey.RevokedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("API key revoked: {KeyPrefix}... (ID {KeyId})", apiKey.KeyPrefix, keyId);
        return true;
    }

    private static string ComputeHash(string rawKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexStringLower(bytes);
    }
}
