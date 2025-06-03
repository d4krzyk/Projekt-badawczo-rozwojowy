# 3rd-Party
from fastapi import APIRouter
from fastapi import Depends
from fastapi import Request
from fastapi.responses import JSONResponse
from sqlalchemy.orm import Session

# Project
from database.engine import get_db

# Local
from .requests import LoginRequest
from .requests import RegisterRequest
from .service import login_user
from .service import logout_user
from .service import register_user

router = APIRouter(prefix="/auth", tags=["Auth"])


@router.post("/register")
def register(
    data: RegisterRequest,
    db: Session = Depends(get_db)
) -> JSONResponse:
    token: str = register_user(data.username, data.email, data.password, db)
    return JSONResponse(content={"access_token": token, "token_type": "bearer"})


@router.post("/login")
def login(
    data: LoginRequest,
    db: Session = Depends(get_db)
) -> JSONResponse:
    token: str = login_user(data.identifier, data.password, db)
    return JSONResponse(content={"access_token": token, "token_type": "bearer"})


@router.post("/logout")
def logout(
    request: Request,
    db: Session = Depends(get_db)
) -> JSONResponse:
    ok: bool = logout_user(request.state.user_id, db)
    return JSONResponse(content={"detail": "Wylogowano" if ok else "Nie był zalogowany"})
