.markdown
# Copilot Instructions

## Project Overview
Skojjt is a **Blazor Server** attendance tracking system for Swedish scout groups (scoutkårer). It manages meeting attendance, troop/member management, badge tracking, and exports reports for municipal activity grants (aktivitetsbidrag / DAK). The UI is in **Swedish**.

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
- **MudBlazor** (v8) for all UI components. Use `MudBlazor` components, not raw HTML or Bootstrap.
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
- Users can **only** access scout groups listed in their `AccessibleGroupIds` (from ScoutID claims).
- Always check access with `ICurrentUserService.HasGroupAccess(scoutGroupId)` or `RequireGroupAccess(scoutGroupId)`.
- Admin users only have elevated access when admin mode is explicitly active (`IAdminModeService.IsAdminModeActive`).

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
- In the MudBlazor version used in this project, `MudAvatar` parameter casing should be lowercase `image` not `Image`.

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