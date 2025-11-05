from pydantic import BaseModel
from datetime import datetime

# Schemas for Get User Sessions endpoint
class BookInfoForEvent(BaseModel):
    name: str

    class Config:
        orm_mode = True


class BookSessionEventInfo(BaseModel):
    open_time: datetime
    close_time: datetime | None
    book: BookInfoForEvent

    class Config:
        orm_mode = True


class BookLinkEvent(BaseModel):
    link: str
    click_time: datetime | None

    class Config:
        orm_mode = True


class RoomInfo(BaseModel):
    name: str
    enter_time: datetime
    exit_time: datetime
    book_session_events: list[BookSessionEventInfo]
    book_link_events: list[BookLinkEvent]

    class Config:
        orm_mode = True


class SessionInfo(BaseModel):
    id: int
    start_time: datetime
    end_time: datetime | None
    rooms: list[RoomInfo]

    class Config:
        orm_mode = True


class UserSessionsResponse(BaseModel):
    user_name: str
    sessions: list[SessionInfo]


# Schemas for Full Session endpoint
class BookLog(BaseModel):
    bookName: str
    openTime: float
    closeTime: float


class LinkLog(BaseModel):
    linkName: str
    clickTime: float


class RoomLog(BaseModel):
    roomName: str
    enterTime: float
    exitTime: float
    bookLogs: list[BookLog]
    linkLogs: list[LinkLog]


class FullSessionRequest(BaseModel):
    user_name: str
    session_logs: list[RoomLog]