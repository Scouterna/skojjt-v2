using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Skojjt.Core.Authentication;
using Skojjt.Core.Entities;
using Skojjt.Infrastructure.Data;

namespace Skojjt.Infrastructure.Authentication;

/// <summary>
/// Service for synchronizing user data from ScoutID claims to the database.
/// </summary>
public class UserSyncService : IUserSyncService
{
    private readonly IDbContextFactory<SkojjtDbContext> _contextFactory;
    private readonly ILogger<UserSyncService> _logger;

    public UserSyncService(
        IDbContextFactory<SkojjtDbContext> contextFactory,
        ILogger<UserSyncService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<string> SyncUserAsync(ScoutIdClaims claims, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(claims.Uid))
        {
            _logger.LogWarning("Cannot sync user: UID is empty");
            throw new ArgumentException("User UID is required", nameof(claims));
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var existingUser = await context.Users
            .FirstOrDefaultAsync(u => u.Id == claims.Uid, cancellationToken);

        if (existingUser == null)
        {
            // Create new user
            var newUser = new User
            {
                Id = claims.Uid,
                Email = claims.Email ?? string.Empty,
                DisplayName = claims.DisplayName,
                Name = claims.DisplayName,
                //GroupNo = claims.GroupNo,
                //ScoutGroupId = claims.GroupId > 0 ? claims.GroupId : null,
                //HasAccess = true,
                //IsMemberRegistrar = claims.IsMemberRegistrar(),
                //AccessibleGroupIds = string.Join(",", claims.AccessibleGroupIds),
                //GroupRolesJson = claims.GroupRoles.Count > 0 
                //    ? JsonSerializer.Serialize(claims.GroupRoles) 
                //    : null,
                LastLoginAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Users.Add(newUser);
            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created new user {UserId} ({DisplayName})",
                newUser.Id, newUser.DisplayName);

            return newUser.Id;
        }
        else
        {
            // Update existing user with latest ScoutID data
            existingUser.Email = claims.Email ?? existingUser.Email;
            existingUser.DisplayName = claims.DisplayName ?? existingUser.DisplayName;
            existingUser.Name = claims.DisplayName ?? existingUser.Name;
            //existingUser.GroupNo = claims.GroupNo ?? existingUser.GroupNo;
            //existingUser.ScoutGroupId = claims.GroupId > 0 ? claims.GroupId : existingUser.ScoutGroupId;
            //existingUser.HasAccess = true;
            //existingUser.IsMemberRegistrar = claims.IsMemberRegistrar;
            //existingUser.AccessibleGroupIds = string.Join(",", claims.AccessibleGroupIds);
            //existingUser.GroupRolesJson = claims.GroupRoles.Count > 0 
            //    ? JsonSerializer.Serialize(claims.GroupRoles) 
            //    : existingUser.GroupRolesJson;
            existingUser.LastLoginAt = DateTime.UtcNow;
            existingUser.UpdatedAt = DateTime.UtcNow;

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Updated user {UserId} ({DisplayName}) last login",
                existingUser.Id, existingUser.DisplayName);

            return existingUser.Id;
        }
    }
}
