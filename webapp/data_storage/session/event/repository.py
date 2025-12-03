from datetime import datetime
from sqlalchemy.orm import Session

from .models import BookSessionEvent


class EventRepository:
    def __init__(self, db: Session):
        self.db = db

    def create_book_session_event(self, book_id: int, user_session_id: int, open_time: datetime, close_time: datetime) -> BookSessionEvent:
        event = BookSessionEvent(
            book_id=book_id,
            user_session_id=user_session_id,
            open_time=open_time,
            close_time=close_time,
        )
        self.db.add(event)
        return event
