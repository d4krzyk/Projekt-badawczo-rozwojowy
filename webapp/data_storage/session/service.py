from datetime import datetime, timedelta
from sqlalchemy.orm import Session
from collections import defaultdict

from .usersession.schemas import (
    FullSessionRequest,
    SessionInfo,
    UserGroupedSessions,
    AllSessionsGroupedResponse,
)
from .user.schemas import UserSessionsResponse
from . import base_repository
from .user.repository import UserRepository
from .usersession.repository import UserSessionRepository
from .room.repository import RoomRepository
from .book.repository import BookRepository
from .event.repository import EventRepository
from .link.repository import LinkRepository
from .user.service import UserService
from .room.service import RoomService
from .book.service import BookService
from .link.service import LinkService


class SessionService:
    def __init__(self, db: Session):
        self.db = db
        self._init_repositories()
        self._init_services()

    def _init_repositories(self):
        self.user_repo = UserRepository(self.db)
        self.usersession_repo = UserSessionRepository(self.db)
        self.room_repo = RoomRepository(self.db)
        self.book_repo = BookRepository(self.db)
        self.event_repo = EventRepository(self.db)
        self.link_repo = LinkRepository(self.db)

    def _init_services(self):
        self.user_service = UserService(self.user_repo)
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

        user = self.user_service.get_or_create_user(request.user_name)
        start_time = datetime.utcnow()
        end_time = start_time + timedelta(seconds=request.session_logs[-1].exitTime)

        session = self.usersession_repo.create(
            user_id=user.id,
            start_time=start_time,
            end_time=end_time,
            is_web=is_web,
        )

        for room_log in request.session_logs:
            self.room_service.create_room_and_logs(room_log, session.id, start_time)

        self.db.commit()

    # ==================== READ ====================

    def get_user_sessions(self, user_name: str) -> UserSessionsResponse:
        user = self._get_user_or_raise(user_name)
        sessions_db = self.usersession_repo.get_all_for_user(user.id)
        sessions = [SessionInfo.from_orm(s) for s in sessions_db]
        return UserSessionsResponse(user_name=user_name, sessions=sessions)

    def get_all_sessions(self) -> AllSessionsGroupedResponse:
        sessions_db = self.usersession_repo.get_all()
        return self._build_grouped_response(sessions_db)

    def get_sessions_by_type(self, is_web: bool) -> AllSessionsGroupedResponse:
        sessions_db = self.usersession_repo.get_all_by_type(is_web)
        return self._build_grouped_response(sessions_db)

    def check_user_exists(self, user_name: str, is_web: bool = False) -> bool:
        """Checks if a user exists and has a session of the specified type."""
        user = self.user_repo.get_by_name(user_name)
        if not user:
            return False
        return self.usersession_repo.exists_for_user_and_type(user.id, is_web)

    # ==================== DELETE ====================

    def delete_session(self, user_name: str, session_id: int):
        user = self._get_user_or_raise(user_name)
        session = self._get_session_or_raise(session_id)
        self._validate_session_ownership(session, user, session_id, user_name)

        self.usersession_repo.delete_by_id(session_id)
        self.db.commit()

    def delete_session_by_id(self, session_id: int):
        session = self._get_session_or_raise(session_id)
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

    def _build_grouped_response(self, sessions_db) -> AllSessionsGroupedResponse:
        users_dict = defaultdict(lambda: {"app_sessions": [], "web_sessions": []})

        for session in sessions_db:
            user_name = session.data_user.name
            session_info = SessionInfo.from_orm(session)
            key = "web_sessions" if session.is_web else "app_sessions"
            users_dict[user_name][key].append(session_info)

        users = [
            UserGroupedSessions(
                user_name=name,
                app_sessions=data["app_sessions"],
                web_sessions=data["web_sessions"],
            )
            for name, data in users_dict.items()
        ]

        return AllSessionsGroupedResponse(users=users)
