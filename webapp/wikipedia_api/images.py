import requests
import json
from utils import get_headers
import re
import argparse
from bs4 import BeautifulSoup
from urllib.parse import unquote
import unicodedata

images_generator_url = 'https://en.wikipedia.org/w/api.php?action=query&formatversion=2&format=json&generator=images&gimlimit=200&prop=pageimages&pithumbsize=1280&pilicense=free&titles='

images_url = 'https://en.wikipedia.org/w/api.php?action=query&prop=images&format=json&imlimit=200&formatversion=2&titles='
pageimages_url = 'https://en.wikipedia.org/w/api.php?action=query&prop=pageimages&formatversion=2&format=json&pithumbsize=1280&titles='

EXCLUDED_SUBSTRINGS = {
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
    ("table", "infobox"),
    ("table", "sidebar"),
    ("div", "navbox"),
    ("div", "vertical-navbox"),
    ("div", "metadata"),
    ("table", "ambox"),
    ("div", "mw-references-wrap"),
    ("div", "portal-bar"),
    ("div", "noprint"),
    ("div", "noviewer"),
    ("div", "authority-control"),
    ("div", "sistersitebox"),
    ("div", "printfooter"),
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
    for tag_name, class_name in EXCLUDED_CONTAINERS:
        parent = tag.find_parent(tag_name, class_=class_name)
        if parent:
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

def normalize_filename(name: str) -> str:
    if not name:
        return ""

    name = unquote(name)

    name = unicodedata.normalize("NFKD", name)
    name = "".join(c for c in name if not unicodedata.combining(c))

    name = (
        name.replace("ü", "ue").replace("Ü", "Ue")
            .replace("ö", "oe").replace("Ö", "Oe")
            .replace("ä", "ae").replace("Ä", "Ae")
    )

    name = re.sub(r"-\d+(?=\.[^.]+$)", "", name)

    return name.lower()

def resolve_file_pageids(file_titles: list[str]) -> dict[str, int]:
    if not file_titles:
        return {}

    titles_param = "|".join(f"File:{t}" for t in file_titles)

    response = requests.get(
        "https://commons.wikimedia.org/w/api.php",
        params={
            "action": "query",
            "format": "json",
            "formatversion": 2,
            "titles": titles_param,
        },
        headers=get_headers()
    )

    pages = response.json()["query"]["pages"]

    return {
        canonical_file_title(page["title"]): page["pageid"]
        for page in pages
        if "pageid" in page
    }

def fetch_thumbnails_by_pageid(pageids: list[int]) -> dict[int, str]:
    if not pageids:
        return {}

    ids_param = "|".join(map(str, pageids))

    response = requests.get(
        "https://commons.wikimedia.org/w/api.php",
        params={
            "action": "query",
            "format": "json",
            "formatversion": 2,
            "pageids": ids_param,
            "prop": "pageimages",
            "pithumbsize": 1280,
        },
        headers=get_headers()
    )

    pages = response.json()["query"]["pages"]

    result = {}
    for page in pages:
        thumb = page.get("thumbnail", {}).get("source")
        if thumb:
            result[page["pageid"]] = thumb

    return result

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

def canonical_file_title(title: str) -> str:
    if not title:
        return ""

    title = title.replace(" ", "_")
    if title.startswith("File:"):
        title = title.split("File:", 1)[1]

    return title

def extract_image_captions(article_url: str) -> list[dict]:
    soup = get_soup(article_url)
    content_div = soup.select_one("div#mw-content-text")
    if not content_div:
        return []

    ordered = []

    def handle(img, caption):
        file_link = img.find_parent("a")
        if not file_link:
            return

        href = file_link.get("href")
        if not href:
            return

        if not href.startswith("/wiki/File:"):
            return

        try:
            filename = unquote(href.split("File:", 1)[1])
        except IndexError:
            return

        canonical = canonical_file_title(filename)
        normalized = normalize_filename(filename)

        in_excluded = is_valid_container(img)

        ordered.append({
            "canonical": canonical,
            "normalized": normalized,
            "caption": clean_caption(caption),
            "in_excluded_container": in_excluded
        })

    for figure in content_div.find_all("figure"):
        img = figure.find("img")
        cap = figure.find("figcaption")
        if img and cap:
            handle(img, cap)

    for thumb in content_div.find_all("div", class_="thumb"):

        if "tmulti" in thumb.get("class", []):
            singles = thumb.find_all("div", class_="tsingle")
            for single in singles:
                img = single.find("img")
                cap = single.find("div", class_="thumbcaption")
                if img and cap:
                    handle(img, cap)

        else:
            img = thumb.find("img")
            cap = thumb.find("div", class_="thumbcaption")
            if img and cap:
                handle(img, cap)

    for gallery in content_div.find_all("div", class_="gallerybox"):
        img = gallery.find("img")
        cap = gallery.find("div", class_="gallerytext")
        if img and cap:
            handle(img, cap)

    for img in content_div.select("p a[href^='/wiki/File:'] img, \
                                   li a[href^='/wiki/File:'] img, \
                                   dd a[href^='/wiki/File:'] img"):

        if img.find_parent("figure") \
           or img.find_parent("div", class_="thumb") \
           or img.find_parent("div", class_="gallerybox"):
            continue

        if is_valid_container(img):
            continue

        alt_text = img.get("alt", "").strip()
        if not alt_text:
            alt_text = "[no caption]"

        fake_tag = soup.new_tag("span")
        fake_tag.string = alt_text

        handle(img, fake_tag)

    return ordered

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
        caption_data = captions.get(filename)
        if caption_data and caption_data["in_excluded_container"] \
           and not is_valid_image_url(url):
            continue

        filename = extract_commons_filename(url)
        filename = normalize_filename(filename)

        caption = captions.get(filename)

        result[url] = format_image_caption(caption)

    return format_output(page_name, result)


def images_generator(page_name: str) -> dict:
    article_url = f"https://en.wikipedia.org/wiki/{page_name}"

    ordered = extract_image_captions(article_url)
    if not ordered:
        return format_output(page_name, {})

    canonical_titles = [item["canonical"] for item in ordered]

    file_to_pageid = resolve_file_pageids(canonical_titles)
    pageid_to_thumb = fetch_thumbnails_by_pageid(list(file_to_pageid.values()))

    images = []

    for item in ordered:
        canonical = item["canonical"]
        caption = item["caption"]

        pageid = file_to_pageid.get(canonical)

        if not pageid:
            response = requests.get(
                pageimages_url + "File:" + canonical,
                headers=get_headers()
            ).json()

            pages = response.get("query", {}).get("pages", [])
            if pages and "thumbnail" in pages[0]:
                url = pages[0]["thumbnail"]["source"]
                if not (item["in_excluded_container"] and not is_valid_image_url(url)):
                    images.append({url: item["caption"]})
            continue

        url = pageid_to_thumb.get(pageid)
        if not url:
            continue

        if item["in_excluded_container"] and not is_valid_image_url(url):
            continue

        images.append({url: caption})

    return {
        "page_name": page_name.lower(),
        "images": images
    }