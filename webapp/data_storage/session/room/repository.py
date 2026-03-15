from datetime import datetime
from sqlalchemy.orm import Session

from .models import Room


class RoomRepository:
    def __init__(self, db: Session):
        self.db = db

    def create(
        self,
        name: str,
        enter_time: datetime,
        exit_time: datetime,
        user_session_id: int,
        cursor_log: list[float],
        image_logs: list[dict],
    ) -> Room:
        room = Room(
            name=name,
            enter_time=enter_time,
            exit_time=exit_time,
            user_session_id=user_session_id,
            cursor_log=cursor_log,
            image_logs=image_logs,
        )
        self.db.add(room)
        self.db.flush()
        return room
