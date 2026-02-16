# -*- coding: utf-8 -*-
"""Check badge mapping."""

from convert_export import DatastoreExportConverter

c = DatastoreExportConverter('C:/src/skojjt-v2/datastore_export/2025-12-23', './json_export')

# Check Badge raw data
raw_badges = c.convert_kind('Badge')
print(f'Raw Badges: {len(raw_badges)} records')

# Build badge_id_map
badges, badge_id_map = c.transform_badges(raw_badges)
print(f'Badge ID map has {len(badge_id_map)} entries')

# Sample badge IDs
print('\nSample badge_id_map entries:')
for old_key, new_id in list(badge_id_map.items())[:5]:
    print(f'  {old_key} -> {new_id}')

# Check BadgePartDone raw data
raw_parts = c.convert_kind('BadgePartDone')
print(f'\nRaw BadgePartDone: {len(raw_parts)} records')

# Sample badge keys from BadgePartDone
print('\nSample badge keys from BadgePartDone:')
badge_keys_in_parts = set()
for r in raw_parts[:100]:
    badge_key = r.get('badge_key')
    if badge_key:
        badge_id = c.extract_key_id(badge_key)
        badge_keys_in_parts.add(str(badge_id))

print(f'Unique badge IDs in first 100: {len(badge_keys_in_parts)}')
for bk in list(badge_keys_in_parts)[:5]:
    in_map = bk in badge_id_map
    print(f'  {bk}: {"IN MAP" if in_map else "NOT IN MAP"}')
