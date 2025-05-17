from fastapi import APIRouter, Request, Depends, HTTPException
from sqlalchemy.orm import Session
from authorization import service
from authorization.requests import RegisterRequest, LoginRequest
from database.engine import get_db
from typing import Dict
from fastapi.responses import JSONResponse

router = APIRouter(prefix="/auth", tags=["Auth"])

@router.post("/register")
def register(
    data: RegisterRequest,
    db: Session = Depends(get_db)
) -> JSONResponse:
    token: str = service.register_user(data.username, data.email, data.password, db)
    return JSONResponse(content={"access_token": token, "token_type": "bearer"})

@router.post("/login")
def login(
    data: LoginRequest,
    db: Session = Depends(get_db)
) -> JSONResponse:
    token: str = service.login_user(data.identifier, data.password, db)
    return JSONResponse(content={"access_token": token, "token_type": "bearer"})

@router.post("/logout")
def logout(
    request: Request,
    db: Session = Depends(get_db)
) -> JSONResponse:
    ok: bool = service.logout_user(request.state.user_id, db)
    return JSONResponse(content={"detail": "Wylogowano" if ok else "Nie był zalogowany"})
