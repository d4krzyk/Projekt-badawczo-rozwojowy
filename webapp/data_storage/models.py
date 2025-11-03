# 3rd-Party
from sqlalchemy import Boolean
from sqlalchemy import Column
from sqlalchemy import Integer
from sqlalchemy import String
from sqlalchemy import DateTime
from sqlalchemy import ForeignKey
from sqlalchemy.orm import relationship

# Project
from database.engine import Base


class DataUser(Base):
    __tablename__ = "data_users"
    id = Column(Integer, primary_key=True)
    name = Column(String, unique=True, nullable=False)
    
    user_sessions = relationship("UserSession", back_populates="data_user", cascade="all, delete-orphan")


class UserSession(Base):
    __tablename__ = "user_sessions"
    id = Column(Integer, primary_key=True)
    data_user_id = Column(
        Integer, ForeignKey("data_users.id", ondelete="CASCADE", name="fk_user_sessions_data_user_id"), nullable=False
    )
    start_time = Column(DateTime, nullable=False)
    end_time = Column(DateTime, nullable=True)

    data_user = relationship("DataUser", back_populates="user_sessions")
    rooms = relationship("Room", back_populates="user_session", cascade="all, delete-orphan")
    book_session_events = relationship("BookSessionEvent", back_populates="user_session", cascade="all, delete-orphan")
    book_links = relationship("BookLink", back_populates="user_session", cascade="all, delete-orphan")


class Room(Base):
    __tablename__ = "rooms"
    id = Column(Integer, primary_key=True)
    name = Column(String, nullable=False)
    enter_time = Column(DateTime, nullable=False)
    exit_time = Column(DateTime, nullable=False)

    user_session_id = Column(
        Integer, ForeignKey("user_sessions.id", ondelete="CASCADE", name="fk_rooms_user_session_id"), nullable=False
    )
    user_session = relationship("UserSession", back_populates="rooms")

    books = relationship("Book", back_populates="room", cascade="all, delete-orphan")


class Book(Base):
    __tablename__ = "books"
    id = Column(Integer, primary_key=True)
    room_id = Column(
        Integer, ForeignKey("rooms.id", ondelete="CASCADE", name="fk_books_room_id"), nullable=False
    )
    name = Column(String, nullable=False)

    room = relationship("Room", back_populates="books")
    links = relationship(
        "BookLink", back_populates="book", cascade="all, delete-orphan"
    )
    session_events = relationship(
        "BookSessionEvent", back_populates="book", cascade="all, delete-orphan"
    )


class BookSessionEvent(Base):
    __tablename__ = "book_session_events"
    id = Column(Integer, primary_key=True)
    book_id = Column(
        Integer, ForeignKey("books.id", ondelete="CASCADE", name="fk_book_session_events_book_id"), nullable=False
    )
    user_session_id = Column(
        Integer, ForeignKey("user_sessions.id", ondelete="CASCADE", name="fk_book_session_events_user_session_id"), nullable=False
    )
    open_time = Column(DateTime, nullable=False)
    close_time = Column(DateTime, nullable=True)

    book = relationship("Book", back_populates="session_events")
    user_session = relationship("UserSession", back_populates="book_session_events")


class BookLink(Base):
    __tablename__ = "book_links"
    id = Column(Integer, primary_key=True)
    link = Column(String, unique=True, nullable=False)
    click_time = Column(DateTime, nullable=True)

    book_id = Column(
        Integer, ForeignKey("books.id", ondelete="CASCADE", name="fk_book_links_book_id"), nullable=False
    )
    user_session_id = Column(
        Integer, ForeignKey("user_sessions.id", ondelete="CASCADE", name="fk_book_links_user_session_id"), nullable=False
    )
    book = relationship("Book", back_populates="links")
    user_session = relationship("UserSession", back_populates="book_links")
