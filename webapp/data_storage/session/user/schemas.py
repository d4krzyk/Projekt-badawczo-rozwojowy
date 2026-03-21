from pydantic import BaseModel

from ..usersession.schemas import SessionInfo


class DataUserResponse(BaseModel):
    id: int
    name: str
    app_sessions_count: int
    web_sessions_count: int

    class Config:
        from_attributes = True


class UserSessionsResponse(BaseModel):
    user_name: str
    sessions: list[SessionInfo]


class GroupSessionsResponse(BaseModel):
    group_name: str
    users: list[UserSessionsResponse]
