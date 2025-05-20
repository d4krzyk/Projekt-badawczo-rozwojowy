import time
from fastapi import APIRouter, Query, HTTPException
from typing import Optional
from wikipediaapi.category_naive import top_category
from wikipediaapi.category_shortcut import main_category


router = APIRouter(prefix="/category-extract", tags=["Wikipedia"])

# Zakładam, że extract_categories i top_category są zaimportowane/podane wyżej

@router.get("/naive")
def get_top_category(page_name: str = Query(..., description="Nazwa strony Wikipedia")):
    time_start = time.time()
    result = top_category(page_name)
    time_end = time.time()
    return {"page_name": page_name, "top_category": result, "time": time_end - time_start}

@router.get("/shortcut")
def get_top_category(page_name: str = Query(..., description="Nazwa strony Wikipedia")):
    time_start = time.time()
    result = main_category(page_name)
    time_end = time.time()
    return {"page_name": page_name, "top_category": result, "time": time_end - time_start}