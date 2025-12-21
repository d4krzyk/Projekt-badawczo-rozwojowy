# 3rd-Party
import time
from fastapi import APIRouter
from fastapi import Query
from fastapi import Request

# Local
from . import content
from . import utils
from . import infobox

router = APIRouter(prefix="/scraping", tags=["Web Scraping"])


@router.post("/content")
def extract_endpoint(
    req: Request, article_name: str = Query(..., description="Nazwa artykułu np. Pope")
):
    url = utils.article_url_by_name(article_name)
    sections = content.extract_sections_as_nested_list(url)
    # sections = extract_sections_as_list(url)
    return sections


@router.post("/category")
def extract_endpoint(
    request: Request,
    article_name: str = Query(..., description="Nazwa artykułu np. Pope"),
):
    with request.app.state.cache as scraper:
        title, url = scraper.get_first_category(article_name)

    return {
        "title": title,
        "url": url,
    }


@router.get("/infobox")
def get_infobox(page_name: str = Query(..., description="Nazwa artykułu np. Pope")):
    time_start = time.time()
    result = infobox.extract_infoboxes(page_name)
    time_end = time.time()
    return {"page_name": page_name, "infobox": result, "time": time_end - time_start}
