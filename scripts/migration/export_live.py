# -*- coding: utf-8 -*-
#!/usr/bin/env python3
"""
Export data directly from live Google Cloud Datastore to JSON files.

This script queries the live Datastore directly and exports to JSON format
that can be imported into PostgreSQL.

Requirements:
    pip install google-cloud-datastore

Usage:
    # Set up authentication first:
    gcloud auth application-default login
    
    # Then run:
    python export_live.py --project skojjt --output-dir ./json_export
"""

import argparse
import json
import os
from datetime import datetime, date
from typing import Any, Dict, List, Optional

from google.cloud import datastore


def serialize_value(val) -> Any:
    """Convert Datastore values to JSON-serializable format."""
    if val is None:
        return None
    if isinstance(val, datetime):
        return {"_type": "datetime", "value": val.isoformat()}
    if isinstance(val, date):
        return {"_type": "date", "value": val.isoformat()}
    if isinstance(val, datastore.Key):
        return {
            "_type": "key",
            "kind": val.kind,
            "id": val.id,
            "name": val.name,
            "path": list(val.flat_path)
        }
    if isinstance(val, bytes):
        return {"_type": "bytes", "value": val.hex()}
    if isinstance(val, list):
        return [serialize_value(v) for v in val]
    if isinstance(val, dict):
        return {k: serialize_value(v) for k, v in val.items()}
    return val


def export_kind(client, kind: str, output_file: str, batch_size: int = 500) -> int:
    """Export all entities of a kind using pagination."""
    query = client.query(kind=kind)
    entities = []
    cursor = None
    batch_num = 0
    
    print(f"  Exporting {kind}...")
    
    while True:
        query_iter = query.fetch(start_cursor=cursor, limit=batch_size)
        page = list(query_iter)
        
        for entity in page:
            record = {"_key": serialize_value(entity.key)}
            for k, v in entity.items():
                record[k] = serialize_value(v)
            entities.append(record)
        
        cursor = query_iter.next_page_token
        batch_num += 1
        
        if batch_num % 10 == 0:
            print(f"    Exported {len(entities)} {kind} entities so far...")
        
        if not cursor:
            break
    
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(entities, f, ensure_ascii=False, indent=2)
    
    print(f"    Saved {len(entities)} {kind} entities to {output_file}")
    return len(entities)


def main():
    parser = argparse.ArgumentParser(description='Export Datastore to JSON')
    parser.add_argument('--project', required=True, help='GCP project ID')
    parser.add_argument('--output-dir', default='./json_export', help='Output directory')
    args = parser.parse_args()
    
    os.makedirs(args.output_dir, exist_ok=True)
    
    client = datastore.Client(project=args.project)
    
    kinds = [
        'Semester',
        'ScoutGroup', 
        'Person',
        'Troop',
        'TroopPerson',
        'Meeting',
        'UserPrefs',
        'Badge',
        'BadgePartDone',
        'BadgeCompleted',
        'TroopBadge',
        'BadgeTemplate'
    ]
    
    print(f"Exporting from project: {args.project}")
    print(f"Output directory: {args.output_dir}")
    print()
    
    stats = {}
    for kind in kinds:
        output_file = os.path.join(args.output_dir, f'{kind.lower()}.json')
        try:
            count = export_kind(client, kind, output_file)
            stats[kind] = count
        except Exception as e:
            print(f"    Error exporting {kind}: {e}")
            stats[kind] = 0
    
    print()
    print("Export complete!")
    print()
    print("Summary:")
    for kind, count in stats.items():
        print(f"  {kind}: {count} records")


if __name__ == '__main__':
    main()
