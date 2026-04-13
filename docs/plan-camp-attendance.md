# Feature Plan: Camp Attendance Tracking with Scoutnet Activity Import

## Summary

Add support for tracking attendance at camps (läger) by importing a Scoutnet activity/project
as a special "camp troop" in Skojjt. A camp troop auto-generates one meeting per day for
the camp's date range, and participants are imported from the Scoutnet project participant list.
Camp attendance data integrates with existing DAK and Lägerbidrag exports.

## Background

### Current model
- **Troop** = a scout troop (avdelning) per semester, with `ScoutnetId` as a natural key
- **Meeting** = a single gathering, unique per `(TroopId, MeetingDate)`, with `IsHike` flag
- **MeetingAttendance** = junction table (MeetingId + PersonId)
- Hike/camp meetings already exist: the "Nytt möte" dialog has a "Läger/vandring" checkbox
  with multi-day support that creates one `Meeting` per day with `IsHike = true`
- **Lagerbidrag export** already generates camp subsidy reports from meeting data

### What's missing
- No way to distinguish a camp troop from a regular troop in the data model
- No integration with Scoutnet's project/activity participant API
- No way to import participants from a Scoutnet activity into a camp troop
- Camp troop defaults (all-day duration, location, start time) differ from regular troops

### Scoutnet project participant API (verified on demo2.custard.no, project 1190)

**Endpoint**: `GET api/project/get/participants` with project ID + project-level API key.

Actual response from the demo server:
```json
{
  "participants": {
    "3406451": {
      "checked_in": false,
      "attended": false,
      "cancelled": false,
      "confirmed": true,
      "confirmed_at": "2026-04-12 08:55:54",
      "member_status": 2,
      "member_no": 3406451,
      "group_registration": false,
      "first_name": "Martin",
      "last_name": "Green",
      "ssno": "",
      "registration_date": "2026-04-12 08:55:54",
      "cancelled_date": null,
      "sex": "1",
      "date_of_birth": "2010-08-08",
      "primary_email": "demo2@custard.no",
      "fee_id": 2139,
      "primary_membership_info": {
        "patrol_id": 19018,
        "patrol_name": "4196 Kangchenjunga patrol",
        "troop_id": 9355,
        "troop_name": "1043 Stickerbook troop",
        "group_id": 787,
        "group_name": "7490 Aconcagua group",
        "district_id": 696,
        "district_name": "2805 Makalu district",
        "region_id": null,
        "region_name": null,
        "organisation_id": 692,
        "organisation_name": "1533 Kangchenjunga organisation"
      },
      "group_registration_info": {
        "group_id": null,
        "patrol_id": null,
        "group_name": null,
        "org_id": null,
        "org_name": null,
        "district_id": null,
        "district_name": null,
        "patrol_name": null
      },
      "group_id": "Deprecated - use group_registration_info",
      "patrol_id": "Deprecated - use group_registration_info",
      "questions": [],
      "contact_info": []
    }
  },
  "labels": {
    "member_status": { "1": "Avregistrerad", "2": "Aktiv", ... },
    "sex": { "0": "Okänt", "1": "Man", "2": "Kvinna", "3": "Annat", "4": "Icke-binär" },
    "project_fee": { "2139": "Standardavgift" },
    "contact_type": []
  }
}
```

**Key findings:**
- `primary_membership_info.group_id` is the reliable way to determine the participant's group
- Old top-level fields (`group_id`, `patrol_id`, etc.) are deprecated strings: `"Deprecated - use group_registration_info"`
- `checked_in` and `attended` are separate booleans (can be checked in but not attended)
- `cancelled` / `confirmed` indicate registration status
- `member_no` is the Scoutnet member number = `Person.Id` in our DB
- The `labels` object provides translations for enum values

**Authentication**: Requires a **project-level** API key (separate from group-level keys).
Each project has its own set of API keys generated on the project's API page in Scoutnet.

### Scoutnet API endpoint map (verified on demo2.custard.no)

All endpoints below return **401 Unauthorized** (meaning they exist and are enabled on the
server, but require the correct API key):

| Endpoint | Auth type | Key source | What it does |
|---|---|---|---|
| `api/group/memberlist` | Group key | "Medlemmar i Kår" | List all members ✅ verified |
| `api/organisation/register/member` | Group key | "Väntelista" | Register waiting list ✅ verified |
| `api/organisation/update/membership` | Group key | "Uppdatera medlemskap" | Update membership ✅ verified |
| `api/organisation/group` | Group key | "Visa kårinformation" | **viewGroup** — group info, stats, leader ✅ verified |
| `api/organisation/project` | Group key | Separate "Projekt" key | **viewGroupProjects** — lists projects ✅ verified (returns `[]` for demo group) |
| `api/project/get/participants` | Project key | Per-project API page | **Get project participants** — the import source |
| `api/project/get/groups` | Project key | Per-project API page | List groups with members in project |
| `api/project/get/patrols` | Project key | Per-project API page | List patrols in project |
| `api/project/get/questions` | Project key | Per-project API page | List project questions |
| `api/project/get/published` | System key | System admin | List all published projects |
| `api/organisation/published_roles/all` | System key | System admin | Published roles |
| `api/organisation/group/all` | System key | System admin | All groups |

Auth API endpoints (require JWT from `/api/authenticate` with member username/password):
- `/api/get/projects/available` — available projects for the logged-in member
- `/api/get/projects/registered` — registered projects for the logged-in member

### Verified API behavior (demo2.custard.no, group 787)

**viewGroup** (`api/organisation/group`, key: `d3756c...`):
- Returns `Group` (name, membercount, stats, etc.) and `Leader` (name, contactdetails)
- Does **NOT** include a `"projects"` URL — the docs say this appears only when the group
  has members registered on projects. The demo group has none currently.
- Response is ~3 KB JSON

**viewGroupProjects** (`api/organisation/project`, key: `c1125e...`):
- Returns `[]` (empty array) even with a confirmed participant from the group
- This is a **separate API key** from viewGroup, not derived from the viewGroup response
- Both keys are generated independently on the group's Scoutnet API page
- The empty result may be because the participant was registered individually, not as a
  "group registration" (`group_registration: false` in the participant response).
  The docs state: *"Only projects that have group members from this group registered to them
  will be returned."* — this may require group-level registration, not individual sign-up.

**Project participant API** (`api/project/get/participants`, key: `dd37b7...`):
- ✅ **Verified working** — returns full participant data for project 1190
- Requires a **project-level** API key (different from the group-level keys)
- Each project has its own set of API keys generated on the project's API page
- Returns rich data: member info, registration status, primary membership info
- Old top-level `group_id`/`patrol_id` fields are **deprecated** — use `primary_membership_info`

### Options for listing a group's camp activities

**Option A: viewGroupProjects (group-level project listing)**

1. Store the **viewGroupProjects API key** on `ScoutGroup` (group-level, generated once)
2. Call `api/organisation/project` → returns list of projects with name, dates, etc.
3. User picks a project → enters the **project-level API key** to fetch participants
4. Call `api/project/get/participants` → returns participant list for import

**Pros:** Machine-to-machine, browse available activities. Group-level key stored once.
**Cons:** Requires one additional API key stored on `ScoutGroup`.
The per-project API key is still entered per import.
**Only shows projects where members are registered via "group registration"** — most
Swedish scout camps use individual registration, so this endpoint returns `[]` in practice.

**Option B: Direct project import (simplest, no listing)**

1. User manually enters the Scoutnet project ID (visible in the project URL on Scoutnet)
2. User enters the project-level API key
3. Call `api/project/get/participants` directly

**Pros:** No additional group-level key needed. Works today.
**Cons:** User must know the project ID and manually look it up on Scoutnet.
No browse/search UI for available activities.

### Recommendation

**Start with Option B** (direct project import) — this is the only approach fully verified
to work. The viewGroupProjects endpoint returns empty even with a confirmed participant,
likely requiring "group registration" mode which isn't always used for camps.

The import dialog asks the user for:
1. Scoutnet project ID (from the activity URL, e.g., `/activities/view/1190` → ID = 1190)
2. Project participant API key (from the project's API settings page)

**Future: Option A** is unlikely to be useful because most Swedish scout camps use individual
registration on Scoutnet, which means `viewGroupProjects` returns `[]`. The endpoint only
returns projects with "group registration" participants (`group_registration: true`).

---

## Design

### Troop type

Add a `TroopType` enum to distinguish troop categories:

```csharp
public enum TroopType
{
    Regular = 0,  // Normal scout troop
    Camp = 1      // Camp/activity (läger)
}
```

Add `TroopType` property to the `Troop` entity. Default is `Regular` (0), so all existing
troops remain unchanged with no data migration needed beyond adding the column.

### Camp-specific metadata on Troop

Add optional camp fields to `Troop`:

| Property | Type | Purpose |
|---|---|---|
| `TroopType` | `TroopType` (int) | Distinguish camp from regular troop |
| `CampStartDate` | `DateOnly?` | First day of camp (null for regular troops) |
| `CampEndDate` | `DateOnly?` | Last day of camp (null for regular troops) |
| `ScoutnetProjectId` | `int?` | Scoutnet project/activity ID if imported (null for manual) |

These are nullable so they don't affect existing troops. The `CampStartDate`/`CampEndDate` are
denormalized (meetings cover the dates too) but useful for display and re-import logic.

### ScoutnetId allocation for camp troops

- **Manual camp creation** → uses existing `NextLocalTroopId` counter (250–999), same as
  any locally created troop
- **Scoutnet activity import** → uses `NextLocalTroopId` as well, since Scoutnet project IDs
  are in a different namespace than troop IDs and could collide. The `ScoutnetProjectId` field
  stores the original project ID for reference/re-import.

### Scoutnet project API client

Add new methods to `IScoutnetApiClient`:

```csharp
/// <summary>
/// Fetches the list of projects the group participates in.
/// Uses the viewGroupProjects API key (group-level).
/// </summary>
Task<List<ScoutnetProject>> GetGroupProjectsAsync(
    int groupId,
    string apiKey,
    CancellationToken cancellationToken = default);

/// <summary>
/// Fetches participants for a specific project/activity.
/// Uses a project-level API key (entered per import, not stored).
/// </summary>
Task<ScoutnetProjectParticipantsResponse> GetProjectParticipantsAsync(
    int projectId,
    string apiKey,
    CancellationToken cancellationToken = default);
```

**URLs**:
- Group projects: `GET api/organisation/project?id={groupId}&key={apiKey}`
- Project participants: `GET api/project/get/participants?id={projectId}&key={apiKey}`

### Response models (based on verified API responses)

```csharp
/// <summary>
/// A project/activity from the viewGroupProjects endpoint.
/// </summary>
public class ScoutnetProject
{
    public string Name { get; set; } = string.Empty;
    public string? Starts { get; set; }
    public string? Ends { get; set; }
    public string? Description { get; set; }
    public string? Updated { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
}

/// <summary>
/// Response from api/project/get/participants.
/// Participants keyed by member number (string).
/// </summary>
public class ScoutnetProjectParticipantsResponse
{
    public Dictionary<string, ScoutnetProjectParticipant> Participants { get; set; } = new();
}

/// <summary>
/// A participant in a Scoutnet project/activity.
/// Fields based on verified API response from demo2.custard.no.
/// </summary>
public class ScoutnetProjectParticipant
{
    public int MemberNo { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool CheckedIn { get; set; }
    public bool Attended { get; set; }
    public bool Cancelled { get; set; }
    public bool Confirmed { get; set; }
    public int MemberStatus { get; set; }
    public bool GroupRegistration { get; set; }
    public string? DateOfBirth { get; set; }
    public string? Sex { get; set; }
    public string? PrimaryEmail { get; set; }
    public string? RegistrationDate { get; set; }
    public string? CancelledDate { get; set; }

    /// <summary>
    /// The participant's primary group membership.
    /// Use this instead of the deprecated top-level group_id/group_name fields.
    /// </summary>
    public ScoutnetPrimaryMembershipInfo? PrimaryMembershipInfo { get; set; }
}

/// <summary>
/// Primary membership info nested within a project participant.
/// </summary>
public class ScoutnetPrimaryMembershipInfo
{
    public int? GroupId { get; set; }
    public string? GroupName { get; set; }
    public int? TroopId { get; set; }
    public string? TroopName { get; set; }
    public int? PatrolId { get; set; }
    public string? PatrolName { get; set; }
    public int? DistrictId { get; set; }
    public string? DistrictName { get; set; }
}
```

---

## Implementation Plan

### Phase 1: Data model & migration

**Files to modify:**
- `src/Skojjt.Core/Entities/Troop.cs` — add `TroopType`, `CampStartDate`, `CampEndDate`, `ScoutnetProjectId`
- `src/Skojjt.Core/Entities/TroopType.cs` — new enum file
- `src/Skojjt.Infrastructure/Data/Configurations/TroopConfiguration.cs` — configure new columns
- `src/Skojjt.Shared/DTOs/TroopDtos.cs` — add `TroopType` to DTOs
- EF migration: `AddTroopTypeAndCampFields`

**Column details:**
| Column | DB name | Type | Default | Nullable |
|---|---|---|---|---|
| `TroopType` | `troop_type` | `integer` | `0` | No |
| `CampStartDate` | `camp_start_date` | `date` | — | Yes |
| `CampEndDate` | `camp_end_date` | `date` | — | Yes |
| `ScoutnetProjectId` | `scoutnet_project_id` | `integer` | — | Yes |

### Phase 2: Camp creation UI

**Files to modify:**
- `src/Skojjt.Web/Components/Pages/SemesterTroops.razor` — add "Skapa läger" button and dialog
- `src/Skojjt.Web/Controllers/TroopsController.cs` — handle camp creation in Create endpoint

**Camp creation dialog fields:**
- Namn (camp name) — required
- Plats (location) — required, maps to `DefaultMeetingLocation`
- Startdatum — required, maps to `CampStartDate`
- Slutdatum — required, maps to `CampEndDate`

**On create:**
1. Allocate `ScoutnetId` from `NextLocalTroopId`
2. Create `Troop` with `TroopType = Camp`, `CampStartDate`, `CampEndDate`
3. Auto-generate one `Meeting` per day in the date range with:
   - `IsHike = true`
   - `Name = "{CampName} dag {N}"`
   - `DurationMinutes = 1440` (full day)
   - `Location = camp location`
   - `StartTime = 08:00`

### Phase 3: Camp troop display

**Files to modify:**
- `src/Skojjt.Web/Components/Pages/SemesterTroops.razor` — show camp icon 🏕️ on camp troop cards
- `src/Skojjt.Web/Components/Pages/TroopAttendance.razor` — show camp date range in header,
  adjust "Nytt möte" to "Ny lägerdag" for camps

**Visual differences for camp troops:**
- Card shows 🏕️ icon next to name
- Card shows date range instead of just member/meeting counts
- Camp troops sorted after regular troops in the list

### Phase 4: Scoutnet project participant import

**New files:**
- `src/Skojjt.Infrastructure/Scoutnet/ScoutnetProjectModels.cs` — response models
- `src/Skojjt.Infrastructure/Scoutnet/ScoutnetProjectImportService.cs` — import logic

**Files to modify:**
- `src/Skojjt.Infrastructure/Scoutnet/IScoutnetApiClient.cs` — add `GetProjectParticipantsAsync`
- `src/Skojjt.Infrastructure/Scoutnet/ScoutnetApiClient.cs` — implement the method
- `src/Skojjt.Web/Components/Pages/SemesterTroops.razor` — add "Importera läger från Scoutnet"
  button and dialog

**Import dialog fields:**
- Scoutnet projekt-ID — required (integer)
- API-nyckel (projekt) — required (one-time, not stored)
- Namn (camp name) — pre-filled from project if available, editable
- Plats — required
- Startdatum / Slutdatum — required

**Import flow:**
1. User enters project ID + API key → "Hämta deltagare" button
2. Fetch participants from Scoutnet API
3. Display preview: participant count, list of names, which are already in the group
4. User confirms → create camp troop + meetings + add participants as `TroopPerson`
5. For participants that are already `Person` records in the group: add `TroopPerson` directly
6. For participants NOT in the group: show warning — they must be imported via Scoutnet member
   import first (or auto-create minimal `Person` records)

**Matching logic:**
- Match by `member_no` (Scoutnet member number = `Person.Id`)
- Use `primary_membership_info.group_id` (not the deprecated top-level `group_id`) to identify
  which group the participant belongs to
- Participants where `primary_membership_info.group_id` matches the current scout group
  are imported directly
- Participants from other groups are flagged — they need to be added to the scout group first
- Filter out `cancelled = true` participants by default

### Phase 5: Re-import / sync

**Files to modify:**
- `src/Skojjt.Web/Components/Pages/TroopAttendance.razor` — add "Synka med Scoutnet" button
  for camp troops that have `ScoutnetProjectId`

**Sync behavior:**
- Fetch current participant list from Scoutnet
- Add new participants as `TroopPerson` (if they exist as `Person`)
- Optionally mark attendance for `checked_in = true` participants on all camp days
- Do NOT remove participants that are no longer in Scoutnet (manual cleanup)

### Phase 6: Export integration

**Files to modify:**
- `src/Skojjt.Infrastructure/Exports/DakXmlExporter.cs` — camp troops export correctly
  (they already work since camps use `Meeting` with `IsHike = true`)
- `src/Skojjt.Infrastructure/Exports/LagerbidragExporter.cs` — camp troops are selectable
  in the Lagerbidrag dialog (already works via troop selection dropdown)

**Verification needed:**
- Confirm DAK export handles camp meetings (daily, full-day) correctly
- Confirm Lagerbidrag export calculates days/nights correctly from camp meetings
- Confirm Aktivitetsbidrag CSV counts camp attendance correctly

---

## Edge Cases & Decisions

### Camp spanning two semesters
A camp could span the VT/HT boundary (June–July). Decision: **camp belongs to the semester
of its start date**. All meetings (including those in the next semester's date range) belong
to the same troop. The `Semester.IsValidDate()` check in meeting creation should be relaxed
for camp troops, or the camp end date should be capped to the semester end.

**Recommendation**: Allow camp meetings outside semester bounds for camp troops. Add a
`Troop.IsCamp` computed property and skip the semester date validation for camps.

### Participants from other groups
Scoutnet activities can have participants from multiple groups. These members won't exist as
`Person` records in the current group. Decision: **only import participants that already exist
as Person records in the database**. Show a warning listing unmatched participants with
a suggestion to run Scoutnet member import first.

### Duplicate import prevention
If the same Scoutnet project is imported twice, use `ScoutnetProjectId` to detect the
existing camp troop and offer to update it instead of creating a duplicate.

### Camp with no Scoutnet project
Camps created manually (without Scoutnet import) have `ScoutnetProjectId = null`.
They function identically but lack the "Synka med Scoutnet" option.

### Meeting unique constraint
The existing unique constraint `(TroopId, MeetingDate)` already prevents duplicate days.
Camp meeting auto-generation naturally respects this.

### Person flow graph exclusion
Camp troops must **not** appear in the person flow Sankey chart (`PersonFlowService`).
The flow chart shows how members move between regular troops (avdelningar) across semesters —
camp attendance is transient and doesn't represent a troop placement.

The current query in `PersonFlowService.GetFlowAsync` already filters on
`t.UnitTypeId.HasValue && ScoutUnitTypes.ValidIds.Contains(t.UnitTypeId.Value)`.
Camp troops will have `UnitTypeId = null` (camps don't map to a scout age group), so they
are excluded automatically. For defense-in-depth, add an explicit `TroopType == Regular`
filter:

```csharp
// In PersonFlowService.GetFlowAsync base query:
.Where(t => t.ScoutGroupId == scoutGroupId
            && orderedIds.Contains(t.SemesterId)
            && t.TroopType == TroopType.Regular  // Exclude camp troops
            && t.UnitTypeId.HasValue
            && ScoutUnitTypes.ValidIds.Contains(t.UnitTypeId.Value))
```

**File to modify:** `src/Skojjt.Infrastructure/Services/PersonFlowService.cs` (Phase 1)

---

## Testing Plan

### Unit tests (`Skojjt.Infrastructure.Tests`)
- `ScoutnetApiClient.GetProjectParticipantsAsync` — parse response, handle errors
- `ScoutnetProjectImportService` — matching logic, edge cases
- DAK/Lagerbidrag export with camp troops

### Integration tests
- Create camp troop → verify meetings generated
- Import from Scoutnet project → verify participants matched
- Re-import → verify new participants added, existing unchanged

### Manual testing
- Create manual camp, track attendance, export DAK
- Import from Scoutnet demo server activity
- Verify Lagerbidrag export with camp data
- Verify camp troop displays correctly in semester overview

---

## API Keys Summary

| Endpoint | Key source | Storage | Status |
|---|---|---|---|
| Member list | Group API page → "Medlemmar i Kår" | `ScoutGroup.ApiKeyAllMembers` | ✅ Verified |
| Waiting list registration | Group API page → "Väntelista" | `ScoutGroup.ApiKeyWaitinglist` | ✅ Verified |
| Update membership | Group API page → "Uppdatera medlemskap" | `ScoutGroup.ApiKeyUpdateMembership` | ✅ Verified |
| View group info | Group API page → "Visa kårinformation" | Not needed for MVP | ✅ Verified |
| View group projects | Group API page → separate key | Not needed for MVP | ⚠️ Returns `[]` (see notes) |
| **Project participants** | **Project API page** (per project) | **Not stored** — entered per import | ✅ **Verified** |

The project participant API key is per-activity, not per-group, so it is entered per import
and not stored permanently. The user copies it from the project's API page on Scoutnet.

The viewGroupProjects endpoint returns `[]` even with a confirmed participant from the group.
This may require "group registration" mode. Not blocking for MVP since we use direct import.

---

## Migration / Rollout

1. Database migration adds columns with defaults — **no data migration needed**
2. Existing troops get `TroopType = 0` (Regular) automatically
3. Feature is additive — no changes to existing troop behavior
4. Camp creation available immediately after deployment
5. Scoutnet import available when project API keys are configured in Scoutnet
