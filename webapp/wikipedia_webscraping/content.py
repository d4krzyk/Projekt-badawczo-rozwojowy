# Standard Library
import re
from collections import deque

# 3rd-Party
import requests
from bs4 import BeautifulSoup

# Project
from utils import get_headers


def get_soup(url):
    response = requests.get(url, headers=get_headers())
    if response.status_code != 200:
        raise Exception(f"Failed to load {url}")
    return BeautifulSoup(response.text, "html.parser")


def extract_sections_as_nested_list(article_url):
    try:
        soup = get_soup(article_url)
    except Exception:
        return

    content_div = soup.select_one("div#mw-content-text")
    if not content_div:
        raise Exception("Could not find article content.")

    for infobox in content_div.find_all("table", class_="infobox"):
        infobox.decompose()

    for tag in content_div.find_all(["i", "em"]):
        tag.unwrap()

    skip_tags = {"table", "style", "script", "noscript", "math", "img"}
    skip_sections = {
        "See also",
        "Further reading",
        "External links",
        "Notes",
        "References",
    }

    stack = deque()
    root = {"level": 0, "name": "__ROOT__", "subsections": []}
    stack.append(root)

    intro_content = ""

    def clean_text(tag):
        for sup in tag.find_all("sup", class_="reference"):
            sup.decompose()
        return re.sub(r"\s{2,}", " ", tag.get_text(separator=" ", strip=True)).strip()

    started_main_content = False

    for tag in content_div.descendants:
        if not started_main_content:
            if tag.name == "h2":
                started_main_content = True
            elif tag.name == "p":
                text = clean_text(tag)
                if text and len(text.split()) > 10:
                    started_main_content = True
                    intro_content += text + "<br>"
                else:
                    continue
            else:
                continue

        if not hasattr(tag, "name"):
            continue

        if tag.name in ["h2", "h3", "h4"]:
            level = int(tag.name[1])
            headline = tag.find("span", class_="mw-headline")
            title = headline.text.strip() if headline else tag.get_text(strip=True)

            if title in skip_sections:
                continue

            while stack and stack[-1]["level"] >= level:
                stack.pop()

            new_section = {
                "level": level,
                "name": title,
                "content": "",
                "subsections": [],
            }
            stack[-1].setdefault("subsections", []).append(new_section)
            stack.append(new_section)
            continue

        if tag.name in skip_tags:
            continue

        if tag.name in ["p", "ul", "ol"] and not tag.find_parent(
            ["table", "div"],
            class_=[
                "hatnote",
                "metadata",
                "ambox",
                "mbox",
                "tmbox",
                "messagebox",
                "notice",
                "navbox",
                "vertical-navbox",
                "infobox",
                "toc",
                "mw-stack",
            ],
        ):
            text = clean_text(tag)
            if text:
                if len(stack) == 1:
                    intro_content += text + "<br>"
                else:
                    stack[-1]["content"] += text + "<br>"

    if intro_content.strip():
        root["subsections"].insert(
            0, {"name": "Introduction", "content": intro_content.strip()}
        )

    def strip_levels(section):
        return {
            "name": section["name"],
            **(
                {"content": section["content"].strip()}
                if section.get("content", "").strip()
                else {}
            ),
            **(
                {
                    "subsections": [
                        strip_levels(s) for s in section.get("subsections", [])
                    ]
                }
                if section.get("subsections")
                else {}
            ),
        }

    result = [strip_levels(s) for s in root["subsections"]]
    return result
  
def extract_sections_as_list(article_url):
    # Pobiera treść artykułu z Wikipedia i zwraca słownik,
    # gdzie kluczem jest nazwa sekcji, a wartością połączone paragrafy

    soup = get_soup(article_url)
    content_div = soup.select_one("div#mw-content-text")
    if not content_div:
        raise Exception("Could not find article content.")

    # Usun infoboxy i znaczniki formatowania
    for infobox in content_div.find_all("table", class_="infobox"):
        infobox.decompose()
    for tag_name in ["i", "em", "b", "strong"]:
        for tag in content_div.find_all(tag_name):
            tag.unwrap()

    skip_tags = {"table", "style", "script", "noscript", "math", "img"}
    skip_sections = {
        "See also",
        "Further reading",
        "External links",
        "Notes",
        "References",
    }

    sections = []
    current_section = "Introduction"
    paragraphs = []
    skip_current = False
    started = False

    def clean_tag(tag):
        # Usun odniesienia w indeksie górnym
        for sup in tag.find_all("sup", class_="reference"):
            sup.decompose()
        # Konwertuj linki do Markdown
        for a in tag.find_all("a"):
            if a.has_attr("href"):
                href = a["href"]
                if href.startswith("/wiki/"):
                    href = "https://en.wikipedia.org" + href
                text = a.get_text(strip=True)
                a.replace_with(f"[{text}]({href})")
        # Wyodrebnij oczyszczony tekst
        return re.sub(r"\s{2,}", " ", tag.get_text(separator=" ", strip=True)).strip()

    # Przejrzyj wszystkie istotne tagi w kolejności dokumentu wewnątrz  zawartości diva
    for tag in content_div.find_all(["h2", "p", "ul", "ol"], recursive=True):
        # Pomijaj niechciane tagi i sekcje referencyjne
        if tag.name in skip_tags:
            continue

        # Nagłówki h2 oznaczają nowe sekcje
        if tag.name == "h2":
            if paragraphs and not skip_current:
                sections.append((current_section, paragraphs))
            headline = tag.find("span", class_="mw-headline")
            title = headline.text.strip() if headline else tag.get_text(strip=True)
            current_section = title
            skip_current = title in skip_sections
            paragraphs = []
            started = False
            continue

        if skip_current:
            continue

        # Zbieraj paragrafy oraz listy
        if not started:
            if tag.name != "p" or not tag.get_text(strip=True):
                continue
            started = True

        if tag.name == "p":
            text = clean_tag(tag)
            if text:
                paragraphs.append(text)

        elif tag.name == "ul":
            for li in tag.find_all("li", recursive=False):
                text = clean_tag(li)
                if text:
                    paragraphs.append(f"- {text}")

        elif tag.name == "ol":
            for i, li in enumerate(tag.find_all("li", recursive=False), start=1):
                text = clean_tag(li)
                if text:
                    paragraphs.append(f"{i}. {text}")

    # Dodaj ostatnią sekcję
    if paragraphs and not skip_current:
        sections.append((current_section, paragraphs))

    # Spłaszcz listę sekcji do słownika
    result = {}
    for sec, paras in sections:
        combined = "<br>".join(paras)
        result[sec] = combined

    return result

# Przyklad uzycia:
# import json
# data = extract_sections_as_list("https://en.wikipedia.org/wiki/Python_(programming_language)")
# print(json.dumps(data, ensure_ascii=False, indent=2))
