from datetime import datetime, timedelta
from sqlalchemy.orm import Session, joinedload
from sqlalchemy import select

from ..models import (
    DataUser,
    UserSession,
    Room,
    Book,
    BookSessionEvent,
    BookLink,
)
from ..schemas import FullSessionRequest


def create_full_session(db: Session, request: FullSessionRequest):
    # Find or create DataUser
    data_user = (
        db.execute(select(DataUser).where(DataUser.name == request.user_name))
        .scalars()
        .first()
    )
    if not data_user:
        data_user = DataUser(name=request.user_name)
        db.add(data_user)
        db.flush()

    # Create UserSession
    session_start_time = datetime.utcnow()
    session_end_time = session_start_time + timedelta(
        seconds=request.session_logs[-1].exitTime
    )
    user_session = UserSession(
        data_user_id=data_user.id,
        start_time=session_start_time,
        end_time=session_end_time,
    )
    db.add(user_session)
    db.flush()

    for room_log in request.session_logs:
        # Create Room
        room = Room(
            name=room_log.roomName,
            enter_time=session_start_time + timedelta(seconds=room_log.enterTime),
            exit_time=session_start_time + timedelta(seconds=room_log.exitTime),
            user_session_id=user_session.id,
        )
        db.add(room)
        db.flush()

        for book_log in room_log.bookLogs:
            # Create Book
            book = Book(name=book_log.bookName, room_id=room.id)
            db.add(book)
            db.flush()

            # Create BookSessionEvent
            book_session_event = BookSessionEvent(
                book_id=book.id,
                user_session_id=user_session.id,
                open_time=session_start_time + timedelta(seconds=book_log.openTime),
                close_time=session_start_time
                + timedelta(seconds=book_log.closeTime),
            )
            db.add(book_session_event)

        for link_log in room_log.linkLogs:
            # Create BookLink
            book_link = BookLink(
                link=link_log.linkName,
                click_time=session_start_time
                + timedelta(seconds=link_log.clickTime),
                room_id=room.id,
                user_session_id=user_session.id,
            )
            db.add(book_link)

    db.commit()
    return


def get_user_sessions(db: Session, user_name: str):
    # Find the user
    data_user = (
        db.execute(select(DataUser).where(DataUser.name == user_name))
        .scalars()
        .first()
    )
    if not data_user:
        raise ValueError(f"User '{user_name}' not found")

    # Get all sessions for the user, loading related rooms, books, book events, and book links
    user_sessions_db = (
        db.execute(
            select(UserSession)
            .where(UserSession.data_user_id == data_user.id)
            .options(
                joinedload(UserSession.rooms).joinedload(Room.book_links),
                joinedload(UserSession.rooms).joinedload(Room.books).joinedload(Book.session_events).joinedload(BookSessionEvent.book),
            )
            .order_by(UserSession.start_time)
        )
        .scalars()
        .unique()
        .all()
    )

    sessions_response = []
    for session_db in user_sessions_db:
        rooms_response = []
        for room_db in session_db.rooms:
            book_session_events_response = []
            for book_db in room_db.books:
                for event_db in book_db.session_events:
                    book_session_events_response.append({
                        "open_time": event_db.open_time,
                        "close_time": event_db.close_time,
                        "book": {"name": book_db.name}
                    })
            
            book_links_response = []
            for link_db in room_db.book_links:
                book_links_response.append({
                    "link": link_db.link,
                    "click_time": link_db.click_time
                })

            rooms_response.append({
                "name": room_db.name,
                "enter_time": room_db.enter_time,
                "exit_time": room_db.exit_time,
                "book_session_events": book_session_events_response,
                "book_link_events": book_links_response,
            })
        
        sessions_response.append({
            "id": session_db.id,
            "start_time": session_db.start_time,
            "end_time": session_db.end_time,
            "rooms": rooms_response,
        })

    return {"user_name": user_name, "sessions": sessions_response}

def clear_all_data(db: Session):
    # Order of deletion is important due to foreign key constraints
    db.query(BookLink).delete()
    db.query(BookSessionEvent).delete()
    db.query(Book).delete()
    db.query(Room).delete()
    db.query(UserSession).delete()
    db.query(DataUser).delete()
    db.commit()
    return