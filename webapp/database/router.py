# 3rd-Party
from fastapi import APIRouter
from sqlalchemy import text

# Local
from .engine import engine

router = APIRouter(prefix="/database", tags=["Database"])


@router.get("/health", summary="Sprawdzenie stanu połączenia z bazą danych")
def database_healthcheck():
    with engine.connect() as connection:
        result = connection.execute(text("SELECT * FROM api_keys"))
        keys = list(result.mappings())  # <-- To daje listę słowników
    return {"status": "ok", "api_keys": keys}
