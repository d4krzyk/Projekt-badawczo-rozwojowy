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

    def get_all(self) -> list[UserSession]:
        """Fetches all sessions with eagerly loaded relationships."""
        return (
            self.db.execute(
                select(UserSession)
                .options(
                    joinedload(UserSession.data_user),
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

    def create(
        self,
        user_id: int,
        start_time: datetime,
        end_time: datetime,
        is_web: bool = False,
    ) -> UserSession:
        user_session = UserSession(
            data_user_id=user_id,
            start_time=start_time,
            end_time=end_time,
            is_web=is_web,
        )
        self.db.add(user_session)
        self.db.flush()
        return user_session

    def exists_for_user_and_type(self, user_id: int, is_web: bool) -> bool:
        """Checks if a session exists for the given user and session type."""
        result = (
            self.db.execute(
                select(UserSession)
                .where(UserSession.data_user_id == user_id)
                .where(UserSession.is_web == is_web)
                .limit(1)
            )
            .scalars()
            .first()
        )
        return result is not None

    def get_by_id(self, session_id: int) -> UserSession | None:
        return self.db.get(UserSession, session_id)

    def delete_by_id(self, session_id: int):
        session = self.get_by_id(session_id)
        if session:
            self.db.delete(session)

    def delete_all_for_user(self, user_id: int):
        self.db.query(UserSession).filter(UserSession.data_user_id == user_id).delete()

    def get_all_by_type(self, is_web: bool) -> list[UserSession]:
        return (
            self.db.execute(
                select(UserSession)
                .where(UserSession.is_web == is_web)
                .options(
                    joinedload(UserSession.data_user),
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
