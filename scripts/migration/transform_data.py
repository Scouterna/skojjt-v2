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
    
    def extract_troop_key_id(self, key_obj:str) -> tuple[int, int, int]:
        """Extract TroopId/GroupId/SemesterId from a Datastore key object."""
        s = key_obj.split('/')
        if len(s) < 2:
            if key_obj in self.bad_troop_keys:
                return self.bad_troop_keys[key_obj]
            else:
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
        return None

    def extract_meeting_key_id(self, key_obj:str) -> tuple[int, int, int, str]:
        """Extract TroopId/GroupId/SemesterId-date from a Datastore key object."""
        # example: 18309/lerumsscoutkår/2020-ht-2020-10-15
        s = key_obj.split('/')
        if len(s) < 2:
            if key_obj in self.bad_troop_keys:
                return self.bad_troop_keys[key_obj]
            else:
                # inventing a new troop id for bad troop keys
                group_id = 0
                for k, v in self.scout_group_key_to_id.items():
                    if k in key_obj:
                        group_id = v
                        break
                self.bad_troop_counter += 1
                new_troop = (self.bad_troop_counter, group_id, 0, "")
                self.bad_troop_keys[key_obj] = new_troop
                return new_troop
        first_split = key_obj.split('/', 1)
        troop_part = first_split[0]
        remainder = first_split[1] if len(first_split) > 1 else ''
        last_split = remainder.rsplit('/', 1)
        if len(last_split) == 2:
            group_part = last_split[0]
            semester_date_part = last_split[1]
            semester_date_part_split = semester_date_part.split('.')
            semester_part = semester_date_part_split[0]
            date_part = semester_date_part_split[1]
            troop_id = ((int(troop_part) & 0xffffffff) ^ 0x80000000) - 0x80000000
            group_id = self.extract_group_key_id(group_part)
            semester_id = self.convert_semester_id(semester_part)
            return (troop_id, group_id, semester_id, date_part)
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
    
    def transform_troops(self, raw: List[Dict], semesters_lookup: Dict) -> List[Dict]:
        result = []
        for r in raw:
            scoutnet_id = r.get('scoutnet_id')
            
            # Resolve scout_group_id
            troop_key = str(r.get('id'))
            scout_group_id = self.extract_group_key_id(r.get('scout_group_id'))
            
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
                troop_scoutnet_id = troop[0]
                troop_semester_id = troop[2]
            else:
                troop_scoutnet_id = int(scoutnet_id) if scoutnet_id else 0
                troop_semester_id = semester_id

            # Normalize scoutnet_id to signed 32-bit int
            troop_scoutnet_id = int(troop_scoutnet_id) & 0xffffffff
            troop_scoutnet_id = (troop_scoutnet_id ^ 0x80000000) - 0x80000000
            
            # Composite key: scoutnet_id + semester_id
            composite_id = f"{troop_scoutnet_id}-{troop_semester_id}"

            if composite_id in self.all_troops_set:
                print(f'  Warning: duplicate composite troop id {composite_id}')
                continue
            self.all_troops_set.add(composite_id)
            
            result.append({
                'id': composite_id,
                'scoutnet_id': troop_scoutnet_id,
                'scout_group_id': scout_group_id,
                'semester_id': troop_semester_id,
                'name': r.get('name', ''),
                'default_start_time': str(default_time),
                'default_duration_minutes': r.get('defaultduration', 90),
                'report_id': r.get('rapportID')
            })
        return result
    
    def transform_troop_persons(self, raw: List[Dict]) -> List[Dict]:
        result = []
        seen = set()
        
        for r in raw:
            troop_key = r.get('troop_id')
            person_id = r.get('person_id')
            
            if not troop_key or not person_id:
                continue

            troop_id = self.extract_troop_key_id(troop_key)
            if not troop_id:
                print(f'  Warning: could not extract troop_id from {r}')
                continue
            
            # Composite key: scoutnet_id + semester_id
            troop_composite_id = f"{troop_id[0]}-{troop_id[2]}"
            
            # Dedupe key includes person_id
            key = (troop_composite_id, int(person_id))
            if key in seen:
                continue
            seen.add(key)

            if troop_composite_id not in self.all_troops_set:
                print(f'  Warning: removing TroopPerson with missing troop_id={troop_composite_id}')
                continue
            if person_id not in self.all_persons_set:
                print(f'  Warning: removing TroopPerson with missing person_id={person_id}')
                continue
            
            result.append({
                'troop_id': troop_composite_id,
                'scoutnet_id': troop_id[0],
                'scout_group_id': troop_id[1],
                'semester_id': troop_id[2],
                'person_id': person_id,
                'is_leader': r.get('is_leader', False)
            })
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

            # Composite troop key: scoutnet_id + semester_id
            troop_composite_id = f"{troop_id_tuple[0]}-{troop_id_tuple[2]}"

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
            """Extract TroopId/GroupId/SemesterId-date from a Datastore key object."""
            # example: 18309/lerumsscoutkår/2020-ht-2020-10-15 -> (18309, lerumsscoutkår, 2020-ht, 2020-10-15)

            new_id = f"{meeting_id_tuple[0]}-{meeting_id_tuple[1]}-{meeting_id_tuple[3]}"
            if new_id in seen_meetings:
                print(f'  Warning: duplicate meeting id {new_id}')
                continue
            seen_meetings.add(new_id)
            ScoutnetTroopId = ((int(troop_id_tuple[0]) & 0xffffffff) ^ 0x80000000) - 0x80000000
            

            meetings.append({
                'ScoutnetTroopId': ScoutnetTroopId,
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
            
            # Composite troop key: scoutnet_id + semester_id
            troop_composite_id = f"{meeting_id_tuple[0]}-{meeting_id_tuple[2]}"
            meeting_id = f"{meeting_id_tuple[0]}-{meeting_id_tuple[1]}-{meeting_id_tuple[3]}"
            attendances.append({
            'TroopScoutnetId': meeting_id_tuple[0],
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
            
            key = (troop_id[0], troop_id[2], badge_id)
            if key in seen:
                continue
            seen.add(key)
            
            result.append({
                'ScoutnetTroopId': troop_id[0],
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
        self.save_json('persons.json', persons)
        
        # 4. Troops
        print("Transforming Troop...")
        raw_troops = self.load_json('troops.json')
        troops = self.transform_troops(raw_troops, semesters_lookup)
        self.save_json('troops.json', troops)
        
        # 5. TroopPersons
        print("Transforming TroopPerson...")
        raw_troop_persons = self.load_json('troop_persons.json')
        troop_persons = self.transform_troop_persons(raw_troop_persons)
        self.save_json('troop_persons.json', troop_persons)
        
        # 6. Meetings + Attendances
        print("Transforming Meeting...")
        raw_meetings = self.load_json('meetings.json')
        raw_meeting_attendances = self.load_json('meeting_attendances.json')
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
