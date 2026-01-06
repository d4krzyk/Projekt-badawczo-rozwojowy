from datetime import datetime, timedelta
from sqlalchemy.orm import Session

from .schemas import RoomLog
from .repository import RoomRepository
from ..book.service import BookService
from ..link.service import LinkService


class RoomService:
    def __init__(self, repository: RoomRepository, book_service: BookService, link_service: LinkService):
        self.repository = repository
        self.book_service = book_service
        self.link_service = link_service

    def create_room_and_logs(
        self, room_log: RoomLog, user_session_id: int, session_start_time: datetime
    ):
        """Creates a Room and all its associated logs (books, links)."""
        room = self.repository.create(
            name=room_log.roomName,
            enter_time=session_start_time + timedelta(seconds=room_log.enterTime),
            exit_time=session_start_time + timedelta(seconds=room_log.exitTime),
            user_session_id=user_session_id,
        )

        for book_log in room_log.bookLogs:
            self.book_service.create_book_and_events(book_log, room.id, user_session_id, session_start_time)

        for link_log in room_log.linkLogs:
            self.link_service.create_link_log(link_log, room.id, user_session_id, session_start_time)