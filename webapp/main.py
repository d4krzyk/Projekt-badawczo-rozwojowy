# Standard Library
from contextlib import asynccontextmanager

# 3rd-Party
from fastapi import FastAPI

# Project
import wikipedia_api
import wikipedia_webscraping
import wikipedia_dumps
import data_storage
from wikipedia_webscraping.category import CategoryScraper

# Local
from router import router as main_router

# ENABLE_DUMPS = True
ENABLE_DUMPS = False
ENABLE_CACHE = True

# Lifespan
@asynccontextmanager
async def lifespan(app: FastAPI):
    """Ładowanie danych tylko raz przy starcie."""
    if ENABLE_DUMPS:
        app.state.category_child_to_parent = (
            wikipedia_dumps.get_data.get_or_create_reversed_categories()
        )
        app.state.title_to_id = wikipedia_dumps.get_data.get_or_create_title_to_id()
        app.state.id_to_title = wikipedia_dumps.utils.load_from_file(
            "wikipedia_dumps/data/id_to_title_min.json"
        )
    if ENABLE_CACHE:
        cache: CategoryScraper = CategoryScraper()
        app.state.cache = cache
    yield

app = FastAPI(lifespan=lifespan)

app.include_router(data_storage.router.data_storage_router)
app.include_router(wikipedia_api.router.router_categories)
app.include_router(wikipedia_api.router.router_images)
app.include_router(wikipedia_dumps.router.router)
app.include_router(wikipedia_webscraping.router.router)
app.include_router(main_router)
