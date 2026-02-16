# use this file for debugging in VS
from convert_export import convert
from transform_data import transform

# convert('../../datastore_export/2025-12-23/', './raw_export')

transform('./raw_export/', './json_export/')