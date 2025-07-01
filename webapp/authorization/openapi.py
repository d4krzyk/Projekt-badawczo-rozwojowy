# 3rd-Party
from fastapi.openapi.utils import get_openapi


def build_custom_openapi(app):
    def custom_openapi():
        if app.openapi_schema:
            return app.openapi_schema
        schema = get_openapi(
            title="API z JWT",
            version="1.0.0",
            description="Autoryzacja z Bearer Token",
            routes=app.routes,
        )
        schema["components"]["securitySchemes"] = {
            "BearerAuth": {"type": "http", "scheme": "bearer", "bearerFormat": "JWT"}
        }
        for path in schema["paths"].values():
            for method in path.values():
                method.setdefault("security", []).append({"BearerAuth": []})
        app.openapi_schema = schema
        return schema
    return custom_openapi
