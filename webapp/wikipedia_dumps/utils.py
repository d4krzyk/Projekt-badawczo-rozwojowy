import json
import os


def load_from_file(file_name):
    """Open file and return it as json."""
    if os.path.exists(file_name):
        with open(file_name, 'r', encoding='utf-8') as f:
            return json.load(f)
    return None
