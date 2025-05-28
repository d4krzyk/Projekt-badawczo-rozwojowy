from .get_data import get_or_create_reversed_categories
from .get_data import get_or_create_title_to_id
from .utils import load_from_file


def get_main_category_by_id(category_id, category_child_to_parent: dict, id_to_title: dict) -> str:
    """Return main category based on the given category id."""

    # Skip categories starting with these phrases
    # Disabling it can speed up the process,
    # just comment the id_to_title above and last line in the condition at the bottom
    excluded_names = [
        'Wikipedia_',
    ]

    visited = []

    # "7345184": "Main_topic_classifications",
    exit_condition = '7345184'

    queue = [category_id]
    while len(queue) > 0:
        current = queue.pop(0)
        visited.append(int(current))

        categories = category_child_to_parent.get(str(current), [])
        if not categories:
            categories = category_child_to_parent.get(int(current), [])

        if exit_condition in categories or int(exit_condition) in categories:
            return id_to_title.get(current)

        queue.extend([
            item for item in categories
            if int(item) not in visited
            and str(item) not in queue
            and not id_to_title.get(str(item)).startswith(tuple(excluded_names))
        ])


def get_main_category_by_name(category: str, title_to_id: dict, category_child_to_parent: dict, id_to_title: dict) -> str:
    """Return main category based on the given category name."""
    category_name = category.replace(' ', '_').lower()
    category_id = title_to_id.get(category_name)
    return get_main_category_by_id(category_id, category_child_to_parent, id_to_title)
