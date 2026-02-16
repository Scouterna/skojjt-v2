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

**PowerShell:**
```powershell
Invoke-RestMethod -Method Post -Uri "http://localhost:5286/api/v1/admin/migrate" -ContentType "application/json" -Body '{"importDirectory":"C:\\src\\skojjt-v2\\scripts\\migration\\json_export"}'
```

**curl:**
```bash
curl -X POST "http://localhost:5286/api/v1/admin/migrate" -H "Content-Type: application/json" -d "{\"importDirectory\":\"C:\\\\src\\\\skojjt-v2\\\\scripts\\\\migration\\\\json_export\"}"
```

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

```bash
gsutil mb -l europe-north1 -p skojjt gs://skojjt-migration-export
gcloud datastore export gs://skojjt-migration-export/$(date +%Y%m%d) --project=skojjt
gsutil -m cp -r gs://skojjt-migration-export/YYYYMMDD ./datastore_export/
python convert_export.py --export-dir ./datastore_export/YYYYMMDD --output-dir ./raw_export
python transform_data.py --input-dir ./raw_export --output-dir ./json_export
```

---

## Data Format Notes

| ID Type | Format | Example |
|---------|--------|---------|
| Semester | `(year * 10) + (isAutumn ? 1 : 0)` | `20251` (autumn 2025), `20250` (spring 2025) |
| ScoutGroup | Scoutnet group ID (integer) | `12345` |
| Troop | `{scoutnet_troop_id}/{scout_group_id}/{semester_id}` | `18309/12345/20201` |
| Meeting | `{troop_id}-{YYYY-MM-DD}` | `18309/12345/20201-2020-10-15` |

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
