# -*- coding: utf-8 -*-
"""Debug repeated properties in Meeting."""

import struct

file_path = 'C:/src/skojjt-v2/datastore_export/2025-12-23/all_namespaces/kind_Meeting/output-1000'

def read_varint(data, pos):
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

def read_records(data):
    pos = 0
    records = []
    while pos + 7 <= len(data):
        crc = struct.unpack('<I', data[pos:pos+4])[0]
        length = struct.unpack('<H', data[pos+4:pos+6])[0]
        record_type = data[pos+6]
        if length == 0 or pos + 7 + length > len(data):
            break
        records.append(data[pos+7:pos+7+length])
        pos += 7 + length
    return records

with open(file_path, 'rb') as f:
    data = f.read()

records = read_records(data)
print(f'Found {len(records)} records')

# Find a meeting and count all its properties
for rec in records[:10]:
    properties = {}
    pos = 0
    
    while pos < len(rec):
        fb = rec[pos]
        fn = fb >> 3
        wt = fb & 0x07
        pos += 1
        
        if wt == 2:
            flen, pos = read_varint(rec, pos)
            fd = rec[pos:pos+flen]
            pos += flen
            
            if fn == 14:  # Property
                ppos = 0
                prop_name = None
                
                while ppos < len(fd):
                    pb = fd[ppos]
                    pf = pb >> 3
                    pw = pb & 0x07
                    ppos += 1
                    
                    if pw == 2:
                        plen, ppos = read_varint(fd, ppos)
                        pd = fd[ppos:ppos+plen]
                        ppos += plen
                        
                        if pf == 3:
                            prop_name = pd.decode('utf-8', errors='replace')
                    elif pw == 0:
                        _, ppos = read_varint(fd, ppos)
                
                if prop_name:
                    if prop_name not in properties:
                        properties[prop_name] = 0
                    properties[prop_name] += 1
        elif wt == 0:
            _, pos = read_varint(rec, pos)
    
    if 'attendingPersons' in properties and properties['attendingPersons'] > 1:
        print(f'\nRecord with multiple attendingPersons:')
        for name, count in sorted(properties.items()):
            print(f'  {name}: {count} occurrences')
        break
else:
    # Show first record's properties anyway
    print('\nFirst record properties:')
    for name, count in sorted(properties.items()):
        print(f'  {name}: {count} occurrences')
