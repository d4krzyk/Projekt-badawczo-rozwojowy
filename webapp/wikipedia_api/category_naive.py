# Standard Library
import json

# 3rd-Party
import requests

# Project
from utils import get_headers

base_url = "https://en.wikipedia.org/w/api.php?action=query&prop=categories&clshow=!hidden&cllimit=100&format=json&titles="  # noqa :E501
redirect_url = 'https://en.wikipedia.org/w/rest.php/v1/search/page?limit=1&q='


def get_redirected_name(page_name: str) -> str:
    json_content = requests.get(
        redirect_url + page_name, headers=get_headers()).content
    json_object = json.loads(json_content)

    return json_object['pages'][0]['key']


def extract_categories(page_name: str):
    json_content = requests.get(
        base_url + page_name, headers=get_headers()).content
    val = list(json.loads(json_content)['query']['pages'].values())[0]
    if 'categories' in val:
        return list(
            reversed(
                [
                    category['title'] for category
                    in filter(lambda x: 'hidden' not in x, val['categories'])
                ]
            )
        )
    else:
        return []


def main_category(page_name: str) -> str:
    page_name = get_redirected_name(page_name)
    visited = []

    exit_condition = "Category:Main topic classifications"

    queue = [page_name]
    while len(queue) > 0:
        current = queue.pop(0)
        visited.append(current)

        print("Visiting", current)

        categories = extract_categories(current)

        if exit_condition in categories:
            return current

        queue.extend(
            filter(lambda x: x not in visited and x not in queue, categories))


# def main():
#     print("Final:", top_category("Pope"))


# if __name__ == "__main__":
#     main()
