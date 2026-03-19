# -*- coding: utf-8 -*-
#!/usr/bin/env python3
"""
Transform exported Datastore JSON to PostgreSQL-ready format.

This script takes JSON files exported from Datastore (via export_live.py)
and transforms them to match the PostgreSQL schema with deterministic IDs.

Usage:
    python transform_data.py --input-dir ./raw_export --output-dir ./json_export
"""

import argparse
import json
import os
from datetime import datetime
from typing import Any, Dict, List, Optional, Tuple


class DataTransformer:
    def __init__(self, input_dir: str, output_dir: str):
        self.input_dir = input_dir
        self.output_dir = output_dir
        self.badge_id_map = {}
        self.badge_counter = 1
        self.bad_troop_counter = 0
        self.bad_troop_keys = {}
        self.all_troops_set = set()
        self.all_persons_set = set()
        self.stats = {}
        # Mapping from ScoutGroup key name -> scoutnetID
        self.scout_group_key_to_id = {}
        # Mapping from raw troop key part -> resolved int scoutnet_id
        # Populated during transform_troops, used by downstream transforms
        self.troop_key_to_scoutnet_id: Dict[str, int] = {}
        # Reserved ID range 250-1000 for local troops without a Scoutnet ID.
        # Real Scoutnet IDs are auto-increment starting well above 1000.
        self._local_troop_id_counter = 250
        self._local_troop_id_map: Dict[Tuple[str, int], int] = {}
        os.makedirs(output_dir, exist_ok=True)
    
    def load_json(self, filename: str) -> List[Dict]:
        path = os.path.join(self.input_dir, filename)
        if os.path.exists(path):
            with open(path, 'r', encoding='utf-8') as f:
                return json.load(f)
        else:
            raise Exception(f"File does {path} not exist. Make sure to run convert_export.py first, see README.md");
        return []
    
    def save_json(self, filename: str, data: List[Dict]):
        path = os.path.join(self.output_dir, filename)
        with open(path, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
        print(f"  Saved {len(data)} records to {filename}")
        self.stats[filename] = len(data)

    def convert_semester_id(self, semester_key: str) -> int:
        """Convert semester key name to int ID."""
        parts = semester_key.split('-')
        if len(parts) == 2:
            year = int(parts[0])
            is_autumn = 1 if parts[1] == 'ht' else 0
            return (year * 10) + is_autumn
        return 0
    
    def _resolve_legacy_troop_key(self, troop_key: str) -> Optional[Tuple[str, int]]:
        """Resolve a legacy v1 troop key like 'bävertynneredsscoutkår'.

        In the old v1 system, troop name and group name were concatenated
        without a separator: '{troopname}{groupkey}'. We match the suffix
        against known scout group keys.

        Returns (troop_name, group_id) or None if no match.
        """
        # Also handle 2-part keys like 'pscoutlärjedalen/hjällboscoutkår'
        if '/' in troop_key:
            parts = troop_key.split('/', 1)
            group_id = self.extract_group_key_id(parts[1])
            if group_id:
                return (parts[0], group_id)

        # Try suffix matching against known group keys (longest match first)
        for gk in sorted(self.scout_group_key_to_id.keys(), key=len, reverse=True):
            if troop_key.endswith(gk):
                troop_name = troop_key[:-len(gk)]
                if troop_name:  # Ensure there's actually a troop name part
                    return (troop_name, self.scout_group_key_to_id[gk])

        return None

    def extract_troop_key_id(self, key_obj:str) -> tuple[int, int, int]:
        """Extract TroopId/GroupId/SemesterId from a Datastore key object."""
        s = key_obj.split('/')
        if len(s) < 2:
            if key_obj in self.bad_troop_keys:
                return self.bad_troop_keys[key_obj]
            else:
                # Try legacy format: '{troopname}{groupkey}' concatenated
                legacy = self._resolve_legacy_troop_key(key_obj)
                if legacy:
                    troop_name, group_id = legacy
                    self.bad_troop_counter += 1
                    new_troop = (self.bad_troop_counter, group_id, 0)
                    self.bad_troop_keys[key_obj] = new_troop
                    return new_troop
                # inventing a new troop id for bad troop keys
                group_id = 0
                for k, v in self.scout_group_key_to_id.items():
                    if k in key_obj:
                        group_id = v
                        break
                self.bad_troop_counter += 1
                new_troop = (self.bad_troop_counter, group_id, 0)
                self.bad_troop_keys[key_obj] = new_troop
                return new_troop
        first_split = key_obj.split('/', 1)
        troop_part = first_split[0]
        remainder = first_split[1] if len(first_split) > 1 else ''
        last_split = remainder.rsplit('/', 1)
        if len(last_split) == 2:
            group_part = last_split[0]
            semester_part = last_split[1]
            troop_id = int(troop_part)
            group_id = self.extract_group_key_id(group_part)
            semester_id = self.convert_semester_id(semester_part)
            return (troop_id, group_id, semester_id)
        # 2-part key: '{name}/{group}' (legacy format without semester)
        legacy = self._resolve_legacy_troop_key(key_obj)
        if legacy:
            troop_name, group_id = legacy
            if key_obj in self.bad_troop_keys:
                return self.bad_troop_keys[key_obj]
            self.bad_troop_counter += 1
            new_troop = (self.bad_troop_counter, group_id, 0)
            self.bad_troop_keys[key_obj] = new_troop
            return new_troop
        return None

    def _semester_id_from_date(self, date_str: str) -> int:
        """Infer semester ID from a date string like '2016-10-29'."""
        parts = date_str.split('-')
        if len(parts) >= 2:
            year = int(parts[0])
            month = int(parts[1])
            is_autumn = 1 if month >= 7 else 0
            return year * 10 + is_autumn
        return 0

    def extract_meeting_key_id(self, key_obj:str) -> tuple[int, int, int, str]:
        """Extract TroopId/GroupId/SemesterId/date from a Datastore key object.

        Handles three formats:
        1. Standard: '{troop_id}/{group}/{semester}.{date}'
           e.g. '18309/lerumsscoutkår/2020-ht.2020-10-15'
        2. Legacy 2-part: '{name}/{group}.{date}'
           e.g. 'pscoutlärjedalen/hjällboscoutkår.2016-10-29'
        3. Legacy 1-part: '{troopname}{groupkey}.{date}'
           e.g. 'bävertynneredsscoutkår.2016-10-29'
        """
        # Split off the date part (always after the last '.')
        dot_idx = key_obj.rfind('.')
        if dot_idx == -1:
            return None
        troop_part_full = key_obj[:dot_idx]
        date_part = key_obj[dot_idx + 1:]

        # Try standard format first: '{troop_id}/{group}/{semester}'
        parts = troop_part_full.split('/')
        if len(parts) == 3:
            try:
                troop_id = ((int(parts[0]) & 0xffffffff) ^ 0x80000000) - 0x80000000
                group_id = self.extract_group_key_id(parts[1])
                semester_id = self.convert_semester_id(parts[2])
                return (troop_id, group_id, semester_id, date_part)
            except (ValueError, IndexError):
                pass

        # Legacy formats: resolve troop key and infer semester from date
        troop_tuple = self.extract_troop_key_id(troop_part_full)
        if troop_tuple:
            semester_id = troop_tuple[2]
            if semester_id == 0:
                semester_id = self._semester_id_from_date(date_part)
            return (troop_tuple[0], troop_tuple[1], semester_id, date_part)

        return None


    def extract_group_key_id(self, key_obj:str) -> int:
        """Extract ID or name from a Datastore key object."""
        if key_obj in self.scout_group_key_to_id:
            return self.scout_group_key_to_id[key_obj]
        else:
            for k,v in self.scout_group_key_to_id.items():
                if key_obj in k:
                    return v
        return 0

    def parse_date(self, date_obj) -> Optional[str]:
        if date_obj is None:
            return None
        if isinstance(date_obj, dict):
            if date_obj.get('_type') in ('datetime', 'date'):
                return date_obj['value'][:10]
        if isinstance(date_obj, str):
            if len(date_obj) >= 10 and date_obj.isdigit():
                date_epoch = int(date_obj)
                return datetime.utcfromtimestamp(date_epoch).strftime('%Y-%m-%d')
        return date_obj
    
    def parse_datetime(self, dt_obj) -> Optional[str]:
        if dt_obj is None:
            return None
        if isinstance(dt_obj, dict) and dt_obj.get('_type') == 'datetime':
            return dt_obj['value']
        return str(dt_obj) if dt_obj else None
    
    def build_scout_group_lookup(self, raw_groups: List[Dict]):
        """Build mapping from ScoutGroup key names to scoutnetIDs."""
        for r in raw_groups:
            entity_key = r.get('id')
            key_name = r.get('key')
            if key_name and entity_key:
                self.scout_group_key_to_id[str(key_name)] = int(entity_key)
        print(f"  Built scout group lookup with {len(self.scout_group_key_to_id)} entries")
    
    def transform_semesters(self, raw: List[Dict]) -> List[Dict]:
        result = []
        semester_set = set()
        for r in raw:
            year = r.get('year', 0)
            is_autumn = r.get('is_autumn', False)
            # Generate int ID: (year * 10) + (1 if autumn else 0)
            semester_id = (year * 10) + (1 if is_autumn else 0)
            if semester_id in semester_set:
                print(f"  Warning: duplicate Semester id {semester_id} for year={year} is_autumn={is_autumn}")
                continue
            semester_set.add(semester_id)
            result.append({
                'id': semester_id,
                'year': year,
                'is_autumn': is_autumn
            })
        return result
    
    def transform_scout_groups(self, raw: List[Dict]) -> List[Dict]:
        result = []
        for r in raw:
            scoutnet_id = r.get('id')
            if not scoutnet_id:
                print(f"  Warning: ScoutGroup without scoutnetID: {r.get('name')}")
                continue
            result.append({
                'id': int(scoutnet_id),
                'name': r.get('name', ''),
                'organisation_number': r.get('organisation_number'),
                'association_id': r.get('association_id'),
                'municipality_id': r.get('municipality_id', '1480'),
                'api_key_waitinglist': r.get('api_key_waitinglist'),
                'api_key_all_members': r.get('api_key_all_members'),
                'bank_account': r.get('bank_account'),
                'address': r.get('address'),
                'postal_address': r.get('postal_address'),
                'email': r.get('email'),
                'phone': r.get('phone'),
                'default_location': r.get('default_location'),
                'signatory': r.get('signatory'),
                'signatory_phone': r.get('signatory_phone'),
                'signatory_email': r.get('signatory_email'),
                'attendance_min_year': r.get('attendance_min_year', 10),
                'attendance_incl_hike': r.get('attendance_incl_hike', True)
            })
        return result
    
    def transform_persons(self, raw: List[Dict]) -> List[Dict]:
        result = []
        for r in raw:
            id = r.get('id')
            if not id:
                name = f"{r.get('firstname', '')} {r.get('lastname', '')}"
                print(f"  Warning: Person without id: {name}")
                continue
            
            # Try to resolve scout_group_id
            sg_name = r.get('scout_group_id')
            scout_group_id = self.extract_group_key_id(sg_name)
            
            if int(id) in self.all_persons_set:
                print(f'  Warning: duplicate person id {id}')
                continue
            self.all_persons_set.add(int(id))
            result.append({
                'id': int(id),
                'scout_group_id': scout_group_id,
                'first_name': r.get('first_name', ''),
                'last_name': r.get('last_name', ''),
                'birth_date': self.parse_date(r.get('birth_date')),
                'personal_number': r.get('personal_number'),
                'patrol': r.get('patrol'),
                'email': r.get('email'),
                'phone': r.get('phone'),
                'mobile': r.get('mobile'),
                'alt_email': r.get('alt_email'),
                'mum_name': r.get('mum_name'),
                'mum_email': r.get('mum_email'),
                'mum_mobile': r.get('mum_mobile'),
                'dad_name': r.get('dad_name'),
                'dad_email': r.get('dad_email'),
                'dad_mobile': r.get('dad_mobile'),
                'street': r.get('street'),
                'zip_code': r.get('zip_code'),
                'zip_name': r.get('zip_name'),
                'group_roles': r.get('group_roles'),
                'member_years': r.get('member_years', []) or [],
                'not_in_scoutnet': r.get('not_in_scoutnet', False),
                'removed': r.get('removed', False)
            })
        return result

    def _create_stub_persons(self, raw_troop_persons: List[Dict],
                              raw_meeting_attendances: List[Dict]) -> List[Dict]:
        """Create stub Person records for orphaned references.

        Some persons were deleted from Scoutnet/Datastore but are still
        referenced by TroopPerson and MeetingAttendance records. We create
        minimal stubs so the attendance data is preserved. The next Scoutnet
        sync will fill in their real details if they're still active.
        """
        # Collect all person IDs referenced by troop assignments and attendance.
        int32_max = 2_147_483_647
        referenced_ids: set[int] = set()
        for tp in raw_troop_persons:
            pid = tp.get('person_id')
            if pid and isinstance(pid, int) and 0 < pid <= int32_max:
                referenced_ids.add(pid)
        for a in raw_meeting_attendances:
            pid = a.get('person_id')
            if pid and isinstance(pid, int) and 0 < pid <= int32_max:
                referenced_ids.add(int(pid))

        orphaned = referenced_ids - self.all_persons_set
        if not orphaned:
            return []

        # Try to determine scout_group_id from troop_person references
        person_group: Dict[int, int] = {}
        for tp in raw_troop_persons:
            pid = tp.get('person_id')
            if pid in orphaned:
                troop_key = tp.get('troop_id', '')
                parts = troop_key.split('/')
                if len(parts) >= 2:
                    group_id = self.extract_group_key_id(parts[1])
                    if group_id:
                        person_group[pid] = group_id

        stubs = []
        for pid in sorted(orphaned):
            self.all_persons_set.add(pid)
            group_id = person_group.get(pid, 0)
            stubs.append({
                'id': pid,
                'scout_group_id': group_id,
                'first_name': 'Okänd',
                'last_name': f'medlem {pid}',
                'birth_date': None,
                'personal_number': None,
                'patrol': None,
                'email': None,
                'phone': None,
                'mobile': None,
                'alt_email': None,
                'mum_name': None,
                'mum_email': None,
                'mum_mobile': None,
                'dad_name': None,
                'dad_email': None,
                'dad_mobile': None,
                'street': None,
                'zip_code': None,
                'zip_name': None,
                'group_roles': None,
                'member_years': [],
                'not_in_scoutnet': True,
                'removed': True
            })
            print(f"  Created stub person: {pid} (Okänd medlem, group {group_id})")

        print(f"  Created {len(stubs)} stub person(s) for orphaned references")
        return stubs

    def _build_personnummer_remap(self, raw_persons: List[Dict]) -> Tuple[Dict[str, int], Dict[Tuple[str, str], int]]:
        """Build a mapping from personnummer-as-ID → real Scoutnet member_no.

        In the old v1 system, some scout groups used the personnummer
        (12-digit YYYYMMDDNNNN or 8-digit YYYYMMDD) as the person ID
        instead of the Scoutnet member number. We map these back to the
        real person by matching against the personal_number field.
        """
        # Exact personnummer -> member_no
        pnr_to_id: Dict[str, int] = {}
        for p in raw_persons:
            pnr = p.get('personal_number')
            mid = p.get('id')
            if pnr and mid:
                pnr_clean = str(pnr).replace('-', '')
                pnr_to_id[pnr_clean] = int(mid)
                if len(pnr_clean) >= 8:
                    pnr_to_id[pnr_clean[:8]] = int(mid)

        # (birth_date_prefix, scout_group_key) -> member_no for fuzzy matching
        birth_group_to_id: Dict[Tuple[str, str], int] = {}
        ambiguous: set[Tuple[str, str]] = set()
        for p in raw_persons:
            pnr = p.get('personal_number')
            mid = p.get('id')
            sg = p.get('scout_group_id')
            if pnr and mid and sg:
                prefix = str(pnr).replace('-', '')[:8]
                key = (prefix, str(sg))
                if key in birth_group_to_id and birth_group_to_id[key] != int(mid):
                    ambiguous.add(key)
                birth_group_to_id[key] = int(mid)
        for k in ambiguous:
            del birth_group_to_id[k]

        return pnr_to_id, birth_group_to_id

    def _apply_person_id_remap(self, raw_troop_persons: List[Dict],
                                raw_meeting_attendances: List[Dict],
                                pnr_to_id: Dict[str, int],
                                birth_group_to_id: Dict[Tuple[str, str], int]) -> int:
        """Remap personnummer-as-ID references to real Scoutnet member_nos.

        Modifies the raw data in-place. Returns the number of remapped references.
        """
        # Build orphan -> group_key mapping from troop_person references
        known = self.all_persons_set
        orphan_groups: Dict[int, set] = {}
        for tp in raw_troop_persons:
            pid = tp.get('person_id')
            if pid and pid not in known:
                troop_key = tp.get('troop_id', '')
                parts = troop_key.split('/')
                if len(parts) >= 2:
                    orphan_groups.setdefault(pid, set()).add(parts[1])
                else:
                    # Legacy key (e.g. 'bävertynneredsscoutkår') — resolve group
                    legacy = self._resolve_legacy_troop_key(troop_key)
                    if legacy:
                        # Find the group key string for the resolved group_id
                        group_id = legacy[1]
                        for gk, gid in self.scout_group_key_to_id.items():
                            if gid == group_id:
                                orphan_groups.setdefault(pid, set()).add(gk)
                                break
                    else:
                        # Still track for exact personnummer matching
                        orphan_groups.setdefault(pid, set())

        # Build the remap table: old personnummer-ID -> real scoutnet ID
        remap: Dict[int, int] = {}
        for oid in orphan_groups:
            s = str(oid)
            if s in pnr_to_id:
                remap[oid] = pnr_to_id[s]
            elif oid > 19000000:
                prefix = s[:8]
                for gk in orphan_groups.get(oid, set()):
                    key = (prefix, gk)
                    if key in birth_group_to_id:
                        remap[oid] = birth_group_to_id[key]
                        break

        # Also check meeting attendances for IDs not in troop_persons
        for a in raw_meeting_attendances:
            pid = a.get('person_id')
            if pid and pid not in known and pid not in remap:
                s = str(pid)
                if s in pnr_to_id:
                    remap[pid] = pnr_to_id[s]

        if not remap:
            return 0

        print(f"  Built personnummer remap table: {len(remap)} entries")
        for old_id, new_id in list(remap.items())[:5]:
            print(f"    {old_id} -> {new_id}")
        if len(remap) > 5:
            print(f"    ... and {len(remap) - 5} more")

        # Apply remap in-place
        count = 0
        for tp in raw_troop_persons:
            pid = tp.get('person_id')
            if pid in remap:
                tp['person_id'] = remap[pid]
                count += 1
        for a in raw_meeting_attendances:
            pid = a.get('person_id')
            if pid in remap:
                a['person_id'] = remap[pid]
                count += 1

        print(f"  Remapped {count} personnummer references to Scoutnet IDs")
        return count

    @staticmethod
    def _normalize_to_signed_int32(val: int) -> int:
        """Normalize an integer to a signed 32-bit int."""
        return ((val & 0xffffffff) ^ 0x80000000) - 0x80000000

    def _get_local_troop_id(self, troop_name: str, scout_group_id: int) -> int:
        """Assign an ID from the reserved range 250-1000 for a local troop.

        The same (name, scout_group_id) pair always returns the same ID.
        Real Scoutnet IDs are auto-increment starting well above 1000,
        so this range is safe from collisions.
        """
        key = (troop_name.strip(), scout_group_id)
        if key in self._local_troop_id_map:
            return self._local_troop_id_map[key]
        assigned = self._local_troop_id_counter
        if assigned > 1000:
            raise ValueError(
                f"Local troop ID range 250-1000 exhausted. "
                f"Cannot assign ID for '{troop_name}' in group {scout_group_id}")
        self._local_troop_id_counter += 1
        self._local_troop_id_map[key] = assigned
        return assigned

    def _build_troop_name_to_id_lookup(self, raw: List[Dict]) -> Dict[Tuple[str, str], int]:
        """First pass: build (troop_name, scout_group_key) -> scoutnet_id
        from troops that have a valid numeric scoutnet_id.

        When the same troop appears with a string ID in an earlier semester,
        we can resolve it to the real Scoutnet ID via this lookup.
        """
        lookup: Dict[Tuple[str, str], int] = {}
        for r in raw:
            sid = str(r.get('scoutnet_id', ''))
            if not sid.lstrip('-').isdigit():
                continue
            name = r.get('name', '').strip()
            group = r.get('scout_group_id', '')
            if name and group:
                numeric_id = self._normalize_to_signed_int32(int(sid))
                existing = lookup.get((name, group))
                # Keep the value — all numeric entries for the same name/group
                # should resolve to the same Scoutnet ID.
                if existing is not None and existing != numeric_id:
                    print(f"  Warning: troop name '{name}' in group '{group}' "
                          f"maps to multiple scoutnet_ids: {existing} and {numeric_id}")
                lookup[(name, group)] = numeric_id
        return lookup

    def _resolve_troop_scoutnet_id(self, raw_id, troop_name: str,
                                    scout_group_key: str,
                                    scout_group_id: int,
                                    name_lookup: Dict[Tuple[str, str], int]) -> int:
        """Resolve a troop's scoutnet_id to an int.

        Strategy (in order):
        1. If raw_id is already numeric, normalize and return it.
        2. Look up (troop_name, scout_group_key) in the name lookup built
           from troops that have a valid numeric ID in another semester.
        3. Fall back to an ID from the reserved range 250-1000.
        """
        if raw_id is not None:
            sid = str(raw_id)
            if sid.lstrip('-').isdigit():
                return self._normalize_to_signed_int32(int(sid))

        # Try name-based lookup
        name_key = (troop_name.strip(), scout_group_key)
        if name_key in name_lookup:
            return name_lookup[name_key]

        # Last resort: assign from reserved range 250-1000
        generated = self._get_local_troop_id(troop_name, scout_group_id)
        print(f"  Info: assigned local troop id {generated} for "
              f"'{troop_name}' in group {scout_group_id}")
        return generated

    def transform_troops(self, raw: List[Dict], semesters_lookup: Dict) -> List[Dict]:
        # First pass: build name -> numeric scoutnet_id lookup
        name_lookup = self._build_troop_name_to_id_lookup(raw)
        print(f"  Built troop name lookup with {len(name_lookup)} entries")

        result = []
        for r in raw:
            scoutnet_id = r.get('scoutnet_id')

            # Resolve scout_group_id
            troop_key = str(r.get('id'))
            scout_group_key = r.get('scout_group_id', '')
            scout_group_id = self.extract_group_key_id(scout_group_key)

            semester_key = r.get('semester_id')
            semester_id = None
            if semester_key:
                semester_id = int(semester_key.split('-')[0])*10 + (0 if semester_key.split('-')[1] == '0' else 1)

            default_time = r.get('defaultstarttime', '18:30')
            if isinstance(default_time, dict):
                default_time = '18:30'

            # Extract troop info from key
            troop = self.extract_troop_key_id(troop_key)
            if troop:
                raw_scoutnet_id = troop[0]
                troop_semester_id = troop[2]
            else:
                raw_scoutnet_id = scoutnet_id
                troop_semester_id = semester_id

            troop_name = r.get('name', '')
            troop_scoutnet_id = self._resolve_troop_scoutnet_id(
                raw_scoutnet_id, troop_name, scout_group_key,
                scout_group_id, name_lookup)

            # Remember the mapping so downstream transforms can resolve the same key
            self.troop_key_to_scoutnet_id[troop_key] = troop_scoutnet_id

            # Composite key: scoutnet_id + scout_group_id + semester_id
            composite_id = f"{troop_scoutnet_id}-{scout_group_id}-{troop_semester_id}"

            if composite_id in self.all_troops_set:
                print(f'  Warning: duplicate composite troop id {composite_id}')
                continue
            self.all_troops_set.add(composite_id)

            result.append({
                'id': composite_id,
                'scoutnet_id': troop_scoutnet_id,
                'scout_group_id': scout_group_id,
                'semester_id': troop_semester_id,
                'name': troop_name,
                'default_start_time': str(default_time),
                'default_duration_minutes': r.get('defaultduration', 90),
                'report_id': r.get('rapportID')
            })
        return result

    def _create_troops_from_references(self, troops: List[Dict],
                                        raw_troop_persons: List[Dict],
                                        raw_meetings: List[Dict]) -> List[Dict]:
        """Create missing Troop records referenced by TroopPerson or Meeting data.

        In the old v1 system, some troops had member assignments and meetings
        but no Troop entity was ever created (or it was deleted). We create
        minimal troop records so the downstream data is preserved.
        """
        # Collect all troop keys referenced by troop_persons and meetings
        referenced_keys: set[str] = set()
        for r in raw_troop_persons:
            tk = r.get('troop_id')
            if tk:
                referenced_keys.add(tk)
        for m in raw_meetings:
            tk = m.get('troop_id')
            if tk:
                referenced_keys.add(tk)

        # Find which ones don't have a Troop record
        existing_keys = set(str(t.get('id')) for t in troops if t.get('id'))
        missing_keys = set()
        for k in referenced_keys:
            if k not in existing_keys:
                # Check if the composite ID exists in all_troops_set
                troop_id = self.extract_troop_key_id(k)
                if troop_id:
                    scoutnet_id = self._resolve_troop_key_scoutnet_id(k, troop_id)
                    composite_id = f"{scoutnet_id}-{troop_id[1]}-{troop_id[2]}"
                    if composite_id not in self.all_troops_set:
                        missing_keys.add(k)

        if not missing_keys:
            return []

        # For legacy troop keys with semester_id=0, collect semesters from meeting dates
        key_semesters: Dict[str, set] = {}
        for m in raw_meetings:
            tk = m.get('troop_id')
            if tk in missing_keys:
                date_str = m.get('meeting_date', '')
                if date_str:
                    sem = self._semester_id_from_date(date_str)
                    if sem:
                        key_semesters.setdefault(tk, set()).add(sem)

        # Resolve troop names from legacy keys
        key_names: Dict[str, str] = {}
        for k in missing_keys:
            legacy = self._resolve_legacy_troop_key(k)
            if legacy:
                key_names[k] = legacy[0].capitalize()

        created = []
        for k in sorted(missing_keys):
            troop_id = self.extract_troop_key_id(k)
            if not troop_id:
                continue

            scoutnet_id = self._resolve_troop_key_scoutnet_id(k, troop_id)
            scout_group_id = troop_id[1]
            semester_id = troop_id[2]
            troop_name = key_names.get(k, f'Avdelning {scoutnet_id}')

            if not scout_group_id:
                continue

            # Determine which semesters to create troops for
            if semester_id:
                semesters_to_create = [semester_id]
            else:
                # Legacy key without semester — create one troop per semester found in meetings
                semesters_to_create = sorted(key_semesters.get(k, set()))
                if not semesters_to_create:
                    continue  # No meetings → nothing to create

            for sem in semesters_to_create:
                composite_id = f"{scoutnet_id}-{scout_group_id}-{sem}"
                if composite_id in self.all_troops_set:
                    continue

                self.all_troops_set.add(composite_id)
                self.troop_key_to_scoutnet_id[k] = scoutnet_id

                created.append({
                    'id': composite_id,
                    'scoutnet_id': scoutnet_id,
                    'scout_group_id': scout_group_id,
                    'semester_id': sem,
                    'name': troop_name,
                    'default_start_time': '18:30',
                    'default_duration_minutes': 90,
                    'report_id': None
                })

        if created:
            print(f"  Created {len(created)} troop(s) from TroopPerson/Meeting references")

        return created

    def _resolve_troop_key_scoutnet_id(self, raw_troop_key: str, troop_id_tuple: tuple) -> int:
        """Resolve a raw troop key to a scoutnet_id int.

        Uses the troop_key_to_scoutnet_id mapping built during transform_troops.
        Falls back to signed-int normalization for numeric values or the
        reserved range 250-1000 for string IDs.
        """
        if raw_troop_key in self.troop_key_to_scoutnet_id:
            return self.troop_key_to_scoutnet_id[raw_troop_key]
        # Fallback: try to normalize directly
        raw_id = troop_id_tuple[0]
        sid = str(raw_id)
        if sid.lstrip('-').isdigit():
            return self._normalize_to_signed_int32(int(sid))
        scout_group_id = troop_id_tuple[1] if len(troop_id_tuple) > 1 else 0
        return self._get_local_troop_id(sid, scout_group_id)

    def transform_troop_persons(self, raw: List[Dict]) -> List[Dict]:
        result = []
        seen = set()
        skipped_missing_troop = 0
        skipped_missing_person = 0
        skipped_pnr_person = 0

        for r in raw:
            troop_key = r.get('troop_id')
            person_id = r.get('person_id')

            if not troop_key or not person_id:
                continue

            troop_id = self.extract_troop_key_id(troop_key)
            if not troop_id:
                print(f'  Warning: could not extract troop_id from {r}')
                continue

            scoutnet_id = self._resolve_troop_key_scoutnet_id(troop_key, troop_id)

            # Composite key: scoutnet_id + scout_group_id + semester_id
            troop_composite_id = f"{scoutnet_id}-{troop_id[1]}-{troop_id[2]}"

            # Dedupe key includes person_id
            key = (troop_composite_id, int(person_id))
            if key in seen:
                continue
            seen.add(key)

            if troop_composite_id not in self.all_troops_set:
                skipped_missing_troop += 1
                continue
            if person_id not in self.all_persons_set:
                if person_id > 19000000:
                    skipped_pnr_person += 1
                else:
                    skipped_missing_person += 1
                continue

            result.append({
                'troop_id': troop_composite_id,
                'scoutnet_id': scoutnet_id,
                'scout_group_id': troop_id[1],
                'semester_id': troop_id[2],
                'person_id': person_id,
                'is_leader': r.get('is_leader', False)
            })

        if skipped_missing_troop:
            print(f"  Skipped {skipped_missing_troop} TroopPerson(s) with missing troop")
        if skipped_missing_person:
            print(f"  Skipped {skipped_missing_person} TroopPerson(s) with missing person (deleted from Scoutnet)")
        if skipped_pnr_person:
            print(f"  Skipped {skipped_pnr_person} TroopPerson(s) with unresolvable personnummer-as-ID (person not in DB)")

        return result
    
    def transform_meetings(self, raw: List[Dict], raw_meeting_attendances: List[Dict]) -> Tuple[List[Dict], List[Dict]]:
        meetings = []
        attendances = []
        seen_meetings = set()
        seen_attendances = set()

        for r in raw:
            troop_key = r.get('troop_id')
            troop_id_tuple = self.extract_troop_key_id(troop_key)
            if not troop_id_tuple:
                print(f'  Warning: can not extract troop_id_key from {r}')
                continue

            scoutnet_troop_id = self._resolve_troop_key_scoutnet_id(troop_key, troop_id_tuple)

            d = r.get('meeting_date')
            t = r.get('start_time', '18:30')

            # Normalize duration_minutes to signed 32-bit int
            default_duration = int(90)
            duration_minutes = r.get('duration_minutes', default_duration)
            duration_minutes = (((duration_minutes & 0xffffffff) ^ 0x80000000) - 0x80000000)
            if duration_minutes == 0:
                duration_minutes = default_duration
            elif duration_minutes <= 0:
                duration_minutes = -duration_minutes
            elif duration_minutes > 1440:
                duration_minutes = 1440

            id = r.get("id")
            meeting_id_tuple = self.extract_meeting_key_id(id)
            # example: 18309/lerumsscoutkår/2020-ht-2020-10-15 -> (18309, group_id, semester_id, 2020-10-15)

            new_id = f"{meeting_id_tuple[0]}-{meeting_id_tuple[1]}-{meeting_id_tuple[3]}"
            if new_id in seen_meetings:
                print(f'  Warning: duplicate meeting id {new_id}')
                continue
            seen_meetings.add(new_id)

            meetings.append({
                'ScoutnetTroopId': scoutnet_troop_id,
                'GroupId': troop_id_tuple[1],
                'SemesterId': troop_id_tuple[2],
                'MeetingDate': d,
                'StartTime': t,
                'Name': r.get('name', 'Meeting'),
                'DurationMinutes': duration_minutes,
                'IsHike': r.get('is_hike', False)
            })

        for a in raw_meeting_attendances:
            meeting_id = a.get('meeting_id')
            person_id = a.get('person_id')
            meeting_id_tuple = self.extract_meeting_key_id(meeting_id)
            if not meeting_id_tuple:
                print(f'  Warning: can not extract meeting_id_tuple from {a}')
                continue

            meeting_troop_id = self._resolve_troop_key_scoutnet_id(meeting_id, meeting_id_tuple)
            attendances.append({
                'TroopScoutnetId': meeting_troop_id,
                'GroupId': meeting_id_tuple[1],
                'SemesterId': meeting_id_tuple[2],
                'PersonId': int(person_id),
                'MeetingDate': meeting_id_tuple[3]
            })

        return meetings, attendances
    
    def transform_users(self, raw: List[Dict]) -> List[Dict]:
        result = []
        seen = set()
        
        for r in raw:
            id = r.get('id')
            if not id or id in seen:
                continue
            seen.add(id)
            
            sg_key = r.get('scout_group_id')
            if not sg_key:
                continue # users with no groupaccess has not access to anything in skojjt, ignore.
            scout_group_id = self.extract_group_key_id(sg_key)
            
            result.append({
                'Id': id,
                'Email': r.get('email'),
                'Name': r.get('name'),
                'ScoutGroupId': scout_group_id,
                'ActiveSemesterId': None,
                'HasAccess': r.get('has_access', False),
                'IsAdmin': r.get('is_admin', False)
            })
        return result
    
    def transform_badges(self, raw: List[Dict]) -> Tuple[List[Dict], Dict[str, int]]:
        result = []
        id_map = {}
        
        for idx, r in enumerate(raw, start=1):
            id = r.get('id')
            id_map[id] = idx
            
            sg_key = r.get('scout_group_id')
            scout_group_id = self.extract_group_key_id(sg_key)
            
            result.append({
                'Id': int(id),
                'ScoutGroupId': scout_group_id,
                'Name': r.get('name'),
                'Description': r.get('description'),
                'PartsScoutShort': r.get('parts_scout_short', []) or [],
                'PartsScoutLong': r.get('parts_scout_long', []) or [],
                'PartsAdminShort': r.get('parts_admin_short', []) or [],
                'PartsAdminLong': r.get('parts_admin_long', []) or [],
                'ImageUrl': r.get('image_url')
            })
        
        return result, id_map
    
    def transform_badge_parts_done(self, raw: List[Dict], badge_id_map: Dict[str, int]) -> List[Dict]:
        result = []
        seen = set()
        
        for r in raw:
            person_id = r.get('person_id')
            old_badge_id = r.get('badge_id')
            badge_id = badge_id_map.get(old_badge_id)
            
            if not person_id or not badge_id:
                continue
            
            part_idx = r.get('idx', 0)
            is_scout_part = r.get('is_scout_part', True)
            
            key = (int(person_id), badge_id, part_idx, is_scout_part)
            if key in seen:
                continue
            seen.add(key)
            
            result.append({
                'PersonId': int(person_id),
                'BadgeId': badge_id,
                'PartIndex': part_idx,
                'IsScoutPart': is_scout_part,
                'ExaminerName': r.get('examiner_name'),
                'CompletedDate': self.parse_date(r.get('completed_date'))
            })
        return result
    
    def transform_badges_completed(self, raw: List[Dict], badge_id_map: Dict[str, int]) -> List[Dict]:
        result = []
        seen = set()
        
        for r in raw:
            person_id = r.get('person_id')
            old_badge_id = r.get('badge_id')
            badge_id = badge_id_map.get(old_badge_id)
            
            if not person_id or not badge_id:
                continue
            
            key = (int(person_id), badge_id)
            if key in seen:
                continue
            seen.add(key)
            
            result.append({
                'PersonId': int(person_id),
                'BadgeId': badge_id,
                'Examiner': r.get('examiner'),
                'CompletedDate': self.parse_date(r.get('completed_date'))
            })
        return result
    
    def transform_troop_badges(self, raw: List[Dict], badge_id_map: Dict[str, int]) -> List[Dict]:
        result = []
        seen = set()

        for r in raw:
            troop_key = r.get('troop_id')
            # TroopId/GroupId/SemesterId
            troop_id = self.extract_troop_key_id(troop_key)
            old_badge_id = r.get('badge_id')
            badge_id = badge_id_map.get(old_badge_id)

            if not troop_id or not badge_id:
                continue

            scoutnet_id = self._resolve_troop_key_scoutnet_id(troop_key, troop_id)

            key = (scoutnet_id, troop_id[1], troop_id[2], badge_id)
            if key in seen:
                continue
            seen.add(key)

            result.append({
                'ScoutnetTroopId': scoutnet_id,
                'ScoutGroupId': troop_id[1],
                'SemesterId': troop_id[2],
                'BadgeId': badge_id,
                'SortOrder': r.get('sort_order')
            })
        return result
    
    def transform_badge_templates(self, raw: List[Dict]) -> List[Dict]:
        result = []
        for idx, r in enumerate(raw, start=1):
            result.append({
                'Id': idx,
                'Name': r.get('name'),
                'Description': r.get('description'),
                'PartsScoutShort': r.get('parts_scout_short', []) or [],
                'PartsScoutLong': r.get('parts_scout_long', []) or [],
                'PartsAdminShort': r.get('parts_admin_short', []) or [],
                'PartsAdminLong': r.get('parts_admin_long', []) or [],
                'ImageUrl': r.get('img_url')
            })
        return result
    
    def transform_all(self):
        print("Transforming data for PostgreSQL import...")
        print(f"  Input directory: {self.input_dir}")
        print(f"  Output directory: {self.output_dir}")
        print()
        
        # Load ScoutGroups first to build lookup
        print("Loading ScoutGroup for lookup...")
        raw_groups = self.load_json('scout_groups.json')
        self.build_scout_group_lookup(raw_groups)
        
        # 1. Semesters
        print("Transforming Semester...")
        raw_semesters = self.load_json('semesters.json')
        semesters = self.transform_semesters(raw_semesters)
        self.save_json('semesters.json', semesters)
        
        # Build lookup - maps old key to new int ID: (year*10)+(1 if autumn else 0)
        semesters_lookup = {}
        for s in raw_semesters:
            key = s.get('_key', {})
            key_id = key.get('id') or key.get('name')
            if key_id:
                year = s.get('year', 0)
                is_autumn = s.get('ht', False)
                semesters_lookup[key_id] = (year * 10) + (1 if is_autumn else 0)
        
        # 2. ScoutGroups
        print("Transforming ScoutGroup...")
        groups = self.transform_scout_groups(raw_groups)
        self.save_json('scout_groups.json', groups)
        
        # 3. Persons
        print("Transforming Person...")
        raw_persons = self.load_json('persons.json')
        persons = self.transform_persons(raw_persons)

        # 3b. Remap personnummer-as-ID references to real Scoutnet member numbers.
        # In v1, some groups used personnummer as the person ID. We fix this
        # before creating stubs so remapped persons don't get unnecessary stubs.
        raw_troop_persons = self.load_json('troop_persons.json')
        raw_meeting_attendances = self.load_json('meeting_attendances.json')
        pnr_to_id, birth_group_to_id = self._build_personnummer_remap(raw_persons)
        self._apply_person_id_remap(
            raw_troop_persons, raw_meeting_attendances,
            pnr_to_id, birth_group_to_id)

        # 3c. Create stub records for remaining orphaned person references
        # (persons deleted from Scoutnet but still referenced by TroopPerson/Attendance)
        stubs = self._create_stub_persons(raw_troop_persons, raw_meeting_attendances)
        if stubs:
            persons.extend(stubs)

        self.save_json('persons.json', persons)
        
        # 4. Troops
        print("Transforming Troop...")
        raw_troops = self.load_json('troops.json')
        troops = self.transform_troops(raw_troops, semesters_lookup)

        # 4b. Create missing Troop records referenced by TroopPerson/Meeting data.
        # In v1, some troops had members and meetings but no Troop entity.
        raw_meetings = self.load_json('meetings.json')
        stub_troops = self._create_troops_from_references(
            raw_troops, raw_troop_persons, raw_meetings)
        if stub_troops:
            troops.extend(stub_troops)

        self.save_json('troops.json', troops)

        # 5. TroopPersons (already loaded above for stub detection)
        print("Transforming TroopPerson...")
        troop_persons = self.transform_troop_persons(raw_troop_persons)
        self.save_json('troop_persons.json', troop_persons)

        # 6. Meetings + Attendances (attendances already loaded above, meetings reused)
        print("Transforming Meeting...")
        meetings, attendances = self.transform_meetings(raw_meetings, raw_meeting_attendances)
        self.save_json('meetings.json', meetings)
        self.save_json('meeting_attendances.json', attendances)
        
        # 7. Users
        print("Transforming UserPrefs...")
        raw_users = self.load_json('users.json')
        users = self.transform_users(raw_users)
        self.save_json('users.json', users)
        
        # 8. BadgeTemplates
        print("Transforming BadgeTemplate...")
        raw_templates = self.load_json('badge_templates.json')
        templates = self.transform_badge_templates(raw_templates)
        self.save_json('badge_templates.json', templates)
        
        # 9. Badges
        print("Transforming Badge...")
        raw_badges = self.load_json('badges.json')
        badges, badge_id_map = self.transform_badges(raw_badges)
        self.save_json('badges.json', badges)
        self.save_json('_badge_id_map.json', badge_id_map)
        
        # 10. TroopBadges
        print("Transforming TroopBadge...")
        raw_troop_badges = self.load_json('troop_badges.json')
        troop_badges = self.transform_troop_badges(raw_troop_badges, badge_id_map)
        self.save_json('troop_badges.json', troop_badges)
        
        # 11. BadgePartsDone
        print("Transforming BadgePartDone...")
        raw_parts = self.load_json('badge_parts_done.json')
        parts_done = self.transform_badge_parts_done(raw_parts, badge_id_map)
        self.save_json('badge_parts_done.json', parts_done)
        
        # 12. BadgesCompleted
        print("Transforming BadgeCompleted...")
        raw_completed = self.load_json('badges_completed.json')
        completed = self.transform_badges_completed(raw_completed, badge_id_map)
        self.save_json('badges_completed.json', completed)
        
        print()
        print("Transformation complete!")
        print()
        print("Summary:")
        for filename, count in self.stats.items():
            print(f"  {filename}: {count} records")


def transform(input_dir:str, output_dir:str):
    transformer = DataTransformer(input_dir, output_dir)
    transformer.transform_all()


def main():
    parser = argparse.ArgumentParser(description='Transform Datastore JSON to PostgreSQL format')
    parser.add_argument('--input-dir', required=True, help='Directory with raw JSON exports')
    parser.add_argument('--output-dir', default='./pg_import', help='Output directory')
    args = parser.parse_args()

    transform(args.input_dir, args.output_dir)
   


if __name__ == '__main__':
    main()
