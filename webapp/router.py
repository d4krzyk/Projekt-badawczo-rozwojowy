"""Main router file."""
# Standard Library
from typing import Optional

# 3rd-Party
from fastapi import APIRouter
from fastapi import Query
from fastapi import Request
from fastapi.exceptions import HTTPException

# Project
from webscraping.content import extract_sections_as_nested_list
from webscraping.utils import get_wikipedia_url
from wikipediaapi.category_batching import main_category
from wikipedia_dumps.category import get_main_category_by_name

router = APIRouter(prefix='', tags=['Main'])


@router.get('/article')
def get_article_data(
    request: Request,
    name: Optional[str] = Query(None, description='Nazwa artykułu'),
    url: Optional[str] = Query(None, description='URL artykułu'),
    use_dumps: Optional[bool] = Query(True, description='Używaj wikipedia dumps'),
):
    if name:
        article_name = name
        article_url = get_wikipedia_url(name)
    elif url:
        article_name = ''  # TODO: wyciągnąć nazwę artykułu z urla, np. przez api - lepiej tak niż przerabiać url stringa
        article_url = get_wikipedia_url(url)
    else:
        raise HTTPException(
            status_code=404,
            detail='Musisz podać albo article_name, albo article_url'
        )

    category = ''
    if use_dumps:
        # Pobieranie kategorii z dumpów, na podstawie pierwszej wyciągniętej kategorii z api.
        first_category = ''  # TODO: wyciągnąć pierwszą kategorię artykułu, z api.
        if first_category:
            category = get_main_category_by_name(first_category, request)

    if not category:
        # Pobieranie z api, gdyby z dumpów nie było.
        category = main_category(article_name)
        if category:
            category = category.split('Category:')[-1]  # usunięcie 'Category:'
    content = extract_sections_as_nested_list(article_url)
    if content:
        return {
            'name': article_name,
            'url': article_url,
            'category': category,
            'content': content,
        }
    raise HTTPException(
        status_code=404,
        detail='Article not found'
    )
