from fastapi import APIRouter
from sqlalchemy import text
from sqlalchemy.exc import OperationalError
from starlette.responses import JSONResponse
from database.engine import engine

router = APIRouter(prefix="/database", tags=["Database"])

@router.get("/health", summary="Sprawdzenie stanu połączenia z bazą danych")
def database_healthcheck():
    with engine.connect() as connection:
        result = connection.execute(text("SELECT * FROM api_keys"))
        keys = list(result.mappings())  # <-- To daje listę słowników
    return {"status": "ok", "api_keys": keys}
