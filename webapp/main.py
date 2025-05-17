from fastapi import FastAPI, Request

app = FastAPI()

from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError



#OpenAPI - auth
from authorization.openapi import build_custom_openapi
app.openapi = build_custom_openapi(app)

# Middleware
from database.middleware import DatabaseHealthMiddleware
from authorization.middleware import JWTAuthMiddleware

app.add_middleware(DatabaseHealthMiddleware)
app.add_middleware(JWTAuthMiddleware)


# Routers
from database.router import router as database_router
from authorization.router import router as auth_router

app.include_router(database_router)
app.include_router(auth_router)

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