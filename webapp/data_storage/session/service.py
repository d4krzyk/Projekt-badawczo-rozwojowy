from datetime import datetime, timedelta
from sqlalchemy.orm import Session

from .usersession.schemas import FullSessionRequest, SessionInfo
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
        # Repositories
        self.user_repo = UserRepository(db)
        self.usersession_repo = UserSessionRepository(db)
        self.room_repo = RoomRepository(db)
        self.book_repo = BookRepository(db)
        self.event_repo = EventRepository(db)
        self.link_repo = LinkRepository(db)
        
        # Services
        self.user_service = UserService(self.user_repo)
        self.link_service = LinkService(self.link_repo)
        self.book_service = BookService(self.book_repo, self.event_repo)
        self.room_service = RoomService(self.room_repo, self.book_service, self.link_service)

    def create_full_session(self, request: FullSessionRequest):
        """Creates a full user session from request data."""
        if not request.session_logs:
            return

        data_user = self.user_service.get_or_create_user(request.user_name)

        session_start_time = datetime.utcnow()
        session_end_time = session_start_time + timedelta(
            seconds=request.session_logs[-1].exitTime
        )
        
        user_session = self.usersession_repo.create(
            user_id=data_user.id,
            start_time=session_start_time,
            end_time=session_end_time,
        )

        for room_log in request.session_logs:
            self.room_service.create_room_and_logs(room_log, user_session.id, session_start_time)

        self.db.commit()

    def get_user_sessions(self, user_name: str):
        data_user = self.user_service.get_user_by_name(user_name)
        user_sessions_db = self.usersession_repo.get_all_for_user(data_user.id)
        sessions = [SessionInfo.from_orm(s) for s in user_sessions_db]
        return UserSessionsResponse(user_name=user_name, sessions=sessions)

    def clear_all_data(self):
        base_repository.clear_all_data(self.db)
        self.db.commit()