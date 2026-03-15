"""Webscraping utils file."""

# Standard Library
from urllib.parse import unquote
from urllib.parse import urlparse


WIKI_BASE = "https://en.wikipedia.org"


def is_article_url(text: str) -> bool:
    return all([
        text.startswith("http"),
        text.find(".wikipedia.org/wiki/") != -1,
    ])


def article_url_by_name(article_name: str) -> str:
    if is_article_url(article_name):
        return article_name

    title = article_name.strip().replace(" ", "_")
    return f"{WIKI_BASE}/wiki/{title}"


def article_name_by_url(article_url: str) -> str | None:
    if not is_article_url(article_url):
        return None

    return article_url.split("/wiki/")[-1].replace("_", " ")
