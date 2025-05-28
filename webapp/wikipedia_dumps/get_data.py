# Standard Library
import json
from collections import defaultdict

# Local
from .utils import load_from_file


def get_or_create_title_to_id():
    """Open title_to_id json. Create it if it does not exist."""
    title_to_id = load_from_file('wikipedia_dumps/data/title_to_id_min.json')
    if title_to_id:
        return title_to_id
    id_to_title = load_from_file('wikipedia_dumps/data/id_to_title_min.json')
    title_to_id = {v.lower(): k for k, v in id_to_title.items()}
    with open('wikipedia_dumps/data/title_to_id_min.json', 'w') as f:
        json.dump(title_to_id, f, separators=(',', ':'))
    return title_to_id


def create_reverse_mapping(category_links):
    child_to_parent = defaultdict(list)

    for parent_id, children in category_links.items():
        for child in children:
            child_to_parent[child].append(parent_id)

    return child_to_parent


def get_or_create_reversed_categories():
    """Open category_links_reversed json. Create it if it does not exist."""
    categories = load_from_file('wikipedia_dumps/data/category_links_reversed_min.json')
    if categories:
        return categories
    category_links = load_from_file('wikipedia_dumps/data/category_links_min.json')
    categories = create_reverse_mapping(category_links)
    with open('wikipedia_dumps/data/category_links_reversed_min.json', 'w') as f:
        json.dump(categories, f, separators=(',', ':'))
    return categories
