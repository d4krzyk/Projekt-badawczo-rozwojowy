from datetime import datetime
from sqlalchemy.orm import Session, joinedload
from sqlalchemy import select

from ..models import Room, UserSession, DataUser, BookSessionEvent
from ..schemas import SessionCreateRequest, SessionCloseRequest



def create_session(db: Session, request: SessionCreateRequest):
    # Find or create DataUser
    data_user = db.execute(
        select(DataUser).where(DataUser.name == request.user_name)
    ).scalars().first()
    if not data_user:
        data_user = DataUser(name=request.user_name)
        db.add(data_user)
        db.flush()

    # Check for existing active session
    existing_session = db.execute(
        select(UserSession).where(
            UserSession.data_user_id == data_user.id, UserSession.end_time == None
        )
    ).first()
    if existing_session:
        raise ValueError(f"Active session already exists for user '{request.user_name}'")

    # Create new UserSession
    user_session = UserSession(
        data_user_id=data_user.id,
        start_time=datetime.utcnow(),
        end_time=None,
    )
    db.add(user_session)
    db.flush()

    # Create the initial Room for the session
    room_name = request.wiki_url
    room = Room(
        name=room_name,
        enter_time=datetime.utcnow(),
        exit_time=datetime.utcnow(),  # Will be updated when room/session changes
        user_session_id=user_session.id,
    )
    db.add(room)
    db.commit()
    return


def close_session(db: Session, request: SessionCloseRequest):
    data_user = db.execute(
        select(DataUser).where(DataUser.name == request.user_name)
    ).scalars().first()
    if not data_user:
        raise ValueError(f"User '{request.user_name}' not found")

    # Find active session
    user_session = db.execute(
        select(UserSession).where(
            UserSession.data_user_id == data_user.id, UserSession.end_time == None
        )
    ).scalars().first()

    if not user_session:
        raise ValueError(f"No active session found for user '{request.user_name}'")

    user_session.end_time = datetime.utcnow()
    db.commit()
    return


def get_user_sessions(db: Session, user_name: str):
    # Find the user
    data_user = db.execute(
        select(DataUser).where(DataUser.name == user_name)
    ).scalars().first()
    if not data_user:
        raise ValueError(f"User '{user_name}' not found")

    # Get all sessions for the user, loading related rooms and book events
    user_sessions = (
        db.execute(
            select(UserSession)
            .where(UserSession.data_user_id == data_user.id)
            .options(
                joinedload(UserSession.rooms),
                joinedload(UserSession.book_session_events).joinedload(
                    BookSessionEvent.book
                ),
            )
            .order_by(UserSession.start_time)
        )
        .scalars().unique().all()
    )

    return {"user_name": user_name, "sessions": user_sessions}
