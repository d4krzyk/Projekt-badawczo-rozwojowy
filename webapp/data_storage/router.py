from fastapi import APIRouter

from .book.router import book_router
from .session.router import session_router

data_storage_router = APIRouter()
data_storage_router.include_router(book_router)
data_storage_router.include_router(session_router)
