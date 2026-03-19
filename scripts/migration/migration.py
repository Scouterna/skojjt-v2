# use this file for debugging in VS
from convert_export import convert
from transform_data import transform

# Step 1: Convert Datastore LevelDB export to raw JSON.
# Only needed when you have a fresh Datastore export.
# WARNING: This overwrites raw_export/ — skip if raw_export/ already has good data.
# convert('./datastore_export/20260314', './raw_export')

# Step 2: Transform raw JSON to PostgreSQL-ready format.
print("Transforming raw JSON to PostgreSQL format...")
transform('./raw_export/', './json_export/')

print("Done!")