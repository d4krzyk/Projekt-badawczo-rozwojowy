import json
from fastapi import APIRouter, HTTPException, Request, Query
from pydantic import BaseModel
from typing import List

# import core extraction logic from content.py
from .content import normalize_wikipedia_url, extract_sections_as_list

router = APIRouter(prefix="/scraping", tags=["Web Scraping"])

import pprint

@router.post("/extract",response_model=List[dict[str,str]])
def extract_endpoint(
    req: Request, article_name: str = Query(..., description="Nazwa artykułu np. Pope")
):
    url = normalize_wikipedia_url(article_name)
    sections = extract_sections_as_list(url)
    return sections
