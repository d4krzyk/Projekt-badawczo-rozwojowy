import requests
import json

images_generator_url = 'https://en.wikipedia.org/w/api.php?action=query&formatversion=2&format=json&generator=images&gimlimit=200&prop=pageimages&pithumbsize=1280&pilicense=free&titles='

images_url = 'https://en.wikipedia.org/w/api.php?action=query&prop=images&format=json&imlimit=200&formatversion=2&titles='
pageimages_url = 'https://en.wikipedia.org/w/api.php?action=query&prop=pageimages&formatversion=2&format=json&pithumbsize=1280&titles='


def images_one_by_one(page_name: str) -> [str]:
    json_content = requests.get(images_url + page_name).content
    val = [image['title'] for image in list(
        json.loads(json_content)['query']['pages'][0]['images'])]

    images = []

    for image in val:
        content = requests.get(pageimages_url + image).content
        images.append(json.loads(content)[
                      'query']['pages'][0]['thumbnail']['source'])

    return images


def images_generator(page_name: str) -> [str]:
    json_content = requests.get(images_generator_url + page_name).content

    return [pages['thumbnail']['source'] for pages in json.loads(json_content)['query']['pages']]
