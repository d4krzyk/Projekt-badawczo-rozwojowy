import time
from fastapi import APIRouter, Query, HTTPException
from typing import Optional
from wikipediaapi import category_batching, category_naive, category_shortcut

router = APIRouter(prefix="/category-extract", tags=["Wikipedia"])

# Zakładam, że extract_categories i top_category są zaimportowane/podane wyżej


@router.get("/naive")
def get_top_category(page_name: str = Query(..., description="Nazwa strony Wikipedia")):
    time_start = time.time()
    result = category_naive.main_category(page_name)
    time_end = time.time()
    return {"page_name": page_name, "top_category": result, "time": time_end - time_start}


@router.get("/shortcut")
def get_top_category(page_name: str = Query(..., description="Nazwa strony Wikipedia")):
    time_start = time.time()
    result = category_shortcut.main_category(page_name)
    time_end = time.time()
    return {"page_name": page_name, "top_category": result, "time": time_end - time_start}


@router.get("/batching")
def get_top_category(page_name: str = Query(..., description="Nazwa strony Wikipedia")):
    time_start = time.time()
    result = category_batching.main_category(page_name)
    time_end = time.time()
    return {"page_name": page_name, "top_category": result, "time": time_end - time_start}
