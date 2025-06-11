# 3rd-Party
from fastapi import APIRouter
from fastapi import Query
from fastapi import Request

# Local
from .content import extract_sections_as_list
from .content import extract_sections_as_nested_list
from .utils import get_wikipedia_url

router = APIRouter(prefix="/scraping", tags=["Web Scraping"])


@router.post("/extract")
def extract_endpoint(
    req: Request, article_name: str = Query(..., description="Nazwa artykułu np. Pope")
):
    url = get_wikipedia_url(article_name)
    sections = extract_sections_as_nested_list(url)
    # sections = extract_sections_as_list(url)
    return sections
