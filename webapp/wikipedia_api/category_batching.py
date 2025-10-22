# Standard Library
import json
import os

# 3rd-Party
import requests

# Project
from utils import get_headers

query_url = 'https://en.wikipedia.org/w/api.php?action=query&prop=categories&clshow=!hidden&formatversion=2&cllimit=100&format=json&titles='
redirect_url = 'https://en.wikipedia.org/w/rest.php/v1/search/page?limit=1&q='


categories_shortcut = dict()
stop_list = []

PWD = os.path.dirname(os.path.abspath(__file__))


def get_redirected_name(page_name: str) -> str:
    json_content = requests.get(
        redirect_url + page_name, headers=get_headers()).content
    json_object = json.loads(json_content)

    return json_object['pages'][0]['key']


def extract_categories(page_name: str):
    json_content = requests.get(
        query_url + page_name, headers=get_headers()).content
    pages = list(json.loads(json_content)['query']['pages'])
    categories = []
    for page in pages:
        if 'categories' in page:
            categories.extend([
                category['title'] for category in reversed(page['categories'])
            ])

    return categories


def main_category(page_name: str) -> str:
    page_name = get_redirected_name(page_name)
    visited = []

    global stop_list
    global categories_shortcut

    if len(stop_list) == 0:
        with open(PWD + '/shortcuts.json', 'r') as f:
            categories_shortcut = json.load(f)
        with open(PWD + '/stop.json', 'r') as f:
            stop_list = json.load(f)

    queue = [page_name]
    while len(queue) > 0:
        popped = []

        i = 0

        while len(queue) > 0 and i < 50:
            current = queue.pop(0)

            if current in stop_list:
                while current != categories_shortcut[current]:
                    current = categories_shortcut[current]
                return current.split('Category:')[-1]

            popped.append(current)

            i += 1

        print('Visiting', '|'.join(popped))

        categories = extract_categories('|'.join(popped))

        queue.extend(
            filter(lambda x: x not in visited and x not in queue, categories))
