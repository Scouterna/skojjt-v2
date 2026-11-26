# ScoutID Authentication Flow

This document describes the authentication flow for ScoutID integration in Skojjt v2.

## Overview

ScoutID is the single sign-on service for Scouterna (Swedish Scouting) that authenticates users against Scoutnet and exposes Scoutnet member data (member number, primary group, roles, troops, etc.) as authentication claims. Skojjt uses these claims to determine which scout groups and troops a user can access.

There are **two ScoutID generations**, and Skojjt v2 can connect to either one:

| Generation | Protocol | Status | Skojjt config section |
|------------|----------|--------|------------------------|
| **Legacy ScoutID** (SimpleSAMLphp) | SAML 2.0 | **Currently used by production Skojjt** | `ScoutIdSaml` |
| **New ScoutID** (Keycloak + [`scoutid-keycloak-provider`](https://github.com/Scouterna/scoutid-keycloak-provider)) | OAuth 2.0 / OpenID Connect | Available, will eventually replace the SAML version | `ScoutId` |

The legacy SAML-based ScoutID is what production Skojjt currently authenticates against. The newer Keycloak-based ScoutID is fully supported in code and is expected to become the default once Scouterna fully transitions. Both paths are normalized so that the rest of the application sees the same set of claims (see [Application Claims](#application-claims)).

**Key Resources:**
- ScoutID Documentation: https://etjanster.scout.se/programkatalog/scoutid/
- Legacy ScoutID (SimpleSAMLphp) source: https://github.com/Scouterna/scoutid
- New ScoutID Keycloak deployment: https://github.com/Scouterna/scoutid-keycloak
- New ScoutID Keycloak provider (Scoutnet auth + claim mappers): https://github.com/Scouterna/scoutid-keycloak-provider
- New ScoutID Keycloak theme: https://github.com/Scouterna/scoutid-keycloak-theme

## Authentication Flow

Which flow is used at runtime depends on configuration:

- If `ScoutIdSaml:Enabled` is `true` → **Legacy ScoutID (SAML 2.0)** is used (current production setup).
- Else if `ScoutId:ClientId` is set → **New ScoutID (Keycloak / OIDC)** is used.
- Else, in `Development`, the **Simulated ScoutID** flow is used.

See [`Program.cs`](../src/Skojjt.Web/Program.cs) for the selection logic.

### Production Flow (Legacy ScoutID — SimpleSAML / SAML 2.0)

This is the flow currently used by production Skojjt. The SAML response is processed by `Sustainsys.Saml2`, then normalized by `SamlClaimsNormalizer` so that the downstream `ScoutIdClaimsTransformation` produces the same application claims as the OIDC path.

```
+-------------+     +-------------+     +---------------------+     +-------------+
|   Browser   |     |  Skojjt Web |     | ScoutID (SimpleSAML)|     |   Scoutnet  |
|   (User)    |     |   Server    |     |   (SAML 2.0 IdP)    |     |  (Identity) |
+-------------+     +-------------+     +---------------------+     +-------------+
       |                   |                       |                       |
       | 1. Access /       |                       |                       |
       |------------------>|                       |                       |
       |                   |                       |                       |
       | 2. Not authenticated — Redirect (SAML AuthnRequest, HTTP-Redirect)|
       |<------------------|                       |                       |
       |                                           |                       |
       | 3. SAML AuthnRequest                      |                       |
       |------------------------------------------>|                       |
       |                                           |                       |
       |                                           | 4. Validate user      |
       |                                           |---------------------->|
       |                                           |                       |
       |                                           | 5. User attributes    |
       |                                           |<----------------------|
       |                                           |                       |
       | 6. SAML Response (POST to ACS) with signed assertion              |
       |<------------------------------------------|                       |
       |                   |                       |                       |
       | 7. POST /Saml2/Acs|                       |                       |
       |------------------>|                       |                       |
       |                   | 8. Validate signature,|                       |
       |                   |    normalize claims,  |                       |
       |                   |    issue cookie       |                       |
       |<------------------|                       |                       |
       |                   |                       |                       |
       | 9. Authenticated requests                 |                       |
       |------------------>|                       |                       |
```

### Production Flow (New ScoutID — Keycloak / OIDC)

This is the OIDC flow against the Keycloak-based ScoutID. It is fully supported in Skojjt and is enabled by setting `ScoutId:ClientId` (and leaving `ScoutIdSaml:Enabled` unset/false).

```
+-------------+     +-------------+     +-------------+     +-------------+
|   Browser   |     |  Skojjt Web |     |   ScoutID   |     |   Scoutnet  |
|   (User)    |     |   Server    |     |  (Keycloak) |     |  (Identity) |
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

When a user authenticates, ScoutID Keycloak issues the claims below. Skojjt requests `openid profile email scoutnet-memberships`. The roles that grant access are only in `scoutnet-memberships`, and Keycloak rejects the login with `invalid_scope` unless that scope is assigned to the client. The canonical claim reference is [`keycloak-config/03-scopes.yaml`](../external/scoutid-keycloak-provider/keycloak-config/03-scopes.yaml) in the provider repo, with a full example in [`config_support/id_token_example.json`](../external/scoutid-keycloak-provider/config_support/id_token_example.json).

### Claims Skojjt uses
| Claim | Scope | Description | Example |
|-------|-------|-------------|---------|
| `scoutnet_member_no` | `profile` | Scoutnet member number. Used as the Skojjt user ID. | `"9876543"` |
| `sub` | `openid` | Keycloak UUID by default; only the member number if the client has a "sub member_no" mapper. Fallback user ID when `scoutnet_member_no` is missing. | `"abc12345-…"` |
| `name` | `profile` | Display name | `"Anna Andersson"` |
| `email` | `email` | Email address (required by `CurrentUserService`) | `"anna@example.com"` |
| `memberships` | `scoutnet-memberships` | JSON object with all Scoutnet roles (see below) | `{ "groups": { … } }` |

Other claims (`preferred_username` = `<member_no>@scoutnet`, `primary_group_no`, `group_emails_json`, `scouterna_email`, …) are ignored and trimmed from the auth ticket.

### The `memberships` claim

`memberships` is a JSON object keyed by entity type. Each type maps a Scoutnet entity ID to an entry with a `roles` array of `{"id": <int>, "key": "<string>", "name"?: "<string>"}`:

```json
{
  "groups":   { "999":   { "name": "Exempel Scoutkår", "is_primary": true, "roles": [{"id": 9, "key": "member_registrar"}] } },
  "troops":   { "12345": { "name": "Spårarna", "groupId": 999,  "roles": [{"id": 4, "key": "vice_leader"}] },
                "12346": {                     "groupId": null, "roles": [{"id": 11, "key": "leader"}] } },
  "patrols":  { },
  "organisations": { "692": { "roles": [{"id": 68, "key": "scoutid_admin"}] } },
  "regions": {}, "districts": {}, "corps": {}, "networks": {}
}
```

- Troop `groupId` is only known for troops the user is a *member* of. When it is `null`, Skojjt resolves the troop's group from its own database.
- If the payload would exceed Keycloak's 2048-character attribute limit, ScoutID keeps only `groups` and adds `"error": "memberships_truncated: …"`. Skojjt logs a warning; troop-level roles are lost for that user.

Parsing is done by `ScoutIdMembershipsParser`, which turns each role into a `type:id:key` string (e.g. `organisation:692:scoutid_admin`). This is the same format as the legacy SAML `role` attribute, so both generations go through the same logic.

### Role Format

Skojjt interprets roles as these `type:id:key` patterns:

| Pattern | Meaning |
|---------|---------|
| `group:<group_id>:<role_name>` | Role granted at the scout group level |
| `troop:<troop_scoutnet_id>:<role_name>` | Role granted for a single troop (group from `groupId` or a DB lookup) |
| `organisation:<org_id>:scoutid_admin` | ScoutID system administrator (configurable via `Skojjt:AdminScoutnetRole`) |

Recognized role names (see `ScoutIdClaimsTransformation`):

| Role name | Description |
|-----------|-------------|
| `member_registrar` | Medlemsregistrerare — full access to the group, including member management |
| `leader` | Avdelningsledare |
| `assistant_leader` | Biträdande/assisterande avdelningsledare |
| `vice_leader` | Vice avdelningsledare |
| `other_leader` | Övrig ledare |

## Application Claims

After claims transformation (`ScoutIdClaimsTransformation`), the following custom claims are added:

| Claim Type | Description |
|------------|-------------|
| `scoutid/uid` | Scoutnet member number (OIDC `scoutnet_member_no`, else `sub`; SAML `uid`) |
| `scoutid/display_name` | Display name |
| `scoutid/accessible_groups` | Comma-separated list of scout group IDs the user can access |
| `scoutid/accessible_troops` | Comma-separated list of troop Scoutnet IDs the user can access (for troop-level roles) |
| `scoutid/member_registrar_groups` | Comma-separated list of groups where the user is `member_registrar` |
| `scoutid/admin` | `"true"` if the user is a ScoutID system administrator |

For troop-level role claims (`troop:<id>:<role>`), the transformation looks up the troop's parent scout group from the database (cached in a static `ConcurrentDictionary`) so that both the group and the troop end up in the accessible-claims lists.

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

Skojjt selects the authentication backend based on configuration (see [`Program.cs`](../src/Skojjt.Web/Program.cs)):

1. `ScoutIdSaml:Enabled = true` → legacy SAML ScoutID (current production).
2. Otherwise `ScoutId:ClientId` set → new Keycloak ScoutID (OIDC).
3. Otherwise (Development only) → simulated ScoutID.

### Legacy ScoutID Configuration — SimpleSAML (`ScoutIdSaml`)

This is what **production Skojjt currently uses**. The SAML metadata, signing certificate, and SLO endpoint come from the SimpleSAML-based ScoutID IdP.

```json
{
  "ScoutIdSaml": {
    "Enabled": true,
    "SpEntityId": "https://skojjt.example.se/Saml2",
    "IdpEntityId": "https://scoutid.scout.se/saml2/idp/metadata.php",
    "IdpSsoUrl":   "https://scoutid.scout.se/saml2/idp/SSOService.php",
    "IdpSloUrl":   "https://scoutid.scout.se/saml2/idp/SingleLogoutService.php",
    "IdpMetadataUrl": "https://scoutid.scout.se/saml2/idp/metadata.php"
  }
}
```

Implementation: [`SamlAuthenticationExtensions`](../src/Skojjt.Infrastructure/Authentication/SamlAuthenticationExtensions.cs). Incoming SAML attributes are normalized by `SamlClaimsNormalizer` so the rest of the pipeline is identical to the OIDC path.

### New ScoutID Configuration — Keycloak / OIDC (`ScoutId`)

ScoutID Keycloak is hosted as a Keycloak realm. `Authority` points at that realm, and `Scope` must include `scoutnet-memberships` to receive the roles.

| Environment | Authority |
|-------------|-----------|
| Production | `https://id.scouterna.se/realms/scoutid` |
| ScoutID dev | `https://dev1.id.scouterna.se/realms/scoutid` |
| Local Docker (`external/scoutid-keycloak-provider`) | `http://localhost:8080/realms/master` |

```json
{
  "ScoutIdSaml": { "Enabled": false },
  "ScoutId": {
    "Authority": "https://id.scouterna.se/realms/scoutid",
    "ClientId": "skojjt-v2",
    "ClientSecret": "<from App Service settings / user-secrets, never in appsettings>",
    "Scope": "openid profile email scoutnet-memberships"
  }
}
```

Clients are registered by the ScoutID team (scoutid@scouterna.se); see the [ScoutID wiki](https://github.com/Scouterna/scoutid-keycloak/wiki). Ask for:

- A confidential client with the standard (authorization code) flow. Skojjt sends PKCE S256.
- The `profile`, `email` and `scoutnet-memberships` client scopes assigned.
- Valid redirect URI `https://<host>/signin-oidc` and post-logout redirect URI `https://<host>/*`.

A "sub member_no" mapper is optional, because Skojjt reads `scoutnet_member_no` directly.

**Cutover from SAML:** set `ScoutIdSaml:Enabled` to `false` and provide `ScoutId:ClientSecret` (App Service setting `ScoutId__ClientSecret`). Auth tickets are held in an in-memory store, so the restart signs everyone out and they log in again through ScoutID Keycloak. Both paths produce the same `scoutid/*` claims, so nothing else changes for users.

### Development Configuration

In `Development`, when neither `ScoutIdSaml:Enabled` is `true` nor `ScoutId:ClientId` is set, Skojjt falls back to cookie-based fake authentication backed by `FakeScoutIdService`:

```json
{
  "ScoutIdSaml": { "Enabled": false },
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
├── Skojjt.Core/
│   └── Authentication/
│       ├── ICurrentUserService.cs      # Interface for accessing current user
│       ├── ScoutIdClaimTypes.cs        # Custom claim type constants
│       └── ScoutIdClaims.cs            # Claims model & role constants
│
├── Skojjt.Infrastructure/
│   └── Authentication/
│       ├── CurrentUserService.cs       # Implementation of ICurrentUserService
│       ├── ScoutIdClaimsTransformation.cs  # Condenses OIDC/SAML claims into scoutid/* claims
│       ├── ScoutIdMembershipsParser.cs # Parses the Keycloak memberships JSON + legacy role strings
│       ├── IScoutIdSimulator.cs        # Interface for simulated service
│       └── FakeScoutIdService.cs       # Development/test implementation
│
└── Skojjt.Web/
    ├── Program.cs                      # Authentication configuration
    ├── Controllers/
    │   ├── AuthController.cs           # Unified auth endpoints (login/logout)
    │   └── DevAuthController.cs        # Development login form handlers
    └── Components/Pages/
        └── Login.razor                 # Unified login page (dev + production)
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
| User logs in but has no groups | `memberships` claim missing | Request `scoutnet-memberships` and make sure it is assigned to the client |
| `error=invalid_scope` on callback | A requested scope is not assigned to the Keycloak client | Assign `scoutnet-memberships` (Default or Optional) to the client |

## Migrating from V1 (Skojjt Version 1)

The V1 system (Python/Flask on Google App Engine) used Google Auth with `UserPrefs` entities. Migration considerations:

1. **User Mapping**: Users are identified by their Scoutnet member number (`scoutnet_member_no` / OIDC `sub`), not by email
2. **Group Access**: ScoutID determines access based on Scoutnet roles, not stored preferences
3. **Admin Access**: The `is_admin` flag is now determined by the ScoutID `scoutid_admin` organisation role; per-group elevated access comes from the `member_registrar` role

The V1 source code is preserved in the `v1/` folder for reference during migration.
