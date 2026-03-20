# Skojjt Data Migration Scripts

This directory contains scripts for migrating data from Google Cloud Datastore to PostgreSQL.

## Quick Start - Import Existing Data

If you already have the exported JSON files in `json_export/`, follow these steps:

### 1. Start PostgreSQL

Run from the root of the repository:

```powershell
docker-compose up -d postgres
```

### 2. Apply Database Migrations

```powershell
dotnet ef database update --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

### 3. Start the Application

```powershell
dotnet run --project src/Skojjt.Web
```

Wait until you see: `Now listening on: http://localhost:5286`

### 4. Run the Import

In a **separate terminal**:

**curl (recommended — shows live progress):**
```bash
curl -N -X POST "http://localhost:5286/api/v1/admin/migrate"
```

**PowerShell (live progress):**
```powershell
$response = Invoke-WebRequest -Method Post -Uri "http://localhost:5286/api/v1/admin/migrate" -Headers @{"Accept"="text/event-stream"} -HttpVersion 1.1 -TimeoutSec 600
$response.Content -split "`n" | Where-Object { $_ -match "^data:" } | ForEach-Object { $_ -replace "^data: ", "" }
```

**PowerShell (simple — waits for completion):**
```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5286/api/v1/admin/migrate"
```

The endpoint streams progress as Server-Sent Events, so with `curl -N` or the `Invoke-WebRequest` variant you'll see each import step as it completes.

> **Note:** `Invoke-RestMethod` buffers the entire response and only displays it after the migration finishes. Use `curl -N` or `Invoke-WebRequest` if you want live progress.

> **Note:** The endpoint defaults to `scripts/migration/json_export` relative to the solution root. To specify a custom directory, pass it in the request body:
> ```bash
> curl -N -X POST "http://localhost:5286/api/v1/admin/migrate" -H "Content-Type: application/json" -d '{"importDirectory":"C:/path/to/json_export"}'
> ```

### 5. Verify Import

```powershell
docker exec skojjt-postgres psql -U skojjt -d skojjt -c "SELECT 'semesters' as tbl, COUNT(*) FROM semesters UNION ALL SELECT 'scout_groups', COUNT(*) FROM scout_groups UNION ALL SELECT 'persons', COUNT(*) FROM persons UNION ALL SELECT 'troops', COUNT(*) FROM troops UNION ALL SELECT 'meetings', COUNT(*) FROM meetings ORDER BY tbl;"
```

---

## Full Migration Process

If you need to export fresh data from Datastore:

### Prerequisites

```bash
pip install google-cloud-datastore protobuf
```

### Option A: Direct Export from Live Datastore (Recommended)

```bash
gcloud auth application-default login
cd scripts/migration
python export_live.py --project skojjt --output-dir ./raw_export
python transform_data.py --input-dir ./raw_export --output-dir ./json_export
```

Then follow Quick Start steps 1-5 above.

### Option B: Use Managed Datastore Export

**Bash:**
```bash
gcloud storage buckets create gs://skojjt-migration-export --location=europe-north1 --project=skojjt
d=$(date +%Y%m%d)
gcloud datastore export "gs://skojjt-migration-export/$d" --project=skojjt
mkdir datastore_export
gcloud storage cp --recursive "gs://skojjt-migration-export/$d" ./datastore_export/
python convert_export.py --export-dir "./datastore_export/$d" --output-dir ./raw_export
python transform_data.py --input-dir ./raw_export --output-dir ./json_export
```

**PowerShell:**
```powershell
gcloud storage buckets create gs://skojjt-migration-export --location=europe-north1 --project=skojjt
$d = $(Get-Date -Format 'yyyyMMdd')
gcloud datastore export "gs://skojjt-migration-export/$d" --project=skojjt
mkdir datastore_export
gcloud storage cp --recursive gs://skojjt-migration-export/$d ./datastore_export/
python convert_export.py --export-dir ./datastore_export/$d --output-dir ./raw_export
python transform_data.py --input-dir ./raw_export --output-dir ./json_export
```

> **Note:** The variable `$d` contains the date for the export in the format `YYYYMMDD` (e.g., `20250715`).

---

## Data Format Notes

| ID Type | Format | Example |
|---------|--------|---------|
| Semester | `(year * 10) + (isAutumn ? 1 : 0)` | `20251` (autumn 2025), `20250` (spring 2025) |
| ScoutGroup | Scoutnet group ID (integer) | `12345` |
| Troop | `{scoutnet_troop_id}/{scout_group_id}/{semester_id}` | `18309/12345/20201` |
| Meeting | `{troop_id}-{YYYY-MM-DD}` | `18309/12345/20201-2020-10-15` |

### Troop ID Resolution

Old Datastore data sometimes has string-based troop IDs (from before Scoutnet integration). The transform script resolves these in three steps:

1. **Numeric** — If the raw ID is already numeric, use it directly.
2. **Name matching** — Find the same troop name in the same scout group from a different semester where it has a valid numeric Scoutnet ID.
3. **Reserved range** — For the few troops that can't be resolved, assign an ID from the reserved range **250–1000**. Real Scoutnet IDs are auto-increment starting well above 1000, so this range is safe.

---

## Troubleshooting

### Reset Database

```powershell
docker-compose down -v
docker-compose up -d postgres
Start-Sleep -Seconds 10
dotnet ef database update --project src/Skojjt.Infrastructure --startup-project src/Skojjt.Web
```

### Check PostgreSQL

```powershell
docker exec skojjt-postgres psql -U skojjt -d skojjt -c "\dt"
```

### Connection String

```
Host=localhost;Port=5433;Database=skojjt;Username=skojjt;Password=dev_password_123
```

### Connection Refused on Import

Make sure the web application is running in a separate terminal:
```powershell
dotnet run --project src/Skojjt.Web
```

The API is available at `http://localhost:5286` (not port 5000).

---

## Security Note

The `/api/v1/admin/migrate` endpoint is **only available in Development environment**.
