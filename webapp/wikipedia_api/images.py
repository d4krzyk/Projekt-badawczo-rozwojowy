import requests
import json
from utils import get_headers
import re
import argparse
from bs4 import BeautifulSoup
from urllib.parse import unquote

images_generator_url = 'https://en.wikipedia.org/w/api.php?action=query&formatversion=2&format=json&generator=images&gimlimit=200&prop=pageimages&pithumbsize=1280&pilicense=free&titles='

images_url = 'https://en.wikipedia.org/w/api.php?action=query&prop=images&format=json&imlimit=200&formatversion=2&titles='
pageimages_url = 'https://en.wikipedia.org/w/api.php?action=query&prop=pageimages&formatversion=2&format=json&pithumbsize=1280&titles='

EXCLUDED_SUBSTRINGS = {
    "wikipedia/en/thumb",
    "Wikisource-logo.svg",
    "Wiki_letter_w_cropped.svg",
    "Wikimedia-logo.svg",
    "Wikipedia-logo",

    "Flag_of_",
    "_flag.svg",
    "_Flag.svg",

    "Coat_of_arms_of_",
    "Coat_of_Arms_of_",
    "Emblem_of_",
    "Seal_of_",

    "icon.svg",
    "Icon.svg",
    "pictogram",
    "symbol.svg",

    "Location_map",
    "BlankMap",
    "Locator_map",
}

EXCLUDED_CONTAINERS = [
    "table.infobox",
    "table.sidebar",
    "div.navbox",
    "div.vertical-navbox",
    "div.metadata",
    "table.ambox",
    "div.mw-references-wrap",
]

def format_output(page_name: str, images: dict) -> dict:
    return {
        "page_name": page_name.lower(),
        "images": [
            {url: caption.replace('"', '')}
            for url, caption in images.items()
        ]
    }

def is_valid_image_url(url: str) -> bool:
    if not url:
        return False

    return not any(bad in url for bad in EXCLUDED_SUBSTRINGS)

def is_valid_container(tag) -> bool:
    for selector in EXCLUDED_CONTAINERS:
        if tag.find_parent(selector):
            return True
    return False

def get_soup(url):
    response = requests.get(url, headers=get_headers())
    if response.status_code != 200:
        raise Exception(f"Failed to load {url}")
    return BeautifulSoup(response.text, "html.parser")

def clean_caption(tag):
    for sup in tag.find_all("sup", class_="reference"):
        sup.decompose()

    for a in tag.find_all("a"):
        if a.has_attr("href"):
            href = a["href"]
            if href.startswith("/wiki/"):
                href = "https://en.wikipedia.org" + href
            text = a.get_text(strip=True)
            a.replace_with(f"[{text}]({href})")

    return re.sub(
        r"\s{2,}", " ",
        tag.get_text(separator=" ", strip=True)
    ).strip()

def normalize_commons_url(url: str) -> str | None:
    if not url:
        return None

    if "upload.wikimedia.org" in url:
        url = url.split("upload.wikimedia.org", 1)[1]

    if "/thumb/" in url:
        url = url.split("/thumb/", 1)[1]
        parts = url.split("/")
        if len(parts) >= 3:
            url = "/".join(parts[:-1])

    return url

def extract_commons_filename(url: str) -> str | None:
    if not url:
        return None

    if "upload.wikimedia.org" in url:
        url = url.split("upload.wikimedia.org/", 1)[1]

    if "/thumb/" in url:
        url = url.split("/thumb/", 1)[1]
        parts = url.split("/")
        if len(parts) >= 3:
            return unquote(parts[-2])

    parts = url.split("/")
    return unquote(parts[-1]) if parts else None

def extract_image_captions(article_url: str) -> dict:
    soup = get_soup(article_url)
    content_div = soup.select_one("div#mw-content-text")
    if not content_div:
        return {}

    captions = {}

    def handle(img, caption):

        if is_valid_container(img):
            return

        src = img.get("src")
        if not src:
            return
        if src.startswith("//"):
            src = "https:" + src

        filename = extract_commons_filename(src)
        if filename:
            captions[filename] = clean_caption(caption)

    for figure in content_div.find_all("figure"):
        img = figure.find("img")
        cap = figure.find("figcaption")
        if img and cap:
            handle(img, cap)

    for thumb in content_div.find_all("div", class_="thumb"):
        img = thumb.find("img")
        cap = thumb.find("div", class_="thumbcaption")
        if img and cap:
            handle(img, cap)

    return captions

def format_image_caption(caption: str | None) -> str:
    return caption if caption else "[no caption]"


def images_one_by_one(page_name: str) -> dict:
    article_url = f"https://en.wikipedia.org/wiki/{page_name}"
    captions = extract_image_captions(article_url)

    json_content = requests.get(
        images_url + page_name,
        headers=get_headers()
    ).content

    image_titles = [
        img["title"]
        for img in json.loads(json_content)["query"]["pages"][0].get("images", [])
    ]

    result = {}

    for title in image_titles:
        content = requests.get(
            pageimages_url + title,
            headers=get_headers()
        ).content

        pages = json.loads(content)["query"]["pages"]
        if not pages or "thumbnail" not in pages[0]:
            continue

        url = pages[0]["thumbnail"]["source"]
        if not is_valid_image_url(url):
            continue

        filename = extract_commons_filename(url)
        caption = captions.get(filename)

        result[url] = format_image_caption(caption)

    return format_output(page_name, result)


def images_generator(page_name: str) -> dict:
    article_url = f"https://en.wikipedia.org/wiki/{page_name}"
    captions = extract_image_captions(article_url)

    json_content = requests.get(
        images_generator_url + page_name,
        headers=get_headers()
    ).content

    result = {}

    for page in json.loads(json_content)["query"]["pages"]:
        url = page.get("thumbnail", {}).get("source")
        if not url or not is_valid_image_url(url):
            continue

        filename = extract_commons_filename(url)
        caption = captions.get(filename)

        result[url] = format_image_caption(caption)

    return format_output(page_name, result)