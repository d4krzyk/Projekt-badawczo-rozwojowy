# Standard Library
from typing import List

# 3rd-Party
from sqlalchemy.exc import OperationalError
from starlette.middleware.base import BaseHTTPMiddleware
from starlette.requests import Request
from starlette.responses import JSONResponse

EXEMPT_PATHS: List[str] = [
    "/docs",
    "/redoc",
    "/openapi.json",
    "/metrics",
]

EXEMPT_PREFIXES: List[str] = [
    "/static",
]


class DatabaseHealthMiddleware(BaseHTTPMiddleware):
    async def dispatch(self, request: Request, call_next):
        path = request.url.path
        if self.is_exempt(path):
            return await call_next(request)
        try:
            return await call_next(request)
        except OperationalError:
            return JSONResponse(
                status_code=503,
                content={"detail": "Baza danych jest niedostępna"}
            )
        except Exception as e:
            return JSONResponse(
                status_code=500,
                content={"detail": f"Błąd serwera: {str(e)}"}
            )

    @classmethod
    def is_exempt(cls, path: str) -> bool:
        if path in EXEMPT_PATHS:
            return True
        return any(path.startswith(prefix) for prefix in EXEMPT_PREFIXES)
