from pydantic import BaseModel
from datetime import datetime

from ..room.schemas import RoomInfo, RoomLog


class SessionInfo(BaseModel):
    id: int
    group_name: str | None
    start_time: datetime
    end_time: datetime | None
    is_web: bool
    surrendered: bool | None
    rooms: list[RoomInfo]

    class Config:
        from_attributes = True


class SessionInfoWithUser(SessionInfo):
    user_name: str


class UserGroupedSessions(BaseModel):
    user_name: str
    app_sessions: list[SessionInfo]
    web_sessions: list[SessionInfo]


class GroupGroupedSessions(BaseModel):
    group_name: str
    users: list[UserGroupedSessions]


class AllSessionsGroupedResponse_old(BaseModel):
    users: list[UserGroupedSessions]


class GroupSessionAggregate(BaseModel):
    group_name: str
    sessions: list[SessionInfo]


class SessionTypeGroupedSessions(BaseModel):
    groups: list[GroupSessionAggregate]


class UserSessionsByTypeAndGroup(BaseModel):
    user_name: str
    app_session: SessionTypeGroupedSessions
    web_session: SessionTypeGroupedSessions


class AllSessionsGroupedResponse(BaseModel):
    users: list[UserSessionsByTypeAndGroup]


class FullSessionRequest(BaseModel):
    user_name: str
    group: str | None
    surrendered: bool | None
    session_logs: list[RoomLog]
