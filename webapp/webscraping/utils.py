"""Webscraping utils file."""

# Standard Library
from urllib.parse import unquote
from urllib.parse import urlparse


WIKI_BASE = "https://en.wikipedia.org"


def article_url_by_name(article_name: str) -> str:
    title = article_name.strip().replace(" ", "_")
    return f"{WIKI_BASE}/wiki/{title}"


def article_name_by_url(article_url: str | None) -> str | None:
    article_url = article_url.strip()
    if not article_url:
        return None

    validations = [
        article_url.startswith("http"),
        article_url.find(".wikipedia.org/wiki/") != -1,
    ]
    if not all(validations):
        return None

    return (
        article_url.split("/wiki/")[-1].replace("_", " ") if all(validations) else None
    )
