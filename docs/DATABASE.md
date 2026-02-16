# Database Management Guide

This guide covers how to manage the PostgreSQL database for Skojjt.

## Overview

| Component | Technology |
|-----------|------------|
| Database | PostgreSQL 18 |
| ORM | Entity Framework Core 10 |
| Migrations | EF Core Code-First |
| Local Dev | Docker (port 5433) |

## Quick Reference

```powershell
# Start database
docker-compose up -d postgres

# Apply migrations
dotnet ef database update --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web

# Create new migration
dotnet ef migrations add MigrationName --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web

# Reset database (single line)
docker-compose down -v; docker-compose up -d postgres; Start-Sleep -Seconds 10; dotnet ef database update --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

---

## Local Development Setup

### Prerequisites

- Docker Desktop
- .NET 10 SDK
- EF Core tools (version 10+): 
  ```powershell
  dotnet tool install --global dotnet-ef --version 10.0.1
  ```
  
  To update existing tools:
  ```powershell
  dotnet tool update --global dotnet-ef
  ```

### Start PostgreSQL

```powershell
cd C:\src\skojjt-v2
docker-compose up -d postgres
```

This starts PostgreSQL on **port 5433** (to avoid conflicts with any local PostgreSQL installation on 5432).

### Verify Connection

```powershell
docker exec skojjt-postgres psql -U skojjt -d skojjt -c "SELECT version();"
```

### Connection String

Development connection string (in `appsettings.Development.json`):

```
Host=localhost;Port=5433;Database=skojjt;Username=skojjt;Password=dev_password_123
```

---

## Entity Framework Core Migrations

### View Current Migrations

```powershell
dotnet ef migrations list --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

### Create a New Migration

After modifying entity classes or configurations:

```powershell
dotnet ef migrations add <MigrationName> --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

Example:
```powershell
dotnet ef migrations add AddPersonPhotoUrl --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

### Apply Migrations

```powershell
dotnet ef database update --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

### Apply Specific Migration

```powershell
dotnet ef database update <MigrationName> --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

### Rollback Migration

Roll back to a previous migration:

```powershell
dotnet ef database update <PreviousMigrationName> --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

### Remove Last Migration

Remove the last migration (if not applied):

```powershell
dotnet ef migrations remove --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

### Generate SQL Script

Generate SQL without applying:

```powershell
dotnet ef migrations script --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web -o migration.sql
```

Generate idempotent script (safe to run multiple times):

```powershell
dotnet ef migrations script --idempotent --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web -o migration.sql
```

---

## Database Operations

### Connect with psql

```powershell
docker exec -it skojjt-postgres psql -U skojjt -d skojjt
```

### Common psql Commands

| Command | Description |
|---------|-------------|
| `\dt` | List all tables |
| `\d tablename` | Describe table structure |
| `\di` | List all indexes |
| `\q` | Quit psql |
| `\x` | Toggle expanded display |

### View Table Structure

```powershell
docker exec skojjt-postgres psql -U skojjt -d skojjt -c "\d persons"
```

### Count Records

```powershell
docker exec skojjt-postgres psql -U skojjt -d skojjt -c "SELECT 'persons' as tbl, COUNT(*) FROM persons UNION ALL SELECT 'troops', COUNT(*) FROM troops UNION ALL SELECT 'meetings', COUNT(*) FROM meetings ORDER BY tbl;"
```

### View Foreign Keys

```sql
SELECT
    tc.table_name, 
    kcu.column_name, 
    ccu.table_name AS foreign_table_name,
    ccu.column_name AS foreign_column_name 
FROM information_schema.table_constraints AS tc 
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
JOIN information_schema.constraint_column_usage AS ccu
    ON ccu.constraint_name = tc.constraint_name
WHERE tc.constraint_type = 'FOREIGN KEY';
```

---

## Reset Database

### Full Reset (Delete All Data)

```powershell
docker-compose down -v
docker-compose up -d postgres
Start-Sleep -Seconds 10
dotnet ef database update --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

### Truncate All Tables (Keep Schema)

```sql
TRUNCATE TABLE 
    meeting_attendances,
    badge_parts_done,
    badges_completed,
    troop_badges,
    troop_persons,
    meetings,
    badges,
    badge_templates,
    troops,
    persons,
    users,
    scout_groups,
    semesters
CASCADE;
```

---

## pgAdmin (Optional)

A web-based database management UI is available:

```powershell
docker-compose up -d pgadmin
```

Access at: http://localhost:5050
- Email: `admin@local.dev`
- Password: `admin`

To connect to the database in pgAdmin:
- Host: `postgres` (container name)
- Port: `5432`
- Database: `skojjt`
- Username: `skojjt`
- Password: `dev_password_123`

---

## Backup & Restore

### Create Backup

```powershell
docker exec skojjt-postgres pg_dump -U skojjt skojjt > backup_$(Get-Date -Format "yyyyMMdd_HHmmss").sql
```

### Restore Backup

```powershell
docker exec -i skojjt-postgres psql -U skojjt -d skojjt < backup_file.sql
```

### Backup to Compressed Format

```powershell
docker exec skojjt-postgres pg_dump -U skojjt -Fc skojjt > backup.dump
```

### Restore from Compressed Format

```powershell
docker exec -i skojjt-postgres pg_restore -U skojjt -d skojjt < backup.dump
```

---

## Schema Overview

### Core Tables

| Table | Description | Primary Key |
|-------|-------------|-------------|
| `semesters` | Academic periods (VT/HT) | `{year}-{0\|1}` |
| `scout_groups` | Scout organizations | Scoutnet ID |
| `persons` | Individual scouts/leaders | Member number |
| `troops` | Scout troops per semester | `{scoutnet_id}.{semester_id}` |
| `troop_persons` | Troop membership | (troop_id, person_id) |
| `meetings` | Scout meetings/events | `{troop_id}.{date}` |
| `meeting_attendances` | Attendance records | (meeting_id, person_id) |
| `users` | System users | ScoutID |

### Badge Tables

| Table | Description | Primary Key |
|-------|-------------|-------------|
| `badges` | Badge definitions | Auto-increment |
| `badge_templates` | Reusable badge templates | Auto-increment |
| `troop_badges` | Badges assigned to troops | (troop_id, badge_id) |
| `badge_parts_done` | Completed badge parts | Composite |
| `badges_completed` | Fully earned badges | (person_id, badge_id) |

### Entity Relationships

```
scout_groups
    ??? persons (many)
    ??? troops (many)
    ??? badges (many)
    ??? users (many)

semesters
    ??? troops (many)

troops
    ??? troop_persons (many) ? persons
    ??? meetings (many)
    ??? troop_badges (many) ? badges

meetings
    ??? meeting_attendances (many) ? persons

badges
    ??? badge_parts_done (many) ? persons
    ??? badges_completed (many) ? persons
```

---

## Troubleshooting

### Connection Refused

1. Check if container is running: `docker ps`
2. Check logs: `docker logs skojjt-postgres`
3. Verify port: `docker-compose port postgres 5432`

### Authentication Failed

Verify credentials match `docker-compose.yml`:
- Username: `skojjt`
- Password: `dev_password_123`
- Database: `skojjt`

### Migration Conflicts

If migrations are out of sync:

```powershell
# Remove all migrations
Remove-Item -Path "src/Skojjt.Infrastructure/Data/Migrations/*.cs" -Force

# Reset database and create fresh migration
docker-compose down -v
docker-compose up -d postgres
Start-Sleep -Seconds 10
dotnet ef migrations add InitialCreate --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
dotnet ef database update --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

### Port Already in Use

If port 5433 is in use:

1. Find process: `Get-NetTCPConnection -LocalPort 5433`
2. Change port in `docker-compose.yml` and `appsettings.Development.json`

---

## Production Considerations

### Connection Pooling

Use connection pooling in production:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=...;Database=skojjt;Username=...;Password=...;Pooling=true;MinPoolSize=5;MaxPoolSize=100"
  }
}
```

### SSL/TLS

Enable SSL for production:

```
Host=...;Database=skojjt;Username=...;Password=...;SSL Mode=Require;Trust Server Certificate=false
```

### Migrations in Production

Never run `dotnet ef database update` directly in production. Instead:

1. Generate SQL script: `dotnet ef migrations script --idempotent`
2. Review the script
3. Apply via controlled deployment process
4. Use a migration tool or CI/CD pipeline

### Backups

- Daily automated backups
- Point-in-time recovery enabled
- Test restore procedures regularly
- Store backups in separate location/region
