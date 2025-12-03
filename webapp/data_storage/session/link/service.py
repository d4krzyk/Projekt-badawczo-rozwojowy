from datetime import datetime, timedelta
from sqlalchemy.orm import Session

from .schemas import LinkLog
from .repository import LinkRepository


class LinkService:
    def __init__(self, repository: LinkRepository):
        self.repository = repository

    def create_link_log(
        self, link_log: LinkLog, room_id: int, user_session_id: int, session_start_time: datetime
    ):
        """Creates a BookLink."""
        self.repository.create_book_link(
            link=link_log.linkName,
            click_time=session_start_time + timedelta(seconds=link_log.clickTime),
            room_id=room_id,
            user_session_id=user_session_id,
        )
