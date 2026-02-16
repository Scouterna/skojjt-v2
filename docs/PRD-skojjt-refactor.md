# Product Requirements Document (PRD)
# Skojjt V2 Platform Refactoring

**Version:** 2.0  
**Date:** December 22, 2025  
**Author:** Development Team  
**Status:** Draft  

---

## 1. Executive Summary

### 1.1 Project Overview
Skojjt V2 is a complete rewrite of the scout attendance tracking system. V1 runs on Google App Engine with Python 3.11 (source code preserved in `v1/` folder). This PRD outlines the requirements for the V2 platform to modernize the technology stack, improve maintainability, and enable deployment flexibility.

### 1.2 Goals
- Migrate from Python/Flask (V1) to C#/.NET 10 (V2)
- Replace Google Cloud Datastore with PostgreSQL
- Modernize the frontend with Blazor Web App
- Migrate authentication from Google Auth to ScoutID
- Enable Kubernetes deployment
- Improve test coverage

### 1.3 Success Metrics
| Metric | Target |
|--------|--------|
| Code test coverage | ≥80% |
| Page load time | <2 seconds |
| API response time | <500ms |
| System uptime | 99.9% |
| Successful data migration | 100% |

---

## 2. V1 System Analysis

### 2.1 V1 Technology Stack
| Component | V1 Technology |
|-----------|-------------------|
| Backend | Python 3.11, Flask |
| Database | Google Cloud Datastore (ndb) |
| Frontend | Jinja2 templates, jQuery, Bootstrap |
| Authentication | Google Users API |
| Hosting | Google App Engine |
| Background Tasks | GAE Task Queues |

### 2.2 V1 Data Model
The V1 system uses Google Cloud Datastore with the following core entities:

#### Core Entities
- **ScoutGroup** (Kår) - Scout group organization
  - Properties: name, scoutnetID, organisationsnummer, foreningsID, kommunID, API keys, contact info, attendance settings
  
- **Semester** - Time period (VT/HT + year)
  - Properties: year, ht (boolean)

- **Troop** (Avdelning) - Scout troop per semester
  - Relations: ScoutGroup, Semester
  - Properties: name, scoutnetID, defaultstarttime, defaultduration

- **Person** - Individual scout/leader
  - Relations: ScoutGroup
  - Properties: firstname, lastname, birthdate, personnr, member_no, contact info (email, phone, mobile), parent info, address, patrol, removed flag

- **TroopPerson** - Person membership in troop
  - Relations: Troop, Person
  - Properties: leader (boolean)

- **Meeting** - Meeting/event
  - Relations: Troop, list of Person keys (attendingPersons)
  - Properties: datetime, name, duration, ishike

- **UserPrefs** - System user preferences
  - Relations: ScoutGroup (groupaccess), Semester (activeSemester)
  - Properties: email, name, hasaccess, hasadminaccess

#### Badge System Entities
- **Badge** - Badge definition per scout group
  - Relations: ScoutGroup
  - Properties: name, description, parts_scout_short/long, parts_admin_short/long, img_url

- **BadgePartDone** - Completed badge part
  - Relations: Person, Badge
  - Properties: idx, date, examiner_name, is_scout_part

- **BadgeCompleted** - Fully completed badge
  - Relations: Person, Badge
  - Properties: date, examiner

- **TroopBadge** - Badges assigned to a troop
  - Relations: Troop, Badge

- **BadgeTemplate** - Reusable badge templates

### 2.3 V1 Features

#### Authentication & Authorization
- Google account login
- User access levels: none, group access, admin access
- Per-scout-group permissions

#### Core Functionality
1. **Attendance Tracking**
   - Register attendance per person per meeting
   - Meeting management (create, edit, delete)
   - Bulk attendance toggle (all/none/same as yesterday/same as last meeting)

2. **Member Management**
   - Sync members from Scoutnet API
   - Add persons to waiting list in Scoutnet
   - Person profile management
   - Troop assignment

3. **Badge System**
   - Create/manage badges per scout group
   - Track badge progress (scout parts + admin parts)
   - Badge templates for reuse
   - Badge completion tracking

4. **Reporting**
   - DAK (Digitalt Aktivitetskort) XML export
   - Excel reports (Gothenburg format)
   - Excel reports (Stockholm format)
   - Sensus attendance lists
   - Camp subsidy reports (Lagerbidrag) - Gothenburg & Stockholm
   - JSON export
   - Group summary statistics

5. **Administration**
   - Scout group settings management
   - User access management
   - Data import/merge tools
   - Semester management

### 2.4 External Integrations
- **Scoutnet API** - Member data import, waiting list management
- **Sensus** - Study association reporting
- **Municipality systems** - Attendance reporting (DAK format)

---

## 3. Target Architecture

### 3.1 V2 Technology Stack
| Component | V2 Technology |
|-----------|---------------|
| Backend | C# / .NET 10 |
| API | ASP.NET Core Web API |
| Database | PostgreSQL 18 |
| ORM | Entity Framework Core 10 |
| Frontend | Blazor Web App (.NET 10) |
| UI Framework | MudBlazor (mobile-first responsive design) |
| Authentication | ScoutID (OAuth 2.0 / OIDC) |
| Hosting | Kubernetes |
| CI/CD | GitHub Actions |
| Containerization | Docker |

#### UI Design Principles
- **Mobile-first approach:** Design for mobile screens first, then enhance for larger displays
- **Progressive enhancement:** Core functionality works on all devices, advanced features on capable devices
- **Touch-friendly:** All interactive elements sized for finger input (minimum 44x44px)
- **Responsive breakpoints:** xs (<600px), sm (600-960px), md (960-1280px), lg (1280-1920px), xl (>1920px)

### 3.2 Development Environment

| Component | Tool/Version |
|-----------|-------------|
| OS | Windows 11 |
| IDE | Visual Studio 2026 |
| Runtime | .NET 10 SDK |
| Database (local) | PostgreSQL 18 (Docker or native) |
| Container Runtime | Docker Desktop for Windows |
| Git | Git for Windows |

#### Visual Studio 2026 Setup

**Required Workloads:**
- ASP.NET and web development
- .NET desktop development
- Container development tools

**Recommended Extensions:**
- GitHub Copilot
- PostgreSQL (npgsql) tools

**Recommended NuGet Packages:**
- MudBlazor (UI component library)
- Microsoft.AspNetCore.Authentication.OpenIdConnect (ScoutID)
- Npgsql.EntityFrameworkCore.PostgreSQL

**Solution Structure:**
```
Skojjt.sln
├── src/
│   ├── Skojjt.Web/              # Blazor Web App (frontend + API)
│   ├── Skojjt.Core/             # Domain models, interfaces
│   ├── Skojjt.Infrastructure/   # EF Core, external services
│   └── Skojjt.Shared/           # Shared DTOs (optional, for API clients)
├── tests/
│   ├── Skojjt.Web.Tests/        # Integration tests
│   ├── Skojjt.Core.Tests/       # Unit tests
│   └── Skojjt.Infrastructure.Tests/
├── docker-compose.yml           # Local PostgreSQL, etc.
├── docker-compose.override.yml  # Dev-specific overrides
└── README.md
```

#### Local Development Setup

```powershell
# 1. Install prerequisites (using winget)
winget install -e --id Microsoft.VisualStudio.2026.Professional
winget install Microsoft.DotNet.SDK.10
winget install Microsoft.DotNet.DesktopRuntime.10
winget install Microsoft.DotNet.AspNetCore.10
winget install -e --id Docker.DockerDesktop

# 2. Clone repository
git clone https://github.com/Scouterna/skojjt-v2.git
cd skojjt-v2

# 3. Start local PostgreSQL (via Docker)
docker-compose up -d postgres

# 4. Apply database migrations
dotnet ef database update --project src/Skojjt.Infrastructure

# 5. Run the Blazor Web App (includes API)
dotnet run --project src/Skojjt.Web

# Or open Skojjt.sln in Visual Studio 2026 and press F5
```

#### docker-compose.yml (for local development)

```yaml
services:
  postgres:
    image: postgres:18-alpine
    container_name: skojjt-postgres
    environment:
      POSTGRES_DB: skojjt
      POSTGRES_USER: skojjt
      POSTGRES_PASSWORD: dev_password_123
    ports:
      - "5433:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  # Optional: pgAdmin for database management
  pgadmin:
    image: dpage/pgadmin4
    container_name: skojjt-pgadmin
    environment:
      PGADMIN_DEFAULT_EMAIL: admin@local.dev
      PGADMIN_DEFAULT_PASSWORD: admin
    ports:
      - "5050:80"
    depends_on:
      - postgres

volumes:
  postgres_data:
```

#### appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=skojjt;Username=skojjt;Password=dev_password_123"
  },
  "ScoutId": {
    "Authority": "https://test.scoutid.se",
    "ClientId": "skojjt-dev",
    "ClientSecret": "dev-secret"
  },
  "Scoutnet": {
    "BaseUrl": "https://www.scoutnet.se"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

### 3.3 Production Architecture

Blazor Web App combines frontend and API in a single deployable unit, simplifying the architecture:

```
┌─────────────────────────────────────────────────────────────────┐
│                         Load Balancer                            │
│                      (Ingress Controller)                        │
└─────────────────────────┬───────────────────────────────────────┘
                          │
         ┌────────────────┼────────────────┐
         │                │                │
         ▼                ▼                ▼
┌─────────────┐  ┌─────────────┐  ┌─────────────┐
│   Blazor    │  │   Blazor    │  │   Blazor    │
│   Web App   │  │   Web App   │  │   Web App   │
│   (Pod)     │  │   (Pod)     │  │   (Pod)     │
└─────────────┘  └─────────────┘  └─────────────┘
         │                │                │
         └────────────────┼────────────────┘
                          │
                          ▼
┌─────────────────────────────────────────────────────────────────┐
│                      PostgreSQL                                 │
│                   (StatefulSet/Cloud)                           │
└─────────────────────────────────────────────────────────────────┘
```

**Blazor Rendering Mode:** Interactive Server (SignalR) with optional WebAssembly for specific components.

### 3.4 Database Schema (PostgreSQL)

#### ID Strategy
The database uses **deterministic IDs** based on natural keys from the data, rather than random UUIDs. This approach:
- Simplifies data migration from V1
- Makes IDs predictable and human-readable
- Ensures idempotent inserts (same data = same ID)
- Facilitates debugging and data reconciliation

| Table               | ID Type           | ID Format                                | Example                                      |
|---------------------|-------------------|------------------------------------------|----------------------------------------------|
| semesters           | `INTEGER`         | `(year * 10) + (isAutumn ? 1 : 0)`       | `20251` (autumn 2025), `20250` (spring 2025) |
| scout_groups        | `INTEGER`         | Scoutnet group ID                        | `12345`                                      |
| troops              | `VARCHAR(50)`     | `{scoutnet_troop_id}.{semester_id}`      | `18309.20251`                                |
| persons             | `INTEGER`         | Scoutnet member number                   | `123456`                                     |
| meetings            | `VARCHAR(100)`    | `{troop_id}.{YYYY-MM-DD}`                | `18309.20251.2025-03-15`                     |
| meeting_attendances | Composite PK      | `(meeting_id, person_id)`                | `('18309.20251.2025-03-15', 123456)`         |
| users               | `VARCHAR(255)`    | ScoutID identifier                       | `user@example.com`                           |
| badges              | `SERIAL`          | Auto-increment                           | `1`, `2`, `3`                                |
| badge_templates     | `SERIAL`          | Auto-increment                           | `1`, `2`, `3`                                |

**Note:** The `troops` table uses a composite string key that includes the Scoutnet troop ID and semester ID (integer), separated by `.`. This format ensures uniqueness across semesters while maintaining human-readable IDs and avoiding confusion with date separators.

**Note:** Junction tables (`meeting_attendances`, `troop_persons`, `troop_badges`, `badge_parts_done`, `badges_completed`) use **composite primary keys** rather than synthetic IDs. This ensures data integrity and prevents duplicate entries at the database level.

#### Core Tables

```sql
-- Semesters
-- ID format: (year * 10) + (1 if autumn, 0 if spring)
-- e.g., 20251 for autumn 2025, 20250 for spring 2025
CREATE TABLE semesters (
    id INTEGER PRIMARY KEY,  -- e.g., 20251 (autumn) or 20250 (spring)
    year INTEGER NOT NULL,
    is_autumn BOOLEAN NOT NULL,  -- 1 = HT (autumn), 0 = VT (spring)
    CONSTRAINT chk_semester_id CHECK (id = (year * 10) + CASE WHEN is_autumn THEN 1 ELSE 0 END)
);

-- Scout Groups (Kår)
-- ID is the Scoutnet group ID (integer)
CREATE TABLE scout_groups (
    id INTEGER PRIMARY KEY,  -- Scoutnet group ID
    name VARCHAR(255) NOT NULL,
    organisation_number VARCHAR(20),
    association_id VARCHAR(50),
    municipality_id VARCHAR(10),  -- Required for DAK export, must be configured in settings
    api_key_waitinglist VARCHAR(255),
    api_key_all_members VARCHAR(255),
    bank_account VARCHAR(50),
    address VARCHAR(255),
    postal_address VARCHAR(255),
    email VARCHAR(255),
    phone VARCHAR(50),
    default_location VARCHAR(255),
    signatory VARCHAR(255),
    signatory_phone VARCHAR(50),
    signatory_email VARCHAR(255),
    attendance_min_year INTEGER DEFAULT 10,
    attendance_incl_hike BOOLEAN DEFAULT TRUE,
);

-- Troops (Avdelning)
CREATE TABLE troop (
    id SERIAL PRIMARY KEY,
    scoutnet_troop_id INTEGER,  -- Scoutnet troop ID (numeric part)
    semester_id INTEGER NOT NULL REFERENCES semesters(id),
    scout_group_id INTEGER NOT NULL REFERENCES scout_groups(id),
    name VARCHAR(255) NOT NULL,
    default_start_time TIME DEFAULT '18:30',
    default_duration_minutes INTEGER DEFAULT 90,
    is_locked BOOLEAN DEFAULT FALSE,  -- Prevents edits after reporting
);

-- Persons
-- ID is the Scoutnet member number (always unique)
-- Note: Persons can belong to multiple scout groups via person_scout_groups
CREATE TABLE persons (
    id INTEGER PRIMARY KEY,  -- Scoutnet member number
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    birth_date DATE,
    personal_number VARCHAR(15),
    email VARCHAR(255),
    phone VARCHAR(50),
    mobile VARCHAR(50),
    alt_email VARCHAR(255),
    mum_name VARCHAR(255),
    mum_email VARCHAR(255),
    mum_mobile VARCHAR(50),
    dad_name VARCHAR(255),
    dad_email VARCHAR(255),
    dad_mobile VARCHAR(50),
    street VARCHAR(255),
    zip_code VARCHAR(20),
    zip_name VARCHAR(100),
    group_roles TEXT,
    member_years INTEGER[],
    not_in_scoutnet BOOLEAN DEFAULT FALSE,
    removed BOOLEAN DEFAULT FALSE,
);

-- Person Scout Group membership
-- Tracks which scout groups a person belongs to (independent of troop assignment)
-- Composite primary key: person + scout_group
CREATE TABLE scout_group_persons (
    person_id INTEGER NOT NULL REFERENCES persons(id) ON DELETE CASCADE,
    scout_group_id INTEGER NOT NULL REFERENCES scout_groups(id) ON DELETE CASCADE,
    PRIMARY KEY (person_id, scout_group_id)
);

-- Troop Person membership
-- Composite primary key: troop + person
-- Patrol is per troop membership, not per person
CREATE TABLE troop_persons (
    troop_id VARCHAR(50) NOT NULL REFERENCES troops(id) ON DELETE CASCADE,
    person_id INTEGER NOT NULL REFERENCES persons(id),
    is_leader BOOLEAN DEFAULT FALSE,
    patrol VARCHAR(100),  -- Patrol assignment for this troop
    PRIMARY KEY (troop_id, person_id)
);

-- Meetings
-- ID format: "{troop_id}.{YYYY-MM-DD}" - only one meeting per troop per day
CREATE TABLE meetings (
    id VARCHAR(64) PRIMARY KEY,  -- e.g., "18309.20251.2025-03-15"
    troop_id VARCHAR(50) NOT NULL REFERENCES troops(id) ON DELETE CASCADE,
    meeting_date DATE NOT NULL,
    start_time TIME NOT NULL DEFAULT '18:30',
    name VARCHAR(255) NOT NULL,
    duration_minutes INTEGER DEFAULT 90,
    is_hike BOOLEAN DEFAULT FALSE,
    UNIQUE(troop_id, meeting_date)
);

-- Meeting Attendance (junction table)
-- Composite primary key: meeting + person
-- Denormalized columns for fast statistics queries
CREATE TABLE meeting_attendances (
    meeting_id VARCHAR(64) NOT NULL REFERENCES meetings(id) ON DELETE CASCADE,
    person_id INTEGER NOT NULL REFERENCES persons(id),
    troop_id VARCHAR(50) NOT NULL,  -- Denormalized from meeting
    scoutnet_id INTEGER NOT NULL,    -- Troop's scoutnet ID
    scout_group_id INTEGER NOT NULL, -- Denormalized for fast group stats
    semester_id INTEGER NOT NULL,    -- Denormalized for fast semester stats
    PRIMARY KEY (meeting_id, person_id)
);

-- Users
-- ID is the ScoutID identifier
CREATE TABLE users (
    id VARCHAR(255) PRIMARY KEY,  -- ScoutID identifier
    email VARCHAR(255) NOT NULL UNIQUE,
    name VARCHAR(255),
    scout_group_id INTEGER REFERENCES scout_groups(id),
    active_semester_id INTEGER REFERENCES semesters(id),
    has_access BOOLEAN DEFAULT FALSE,
    is_admin BOOLEAN DEFAULT FALSE,
);

-- Badges
-- ID is auto-generated sequence (no natural key available)
CREATE TABLE badges (
    id SERIAL PRIMARY KEY,
    scout_group_id INTEGER NOT NULL REFERENCES scout_groups(id),
    name VARCHAR(255) NOT NULL,
    description TEXT,
    parts_scout_short TEXT[],
    parts_scout_long TEXT[],
    parts_admin_short TEXT[],
    parts_admin_long TEXT[],
    image_url VARCHAR(500),
    UNIQUE(scout_group_id, name)
);

-- Badge Parts Done
-- Composite primary key: person + badge + part_index + is_scout_part
CREATE TABLE badge_parts_done (
    person_id INTEGER NOT NULL REFERENCES persons(id),
    badge_id INTEGER NOT NULL REFERENCES badges(id) ON DELETE CASCADE,
    part_index INTEGER NOT NULL,
    is_scout_part BOOLEAN NOT NULL,
    examiner_name VARCHAR(255),
    completed_date DATE NOT NULL DEFAULT CURRENT_DATE,
    PRIMARY KEY (person_id, badge_id, part_index, is_scout_part)
);

-- Badge Completed
-- Composite primary key: person + badge
CREATE TABLE badges_completed (
    person_id INTEGER NOT NULL REFERENCES persons(id),
    badge_id INTEGER NOT NULL REFERENCES badges(id) ON DELETE CASCADE,
    examiner VARCHAR(255),
    completed_date DATE NOT NULL DEFAULT CURRENT_DATE,
    PRIMARY KEY (person_id, badge_id)
);

-- Troop Badges (junction)
-- Composite primary key: troop + badge
CREATE TABLE troop_badges (
    troop_id VARCHAR(50) NOT NULL REFERENCES troops(id) ON DELETE CASCADE,
    badge_id INTEGER NOT NULL REFERENCES badges(id) ON DELETE CASCADE,
    sort_order INTEGER,
    PRIMARY KEY (troop_id, badge_id)
);

-- Badge Templates
-- ID is auto-generated sequence (no natural key available)
CREATE TABLE badge_templates (
    id SERIAL PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    description TEXT,
    parts_scout_short TEXT[],
    parts_scout_long TEXT[],
    parts_admin_short TEXT[],
    parts_admin_long TEXT[],
    image_url VARCHAR(500),
);
