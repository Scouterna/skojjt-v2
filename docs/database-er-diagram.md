# Database ER Diagram

## Entity Relationship Diagram

```mermaid
erDiagram
    %% Core Entities
    semesters {
        INTEGER id PK "e.g., 20251 (autumn 2025)"
        INTEGER year
        BOOLEAN is_autumn
        TIMESTAMPTZ created_at
    }

    scout_groups {
        INTEGER id PK "Scoutnet group ID"
        VARCHAR name
        VARCHAR organisation_number
        VARCHAR association_id
        VARCHAR municipality_id
        VARCHAR api_key_waitinglist
        VARCHAR api_key_all_members
        VARCHAR bank_account
        VARCHAR address
        VARCHAR postal_address
        VARCHAR email
        VARCHAR phone
        VARCHAR default_location
        VARCHAR signatory
        VARCHAR signatory_phone
        VARCHAR signatory_email
        INTEGER attendance_min_year
        BOOLEAN attendance_incl_hike
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    troops {
        VARCHAR id PK "scoutnet_id.semester_id"
        INTEGER scoutnet_id
        INTEGER scout_group_id FK
        INTEGER semester_id FK
        VARCHAR name
        TIME default_start_time
        INTEGER default_duration_minutes
        INTEGER report_id
        BOOLEAN is_locked "Prevents edits after reporting"
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    persons {
        INTEGER id PK "Scoutnet member number"
        VARCHAR first_name
        VARCHAR last_name
        DATE birth_date
        VARCHAR personal_number
        VARCHAR email
        VARCHAR phone
        VARCHAR mobile
        VARCHAR alt_email
        VARCHAR mum_name
        VARCHAR mum_email
        VARCHAR mum_mobile
        VARCHAR dad_name
        VARCHAR dad_email
        VARCHAR dad_mobile
        VARCHAR street
        VARCHAR zip_code
        VARCHAR zip_name
        TEXT group_roles
        INTEGER_ARRAY member_years
        BOOLEAN not_in_scoutnet
        BOOLEAN removed
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    scout_group_persons {
        INTEGER person_id PK_FK
        INTEGER scout_group_id PK_FK
        TIMESTAMPTZ created_at
    }

    troop_persons {
        VARCHAR troop_id PK_FK
        INTEGER person_id PK_FK
        BOOLEAN is_leader
        VARCHAR patrol "Patrol per troop membership"
        TIMESTAMPTZ created_at
    }

    meetings {
        VARCHAR id PK "troop_id.YYYY-MM-DD"
        VARCHAR troop_id FK
        DATE meeting_date
        TIME start_time
        VARCHAR name
        INTEGER duration_minutes
        BOOLEAN is_hike
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    meeting_attendances {
        VARCHAR meeting_id PK_FK
        INTEGER person_id PK_FK
        VARCHAR troop_id "Denormalized"
        INTEGER scoutnet_id "Denormalized"
        INTEGER scout_group_id "Denormalized"
        INTEGER semester_id "Denormalized"
        TIMESTAMPTZ created_at
    }

    users {
        VARCHAR id PK "ScoutID identifier"
        VARCHAR email UK
        VARCHAR name
        INTEGER scout_group_id FK
        INTEGER active_semester_id FK
        BOOLEAN has_access
        BOOLEAN is_admin
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    %% Badge System Entities
    badges {
        SERIAL id PK
        INTEGER scout_group_id FK
        VARCHAR name
        TEXT description
        TEXT_ARRAY parts_scout_short
        TEXT_ARRAY parts_scout_long
        TEXT_ARRAY parts_admin_short
        TEXT_ARRAY parts_admin_long
        VARCHAR image_url
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    badge_templates {
        SERIAL id PK
        VARCHAR name UK
        TEXT description
        TEXT_ARRAY parts_scout_short
        TEXT_ARRAY parts_scout_long
        TEXT_ARRAY parts_admin_short
        TEXT_ARRAY parts_admin_long
        VARCHAR image_url
        TIMESTAMPTZ created_at
        TIMESTAMPTZ updated_at
    }

    badge_parts_done {
        INTEGER person_id PK_FK
        INTEGER badge_id PK_FK
        INTEGER part_index PK
        BOOLEAN is_scout_part PK
        VARCHAR examiner_name
        DATE completed_date
        TIMESTAMPTZ created_at
    }

    badges_completed {
        INTEGER person_id PK_FK
        INTEGER badge_id PK_FK
        VARCHAR examiner
        DATE completed_date
        TIMESTAMPTZ created_at
    }

    troop_badges {
        VARCHAR troop_id PK_FK
        INTEGER badge_id PK_FK
        INTEGER sort_order
        TIMESTAMPTZ created_at
    }

    %% Relationships
    scout_groups ||--o{ troops : "has"
    semesters ||--o{ troops : "contains"
    scout_groups ||--o{ scout_group_persons : "has members"
    persons ||--o{ scout_group_persons : "belongs to"
    scout_groups ||--o{ users : "grants access"
    scout_groups ||--o{ badges : "defines"
    semesters ||--o{ users : "active semester"
    
    troops ||--o{ troop_persons : "has members"
    persons ||--o{ troop_persons : "assigned to"
    
    troops ||--o{ meetings : "schedules"
    meetings ||--o{ meeting_attendances : "tracks"
    persons ||--o{ meeting_attendances : "attends"
    
    troops ||--o{ troop_badges : "assigns"
    badges ||--o{ troop_badges : "assigned to"
    
    persons ||--o{ badge_parts_done : "completes"
    badges ||--o{ badge_parts_done : "has parts"
    
    persons ||--o{ badges_completed : "earns"
    badges ||--o{ badges_completed : "completed by"
```

## Relationship Summary

### Core Domain

| From | To | Relationship | Description |
|------|-----|--------------|-------------|
| scout_groups | troops | 1:N | A scout group has many troops |
| semesters | troops | 1:N | A semester contains many troops |
| scout_groups | scout_group_persons | 1:N | A scout group has many member assignments |
| persons | scout_group_persons | 1:N | A person can belong to multiple scout groups |
| troops | troop_persons | 1:N | A troop has many member assignments |
| persons | troop_persons | 1:N | A person can be assigned to multiple troops |
| troops | meetings | 1:N | A troop schedules many meetings |
| meetings | meeting_attendances | 1:N | A meeting tracks many attendances |
| persons | meeting_attendances | 1:N | A person can attend many meetings |

### Badge System

| From | To | Relationship | Description |
|------|-----|--------------|-------------|
| scout_groups | badges | 1:N | A scout group defines many badges |
| troops | troop_badges | 1:N | A troop can assign many badges |
| badges | troop_badges | 1:N | A badge can be assigned to many troops |
| persons | badge_parts_done | 1:N | A person completes many badge parts |
| badges | badge_parts_done | 1:N | A badge has many completable parts |
| persons | badges_completed | 1:N | A person earns many badges |
| badges | badges_completed | 1:N | A badge can be completed by many persons |

### User Management

| From | To | Relationship | Description |
|------|-----|--------------|-------------|
| scout_groups | users | 1:N | A scout group grants access to many users |
| semesters | users | 1:N | A semester can be active for many users |

## Key Design Decisions

1. **Deterministic IDs**: Most entities use natural keys (Scoutnet IDs, composite keys) rather than auto-generated UUIDs
2. **Composite Keys**: Junction tables use composite primary keys for efficiency
3. **Soft Deletes**: Persons have a `removed` flag rather than being deleted
4. **Audit Timestamps**: All tables include `created_at`, most include `updated_at`
5. **VARCHAR IDs for Troops/Meetings**: Human-readable composite string keys using '.' separator (e.g., `18309.20251`, `18309.20251.2025-03-15`) to distinguish from date format
6. **Multi-group membership**: `scout_group_persons` junction table allows persons to belong to multiple scout groups
7. **Patrol per troop**: Patrol assignment is stored in `troop_persons`, allowing different patrols in different troops
8. **Troop locking**: `is_locked` flag on troops prevents accidental edits after attendance has been reported
9. **Denormalized attendance**: `meeting_attendances` includes denormalized columns for fast statistics queries

## Index Strategy

```sql
-- Performance indexes
CREATE INDEX idx_scout_group_persons_group ON scout_group_persons(scout_group_id);
CREATE INDEX idx_troops_scout_group_semester ON troops(scout_group_id, semester_id);
CREATE UNIQUE INDEX idx_troops_natural_key ON troops(scoutnet_id, semester_id);
CREATE INDEX idx_troop_persons_person ON troop_persons(person_id);
CREATE INDEX idx_meetings_troop_date ON meetings(troop_id, meeting_date);
CREATE INDEX idx_meeting_attendances_person ON meeting_attendances(person_id);
CREATE INDEX idx_meeting_attendances_semester ON meeting_attendances(semester_id, person_id);
CREATE INDEX idx_meeting_attendances_group ON meeting_attendances(scout_group_id, semester_id);
CREATE INDEX idx_meeting_attendances_troop ON meeting_attendances(troop_id);
CREATE INDEX idx_badges_scout_group ON badges(scout_group_id);
CREATE INDEX idx_badge_parts_done_badge ON badge_parts_done(badge_id);
