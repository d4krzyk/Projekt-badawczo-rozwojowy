# Standard Library
from typing import List
from typing import Optional

# 3rd-Party
from sqlalchemy.exc import OperationalError
from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import JSONResponse

# Project
from authorization.service import validate_token_against_db
from database.engine import db_session

EXEMPT_PATHS: List[str] = [
    "/auth/login",
    "/auth/register",
    "/docs",
    "/openapi.json",
    "/redoc",
    "/metrics"
]

EXEMPT_PREFIXES: List[str] = [
    "/database",
]


class JWTAuthMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request: Request, call_next):
        request.state.user_id = None

        if self.is_exempt(request.url.path):
            return await call_next(request)

        token = self.extract_token(request)
        if not token:
            return self.unauthorized("Brak tokenu")

        try:
            user_id = self.validate_token(token)
        except OperationalError:
            return JSONResponse(
                status_code=503,
                content={"detail": "Baza danych jest niedostępna"}
            )
        except Exception:
            return self.unauthorized("Wewnętrzny błąd podczas walidacji tokenu")

        if not user_id:
            return self.unauthorized("Token nieprawidłowy lub wygasł")

        request.state.user_id = user_id
        return await call_next(request)

    @classmethod
    def is_exempt(cls, path: str) -> bool:
        if path in EXEMPT_PATHS:
            return True
        return any(path.startswith(prefix) for prefix in EXEMPT_PREFIXES)

    @staticmethod
    def extract_token(request: Request) -> Optional[str]:
        auth = request.headers.get("Authorization")
        if not auth or not auth.startswith("Bearer "):
            return None
        return auth.split(" ", 1)[1]

    @staticmethod
    def unauthorized(detail: str) -> JSONResponse:
        return JSONResponse(status_code=401, content={"detail": detail})

    @staticmethod
    def validate_token(token: str) -> Optional[str]:
        with db_session() as db:
            return validate_token_against_db(token, db)
