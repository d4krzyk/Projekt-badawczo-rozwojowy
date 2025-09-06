# Standard Library
import json

# 3rd-Party
import requests

# Project
from utils import get_headers

subcategories_url = 'https://en.wikipedia.org/w/api.php?action=query&list=categorymembers&cmtype=subcat&cmlimit=100&format=json&cmtitle='  # noqa :E501


def get_subcategories(page_name: str) -> [str]:
    json_content = requests.get(subcategories_url + page_name, headers=get_headers()).content
    subcategories = [category['title'] for category in list(
        json.loads(json_content)['query']['categorymembers'])]
    return subcategories


def generate_shortcut_data_bfs(max_depth: int):
    stop_list = []
    categories_shortcut = dict()

    main_categories = get_subcategories('Category: Main topic classifications')
    main_categories.pop(0)

    stop_list.extend(main_categories)

    queue = []
    current_depth = 0

    queue.append(main_categories)

    while current_depth < max_depth:
        current = queue.pop(0)

        print("Current depth:", current_depth)
        print("Current depth length:", len(current))

        next_depth = []

        for category in current:
            if current_depth == 0:
                categories_shortcut[category] = category

            print("Getting:", category, "( depth", current_depth, ")")
            categories = get_subcategories(category)
            categories = list(filter(lambda x: x not in stop_list, categories))

            next_depth.extend(categories)
            stop_list.extend(categories)

            for subcategory in categories:
                categories_shortcut[subcategory] = category

        current_depth += 1
        queue.append(next_depth)

    with open('shortcuts.json', 'w') as f:
        json.dump(categories_shortcut, f)
    with open('stop.json', 'w') as f:
        json.dump(stop_list, f)


def main():
    generate_shortcut_data_bfs(2)


if __name__ == '__main__':
    main()
