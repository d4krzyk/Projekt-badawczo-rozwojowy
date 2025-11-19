from datetime import datetime
from sqlalchemy.orm import Session

from .models import BookLink


class LinkRepository:
    def __init__(self, db: Session):
        self.db = db

    def create_book_link(self, link: str, click_time: datetime, room_id: int, user_session_id: int) -> BookLink:
        book_link = BookLink(
            link=link,
            click_time=click_time,
            room_id=room_id,
            user_session_id=user_session_id,
        )
        self.db.add(book_link)
        return book_link
