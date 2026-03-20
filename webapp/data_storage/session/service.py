from datetime import datetime, timedelta

from sqlalchemy.orm import Session

from . import base_repository
from .book.repository import BookRepository
from .book.service import BookService
from .event.repository import EventRepository
from .group.repository import GroupRepository
from .group.service import GroupService
from .link.repository import LinkRepository
from .link.service import LinkService
from .room.repository import RoomRepository
from .room.service import RoomService
from .user.repository import UserRepository
from .user.schemas import GroupSessionsResponse, UserSessionsResponse
from .user.service import UserService
from .usersession.repository import UserSessionRepository
from .usersession.schemas import (
    AllSessionsGroupedResponse,
    AllSessionsGroupedResponse_old,
    FullSessionRequest,
    GroupSessionAggregate,
    SessionInfo,
    SessionTypeGroupedSessions,
    UserGroupedSessions,
    UserSessionsByTypeAndGroup,
)


class SessionService:
    def __init__(self, db: Session):
        self.db = db
        self._init_repositories()
        self._init_services()

    def _init_repositories(self):
        self.user_repo = UserRepository(self.db)
        self.group_repo = GroupRepository(self.db)
        self.usersession_repo = UserSessionRepository(self.db)
        self.room_repo = RoomRepository(self.db)
        self.book_repo = BookRepository(self.db)
        self.event_repo = EventRepository(self.db)
        self.link_repo = LinkRepository(self.db)

    def _init_services(self):
        self.user_service = UserService(self.user_repo)
        self.group_service = GroupService(self.group_repo)
        self.link_service = LinkService(self.link_repo)
        self.book_service = BookService(self.book_repo, self.event_repo)
        self.room_service = RoomService(
            self.room_repo, self.book_service, self.link_service
        )

    # ==================== CREATE ====================

    def create_full_session(self, request: FullSessionRequest, is_web: bool = False):
        """Creates a full user session from request data."""
        if not request.session_logs:
            return

        group = self.group_service.get_or_create_group(request.group)
        group_id = group.id if group else None

        data_user = self.user_service.get_or_create_user(request.user_name)

        session_start_time = datetime.utcnow()
        session_end_time = session_start_time + timedelta(
            seconds=request.session_logs[-1].exitTime
        )

        user_session = self.usersession_repo.create(
            user_id=data_user.id,
            group_id=group_id,
            start_time=session_start_time,
            end_time=session_end_time,
            is_web=is_web,
            surrendered=request.surrendered or False,
        )

        for room_log in request.session_logs:
            self.room_service.create_room_and_logs(
                room_log, user_session.id, session_start_time
            )

        self.db.commit()

    # ==================== READ ====================

    def get_user_sessions(self, user_name: str) -> UserSessionsResponse:
        user = self._get_user_or_raise(user_name)
        sessions_db = self.usersession_repo.get_all_for_user(user.id)
        sessions = [SessionInfo.from_orm(session) for session in sessions_db]
        return UserSessionsResponse(user_name=user_name, sessions=sessions)

    def get_group_sessions(self, group_name: str) -> GroupSessionsResponse:
        group = self.group_service.get_group_by_name(group_name)
        if not group:
            raise ValueError(f"Group '{group_name}' not found")

        sessions_db = self.usersession_repo.get_all_for_group(group.id)

        users_dict: dict[str, dict[str, list[SessionInfo]]] = {}

        for session in sessions_db:
            user_name = session.data_user.name
            users_dict.setdefault(user_name, {"app_sessions": [], "web_sessions": []})

            session_info = SessionInfo.from_orm(session)
            if session.is_web:
                users_dict[user_name]["web_sessions"].append(session_info)
            else:
                users_dict[user_name]["app_sessions"].append(session_info)

        users = [
            UserSessionsResponse(
                user_name=user_name,
                sessions=data["app_sessions"] + data["web_sessions"],
            )
            for user_name, data in users_dict.items()
        ]

        return GroupSessionsResponse(group_name=group_name, users=users)

    def get_all_sessions_old(self) -> AllSessionsGroupedResponse_old:
        sessions_db = self.usersession_repo.get_all()
        return self._build_legacy_grouped_response(sessions_db)

    def get_sessions_by_type(self, is_web: bool) -> AllSessionsGroupedResponse:
        sessions_db = self.usersession_repo.get_all_by_type(is_web)
        return self._build_user_grouped_response(sessions_db)

    def get_all_sessions(self) -> AllSessionsGroupedResponse:
        sessions_db = self.usersession_repo.get_all()
        return self._build_user_grouped_response(sessions_db)

    def check_user_exists(self, user_name: str, is_web: bool = False) -> bool:
        """Checks if a user exists and has a session of the specified type."""
        user = self.user_repo.get_by_name(user_name)
        if user is None:
            return False
        return self.usersession_repo.exists_for_user_and_type(user.id, is_web)

    def check_group_exists(self, group_name: str, is_web: bool = False) -> bool:
        """Checks if a group exists and has a session of the specified type."""
        group = self.group_repo.get_by_name(group_name)
        if group is None:
            return False
        return self.usersession_repo.exists_for_group_and_type(group.id, is_web)

    # ==================== DELETE ====================

    def delete_session(self, user_name: str, session_id: int):
        user = self._get_user_or_raise(user_name)
        session = self._get_session_or_raise(session_id)
        self._validate_session_ownership(session, user, session_id, user_name)

        self.usersession_repo.delete_by_id(session_id)
        self.db.commit()

    def delete_session_by_id(self, session_id: int):
        self._get_session_or_raise(session_id)
        self.usersession_repo.delete_by_id(session_id)
        self.db.commit()

    def delete_all_user_sessions(self, user_name: str):
        user = self._get_user_or_raise(user_name)
        self.usersession_repo.delete_all_for_user(user.id)
        self.db.commit()

    def clear_all_data(self):
        base_repository.clear_all_data(self.db)
        self.db.commit()

    # ==================== PRIVATE HELPERS ====================

    def _get_user_or_raise(self, user_name: str):
        user = self.user_service.get_user_by_name(user_name)
        if not user:
            raise ValueError(f"User '{user_name}' not found")
        return user

    def _get_session_or_raise(self, session_id: int):
        session = self.usersession_repo.get_by_id(session_id)
        if not session:
            raise ValueError(f"Session {session_id} not found")
        return session

    def _validate_session_ownership(
        self, session, user, session_id: int, user_name: str
    ):
        if session.data_user_id != user.id:
            raise ValueError(
                f"Session {session_id} does not belong to user '{user_name}'"
            )

    def _build_legacy_grouped_response(
        self, sessions_db
    ) -> AllSessionsGroupedResponse_old:
        users_dict: dict[str, dict[str, list[SessionInfo]]] = {}

        for session in sessions_db:
            user_name = session.data_user.name
            users_dict.setdefault(
                user_name, {"app_sessions": [], "web_sessions": []}
            )

            session_info = SessionInfo.from_orm(session)
            key = "web_sessions" if session.is_web else "app_sessions"
            users_dict[user_name][key].append(session_info)

        users = [
            UserGroupedSessions(
                user_name=user_name,
                app_sessions=data["app_sessions"],
                web_sessions=data["web_sessions"],
            )
            for user_name, data in users_dict.items()
        ]

        return AllSessionsGroupedResponse_old(users=users)

    def _build_session_type_grouped_sessions(
        self, groups_map: dict[str, list[SessionInfo]]
    ) -> SessionTypeGroupedSessions:
        groups = [
            GroupSessionAggregate(group_name=group_name, sessions=sessions)
            for group_name, sessions in groups_map.items()
        ]
        return SessionTypeGroupedSessions(groups=groups)

    def _build_user_grouped_response(self, sessions_db) -> AllSessionsGroupedResponse:
        users_dict: dict[str, dict[str, dict[str, list[SessionInfo]]]] = {}

        for session in sessions_db:
            user_name = session.data_user.name
            group_name = session.group.group_name if session.group else "NO_GROUP"
            session_type_key = "web_session" if session.is_web else "app_session"

            users_dict.setdefault(
                user_name,
                {"app_session": {}, "web_session": {}},
            )

            users_dict[user_name][session_type_key].setdefault(group_name, [])
            users_dict[user_name][session_type_key][group_name].append(
                SessionInfo.from_orm(session)
            )

        users = [
            UserSessionsByTypeAndGroup(
                user_name=user_name,
                app_session=self._build_session_type_grouped_sessions(
                    data["app_session"]
                ),
                web_session=self._build_session_type_grouped_sessions(
                    data["web_session"]
                ),
            )
            for user_name, data in users_dict.items()
        ]

        return AllSessionsGroupedResponse(users=users)
