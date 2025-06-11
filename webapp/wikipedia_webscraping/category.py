import requests
from bs4 import BeautifulSoup
import os
import time
from urllib.parse import urljoin

BASE_URL = "https://en.wikipedia.org/wiki/"
CACHE_DIR = "wikipedia_webscraping/html_cache"
EXPIRY = 7 * 24 * 3600  # seconds (1 week)
TIMEOUT = 10.0

# utwórz katalog cache, jeśli nie istnieje
os.makedirs(CACHE_DIR, exist_ok=True)


class CategoryScraper:
    def __init__(self):
        self.session = requests.Session()
        self.session.headers.update(
            {
                "User-Agent": (
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                    "AppleWebKit/537.36 (KHTML, like Gecko) "
                    "Chrome/124.0.0.0 Safari/537.36"
                )
            }
        )

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_value, traceback):
        self.session.close()

    def _page_url(self, title: str) -> str:
        return BASE_URL + title.strip().replace(" ", "_")

    def _cache_path(self, title: str) -> str:
        filename = title.strip().replace(" ", "_") + ".html"
        return os.path.join(CACHE_DIR, filename)

    def get_first_category(self, title: str) -> tuple[str, str] | tuple[None, None]:
        url = self._page_url(title)
        path = self._cache_path(title)

        if os.path.exists(path) and (time.time() - os.path.getmtime(path) < EXPIRY):
            with open(path, "r", encoding="utf-8") as f:
                html = f.read()
        else:
            resp = self.session.get(url, timeout=TIMEOUT)
            if resp.status_code != 200:
                return None, None
            html = resp.text
            with open(path, "w", encoding="utf-8") as f:
                f.write(html)

        soup = BeautifulSoup(html, "lxml")
        a = soup.select_one("#mw-normal-catlinks ul li a")
        if not a:
            return None, None
        return a["title"], urljoin(url, a["href"])
