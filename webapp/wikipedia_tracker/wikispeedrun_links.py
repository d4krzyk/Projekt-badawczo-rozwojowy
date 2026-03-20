import requests
import json
import urllib.parse
from utils import get_headers
from wikipedia_webscraping.utils import is_article_url
from wikipedia_webscraping.utils import article_name_by_url


def get_page_info(title: str):
    url = 'https://en.wikipedia.org/w/api.php'
    params = {
        'action': 'query',
        'format': 'json',
        'titles': title
    }

    r = requests.get(url, headers=get_headers(), params=params).json()
    page = next(iter(r['query']['pages'].values()))
    return {
        'pageid': str(page['pageid']),
        'title': page['title']
    }


def build_wikispeedrun_link(start, end, group=None):

    if is_article_url(start):
        start = article_name_by_url(start)

    if is_article_url(end):
        end = article_name_by_url(end)

    start_info = get_page_info(start)
    end_info = get_page_info(end)

    state = {
        'startingArticle': start_info,
        'endingArticle': end_info
    }

    state_json = json.dumps(state)
    encoded = urllib.parse.quote(state_json)

    return f'https://wikispeedrun.org/settings?state={encoded}'
