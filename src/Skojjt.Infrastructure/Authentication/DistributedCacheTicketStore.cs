using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Distributed;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Stores authentication tickets server-side in an <see cref="IDistributedCache"/> so the
/// browser cookie only holds a small session key instead of the full set of claims and tokens.
///
/// This is the robust fix for HTTP 431 (Request Header Fields Too Large): users with many
/// ScoutID role claims previously produced an oversized, chunked auth cookie that exceeded
/// Kestrel's request-header limit. With a server-side ticket store the cookie is a few dozen
/// bytes regardless of how many roles/groups/troops a user has.
///
/// NOTE: <c>AddDistributedMemoryCache</c> keeps tickets per-instance and clears them on restart
/// (users would need to sign in again). Blazor Server already requires sticky sessions on
/// Azure App Service, so a single logical instance handles a user's requests. For true
/// multi-instance durability, register a shared backing store (e.g. <c>AddStackExchangeRedisCache</c>)
/// instead — no other change is required because this store depends only on <see cref="IDistributedCache"/>.
/// </summary>
public sealed class DistributedCacheTicketStore : ITicketStore
{
    private const string KeyPrefix = "auth-ticket:";

    // Fallback lifetime used only when a ticket has no explicit expiry; keeps the
    // cache entry from lingering forever. Mirrors the cookie ExpireTimeSpan (7 days).
    private static readonly TimeSpan FallbackLifetime = TimeSpan.FromDays(7);

    private readonly IDistributedCache _cache;

    public DistributedCacheTicketStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public async Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = KeyPrefix + Guid.NewGuid().ToString("N");
        await RenewAsync(key, ticket);
        return key;
    }

    public async Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        var options = new DistributedCacheEntryOptions();
        var expiresUtc = ticket.Properties.ExpiresUtc;
        if (expiresUtc.HasValue)
        {
            options.SetAbsoluteExpiration(expiresUtc.Value);
        }
        else
        {
            options.SetAbsoluteExpiration(FallbackLifetime);
        }

        var bytes = TicketSerializer.Default.Serialize(ticket);
        await _cache.SetAsync(key, bytes, options);
    }

    public async Task<AuthenticationTicket?> RetrieveAsync(string key)
    {
        var bytes = await _cache.GetAsync(key);
        return bytes is null ? null : TicketSerializer.Default.Deserialize(bytes);
    }

    public Task RemoveAsync(string key)
    {
        return _cache.RemoveAsync(key);
    }
}
