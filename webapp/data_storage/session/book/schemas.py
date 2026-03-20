from pydantic import BaseModel, field_serializer
from datetime import datetime

from ..event.schemas import BookSessionEventInfoForBook
from ..utils import strip_wikipedia_prefix


class BookInfoForRoom(BaseModel):
    name: str
    session_events: list[BookSessionEventInfoForBook]

    @field_serializer("name")
    def serialize_name(self, value: str) -> str:
        return strip_wikipedia_prefix(value)

    class Config:
        from_attributes = True


class BookLog(BaseModel):
    bookName: str
    openTime: float
    closeTime: float
