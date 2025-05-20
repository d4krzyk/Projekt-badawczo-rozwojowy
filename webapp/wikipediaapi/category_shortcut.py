import requests
import json
import os

query_url = 'https://en.wikipedia.org/w/api.php?action=query&prop=categories&clshow=!hidden&cllimit=100&format=json&titles='
images_url = 'https://en.wikipedia.org/w/api.php?action=query&prop=images&format=json&imlimit=100&formatversion=2&titles='
pageimages_url = 'https://en.wikipedia.org/w/api.php?action=query&prop=pageimages&formatversion=2&format=json&pithumbsize=100000&titles='

categories_shortcut = dict()
stop_list = []

PWD = os.path.dirname(os.path.abspath(__file__))

def extract_categories(page_name: str):
    json_content = requests.get(query_url + page_name).content
    val = list(json.loads(json_content)['query']['pages'].values())[0]
    if 'categories' in val:
        return list(reversed([category['title']for category in filter(lambda x: 'hidden' not in x, val['categories'])]))
    else:
        return []


def images(page_name: str) -> [str]:
    json_content = requests.get(images_url + page_name).content
    val = [image['title'] for image in list(
        json.loads(json_content)['query']['pages'][0]['images'])]

    images = []

    for image in val:
        content = requests.get(pageimages_url + image).content
        images.append(json.loads(content)[
                      'query']['pages'][0]['thumbnail']['source'])

    return images


def main_category(page_name: str) -> str:
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
        current = queue.pop(0)
        visited.append(current)

        if current in stop_list:
            while current != categories_shortcut[current]:
                current = categories_shortcut[current]
            return current

        print('Visiting', current)

        categories = extract_categories(current)

        queue.extend(
            filter(lambda x: x not in visited and x not in queue, categories))
