from pydantic import BaseModel, Field
from datetime import datetime

from ..book.schemas import BookInfoForRoom, BookLog
from ..link.schemas import BookLinkInfo, LinkLog

class ImageInteractionStats(BaseModel):
    totalTime: float
    count: int


class ImageInteractions(BaseModel):
    visible: ImageInteractionStats
    hoverOn: ImageInteractionStats
    hoverNear: ImageInteractionStats
    hoverSameY: ImageInteractionStats
    clickAttempts: int


class ImageLog(BaseModel):
    photoUrl: str
    interactions: ImageInteractions



class RoomInfo(BaseModel):
    name: str
    enter_time: datetime
    exit_time: datetime
    cursor_log: list[float] = Field(default_factory=list)
    image_logs: list[ImageLog] = Field(default_factory=list)
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
    imageLogs: list[ImageLog] = Field(default_factory=list)
    cursorLog: list[float] = Field(default_factory=list)
