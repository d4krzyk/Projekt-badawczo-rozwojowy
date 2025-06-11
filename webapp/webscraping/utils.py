"""Webscraping utils file."""

# Standard Library
from urllib.parse import unquote
from urllib.parse import urlparse


WIKI_BASE = "https://en.wikipedia.org"


def get_wikipedia_url(article_name):
    """
    Get wikipedia article URL based on the name.
    If wikipedia URL is provided as article_name, normalize it.
    """
    if article_name.startswith('http'):
        parsed = urlparse(unquote(article_name))
        path = parsed.path
        if parsed.netloc in ['en.m.wikipedia.org', 'simple.wikipedia.org', 'en.wikipedia.org']:
            return f'{WIKI_BASE}{path}'
        else:
            raise ValueError('Unsupported Wikipedia domain.')
    else:
        title = article_name.strip().replace(' ', '_')
        return f'{WIKI_BASE}/wiki/{title}'
