# -*- coding: utf-8 -*-
#!/usr/bin/env python3
"""
Convert Google Cloud Datastore LevelDB export to JSON files.
"""

import argparse
import json
import os
import struct
from datetime import datetime, timezone, timedelta
from typing import Any, Dict, List, Optional

HAS_PROTOBUF = True  # We'll do manual parsing


class DatastoreExportConverter:
    """Converts Datastore LevelDB export files to JSON."""

    def __init__(self, export_dir: str, output_dir: str):
        self.export_dir = export_dir
        self.output_dir = output_dir
        self.stats = {}
        self._all_kinds_cache = None  # Lazy cache for all_kinds entities grouped by kind
    
    @staticmethod
    def read_varint(data: bytes, pos: int) -> tuple:
        """Read a varint from data at position, return (value, new_pos)."""
        result = 0
        shift = 0
        while pos < len(data):
            b = data[pos]
            result |= (b & 0x7f) << shift
            pos += 1
            if not (b & 0x80):
                break
            shift += 7
        return result, pos
    
    @staticmethod
    def read_signed_varint(data: bytes, pos: int) -> tuple:
        """Read a signed varint (zigzag encoded)."""
        val, pos = DatastoreExportConverter.read_varint(data, pos)
        return (val >> 1) ^ -(val & 1), pos
    
    def read_leveldb_records(self, file_path: str) -> List[bytes]:
        """Read LevelDB records from a file."""
        with open(file_path, 'rb') as f:
            data = f.read()
        
        records = []
        pos = 0
        pending = b''
        
        while pos + 7 <= len(data):
            crc = struct.unpack('<I', data[pos:pos+4])[0]
            length = struct.unpack('<H', data[pos+4:pos+6])[0]
            record_type = data[pos+6]
            
            if length == 0 or pos + 7 + length > len(data):
                break
            
            record_data = data[pos+7:pos+7+length]
            pos += 7 + length
            
            if record_type == 1:  # FULL
                records.append(record_data)
            elif record_type == 2:  # FIRST
                pending = record_data
            elif record_type == 3:  # MIDDLE
                pending += record_data
            elif record_type == 4:  # LAST
                pending += record_data
                records.append(pending)
                pending = b''
        
        return records
    
    def parse_key(self, key_data: bytes) -> Dict:
        """Parse a Key protobuf (uses field 13 for partition, 14 for path)."""
        result = {"_type": "key", "path": []}
        pos = 0
        
        while pos < len(key_data):
            if pos >= len(key_data):
                break
            fb = key_data[pos]
            fn = fb >> 3
            wt = fb & 0x07
            pos += 1
            
            if wt == 2:  # Length-delimited
                flen, pos = self.read_varint(key_data, pos)
                if pos + flen > len(key_data):
                    break
                fd = key_data[pos:pos+flen]
                pos += flen
                
                if fn == 13:  # partition_id (nested, field 13 is app)
                    # Parse nested partition_id
                    ppos = 0
                    while ppos < len(fd):
                        pb = fd[ppos]
                        pf = pb >> 3
                        pw = pb & 0x07
                        ppos += 1
                        if pw == 2:
                            pl, ppos = self.read_varint(fd, ppos)
                            pd = fd[ppos:ppos+pl]
                            ppos += pl
                            if pf == 13:  # app
                                result["project"] = pd.decode('utf-8', errors='replace')
                        elif pw == 0:
                            _, ppos = self.read_varint(fd, ppos)
                        
                elif fn == 14:  # path (repeated PathElement using groups)
                    ppos = 0
                    current_elem = {}
                    while ppos < len(fd):
                        pb = fd[ppos]
                        pf = pb >> 3
                        pw = pb & 0x07
                        ppos += 1
                        
                        if pw == 3:  # Start group
                            current_elem = {}
                        elif pw == 4:  # End group
                            if current_elem:
                                result["path"].append(current_elem)
                            current_elem = {}
                        elif pw == 2:  # Length-delimited
                            pl, ppos = self.read_varint(fd, ppos)
                            pd = fd[ppos:ppos+pl]
                            ppos += pl
                            if pf == 2:  # kind
                                current_elem["kind"] = pd.decode('utf-8', errors='replace')
                            elif pf == 4:  # name (field 4 in PathElement)
                                current_elem["name"] = pd.decode('utf-8', errors='replace')
                        elif pw == 0:  # Varint
                            pval, ppos = self.read_varint(fd, ppos)
                            if pf == 3:  # id
                                current_elem["id"] = pval
            elif wt == 0:
                _, pos = self.read_varint(key_data, pos)
        
        # Set convenience fields
        if result["path"]:
            last = result["path"][-1]
            result["kind"] = last.get("kind")
            if "id" in last:
                result["id"] = last["id"]
            if "name" in last:
                result["name"] = last["name"]
        
        return result
    
    def parse_value(self, value_data: bytes) -> Any:
        """Parse a Value protobuf.
        
        Value field mapping for Datastore export format:
        - 1: integer_value (varint, can be signed via zigzag or wire type 1)
        - 2: boolean_value (varint)
        - 3: string_value (string)
        - 4: double_value (fixed64)
        - 5: timestamp_value (nested Timestamp)
        - 6: blob_value (bytes)
        - 7: array_value (nested ArrayValue)
        - 10: meaning (varint) - type hint
        - 13/14/15/17: key_value fields (nested Key structure)
        """
        if len(value_data) == 0:
            return None
            
        pos = 0
        # Check if this looks like a key reference (has field 13 = partition)
        if len(value_data) > 2 and value_data[0] == 0x6a:  # field 13, wire type 2
            return self.parse_key_from_value(value_data)
        
        meaning = None  # Type hint from field 10
        
        # First pass: look for meaning field to determine type
        temp_pos = 0
        while temp_pos < len(value_data):
            if temp_pos >= len(value_data):
                break
            fb = value_data[temp_pos]
            fn = fb >> 3
            wt = fb & 0x07
            temp_pos += 1
            
            if wt == 0:  # Varint
                val, temp_pos = self.read_varint(value_data, temp_pos)
                if fn == 10:  # meaning field
                    meaning = val
            elif wt == 1:  # 64-bit fixed
                temp_pos += 8
            elif wt == 2:  # Length-delimited
                flen, temp_pos = self.read_varint(value_data, temp_pos)
                temp_pos += flen
            elif wt == 5:  # 32-bit fixed
                temp_pos += 4
        
        while pos < len(value_data):
            if pos >= len(value_data):
                break
            fb = value_data[pos]
            fn = fb >> 3
            wt = fb & 0x07
            pos += 1
            
            if wt == 0:  # Varint
                val, pos = self.read_varint(value_data, pos)
                if fn == 1:  # integer_value
                    # Check if this should be a timestamp based on meaning
                    # meaning=15 is GD_WHEN (date), meaning=7 is also date-related
                    if meaning in (15, 7, 20):  # Date meanings
                        # Value is microseconds since epoch, convert to datetime
                        # For dates before 1970, the value might need sign conversion
                        try:
                            microseconds = val
                            # Try as-is first, check if reasonable
                            dt = self._microseconds_to_datetime(microseconds)
                            if dt and 1900 <= dt.year <= 2100:
                                return {"_type": "datetime", "value": dt.isoformat()}
                            # Try as signed 64-bit
                            signed_val = struct.unpack('<q', struct.pack('<Q', val & 0xFFFFFFFFFFFFFFFF))[0]
                            dt = self._microseconds_to_datetime(signed_val)
                            if dt:
                                return {"_type": "datetime", "value": dt.isoformat()}
                            return val
                        except:
                            return val
                    return val
                elif fn == 2:  # boolean_value
                    return val != 0
                elif fn == 10:  # meaning - already captured above
                    pass
            elif wt == 1:  # 64-bit fixed (signed integer for dates before 1970)
                if pos + 8 > len(value_data):
                    break
                if fn == 4:  # double_value
                    val = struct.unpack('<d', value_data[pos:pos+8])[0]
                    pos += 8
                    return val
                # Field 1 can be 64-bit fixed for large/negative integers
                if fn == 1:
                    val = struct.unpack('<q', value_data[pos:pos+8])[0]  # signed
                    pos += 8
                    # Check if this should be a timestamp
                    if meaning in (15, 7, 20):
                        dt = self._microseconds_to_datetime(val)
                        if dt:
                            return {"_type": "datetime", "value": dt.isoformat()}
                        return val
                    return val
                pos += 8
            elif wt == 2:  # Length-delimited
                flen, pos = self.read_varint(value_data, pos)
                if pos + flen > len(value_data):
                    break
                fd = value_data[pos:pos+flen]
                pos += flen
                
                if fn == 3:  # string_value
                    return fd.decode('utf-8', errors='replace')
                elif fn == 5:  # timestamp_value (nested Timestamp protobuf)
                    ts_pos = 0
                    seconds = 0
                    nanos = 0
                    while ts_pos < len(fd):
                        if ts_pos >= len(fd):
                            break
                        tb = fd[ts_pos]
                        tf = tb >> 3
                        tw = tb & 0x07
                        ts_pos += 1
                        if tw == 0:
                            tv, ts_pos = self.read_varint(fd, ts_pos)
                            if tf == 1:
                                # Seconds - interpret as signed if needed
                                seconds = struct.unpack('<q', struct.pack('<Q', tv & 0xFFFFFFFFFFFFFFFF))[0]
                            elif tf == 2:
                                nanos = tv
                        elif tw == 1:  # 64-bit fixed for seconds
                            if ts_pos + 8 <= len(fd) and tf == 1:
                                seconds = struct.unpack('<q', fd[ts_pos:ts_pos+8])[0]
                                ts_pos += 8
                    dt = self._seconds_to_datetime(seconds, nanos)
                    if dt:
                        return {"_type": "datetime", "value": dt.isoformat()}
                    return None
                elif fn == 6:  # blob_value
                    return {"_type": "blob", "value": fd.hex()}
                elif fn == 7:  # array_value
                    arr = []
                    apos = 0
                    while apos < len(fd):
                        if apos >= len(fd):
                            break
                        ab = fd[apos]
                        af = ab >> 3
                        aw = ab & 0x07
                        apos += 1
                        if aw == 2 and af == 1:  # values field
                            alen, apos = self.read_varint(fd, apos)
                            if apos + alen <= len(fd):
                                aval = self.parse_value(fd[apos:apos+alen])
                                arr.append(aval)
                                apos += alen
                        elif aw == 0:
                            _, apos = self.read_varint(fd, apos)
                    return arr
                elif fn == 13:  # key_value (start of key structure)
                    # This is a key reference, parse the whole value_data as key
                    return self.parse_key_from_value(value_data)
        return None
    
    def _microseconds_to_datetime(self, microseconds: int) -> Optional[datetime]:
        """Convert microseconds since Unix epoch to datetime.
        
        Handles negative values (dates before 1970) which datetime.fromtimestamp()
        doesn't support on Windows.
        """
        try:
            seconds = microseconds / 1_000_000
            # Use timedelta from epoch to handle negative values
            epoch = datetime(1970, 1, 1, tzinfo=timezone.utc)
            dt = epoch + timedelta(seconds=seconds)
            return dt
        except (ValueError, OverflowError, OSError):
            return None
    
    def _seconds_to_datetime(self, seconds: int, nanos: int = 0) -> Optional[datetime]:
        """Convert seconds since Unix epoch to datetime.
        
        Handles negative values (dates before 1970).
        """
        try:
            epoch = datetime(1970, 1, 1, tzinfo=timezone.utc)
            dt = epoch + timedelta(seconds=seconds, microseconds=nanos // 1000)
            return dt
        except (ValueError, OverflowError, OSError):
            return None
    
    def parse_key_from_value(self, value_data: bytes) -> Dict:
        """Parse a Key from a Value's key_value field.
        
        The key structure in values uses groups (wire type 3/4):
        - field 12: start group (wire type 3) - key wrapper
        - field 13: partition_id.app (string like "e~skojjt")
        - field 14: start group (wire type 3) - path element
        - field 15: kind (string like "Troop")  
        - field 16: id (varint)
        - field 17: name (string) - encoded as 0x8a 0x01 (needs varint decode)
        - field 14: end group (wire type 4)
        - field 12: end group (wire type 4)
        """
        result = {"_type": "key", "path": []}
        pos = 0
        current_elem = {}
        
        while pos < len(value_data):
            if pos >= len(value_data):
                break
            
            # Read field tag (may be multi-byte varint)
            tag, pos = self.read_varint(value_data, pos)
            fn = tag >> 3
            wt = tag & 0x07
            
            if wt == 0:  # Varint
                val, pos = self.read_varint(value_data, pos)
                if fn == 16:  # id
                    current_elem["id"] = val
            elif wt == 2:  # Length-delimited
                flen, pos = self.read_varint(value_data, pos)
                if pos + flen > len(value_data):
                    break
                fd = value_data[pos:pos+flen]
                pos += flen
                
                if fn == 12:  # nested key group as length-delimited
                    nested = self.parse_key_from_value(fd)
                    if nested.get("path"):
                        result["path"].extend(nested["path"])
                elif fn == 13:  # partition_id.app
                    result["project"] = fd.decode('utf-8', errors='replace')
                elif fn == 15:  # kind
                    current_elem["kind"] = fd.decode('utf-8', errors='replace')
                elif fn == 17:  # name
                    current_elem["name"] = fd.decode('utf-8', errors='replace')
            elif wt == 3:  # Start group
                if fn == 14 and current_elem.get("kind"):
                    # Save current element and start new one
                    result["path"].append(current_elem)
                    current_elem = {}
            elif wt == 4:  # End group
                pass
            elif wt == 1:  # 64-bit fixed
                pos += 8
            elif wt == 5:  # 32-bit fixed
                pos += 4
        
        if current_elem and current_elem.get("kind"):
            result["path"].append(current_elem)
        
        # Set convenience fields
        if result["path"]:
            last = result["path"][-1]
            result["kind"] = last.get("kind")
            if "id" in last:
                result["id"] = last["id"]
            if "name" in last:
                result["name"] = last["name"]
        
        return result
    
    def read_key_value(self, data: bytes) -> Any:
        """Read a single key_value (for debugging)."""
        pos = 0
        result = {}
        
        while pos < len(data):
            fb = data[pos]
            fn = fb >> 3
            wt = fb & 0x07
            pos += 1
            
            if wt == 2:  # Length-delimited
                flen, pos = self.read_varint(data, pos)
                fd = data[pos:pos+flen]
                pos += flen
                
                if fn == 13:  # app
                    result["project"] = fd.decode('utf-8', errors='replace')
                elif fn == 14:  # path (repeated PathElement using groups)
                    ppos = 0
                    current_elem = {}
                    while ppos < len(fd):
                        pb = fd[ppos]
                        pf = pb >> 3
                        pw = pb & 0x07
                        ppos += 1
                        
                        if pw == 3:  # Start group
                            current_elem = {}
                        elif pw == 4:  # End group
                            if current_elem:
                                result["path"].append(current_elem)
                            current_elem = {}
                        elif pw == 2:  # Length-delimited
                            pl, ppos = self.read_varint(fd, ppos)
                            pd = fd[ppos:ppos+pl]
                            ppos += pl
                            if pf == 2:  # kind
                                current_elem["kind"] = pd.decode('utf-8', errors='replace')
                            elif pf == 4:  # name (field 4 in PathElement)
                                current_elem["name"] = pd.decode('utf-8', errors='replace')
                        elif pw == 0:  # Varint
                            pval, ppos = self.read_varint(fd, ppos)
                            if pf == 3:  # id
                                current_elem["id"] = pval
            elif wt == 0:  # Varint (exclude_from_indexes)
                _, pos = self.read_varint(data, pos)
        
        return result
    
    def parse_property(self, prop_data: bytes) -> tuple:
        """Parse a Property: returns (name, value)."""
        pos = 0
        name = None
        value = None
        
        while pos < len(prop_data):
            fb = prop_data[pos]
            fn = fb >> 3
            wt = fb & 0x07
            pos += 1
            
            if wt == 2:  # Length-delimited
                flen, pos = self.read_varint(prop_data, pos)
                fd = prop_data[pos:pos+flen]
                pos += flen
                
                if fn == 3:  # name
                    name = fd.decode('utf-8', errors='replace')
                elif fn == 5:  # value
                    value = self.parse_value(fd)
            elif wt == 0:  # Varint (exclude_from_indexes)
                _, pos = self.read_varint(prop_data, pos)
        
        return name, value
    
    def parse_entity_record(self, record: bytes) -> Dict:
        """Parse an entity record from the export.
        
        Note: Repeated properties (like attendingPersons) appear as multiple
        property fields with the same name. We collect them into lists.
        """
        entity = {"_key": None}
        pos = 0
        
        while pos < len(record):
            fb = record[pos]
            fn = fb >> 3
            wt = fb & 0x07
            pos += 1
            
            if wt == 2:  # Length-delimited
                flen, pos = self.read_varint(record, pos)
                if pos + flen > len(record):
                    break
                fd = record[pos:pos+flen]
                pos += flen
                
                if fn == 13:  # key field
                    entity["_key"] = self.parse_key(fd)
                elif fn == 14:  # properties field (repeated)
                    name, value = self.parse_property(fd)
                    if name:
                        # Handle repeated properties by collecting into lists
                        if name in entity:
                            # Property already exists - make it a list or append
                            existing = entity[name]
                            if isinstance(existing, list):
                                existing.append(value)
                            else:
                                entity[name] = [existing, value]
                        else:
                            entity[name] = value
                # Field 16 is version/cursor, skip
            elif wt == 0:  # Varint
                _, pos = self.read_varint(record, pos)
        
        return entity
    
    def _load_all_kinds(self) -> Dict[str, List[Dict]]:
        """Parse all entities from the all_kinds directory, grouped by kind.

        Cached after first call since parsing 1000+ LevelDB files is slow.
        """
        if self._all_kinds_cache is not None:
            return self._all_kinds_cache

        all_kinds_dir = os.path.join(self.export_dir, 'all_namespaces', 'all_kinds')
        if not os.path.exists(all_kinds_dir):
            print(f"Warning: all_kinds directory not found: {all_kinds_dir}")
            self._all_kinds_cache = {}
            return self._all_kinds_cache

        print(f"  Parsing all_kinds directory (this may take a while)...")
        by_kind: Dict[str, List[Dict]] = {}
        output_files = sorted([f for f in os.listdir(all_kinds_dir) if f.startswith('output-')])
        parsed = 0

        for filename in output_files:
            file_path = os.path.join(all_kinds_dir, filename)
            records = self.read_leveldb_records(file_path)
            for record in records:
                try:
                    entity = self.parse_entity_record(record)
                    key = entity.get('_key', {})
                    kind = None
                    if isinstance(key, dict):
                        path = key.get('path', [])
                        if path:
                            kind = path[-1].get('kind')
                    if kind and entity.get('_key'):
                        by_kind.setdefault(kind, []).append(entity)
                        parsed += 1
                except Exception:
                    pass

        print(f"  Parsed {parsed} total entities across {len(by_kind)} kinds")
        for kind, entities in sorted(by_kind.items()):
            print(f"    {kind}: {len(entities)}")

        self._all_kinds_cache = by_kind
        return self._all_kinds_cache

    def convert_kind(self, kind_name: str) -> List[Dict]:
        """Convert all export files for a single entity kind.

        First looks for a per-kind directory (kind_<Name>). If not found,
        falls back to the all_kinds directory used by managed exports.
        """
        kind_dir = os.path.join(self.export_dir, 'all_namespaces', f'kind_{kind_name}')

        if os.path.exists(kind_dir):
            entities = []
            output_files = sorted([f for f in os.listdir(kind_dir) if f.startswith('output-')])

            for filename in output_files:
                file_path = os.path.join(kind_dir, filename)
                records = self.read_leveldb_records(file_path)

                for record in records:
                    try:
                        entity = self.parse_entity_record(record)
                        if entity.get("_key"):
                            entities.append(entity)
                    except Exception:
                        pass

            return entities

        # Fall back to all_kinds directory (managed export without --kinds)
        all_kinds = self._load_all_kinds()
        entities = all_kinds.get(kind_name, [])
        if not entities:
            print(f"Warning: No entities found for kind '{kind_name}'")
        return entities
    
    def save_json(self, filename: str, data):
        """Save data to a JSON file."""
        output_path = os.path.join(self.output_dir, filename)
        with open(output_path, 'w', encoding='utf-8') as f:
            json.dump(data, f, ensure_ascii=False, indent=2)
        count = len(data) if isinstance(data, list) else len(data.keys()) if isinstance(data, dict) else 0
        print(f"  Saved {count} records to {filename}")
        self.stats[filename] = count

    # Transform methods
    def extract_key_id(self, key_obj) -> Optional[Any]:
        """Extract ID or name from a key object."""
        if key_obj is None:
            return None
        if isinstance(key_obj, dict):
            # Prefer id over name
            if key_obj.get('id'):
                return key_obj.get('id')
            return key_obj.get('name')
        return key_obj
    
    def extract_troop_id_from_key(self, key_obj) -> Optional[str]:
        """Extract troop identifier from a key.
        
        Troop keys use composite names like: '{scoutnet_id}/{scoutgroup}/{semester}'
        We return the full key name as the identifier.
        """
        if key_obj is None:
            return None
        # Skip if key_obj is a list (malformed repeated property)
        if isinstance(key_obj, list):
            return None
        if isinstance(key_obj, dict):
            # For troop keys, the name IS the identifier
            return key_obj.get('name') or key_obj.get('id')
        return str(key_obj)

    def extract_parent_key_id(self, key_obj, parent_kind: str) -> Optional[Any]:
        if not key_obj or not isinstance(key_obj, dict):
            return None
        for elem in key_obj.get('path', []):
            if elem.get('kind') == parent_kind:
                return elem.get('id') or elem.get('name')
        return None
    
    def parse_date(self, date_obj) -> Optional[str]:
        """Parse date from various formats.
        
        Datastore can store dates as:
        - dict with _type='datetime' and ISO string value
        - ISO date string
        - Integer timestamp in microseconds since epoch
        
        Note: Legacy App Engine Python 2.7 stored dates with a specific encoding
        that requires special handling for dates before 1970.
        """
        if date_obj is None:
            return None
        if isinstance(date_obj, dict) and date_obj.get('_type') == 'datetime':
            return date_obj['value'][:10]
        if isinstance(date_obj, str):
            return date_obj[:10]
        if isinstance(date_obj, int):
            # Legacy App Engine date encoding
            # The value appears to be microseconds, but stored in a way that
            # requires 32-bit signed interpretation for dates before 1970
            timestamp = date_obj // 1_000_000
            # Mask to 32 bits and convert to signed
            timestamp = timestamp & 0xffffffff
            timestamp = (timestamp ^ 0x80000000) - 0x80000000
            # The offset date 1974-06-15 was empirically determined to produce
            # correct results for this specific dataset's encoding
            if timestamp < 0:
                # For negative timestamps, use empirically determined offset
                dt = datetime(1974, 6, 15, tzinfo=timezone.utc) + timedelta(seconds=timestamp)
            else:
                dt = datetime.fromtimestamp(timestamp, tz=timezone.utc)
            return dt.strftime('%Y-%m-%d')
        return str(date_obj)[:10] if date_obj else None
    
    def parse_datetime(self, dt_obj) -> Optional[str]:
        """Parse datetime from various formats."""
        if dt_obj is None:
            return None
        if isinstance(dt_obj, dict) and dt_obj.get('_type') == 'datetime':
            return dt_obj['value']
        if isinstance(dt_obj, int):
            # Datastore stores timestamps as microseconds since epoch
            try:
                dt = datetime.fromtimestamp(dt_obj / 1_000_000, tz=timezone.utc)
                return dt.isoformat()
            except:
                return None
        return str(dt_obj) if dt_obj else None
    
    def transform_semesters(self, raw: List[Dict]) -> List[Dict]:
        result = []
        for r in raw:
            year = r.get('year', 0)
            is_autumn = r.get('ht', False)
            result.append({
                'id': f"{year}-{'1' if is_autumn else '0'}",
                'year': year,
                'is_autumn': is_autumn
            })
        return result
    
    def transform_scout_groups(self, raw: List[Dict]) -> List[Dict]:
        result = []
        for r in raw:
            scoutnet_id = r.get('scoutnetID', '')
            _key = r.get('_key')
            if not scoutnet_id:
                continue
            result.append({
                'id': str(scoutnet_id),
                'key': _key['name'],
                'name': r.get('name', ''),
                'organisation_number': r.get('organisationsnummer'),
                'association_id': r.get('foreningsID'),
                'municipality_id': r.get('kommunID', '1480'),
                'api_key_waitinglist': r.get('apikey_waitinglist'),
                'api_key_all_members': r.get('apikey_all_members'),
                'bank_account': r.get('bankkonto'),
                'address': r.get('adress'),
                'postal_address': r.get('postadress'),
                'email': r.get('epost'),
                'phone': r.get('telefon'),
                'default_location': r.get('default_lagerplats'),
                'signatory': r.get('firmatecknare'),
                'signatory_phone': r.get('firmatecknartelefon'),
                'signatory_email': r.get('firmatecknaremail'),
                'attendance_min_year': r.get('attendance_min_year', 10),
                'attendance_incl_hike': r.get('attendance_incl_hike', True)
            })
        return result
    
    def transform_persons(self, raw: List[Dict]) -> List[Dict]:
        result = []
        for r in raw:
            member_no = r.get('member_no')
            if not member_no:
                continue
            sg_key = r.get('scoutgroup')
            scout_group_id = self.extract_key_id(sg_key)
            if not scout_group_id:
                scout_group_id = self.extract_parent_key_id(r.get('_key', {}), 'ScoutGroup')
            result.append({
                'id': int(member_no),
                'scout_group_id': str(scout_group_id) if scout_group_id else None,
                'first_name': r.get('firstname', ''),
                'last_name': r.get('lastname', ''),
                'birth_date': self.parse_date(r.get('birthdate')),
                'personal_number': r.get('personnr'),
                'patrol': r.get('patrool') or r.get('patrol'),
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
                'not_in_scoutnet': r.get('notInScoutnet', False),
                'removed': r.get('removed', False)
            })
        return result
    
    def transform_troops(self, raw: List[Dict], semesters_lookup: Dict) -> List[Dict]:
        """Transform Troop entities.
        
        Troops use composite key names like: '{scoutnet_id}/{scoutgroup}/{semester}'
        We use the full key name as the unique identifier.
        """
        result = []
        for r in raw:
            # Get the key name as identifier
            entity_key = r.get('_key', {})
            troop_key_name = entity_key.get('name')
            if not troop_key_name:
                continue
            
            # Extract scoutnet_id from key name if present (first part before /)
            key_parts = troop_key_name.split('/')
            scoutnet_id = key_parts[0] if key_parts else None
            
            sg_key = r.get('scoutgroup')
            scout_group_id = self.extract_key_id(sg_key)
            if not scout_group_id:
                scout_group_id = self.extract_parent_key_id(entity_key, 'ScoutGroup')
            
            semester_key = r.get('semester_key') or r.get('semester')
            semester_id = None
            if semester_key:
                sem_key_name = self.extract_key_id(semester_key)
                if sem_key_name and sem_key_name in semesters_lookup:
                    semester_id = semesters_lookup[sem_key_name]
            
            default_time = r.get('defaultstarttime', '18:30')
            if isinstance(default_time, dict):
                default_time = '18:30'
            
            result.append({
                'id': troop_key_name,  # Use key name as ID
                'scoutnet_id': scoutnet_id,
                'scout_group_id': str(scout_group_id) if scout_group_id else None,
                'semester_id': semester_id,
                'name': r.get('name', ''),
                'default_start_time': str(default_time),
                'default_duration_minutes': r.get('defaultduration', int(90)),
                'report_id': r.get('rapportID')
            })
        return result
    
    def transform_troop_persons(self, raw: List[Dict]) -> List[Dict]:
        """Transform TroopPerson entities."""
        result = []
        seen = set()
        for r in raw:
            # Use key name for troop reference
            troop_key = r.get('troop')
            troop_id = self.extract_troop_id_from_key(troop_key)
            
            # Person uses numeric ID
            person_key = r.get('person')
            person_id = self.extract_key_id(person_key)
            
            if not troop_id or not person_id:
                print(f'Warning: skipping TroopPerson {r}')
                continue
            
            try:
                person_id_int = int(person_id)
            except (ValueError, TypeError):
                continue

            key = (troop_id, person_id_int)
            if key in seen:
                print(f'Warning: duplicate TroopPerson {r}')
                continue

            seen.add(key)
            
            result.append({
                'troop_id': troop_id,
                'person_id': person_id_int,
                'is_leader': r.get('leader', False),
                'patrol': r.get('patrol')
            })
        return result
    
    def transform_meetings(self, raw: List[Dict]) -> tuple:
        """Transform Meeting entities."""
        meetings = []
        attendances = []
        seen_meetings = set()
        seen_attendances = set()
        
        for r in raw:
            # Troop uses key name
            troop_key = r.get('troop')
            troop_id = self.extract_troop_id_from_key(troop_key)
            
            dt = self.parse_datetime(r.get('datetime'))
            if not dt or not troop_id:
                continue
            
            meeting_date = dt[:10]
            start_time = dt[11:16] if len(dt) > 10 else '18:30'
            meeting_id = f"{troop_id}.{meeting_date}"  # Use '.' separator
            
            if meeting_id not in seen_meetings:
                seen_meetings.add(meeting_id)
                duration_minutes = int(r.get('duration', int(90)))
                if duration_minutes > 1440:
                    print(f"  Warning: meeting duration too long ({duration_minutes} min), setting to 90 min: {r}")
                    duration_minutes = int(90)

                meetings.append({
                    'id': meeting_id,
                    'troop_id': troop_id,
                    'meeting_date': meeting_date,
                    'start_time': start_time,
                    'name': r.get('name', 'Meeting'),
                    'duration_minutes': duration_minutes,
                    'is_hike': r.get('ishike', False)
                })
            
            attending = r.get('attendingPersons', []) or []
            for person_key in attending:
                person_id = self.extract_key_id(person_key)
                if person_id:
                    try:
                        person_id_int = int(person_id)
                    except (ValueError, TypeError):
                        continue
                    att_key = (meeting_id, person_id_int)
                    if att_key not in seen_attendances:
                        seen_attendances.add(att_key)
                        attendances.append({
                            'meeting_id': meeting_id,
                            'person_id': person_id_int
                        })
        
        return meetings, attendances
    
    def transform_users(self, raw: List[Dict]) -> List[Dict]:
        result = []
        seen = set()
        for r in raw:
            email = r.get('email', '')
            if isinstance(email, list):
                print(f'  Warning: skipping user with invalid email field: {r}')
                continue
            if not email or email in seen:
                continue
            seen.add(email)
            sg_key = r.get('groupaccess')
            scout_group_id = self.extract_key_id(sg_key)
            
            name = r.get('name')
            if isinstance(name, list):
                name = name[0] if name else None

            hasaccess = r.get('hasaccess', False)
            hasadminaccess = r.get('hasadminaccess', False)

            result.append({
                'id': email,
                'email': email,
                'name': name,
                'scout_group_id': str(scout_group_id) if scout_group_id else None,
                'active_semester_id': None,
                'has_access': hasaccess,
                'is_admin': hasadminaccess
            })
        return result
    
    def transform_badges(self, raw: List[Dict]) -> tuple:
        result = []
        id_map = {}
        for idx, r in enumerate(raw, start=1):
            entity_key = r.get('_key', {})
            old_key = str(entity_key.get('id') or entity_key.get('name') or idx)
            id_map[old_key] = idx
            sg_key = r.get('scoutgroup') or r.get('scout_group')
            scout_group_id = self.extract_key_id(sg_key)
            if not scout_group_id:
                scout_group_id = self.extract_parent_key_id(entity_key, 'ScoutGroup')
            result.append({
                'id': idx,
                'scout_group_id': str(scout_group_id) if scout_group_id else None,
                'name': r.get('name', ''),
                'description': r.get('description'),
                'parts_scout_short': r.get('parts_scout_short', []) or [],
                'parts_scout_long': r.get('parts_scout_long', []) or [],
                'parts_admin_short': r.get('parts_admin_short', []) or [],
                'parts_admin_long': r.get('parts_admin_long', []) or [],
                'image_url': r.get('img_url')
            })
        return result, id_map
    
    def transform_badge_parts_done(self, raw: List[Dict], badge_id_map: Dict[str, int]) -> List[Dict]:
        result = []
        seen = set()
        for r in raw:
            # Field names are person_key and badge_key
            person_key = r.get('person_key') or r.get('person')
            badge_key = r.get('badge_key') or r.get('badge')
            
            person_id = self.extract_key_id(person_key)
            old_badge_id = str(self.extract_key_id(badge_key))
            badge_id = badge_id_map.get(old_badge_id)
            
            if not person_id or not badge_id:
                continue
            
            try:
                person_id_int = int(person_id)
            except (ValueError, TypeError):
                continue
            
            part_idx = r.get('idx', 0)
            is_scout_part = r.get('is_scout_part', True)
            key = (person_id_int, badge_id, part_idx, is_scout_part)
            if key in seen:
                continue
            seen.add(key)
            result.append({
                'person_id': person_id_int,
                'badge_id': badge_id,
                'part_index': part_idx,
                'is_scout_part': is_scout_part,
                'examiner_name': r.get('examiner_name'),
                'completed_date': self.parse_date(r.get('date'))
            })
        return result
    
    def transform_badges_completed(self, raw: List[Dict], badge_id_map: Dict[str, int]) -> List[Dict]:
        result = []
        seen = set()
        for r in raw:
            # Field names are person_key and badge_key
            person_key = r.get('person_key') or r.get('person')
            badge_key = r.get('badge_key') or r.get('badge')
            
            person_id = self.extract_key_id(person_key)
            old_badge_id = str(self.extract_key_id(badge_key))
            badge_id = badge_id_map.get(old_badge_id)
            
            if not person_id or not badge_id:
                continue
            
            try:
                person_id_int = int(person_id)
            except (ValueError, TypeError):
                continue
            
            key = (person_id_int, badge_id)
            if key in seen:
                continue
            seen.add(key)
            result.append({
                'person_id': person_id_int,
                'badge_id': badge_id,
                'examiner': r.get('examiner'),
                'completed_date': self.parse_date(r.get('date'))
            })
        return result
    
    def transform_troop_badges(self, raw: List[Dict], badge_id_map: Dict[str, int]) -> List[Dict]:
        """Transform TroopBadge entities."""
        result = []
        seen = set()
        for r in raw:
            # TroopBadge uses troop_key and badge_key fields
            troop_key = r.get('troop_key') or r.get('troop')
            badge_key = r.get('badge_key') or r.get('badge')
            
            troop_id = self.extract_troop_id_from_key(troop_key)
            old_badge_id = str(self.extract_key_id(badge_key))
            badge_id = badge_id_map.get(old_badge_id)
            
            if not troop_id or not badge_id:
                continue
            
            key = (troop_id, badge_id)
            if key in seen:
                continue
            seen.add(key)
            
            result.append({
                'troop_id': troop_id,
                'badge_id': badge_id,
                'sort_order': r.get('sort_order')
            })
        return result
    
    def transform_badge_templates(self, raw: List[Dict]) -> List[Dict]:
        result = []
        for idx, r in enumerate(raw, start=1):
            result.append({
                'id': idx,
                'name': r.get('name', ''),
                'description': r.get('description'),
                'parts_scout_short': r.get('parts_scout_short', []) or [],
                'parts_scout_long': r.get('parts_scout_long', []) or [],
                'parts_admin_short': r.get('parts_admin_short', []) or [],
                'parts_admin_long': r.get('parts_admin_long', []) or [],
                'image_url': r.get('img_url')
            })
        return result
    
    def convert_all(self):
        """Convert all entity kinds."""
        os.makedirs(self.output_dir, exist_ok=True)
        
        print("Converting Datastore export to JSON...")
        print(f"  Export directory: {self.export_dir}")
        print(f"  Output directory: {self.output_dir}")
        print()
        
        # 1. Semesters
        print("Converting Semester...")
        raw_semesters = self.convert_kind('Semester')
        semesters = self.transform_semesters(raw_semesters)
        self.save_json('semesters.json', semesters)
        
        semesters_lookup = {}
        for s in raw_semesters:
            key = s.get('_key', {})
            key_name = key.get('name')
            if key_name:
                semesters_lookup[key_name] = f"{s.get('year')}-{'1' if s.get('ht') else '0'}"
        
        # 2. ScoutGroups
        print("Converting ScoutGroup...")
        raw_groups = self.convert_kind('ScoutGroup')
        groups = self.transform_scout_groups(raw_groups)
        self.save_json('scout_groups.json', groups)
        
        # 3. Persons
        print("Converting Person...")
        raw_persons = self.convert_kind('Person')
        persons = self.transform_persons(raw_persons)
        self.save_json('persons.json', persons)
        
        # 4. Troops
        print("Converting Troop...")
        raw_troops = self.convert_kind('Troop')
        troops = self.transform_troops(raw_troops, semesters_lookup)
        self.save_json('troops.json', troops)
        
        # 5. TroopPersons
        print("Converting TroopPerson...")
        raw_troop_persons = self.convert_kind('TroopPerson')
        troop_persons = self.transform_troop_persons(raw_troop_persons)
        self.save_json('troop_persons.json', troop_persons)
        
        # 6. Meetings + Attendances
        print("Converting Meeting...")
        raw_meetings = self.convert_kind('Meeting')
        meetings, attendances = self.transform_meetings(raw_meetings)
        self.save_json('meetings.json', meetings)
        self.save_json('meeting_attendances.json', attendances)
        
        # 7. Users
        print("Converting UserPrefs...")
        raw_users = self.convert_kind('UserPrefs')
        users = self.transform_users(raw_users)
        self.save_json('users.json', users)
        
        # 8. BadgeTemplates
        print("Converting BadgeTemplate...")
        raw_templates = self.convert_kind('BadgeTemplate')
        templates = self.transform_badge_templates(raw_templates)
        self.save_json('badge_templates.json', templates)
        
        # 9. Badges
        print("Converting Badge...")
        raw_badges = self.convert_kind('Badge')
        badges, badge_id_map = self.transform_badges(raw_badges)
        self.save_json('badges.json', badges)
        self.save_json('_badge_id_map.json', badge_id_map)
        
        # 10. TroopBadges
        print("Converting TroopBadge...")
        raw_troop_badges = self.convert_kind('TroopBadge')
        troop_badges = self.transform_troop_badges(raw_troop_badges, badge_id_map)
        self.save_json('troop_badges.json', troop_badges)
        
        # 11. BadgePartsDone
        print("Converting BadgePartDone...")
        raw_parts = self.convert_kind('BadgePartDone')
        parts_done = self.transform_badge_parts_done(raw_parts, badge_id_map)
        self.save_json('badge_parts_done.json', parts_done)
        
        # 12. BadgesCompleted
        print("Converting BadgeCompleted...")
        raw_completed = self.convert_kind('BadgeCompleted')
        completed = self.transform_badges_completed(raw_completed, badge_id_map)
        self.save_json('badges_completed.json', completed)
        
        print()
        print("Conversion complete!")
        print()
        print("Summary:")
        for filename, count in self.stats.items():
            print(f"  {filename}: {count} records")

# Interface method for other modules
def convert(export_dir: str, output_dir: str):
    converter = DatastoreExportConverter(export_dir, output_dir)
    converter.convert_all()


def main():
    parser = argparse.ArgumentParser(description='Convert Datastore export to JSON')
    parser.add_argument('--export-dir', required=True, help='Path to Datastore export directory')
    parser.add_argument('--output-dir', default='./json_export', help='Output directory for JSON files')
    args = parser.parse_args()
    
    converter = DatastoreExportConverter(args.export_dir, args.output_dir)
    converter.convert_all()


if __name__ == '__main__':
    main()
