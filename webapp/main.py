# Standard Library
from contextlib import asynccontextmanager

# 3rd-Party
from fastapi import FastAPI
from fastapi import Request
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

# Project
from authorization.router import router as auth_router
from database.middleware import DatabaseHealthMiddleware
from database.router import router as database_router
from webscraping.router import router as scraping_router
from wikipedia_dumps.get_data import get_or_create_reversed_categories
from wikipedia_dumps.get_data import get_or_create_title_to_id
from wikipedia_dumps.router import router as dumps_router
from wikipedia_dumps.utils import load_from_file
from wikipediaapi.router import router as wikiapi_router

# Local
from router import router as main_router


ENABLE_DUMPS = False


# Lifespan
@asynccontextmanager
async def lifespan(app: FastAPI):
    """Ładowanie danych tylko raz przy starcie."""
    if ENABLE_DUMPS:
        app.state.category_child_to_parent = get_or_create_reversed_categories()
        app.state.title_to_id = get_or_create_title_to_id()
        app.state.id_to_title = load_from_file('wikipedia_dumps/data/id_to_title_min.json')
    yield


app = FastAPI(lifespan=lifespan)
# app = FastAPI()
# OpenAPI - auth
# app.openapi = build_custom_openapi(app)
# app.add_middleware(JWTAuthMiddleware)

# Middleware
app.add_middleware(DatabaseHealthMiddleware)


# Routers
app.include_router(database_router)
app.include_router(auth_router)
app.include_router(wikiapi_router)
app.include_router(dumps_router)
app.include_router(scraping_router)
app.include_router(main_router)


# Default
@app.get("/api/data")
def secure(request: Request):
    return {"msg": f"Dostęp przyznany dla użytkownika {request.state.user_id}"}


@app.exception_handler(RequestValidationError)
async def custom_validation_exception_handler(request: Request, exc: RequestValidationError):
    return JSONResponse(
        status_code=422,
        content={
            "errors": [e['msg'] for e in exc.errors()]
        }
    )
