from pydantic import BaseModel
from datetime import datetime

class SessionCreateRequest(BaseModel):
    user_name: str
    wiki_url: str

class SessionCloseRequest(BaseModel):
    user_name: str
    wiki_url: str

class BookOpenRequest(BaseModel):
    user_name: str
    wiki_url: str
    book_url: str

class BookCloseRequest(BaseModel):
    user_name: str
    wiki_url: str
    book_url: str

# Schemas for Get User Sessions endpoint
class RoomInfo(BaseModel):
    name: str
    enter_time: datetime
    exit_time: datetime

    class Config:
        orm_mode = True

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


class SessionInfo(BaseModel):
    id: int
    start_time: datetime
    end_time: datetime | None
    rooms: list[RoomInfo]
    book_session_events: list[BookSessionEventInfo]

    class Config:
        orm_mode = True

class UserSessionsResponse(BaseModel):
    user_name: str
    sessions: list[SessionInfo]