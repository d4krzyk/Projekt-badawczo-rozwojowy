from pydantic import BaseModel
from datetime import datetime

from ..room.schemas import RoomInfo, RoomLog


class SessionInfo(BaseModel):
    id: int
    start_time: datetime
    end_time: datetime | None
    rooms: list[RoomInfo]

    class Config:
        from_attributes = True


class FullSessionRequest(BaseModel):
    user_name: str
    session_logs: list[RoomLog]
