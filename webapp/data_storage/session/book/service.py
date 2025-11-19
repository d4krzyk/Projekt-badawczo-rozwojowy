from datetime import datetime, timedelta
from sqlalchemy.orm import Session

from .schemas import BookLog
from .repository import BookRepository
from ..event.repository import EventRepository


class BookService:
    def __init__(self, book_repository: BookRepository, event_repository: EventRepository):
        self.book_repository = book_repository
        self.event_repository = event_repository

    def create_book_and_events(
        self, book_log: BookLog, room_id: int, user_session_id: int, session_start_time: datetime
    ):
        """Creates a Book and its associated BookSessionEvent."""
        book = self.book_repository.create(name=book_log.bookName, room_id=room_id)
        
        self.event_repository.create_book_session_event(
            book_id=book.id,
            user_session_id=user_session_id,
            open_time=session_start_time + timedelta(seconds=book_log.openTime),
            close_time=session_start_time + timedelta(seconds=book_log.closeTime),
        )
