# Standard Library
import time

# 3rd-Party
from fastapi import APIRouter
from fastapi import Query

# Local
from . import category_batching
from . import category_naive
from . import category_shortcut
from . import images

router_categories = APIRouter(prefix="/category-extract", tags=["Wikipedia API"])
router_images = APIRouter(prefix="/images", tags=["Wikipedia"])

# Zakładam, że extract_categories i top_category są zaimportowane/podane wyżej

@router_categories.get("/naive")
def get_top_category_naive(page_name: str = Query(..., description="Nazwa strony Wikipedia")):
    time_start = time.time()
    result = category_naive.main_category(page_name)
    time_end = time.time()
    return {"page_name": page_name, "top_category": result, "time": time_end - time_start}


@router_categories.get("/shortcut")
def get_top_category_shortcut(page_name: str = Query(..., description="Nazwa strony Wikipedia")):
    time_start = time.time()
    result = category_shortcut.main_category(page_name)
    time_end = time.time()
    return {"page_name": page_name, "top_category": result, "time": time_end - time_start}


@router_categories.get("/batching")
def get_top_category_batching(page_name: str = Query(..., description="Nazwa strony Wikipedia")):
    time_start = time.time()
    result = category_batching.main_category(page_name)
    time_end = time.time()
    return {"page_name": page_name, "top_category": result, "time": time_end - time_start}


@router_images.get("/generator")
def get_images_generator(page_name: str = Query(..., description="Obrazy w artykule")):
    time_start = time.time()
    result = images.images_generator(page_name)
    time_end = time.time()
    return {"page_name": page_name, "images": result, "time": time_end - time_start}


@router_images.get("/one_by_one")
def get_images_one_by_one(page_name: str = Query(..., description="Obrazy w artykule")):
    time_start = time.time()
    result = images.images_one_by_one(page_name)
    time_end = time.time()
    return {"page_name": page_name, "images": result, "time": time_end - time_start}
