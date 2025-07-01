# Standard Library
import os

# 3rd-Party
import orjson


def load_from_file(file_name):
    """Open file and return it as json."""
    if os.path.exists(file_name):
        with open(file_name, 'rb') as f:
            return orjson.loads(f.read())
    return None
