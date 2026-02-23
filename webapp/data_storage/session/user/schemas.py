from pydantic import BaseModel

from ..usersession.schemas import SessionInfo


class UserSessionsResponse(BaseModel):
    user_name: str
    sessions: list[SessionInfo]


class GroupSessionsResponse(BaseModel):
    group_name: str
    users: list[UserSessionsResponse]
