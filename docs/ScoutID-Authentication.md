# ScoutID Authentication Flow

This document describes the authentication flow for ScoutID integration in Skojjt v2.

## Overview

ScoutID is an OAuth 2.0 / OpenID Connect (OIDC) authentication service provided by Scouterna (Swedish Scouting). It allows users to authenticate using their Scoutnet credentials and provides claims about their scout group membership and roles.

**Key Resources:**
- ScoutID Documentation: https://etjanster.scout.se/programkatalog/scoutid/
- ScoutID Source: https://github.com/Scouterna/scoutid

## Authentication Flow

### Production Flow (ScoutID OIDC)

```
+-------------+     +-------------+     +-------------+     +-------------+
|   Browser   |     |  Skojjt Web |     |   ScoutID   |     |   Scoutnet  |
|   (User)    |     |   Server    |     |   (OIDC)    |     |  (Identity) |
+-------------+     +-------------+     +-------------+     +-------------+
       |                   |                   |                   |
       | 1. Access /       |                   |                   |
       |------------------>|                   |                   |
       |                   |                   |                   |
       | 2. Not authenticated                  |                   |
       |   - Redirect to ScoutID               |                   |
       |<------------------|                   |                   |
       |                   |                   |                   |
       | 3. OIDC Authorization Request         |                   |
       |-------------------------------------->|                   |
       |                   |                   |                   |
       |                   |                   | 4. User login     |
       |                   |                   |------------------>|
       |                   |                   |                   |
       |                   |                   | 5. Validate &     |
       |                   |                   |    return claims  |
       |                   |                   |<------------------|
       |                   |                   |                   |
       | 6. Redirect with authorization code   |                   |
       |<--------------------------------------|                   |
       |                   |                   |                   |
       | 7. Callback with code                 |                   |
       |------------------>|                   |                   |
       |                   |                   |                   |
       |                   | 8. Exchange code  |                   |
       |                   |    for tokens     |                   |
       |                   |------------------>|                   |
       |                   |                   |                   |
       |                   | 9. ID Token +     |                   |
       |                   |    Access Token   |                   |
       |                   |<------------------|                   |
       |                   |                   |                   |
       |                   | 10. Claims        |                   |
       |                   |     Transformation|                   |
       |                   |                   |                   |
       | 11. Cookie with   |                   |                   |
       |     session       |                   |                   |
       |<------------------|                   |                   |
       |                   |                   |                   |
       | 12. Authenticated |                   |                   |
       |     requests      |                   |                   |
       |------------------>|                   |                   |
```

### Development Flow (Simulated ScoutID)

In development mode, a simulated ScoutID service allows testing without connecting to the real ScoutID infrastructure:

```
+-------------+     +-------------+     +-----------------------+
|   Browser   |     |  Skojjt Web |     |  FakeScoutIdService   |
|   (User)    |     |   Server    |     |   (Simulated)         |
+-------------+     +-------------+     +-----------------------+
       |                   |                       |
       | 1. GET /dev-login |                       |
       |------------------>|                       |
       |                   |                       |
       | 2. Show test user |                       |
       |    selection page |                       |
       |<------------------|                       |
       |                   |                       |
       | 3. Select test    |                       |
       |    user           |                       |
       |------------------>|                       |
       |                   | 4. Get user by UID    |
       |                   |---------------------->|
       |                   |                       |
       |                   | 5. Return simulated   |
       |                   |    claims             |
       |                   |<----------------------|
       |                   |                       |
       | 6. Create cookie  |                       |
       |    with claims    |                       |
       |<------------------|                       |
       |                   |                       |
       | 7. Authenticated  |                       |
       |    requests       |                       |
       |------------------>|                       |
```

## ScoutID Claims

When a user authenticates, ScoutID provides the following claims:

### Standard OIDC Claims
| Claim | Description | Example |
|-------|-------------|---------|
| `sub` | Subject (user identifier) | `"12345"` |
| `name` | Display name | `"Anna Andersson"` |
| `email` | Email address | `"anna@example.com"` |

### ScoutID-Specific Claims
| Claim | Description | Example |
|-------|-------------|---------|
| `uid` | Scoutnet user/member ID | `"12345"` |
| `group_no` | Scout group number | `"123"` |
| `group_id` | Scout group ID (numeric) | `1001` |
| `roles` | Role assignments (JSON) | `{"group": {"1001": ["9", "1"]}}` |

### Role Codes
| Code | Role Name | Description |
|------|-----------|-------------|
| `1` | Group Leader (Kårledare) | Overall group leadership |
| `2` | Assistant Group Leader | Assists group leader |
| `9` | Member Registrar (Medlemsregistrerare) | Can manage member data |

## Application Claims

After claims transformation (`ScoutIdClaimsTransformation`), the following custom claims are added:

| Claim Type | Description |
|------------|-------------|
| `scoutid/uid` | Scoutnet user ID |
| `scoutid/display_name` | Display name |
| `scoutid/group_no` | Primary group number |
| `scoutid/group_id` | Primary group ID |
| `scoutid/accessible_groups` | Comma-separated list of group IDs user can access |
| `scoutid/member_registrar_groups` | Comma-separated list of groups where user is registrar |
| `scoutid/group_roles` | JSON dictionary of roles per group |

## Access Control

### Group-Based Access

Users can **only** access scout groups listed in their `AccessibleGroupIds`. This is determined by ScoutID based on their roles in Scoutnet.

```csharp
// Check if user has access to a specific group
if (currentUserService.HasGroupAccess(scoutGroupId))
{
    // User can access this group
}

// Require access (throws UnauthorizedAccessException if no access)
currentUserService.RequireGroupAccess(scoutGroupId);
```

### Role-Based Access

Additional role checks can be performed for specific operations:

```csharp
// Check if user is member registrar for a group
if (currentUserService.HasGroupAccess(scoutGroupId))
{
    // User can manage members
}

// Check for specific role
if (currentUserService.IsMemberRegistrar(scoutGroupId))
{
    // User is member registrar
}
```

## Configuration

### Production Configuration (appsettings.json)

```json
{
  "ScoutId": {
    "Authority": "https://scoutid.se",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "Scope": "scoutid"
  }
}
```

### Development Configuration

When `ScoutId:ClientId` is not configured, the system automatically uses the development authentication:

```json
{
  "ScoutId": {
    "Authority": "",
    "ClientId": "",
    "ClientSecret": ""
  }
}
```

## Development Test Users

The `FakeScoutIdService` provides these default test users:

| User | Email | Role | Group ID | Multi-Group |
|------|-------|------|----------|-------------|
| Test Admin | admin@test.scout.se | Member Registrar | 1001 | 1001, 1002 |
| Test Ledare | ledare@test.scout.se | Leader | 1001 | No |
| Multi Grupp | multi@test.scout.se | Multi-group Registrar | 1002 | 1001, 1002 |
| Läsare | readonly@test.scout.se | Read-only | 1001 | No |

### Accessing the Login Page

The unified login page at `/login` automatically adapts to the environment:

1. **Development mode** (no ScoutID configured):
   - Shows test user selection
   - Displays custom login form
   - Shows service configuration panel
   - Displays version number

2. **Production mode** (ScoutID configured):
   - Shows "Login with ScoutID" button
   - Redirects to ScoutID for authentication

### Quick Login Endpoints

- `/dev-auth/quick-login/admin` - Login as admin
- `/dev-auth/quick-login/ledare` - Login as regular leader
- `/dev-auth/quick-login/readonly` - Login as read-only user

## Code Architecture

### Key Components

```
src/
??? Skojjt.Core/
?   ??? Authentication/
?       ??? ICurrentUserService.cs      # Interface for accessing current user
?       ??? ScoutIdClaimTypes.cs        # Custom claim type constants
?       ??? ScoutIdClaims.cs            # Claims model & role constants
?
??? Skojjt.Infrastructure/
?   ??? Authentication/
?       ??? CurrentUserService.cs       # Implementation of ICurrentUserService
?       ??? ScoutIdClaimsTransformation.cs  # Transforms OIDC claims
?       ??? IScoutIdSimulator.cs        # Interface for simulated service
?       ??? FakeScoutIdService.cs       # Development/test implementation
?
??? Skojjt.Web/
    ??? Program.cs                      # Authentication configuration
    ??? Controllers/
    ?   ??? AuthController.cs           # Unified auth endpoints (login/logout)
    ?   ??? DevAuthController.cs        # Development login form handlers
    ??? Components/Pages/
        ??? Login.razor                 # Unified login page (dev + production)
```

### Service Registration

```csharp
// In Program.cs
services.AddScoped<ICurrentUserService, CurrentUserService>();

// For development:
services.AddSingleton<IScoutIdSimulator, FakeScoutIdService>();
```

### Using ICurrentUserService in Components

```razor
@inject ICurrentUserService CurrentUserService

@code {
    protected override void OnInitialized()
    {
        var user = CurrentUserService.GetCurrentUser();
        if (user != null)
        {
            // Access user properties
            var displayName = user.DisplayName;
            var accessibleGroups = user.AccessibleGroupIds;
        }
    }
}
```

## Security Considerations

1. **Group Access Enforcement**: Always validate group access before returning data
2. **Claims Validation**: The `ScoutIdClaimsTransformation` validates and normalizes claims
3. **Role Verification**: Role checks always first verify group access
4. **Session Security**: Production uses secure, HTTP-only cookies with sliding expiration

## Testing

### Unit Testing Authentication

```csharp
// Create a custom test user
var user = FakeScoutIdService.CreateCustomUser(
    uid: "99999",
    email: "test@test.se",
    displayName: "Test User",
    groupId: 2000,
    isMemberRegistrar: true,
    accessibleGroups: [2000, 2001]
);

// Use in tests
var service = new FakeScoutIdService([user]);
var claims = service.CreateClaimsForUser(user);
```

### Integration Testing

The `FakeScoutIdService` can be injected in test scenarios to simulate various user types and permissions without requiring actual ScoutID infrastructure.

## Logout Flow

### Unified Logout
Both development and production use the same logout endpoint:

```
GET /auth/signout ? Cookie cleared ? Redirect to /
```

For production with ScoutID OIDC (federated logout):
```
GET /auth/logout ? OpenIdConnect signout ? ScoutID logout ? Cookie cleared
```

## Error Handling

| Error | Cause | Resolution |
|-------|-------|------------|
| `UnauthorizedAccessException` | User lacks group access | Check `AccessibleGroupIds` |
| Authentication redirect loop | Missing/invalid ScoutID config | Verify configuration |
| Claims missing after login | OIDC scope not configured | Add `scoutid` scope |

## Migrating from V1 (Skojjt Version 1)

The V1 system (Python/Flask on Google App Engine) used Google Auth with `UserPrefs` entities. Migration considerations:

1. **User Mapping**: Users are identified by email, which should match between systems
2. **Group Access**: ScoutID determines access based on Scoutnet roles, not stored preferences
3. **Admin Access**: The `is_admin` flag is now determined by the `MemberRegistrar` role

The V1 source code is preserved in the `v1/` folder for reference during migration.
