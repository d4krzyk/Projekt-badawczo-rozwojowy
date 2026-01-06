from pydantic import BaseModel
from datetime import datetime

from ..event.schemas import BookSessionEventInfoForBook


class BookInfoForRoom(BaseModel):
    name: str
    session_events: list[BookSessionEventInfoForBook]

    class Config:
        from_attributes = True


class BookLog(BaseModel):
    bookName: str
    openTime: float
    closeTime: float
