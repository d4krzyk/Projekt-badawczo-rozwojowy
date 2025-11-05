from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from database.engine import get_db
from ..schemas import (
    UserSessionsResponse,
    FullSessionRequest,
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


@session_router.post("/", status_code=status.HTTP_201_CREATED)
def create_session_endpoint(
    request_body: FullSessionRequest, db: Session = Depends(get_db)
):
    try:
        session_service.create_full_session(db, request_body)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


@session_router.delete("/clear-all-data", status_code=status.HTTP_204_NO_CONTENT)
def clear_all_data_endpoint(db: Session = Depends(get_db)):
    try:
        session_service.clear_all_data(db)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )

