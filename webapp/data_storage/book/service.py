from datetime import datetime
from sqlalchemy.orm import Session
from sqlalchemy import select, desc

from ..models import Book, BookSessionEvent, Room, UserSession, DataUser
from ..schemas import BookOpenRequest, BookCloseRequest



def open_book(db: Session, request: BookOpenRequest):
    # Find active session for the user
    user_session = db.execute(
        select(UserSession).join(DataUser).where(
            DataUser.name == request.user_name, UserSession.end_time == None
        )
    ).scalars().first()
    if not user_session:
        raise ValueError(f"No active session found for user '{request.user_name}'")

    # Find the room within the session
    room_name = request.wiki_url
    room = db.execute(
        select(Room).where(Room.name == room_name, Room.user_session_id == user_session.id)
    ).scalars().first()
    if not room:
        raise ValueError(f"Room '{room_name}' not found in active session")

    book_name = request.book_url
    book = db.execute(
        select(Book).where(Book.name == book_name, Book.room_id == room.id)
    ).scalars().first()
    if not book:
        book = Book(name=book_name, room_id=room.id)
        db.add(book)
        db.flush()

    # Create BookSessionEvent
    book_session_event = BookSessionEvent(
        book_id=book.id,
        user_session_id=user_session.id,
        open_time=datetime.utcnow(),
        close_time=None,
    )
    db.add(book_session_event)
    db.commit()
    return


def close_book(db: Session, request: BookCloseRequest):
    # Find active session for the user
    user_session = db.execute(
        select(UserSession).join(DataUser).where(
            DataUser.name == request.user_name, UserSession.end_time == None
        )
    ).scalars().first()
    if not user_session:
        raise ValueError(f"No active session found for user '{request.user_name}'")

    # Find the book
    book_name = request.book_url
    book = db.execute(select(Book).where(Book.name == book_name)).scalars().first()
    if not book:
        raise ValueError(f"Book '{book_name}' not found")

    # Find the latest open BookSessionEvent for this book and session
    book_session_event = db.execute(
        select(BookSessionEvent)
        .where(
            BookSessionEvent.user_session_id == user_session.id,
            BookSessionEvent.book_id == book.id,
            BookSessionEvent.close_time == None,
        )
        .order_by(desc(BookSessionEvent.open_time))
    ).scalars().first()

    if not book_session_event:
        raise ValueError(f"No open book event found for book '{book_name}'")

    book_session_event.close_time = datetime.utcnow()
    db.commit()
    return
