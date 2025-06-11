"""Main router file."""

# Standard Library
from typing import Optional

# 3rd-Party
from fastapi import APIRouter
from fastapi import Query
from fastapi import Request
from fastapi.exceptions import HTTPException

# Project
import webscraping
import wikipediaapi
import wikipedia_dumps

router = APIRouter(prefix="", tags=["Main"])

# STRATEGIES = ["api", "dumps", "webscraping"]
# STRATEGIES = ["api", "dumps"]
STRATEGIES = ["api"]


@router.get("/article")
def get_article_data(
    request: Request,
    article: str | None = Query(None, description="Nazwa artykułu lub url"),
    category_strategy: str = Query(
        "api", enum=STRATEGIES, description="Strategia pozyskania kategorii"
    ),
):
    article = article.strip() if article and article.strip() else None

    if not article:
        raise HTTPException(status_code=404, detail="Podałeś puste article")

    is_name = True if not webscraping.utils.article_name_by_url(article) else False

    if is_name:
        article_name = article
        article_url = webscraping.utils.article_url_by_name(article_name)
    else:
        article_name = webscraping.utils.article_name_by_url(article)
        article_url = article

    # wybór strategii
    match category_strategy:
        case "api":
            raw = wikipediaapi.category_batching.main_category(article_name)
        case "dumps" | "webscraping":
            raw = None  # do zaimplementowania
        case _:
            raise HTTPException(status_code=404, detail="Strategy not found")

    category = raw.split("Category:")[-1] if raw else None

    # pobranie treści
    content = webscraping.content.extract_sections_as_nested_list(article_url)
    if not content:
        raise HTTPException(status_code=404, detail="Article not found")

    return {
        "name": article_name,
        "url": article_url,
        "category": category,
        "content": content,
    }
