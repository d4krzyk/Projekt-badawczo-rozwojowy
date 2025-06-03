# Standard Library
import re
from urllib.parse import unquote
from urllib.parse import urlparse

# 3rd-Party
import requests
from bs4 import BeautifulSoup

WIKI_BASE = "https://en.wikipedia.org"


def normalize_wikipedia_url(input_str):
    if input_str.startswith("http"):
        parsed = urlparse(unquote(input_str))
        path = parsed.path
        if parsed.netloc in ["en.m.wikipedia.org", "simple.wikipedia.org", "en.wikipedia.org"]:
            return f"{WIKI_BASE}{path}"
        else:
            raise ValueError("Unsupported Wikipedia domain.")
    else:
        title = input_str.strip().replace(" ", "_")
        return f"{WIKI_BASE}/wiki/{title}"


def get_soup(url):
    response = requests.get(url)
    if response.status_code != 200:
        raise Exception(f"Failed to load {url}")
    return BeautifulSoup(response.text, 'html.parser')


def extract_sections_as_list(article_url):
    """
    Pobiera treść artykułu z Wikipedia i zwraca słownik,
    gdzie kluczem jest nazwa sekcji, a wartością połączone paragrafy.
    """
    soup = get_soup(article_url)
    content_div = soup.select_one('div#mw-content-text')
    if not content_div:
        raise Exception("Could not find article content.")

    # Usuń infoboxy i znaczniki formatowania
    for infobox in content_div.find_all('table', class_='infobox'):
        infobox.decompose()
    for tag_name in ['i', 'em', 'b', 'strong']:
        for tag in content_div.find_all(tag_name):
            tag.unwrap()

    skip_tags = {"table", "style", "script", "noscript", "math", "img"}
    skip_sections = {"See also", "Further reading", "External links", "Notes", "References"}

    sections = []
    current_section = "Introduction"
    paragraphs = []
    skip_current = False
    started = False

    for tag in content_div.descendants:
        if not hasattr(tag, 'name'):
            continue

        # Nagłówki h2 oznaczają nowe sekcje
        if tag.name == 'h2':
            if paragraphs and not skip_current:
                sections.append((current_section, paragraphs))
            headline = tag.find('span', class_='mw-headline')
            title = headline.text.strip() if headline else tag.get_text(strip=True)
            current_section = title
            skip_current = title in skip_sections
            paragraphs = []
            continue

        # Pomijaj niechciane tagi i sekcje referencyjne
        if skip_current or tag.name in skip_tags:
            continue

        # Zbieraj paragrafy oraz listy
        if tag.name in ['p', 'ul', 'ol']:
            text = tag.get_text(separator=' ', strip=True)
            if not started:
                if tag.name != 'p' or not text:
                    continue
                started = True

            # Usuń odnośniki dolne
            for sup in tag.find_all('sup', class_='reference'):
                sup.decompose()
            # Oczyść tekst
            text = re.sub(r"\[.*?\]", "", text)
            text = re.sub(r"\s{2,}", " ", text).strip()
            if text:
                paragraphs.append(text)

    # Dodaj ostatnią sekcję
    if paragraphs and not skip_current:
        sections.append((current_section, paragraphs))

    # Spłaszcz listę sekcji do słownika
    result = {}
    for sec, paras in sections:
        combined = ' '.join(paras)
        result[sec] = combined

    return result

# Przykład użycia:
# data = extract_sections_as_list("https://en.wikipedia.org/wiki/Python_(programming_language)")
# print(json.dumps(data, ensure_ascii=False, indent=2))
