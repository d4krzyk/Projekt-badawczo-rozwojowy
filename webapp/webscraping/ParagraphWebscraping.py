import os
import requests
from bs4 import BeautifulSoup
from urllib.parse import urljoin, urlparse, unquote
import re
import json

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
    soup = get_soup(article_url)
    content_div = soup.select_one('div#mw-content-text')
    if not content_div:
        raise Exception("Could not find article content.")

    for infobox in content_div.find_all('table', class_='infobox'):
        infobox.decompose()
    for tag_name in ['i', 'em']:
        for tag in content_div.find_all(tag_name):
            tag.unwrap()
    for tag_name in ['b', 'strong']:
        for tag in content_div.find_all(tag_name):
            tag.unwrap()

    skip_tags = {"table", "style", "script", "noscript", "math", "img"}
    skip_section_titles = {
        "See also",
        "Further reading",
        "External links",
        "Notes",
        "References"
    }

    sections = []
    wikipedia_links = set()
    current_section_name = "Introduction"
    current_paragraphs = []
    skip_current_section = False 
    started_article_content = False

    for tag in content_div.descendants:
        if not hasattr(tag, 'name'):
            continue

        if (tag.name == 'h2') or (tag.name == 'div' and 'mw-heading2' in tag.get('class', [])):
            if tag.name == 'div':
                tag = tag.find('h2')
            if tag and tag.get_text(strip=True):
                if current_paragraphs and not skip_current_section:
                    sections.append([current_section_name, current_paragraphs])

                span = tag.find('span', class_='mw-headline')
                section_title = span.text.strip() if span else tag.get_text(strip=True)
                current_section_name = section_title
                skip_current_section = current_section_name in skip_section_titles
                current_paragraphs = []
            continue

        if tag.name in skip_tags or skip_current_section:
            continue

        if tag.name in ["p", "ul", "ol"]:

            raw_text = tag.get_text(strip=True)
            if not started_article_content:
                if tag.name != 'p' or not raw_text or 'mw-empty-elt' in tag.get('class', []):
                    continue
                started_article_content = True

            for sup in tag.find_all("sup", class_="reference"):
                sup.decompose()

            if not skip_current_section:
                for a in tag.find_all("a", href=True):
                    href = a['href']
                    if href.startswith("/wiki/") and not href.startswith("/wiki/Special:"):
                        full_url = urljoin(WIKI_BASE, href)
                        title = a.get_text(strip=True)
                        if title and full_url:
                            wikipedia_links.add((title, full_url))

            text = tag.get_text(separator=" ", strip=True)
            text = re.sub(r"\[\s*[^]]*?\s*\]", "", text)
            text = re.sub(r"\s{2,}", " ", text).strip()
            text_ascii = text.encode("ascii", errors="ignore").decode("ascii")

            if text_ascii and not skip_current_section:
                current_paragraphs.append(text_ascii)

    if current_paragraphs and not skip_current_section:
        sections.append([current_section_name, current_paragraphs])

    return sections, sorted(wikipedia_links)


def print_sections_as_json(sections, article_title, links):
    content_dict = {}
    for section, paragraphs in sections:
        combined_paragraphs = " ".join(paragraphs).replace('"', '')
        content_dict[section] = combined_paragraphs

    links_dict = {}
    for text, url in links:
        clean_text = text.replace('"', '')
        links_dict[clean_text] = url

    output_data = {
        "name": article_title,
        "content": content_dict,
        "links": links_dict
    }

    print(json.dumps(output_data, indent=4, ensure_ascii=False))



def main():
    print("Wikipedia Article Text Extractor")
    print("Type 'exit' or 'q' to quit.\n")

    while True:
        article = input("Enter a Wikipedia article URL or title: ").strip()
        if article.lower() in {"exit", "q"}:
            print("Goodbye.")
            break

        try:
            url = normalize_wikipedia_url(article)
            sections, links = extract_sections_as_list(url)

            title = url.split("/wiki/")[-1]
            title = unquote(title).replace("_", " ")

            if not sections:
                print("No readable content found.")
                continue

            print_sections_as_json(sections, title, links)

        except Exception as e:
            print(f"Error: {e}")

if __name__ == "__main__":
    main()

