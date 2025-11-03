from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from database.engine import get_db
from ..schemas import (
    SessionCreateRequest,
    SessionCloseRequest,
    UserSessionsResponse,
)
from . import service as session_service

session_router = APIRouter(prefix="/session", tags=["Session"])


@session_router.get("/{user_name}", response_model=UserSessionsResponse)
def get_user_sessions_endpoint(user_name: str, db: Session = Depends(get_db)):
    try:
        return session_service.get_user_sessions(db, user_name)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


@session_router.post("/create", status_code=status.HTTP_201_CREATED)
def create_session_endpoint(
    request_body: SessionCreateRequest, db: Session = Depends(get_db)
):
    try:
        session_service.create_session(db, request_body)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


@session_router.post("/close", status_code=status.HTTP_200_OK)
def close_session_endpoint(
    request_body: SessionCloseRequest, db: Session = Depends(get_db)
):
    try:
        session_service.close_session(db, request_body)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )
