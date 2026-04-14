# Skojjt

Skojjt is a web-based attendance tracking system for Swedish scout groups (_scoutkårer_). It allows troop leaders to register meeting attendance, manage members, and export reports for municipal activity grants (aktivitetsbidrag / DAK).

## Features

- **Attendance tracking** — Register attendance per meeting with auto-save and real-time sync across browsers.
- **Troop & member management** — Organize scouts into troops, assign patrols, and manage leader roles.
- **Hike / camp support** — Separate tracking for hikes and camps.
- **Badge management** — Assign and track badge progress per troop.
- **DAK export** — Export attendance data in the DAK XML format required by Swedish municipalities.
- **Excel & JSON export** — Additional export formats for reporting.
- **Scoutnet integration** — Import member data from Scoutnet.
- **ScoutID authentication** — Single sign-on via ScoutID (Keycloak-based OpenID Connect).

## Tech Stack

- **.NET 10** / C# 14
- **Blazor Server** (interactive SSR) with MudBlazor component library
- **Entity Framework Core** with PostgreSQL
- **Clean architecture** — `Skojjt.Core` (domain), `Skojjt.Infrastructure` (data/services), `Skojjt.Web` (UI)

## Project Structure

```
src/
  Skojjt.Core/            # Domain entities, interfaces, services
  Skojjt.Infrastructure/  # EF Core, repositories, external services
  Skojjt.Shared/          # Shared DTOs and utilities
  Skojjt.Web/             # Blazor Server application
tests/
  Skojjt.Core.Tests/
  Skojjt.Infrastructure.Tests/
  Skojjt.Web.Tests/
tools/
  Skojjt.ImportTool/      # CLI tool for data import
scripts/
  migration/              # Python scripts for migrating data from v1
docs/                     # Design documents and notes
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL](https://www.postgresql.org/) (or use Docker)
- (Optional) [Docker](https://www.docker.com/) for running PostgreSQL and Keycloak locally

## Getting Started

1. **Clone the repository:**
   ```bash
   git clone https://github.com/<your-org>/skojjt-v2.git
   cd skojjt-v2
   git submodule update --init --recursive
   ```

2. **Start PostgreSQL** (example using Docker):
   ```bash
   docker run -d --name skojjt-db \
     -e POSTGRES_USER=skojjt \
     -e POSTGRES_PASSWORD=dev_password_123 \
     -e POSTGRES_DB=skojjt \
     -p 5433:5432 \
     postgres:17
   ```

3. **Apply database migrations:**
   ```bash
   cd src/Skojjt.Web
   dotnet ef database update
   ```

4. **Run the application:**
   ```bash
   dotnet run --project src/Skojjt.Web
   ```
   The app will be available at `https://localhost:7224` (or `http://localhost:5286`).

## Configuration

Application settings are in `src/Skojjt.Web/appsettings.json`. Key sections:

| Section | Description |
|---------|-------------|
| `ConnectionStrings:DefaultConnection` | PostgreSQL connection string |
| `ScoutId` | OpenID Connect settings for ScoutID authentication |
| `Scoutnet` | Scoutnet API base URL |
| `FallbackAuth` | Optional fallback password authentication for development |

For local development, override settings in `appsettings.Development.json`.

## Running Tests

```bash
dotnet test
```

## Health Check

The application exposes a health check endpoint for Azure monitoring:

```
GET /healthz
```

This endpoint is **unauthenticated** and returns:

| Status | HTTP Code | Description |
|--------|-----------|-------------|
| `Healthy` | 200 | Application and database are reachable |
| `Unhealthy` | 503 | Database connectivity check failed |

Configure the Azure App Service health check probe to `https://<your-app>.azurewebsites.net/healthz`.

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
