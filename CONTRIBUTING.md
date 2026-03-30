# Contributing to Skojjt

Thank you for your interest in contributing to Skojjt! This document provides guidelines and information to help you get started.

Skojjt is an attendance tracking system for Swedish scout groups (scoutkårer). The **UI is in Swedish**, but all code identifiers, comments, and documentation are in **English**.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 18](https://www.postgresql.org/) (or use Docker)
- An editor that respects `.editorconfig` (Visual Studio 2026+, VS Code with C# Dev Kit, Rider)
- (Optional) [Docker](https://www.docker.com/) for running PostgreSQL locally

## Getting Started

See the [README](README.md) for setup instructions. A few additional notes for contributors:

- **Never commit secrets.** Use `appsettings.Development.json` (git-ignored) for local settings.
- **Local authentication:** Use the `FallbackAuth` configuration for development without ScoutID.
- **Database migrations:** Run `dotnet ef database update --project src/Skojjt.Web` after pulling changes.

## Architecture

Skojjt follows **Clean Architecture** with three main layers:

| Project | Role | Rules |
|---|---|---|
| `Skojjt.Core` | Domain entities, interfaces, service contracts | **No external dependencies** |
| `Skojjt.Infrastructure` | EF Core repositories, external services | Implements interfaces from Core |
| `Skojjt.Web` | Blazor Server application (MudBlazor UI) | Depends on Core and Infrastructure |
| `Skojjt.Shared` | Shared DTOs and utilities | Used across layers |

**Key architectural rules:**

- Interfaces are defined in `Skojjt.Core`; implementations live in `Skojjt.Infrastructure`.
- Use `IDbContextFactory<SkojjtDbContext>` — never inject `DbContext` directly (Blazor Server concurrency).
- When using explicit transactions, wrap them in `context.Database.CreateExecutionStrategy().ExecuteAsync(…)`.
- All UI components must use **MudBlazor** (v8) — no raw HTML or Bootstrap.

## Coding Standards

The project enforces strict coding standards. `TreatWarningsAsErrors` is enabled — your code must compile with **zero warnings**.

| Rule | Detail |
|---|---|
| Nullable reference types | Enabled globally — handle nulls explicitly |
| File-scoped namespaces | `namespace Foo;` not `namespace Foo { }` |
| Collection expressions | Prefer `[]` over `new List<T>()` |
| Empty strings | Use `string.Empty` over `""` |
| Indentation | 4 spaces, no tabs |
| `var` usage | Explicit types preferred (see `.editorconfig`) |
| UI text | All user-facing strings in **Swedish** |
| Code identifiers | All code identifiers in **English** |

The full coding conventions are enforced by the `.editorconfig` at the repository root.

## Branching & Pull Requests

1. Create a feature branch from `main` (e.g., `feature/badge-export` or `fix/attendance-save`).
2. Make your changes in small, focused commits.
3. Ensure all tests pass (`dotnet test`) and the build succeeds (`dotnet build`) before pushing.
4. Open a pull request against `main` with a clear description of what and why.

### Commit Messages

Use [Conventional Commits](https://www.conventionalcommits.org/) format:

```
feat: add Excel export for attendance cards
fix: correct semester ID calculation for spring terms
docs: update DAK export section in README
chore: bump MudBlazor to v8.1
```

Reference issue numbers where applicable (e.g., `fix: correct date format (#42)`).

## Testing

- **Framework:** MSTest (`[TestClass]` / `[TestMethod]`) — not xUnit or NUnit.
- **Test projects:** `Skojjt.Core.Tests`, `Skojjt.Infrastructure.Tests`, `Skojjt.Web.Tests`.
- Tests run in parallel.
- All new features should include tests.
- All bug fixes should include a regression test.

Run the full test suite before submitting a PR:

```bash
dotnet test
```

## Database Migrations

To create a new EF Core migration:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Skojjt.Infrastructure \
  --startup-project src/Skojjt.Web
```

Migration guidelines:

- Use a descriptive name (e.g., `AddBadgeProgressTable`).
- Review the generated migration carefully before committing.
- **Never edit existing migrations** that have been applied in production.

## Authorization Model

Skojjt uses a two-level authorization model. If your changes touch access control, understand both levels:

- **Group-level:** Users access only scout groups in their `AccessibleGroupIds`. Check with `ICurrentUserService.HasGroupAccess(scoutGroupId)`.
- **Troop-level:** Within a group, users see only troops they have explicit role claims for. Check with `ICurrentUserService.HasTroopAccess(scoutGroupId, troopScoutnetId)`.

## Domain Terminology

The UI is in Swedish. Here is a reference for key domain terms:

| Swedish | English |
|---|---|
| Scoutkår | Scout group |
| Avdelning | Troop |
| Termin | Semester (VT = spring, HT = autumn) |
| Sammankomst | Meeting / gathering |
| Närvarokort | Attendance card |
| Deltagare | Participant |
| Ledare | Leader |
| Märke | Badge |
| Personnummer | Swedish personal identity number |
| Aktivitetsbidrag | Activity grant |
| Patrull | Patrol (subgroup within a troop) |
| DAK | Digitalt Aktivitetskort (digital activity card) |

## Reporting Issues

When reporting a bug, please include:

- Steps to reproduce
- Expected vs. actual behavior
- Browser and OS (if UI-related)
- Relevant log output (if available)

## License

By contributing to Skojjt, you agree that your contributions will be licensed under the [Apache License 2.0](LICENSE).
