# Copilot Instructions

## Project Overview
Skojjt is a **Blazor Server** attendance tracking system for Swedish scout groups (scoutkårer). It manages meeting attendance, troop/member management, badge tracking, and exports reports for municipal activity grants (aktivitetsbidrag / DAK). The UI is in **Swedish**. Scoutnet is the member register for Swedish scouting, and Skojjt integrates with it for authentication and member data. This system is designed for use by scout leaders to manage their groups' attendance and activities efficiently.

## Architecture
- **Clean Architecture** with three main layers:
  - `Skojjt.Core` — Domain entities, interfaces, and service contracts. No external dependencies.
  - `Skojjt.Infrastructure` — EF Core repositories, external service implementations (Scoutnet, DAK export, SAML auth).
  - `Skojjt.Web` — Blazor Server application with MudBlazor UI components.
- `Skojjt.Shared` — Shared DTOs and utilities used across layers.
- Interfaces are defined in `Skojjt.Core`; implementations live in `Skojjt.Infrastructure`.

## Tech Stack & Versions
- **.NET 10** / **C# 14** — target framework `net10.0` across all projects.
- **Blazor Server** with `@rendermode InteractiveServer` — not Razor Pages or MVC.
- **MudBlazor** (v9) for all UI components. Use `MudBlazor` components, not raw HTML or Bootstrap.
- **Entity Framework Core 10** with **PostgreSQL** (Npgsql).
- **MSTest** for unit tests (not xUnit or NUnit).
- Authentication via **ScoutID** (Keycloak-based OpenID Connect / SAML).

## Key Patterns

### Database Access
- Use `IDbContextFactory<SkojjtDbContext>` (not direct `DbContext` injection) — essential for Blazor Server to avoid concurrency issues.
- Create short-lived `DbContext` instances with `await using var context = await _contextFactory.CreateDbContextAsync(ct)` or `await using var context = CreateContext()`.
- A **retrying execution strategy** (`NpgsqlRetryingExecutionStrategy`) is configured. When using explicit transactions, wrap them in `context.Database.CreateExecutionStrategy().ExecuteAsync(...)`.
- Repositories inherit from `Repository<TEntity>` and use the factory pattern.

### Authorization & Access Control
- Authorization operates at **two levels**: group-level and troop-level.
- **Group-level access**: Users can only access scout groups listed in their `AccessibleGroupIds`. Check with `ICurrentUserService.HasGroupAccess(scoutGroupId)` or `RequireGroupAccess(scoutGroupId)`.
- **Troop-level access**: Within a group, users only see troops they have explicit role claims for. Check with `ICurrentUserService.HasTroopAccess(scoutGroupId, troopScoutnetId)`.
- **Member registrar** (`member_registrar` role) has full access to all troops within their group, plus access to group-level management pages (Scoutnet import, all members, add member, badges, group settings).
- **Troop leaders** (`leader`, `assistant_leader`, `other_leader` roles) only have access to the specific troops listed in their role claims.
- Admin users only have elevated access when admin mode is explicitly active (`IAdminModeService.IsAdminModeActive`).
- **ScoutID role claims** come in two formats:
  - `group:<group_id>:<role>` — direct group-level role (e.g., `group:42:member_registrar`).
  - `troop:<troop_scoutnet_id>:<role>` — troop-level role (e.g., `troop:999:other_leader`). These are resolved to their parent scout group via a DB lookup in `ScoutIdClaimsTransformation`, with results cached in a static `ConcurrentDictionary`.
- **Custom claims** emitted after transformation: `AccessibleGroups` (comma-separated group IDs), `AccessibleTroops` (comma-separated troop Scoutnet IDs), `MemberRegistrarGroups` (comma-separated group IDs).

### Blazor Pages
- Pages use `@attribute [Authorize]` and `@rendermode InteractiveServer`.
- Inject repositories and services directly into Razor components via `@inject`.
- Route parameters use the pattern `/sk/{ScoutGroupId:int}/t/{SemesterId:int}/...`.
- Swedish text for all user-facing strings (buttons, labels, alerts, page titles).

### Entity Conventions
- `Person.Id` = Scoutnet member number (not auto-increment).
- `ScoutGroup.Id` = Scoutnet group ID.
- `Meeting.Id` = auto-increment with unique constraint on `(TroopId, MeetingDate)`.
- `Semester.Id` is generated via `(Year * 10) + (isAutumn ? 1 : 0)`.
- A `Person` can belong to multiple `ScoutGroup`s via the `ScoutGroupPerson` join table.

### Coding Style
- `TreatWarningsAsErrors` is enabled — all code must compile without warnings.
- Nullable reference types are enabled (`<Nullable>enable</Nullable>`).
- Use file-scoped namespaces.
- Use collection expressions (`[]`) instead of `new List<T>()` where appropriate.
- Use `string.Empty` over `""` for default string values.
- 4-space indentation, no tabs.

### Testing
- Test framework: **MSTest** with `[TestClass]` / `[TestMethod]` attributes.
- Tests run in parallel (`[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]`).
- Test projects: `Skojjt.Core.Tests`, `Skojjt.Infrastructure.Tests`, `Skojjt.Web.Tests`.

## DAK Export Specifics
- DAK = Digitalt Aktivitetskort, the Swedish municipal attendance reporting format.
- The Sammankomst `kod` attribute must be a unique key for the meeting that fits into the DAK XML schema defining it as a string (minLength=3, maxLength=50). It does not have to be an int32 anymore.
- For 2026, Göteborgs kommun aktivitetsbidrag rates are: Flickor/kvinnor = 9,89 kr per deltagare och sammankomst, Pojkar/män = 8,02 kr per deltagare och sammankomst.
- Gender is determined from the second-to-last digit of the personnummer (even = female, odd = male).

## MudBlazor Specifics
- **MudBlazor v9 breaking changes**: `ActivatorContent` on `MudFileUpload` is replaced by `CustomContent` with explicit `Context` and `OnClick="@context.OpenFilePickerAsync"`. `MudForm.Validate()` is replaced by `ValidateAsync()`. `ShowMessageBox` is replaced by `ShowMessageBoxAsync`. `MudTabs.PanelClass` is renamed to `TabPanelsClass`.
- The `MudDatePicker` week should start on Monday (Swedish locale). Set `CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("sv-SE")` in `Program.cs`, as `UseRequestLocalization` only applies to the initial HTTP request, not Blazor Server SignalR circuit threads.

## Domain Terminology (Swedish → English)
| Swedish | English |
|---|---|
| Scoutkår | Scout group |
| Avdelning | Troop |
| Termin | Semester (VT = spring, HT = autumn) |
| Sammankomst | Meeting/gathering |
| Närvarokort | Attendance card |
| Deltagare | Participant |
| Ledare | Leader |
| Märke | Badge |
| Personnummer | Swedish personal identity number |
| Aktivitetsbidrag | Activity grant |
| Patrull | Patrol (subgroup within a troop) |

## Scoutnet Unit Type Mapping
- Complete Scoutnet unit_type (troop_type) mapping:
  - `0` = empty
  - `1` = Bäverscouter
  - `2` = Spårarscouter
  - `3` = Upptäckarscouter
  - `4` = Äventyrarscouter
  - `5` = Utmanarscouter
  - `6` = Roverscouter
  - `7` = Annat
  - `8` = Familjescouter
