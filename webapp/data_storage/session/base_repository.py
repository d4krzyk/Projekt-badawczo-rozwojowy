from sqlalchemy.orm import Session

from .user.models import DataUser
from .usersession.models import UserSession
from .room.models import Room
from .book.models import Book
from .event.models import BookSessionEvent
from .link.models import BookLink


def clear_all_data(db: Session):
    # Order of deletion is important due to foreign key constraints
    db.query(BookLink).delete()
    db.query(BookSessionEvent).delete()
    db.query(Book).delete()
    db.query(Room).delete()
    db.query(UserSession).delete()
    db.query(DataUser).delete()
