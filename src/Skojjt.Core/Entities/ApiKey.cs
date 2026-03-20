namespace Skojjt.Core.Entities;

/// <summary>
/// API key for authenticating external tool access (e.g., data import).
/// The raw key is only shown once at creation time. Only the SHA256 hash is stored.
/// </summary>
public class ApiKey
{
    /// <summary>
    /// Auto-increment primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// SHA256 hash of the raw API key. Used for validation.
    /// </summary>
    public string KeyHash { get; set; } = string.Empty;

    /// <summary>
    /// First 8 characters of the raw key for identification in the UI (e.g., "skojjt_Ab...").
    /// </summary>
    public string KeyPrefix { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name/description for this key (e.g., "Import v1 data - Göteborg").
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// ScoutID UID of the admin who created this key.
    /// </summary>
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// When this key was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this key expires. Null means no expiration.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Last time this key was used for authentication.
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Whether this key has been manually revoked.
    /// </summary>
    public bool IsRevoked { get; set; }

    /// <summary>
    /// When this key was revoked.
    /// </summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>
    /// Whether this key is currently valid (not revoked and not expired).
    /// </summary>
    public bool IsValid => !IsRevoked && (ExpiresAt == null || ExpiresAt > DateTime.UtcNow);
}
