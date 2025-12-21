from datetime import datetime
from sqlalchemy.orm import Session, joinedload
from sqlalchemy import select

from .models import UserSession
from ..room.models import Room
from ..book.models import Book
from ..event.models import BookSessionEvent


class UserSessionRepository:
    def __init__(self, db: Session):
        self.db = db

    def get_all_for_user(self, user_id: int) -> list[UserSession]:
        """Fetches all sessions for a user with eagerly loaded relationships."""
        return (
            self.db.execute(
                select(UserSession)
                .where(UserSession.data_user_id == user_id)
                .options(
                    joinedload(UserSession.rooms).joinedload(Room.book_links),
                    joinedload(UserSession.rooms)
                    .joinedload(Room.books)
                    .joinedload(Book.session_events),
                )
                .order_by(UserSession.start_time)
            )
            .scalars()
            .unique()
            .all()
        )

    def create(self, user_id: int, start_time: datetime, end_time: datetime) -> UserSession:
        user_session = UserSession(
            data_user_id=user_id,
            start_time=start_time,
            end_time=end_time,
        )
        self.db.add(user_session)
        self.db.flush()
        return user_session
