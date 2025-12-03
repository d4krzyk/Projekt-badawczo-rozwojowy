from pydantic import BaseModel
from datetime import datetime

from ..book.schemas import BookInfoForRoom, BookLog
from ..link.schemas import BookLinkInfo, LinkLog


class RoomInfo(BaseModel):
    name: str
    enter_time: datetime
    exit_time: datetime
    books: list[BookInfoForRoom]
    book_links: list[BookLinkInfo]

    class Config:
        from_attributes = True


class RoomLog(BaseModel):
    roomName: str
    enterTime: float
    exitTime: float
    bookLogs: list[BookLog]
    linkLogs: list[LinkLog]
