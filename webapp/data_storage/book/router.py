from fastapi import APIRouter, Depends, HTTPException, status
from sqlalchemy.orm import Session

from database.engine import get_db
from ..schemas import BookOpenRequest, BookCloseRequest
from . import service as book_service

book_router = APIRouter(prefix="/book", tags=["Book"])


@book_router.post("/open", status_code=status.HTTP_201_CREATED)
def open_book_endpoint(
    request_body: BookOpenRequest, db: Session = Depends(get_db)
):
    try:
        book_service.open_book(db, request_body)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


@book_router.post("/close", status_code=status.HTTP_200_OK)
def close_book_endpoint(
    request_body: BookCloseRequest, db: Session = Depends(get_db)
):
    try:
        book_service.close_book(db, request_body)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )
