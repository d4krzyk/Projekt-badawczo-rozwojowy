from pydantic import BaseModel, Field, field_serializer

from ..book.schemas import BookInfoForRoom, BookLog
from ..link.schemas import BookLinkInfo, LinkLog
from ..utils import strip_wikipedia_prefix

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
    enter_time: float
    exit_time: float
    # cursor_log: list[float] = Field(default_factory=list)
    # image_logs: list[ImageLog] = Field(default_factory=list)
    books: list[BookInfoForRoom]
    book_links: list[BookLinkInfo]

    @field_serializer("name")
    def serialize_name(self, value: str) -> str:
        return strip_wikipedia_prefix(value)

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
