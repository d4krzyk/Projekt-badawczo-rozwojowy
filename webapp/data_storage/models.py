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
    
    rooms = relationship("Room", backref="data_user", cascade="all, delete-orphan")


class Book(Base):
    __tablename__ = "books"
    id = Column(Integer, primary_key=True)
    room_id = Column(
        Integer, ForeignKey("rooms.id", ondelete="CASCADE"), nullable=False
    )
    name = Column(String, nullable=False)
    open_time = Column(DateTime, nullable=False)
    close_time = Column(DateTime, nullable=False)

    room = relationship("Room", back_populates="books")
    links = relationship(
        "BookLink", back_populates="book", cascade="all, delete-orphan"
    )


class BookLink(Base):
    __tablename__ = "book_links"
    id = Column(Integer, primary_key=True)
    book_id = Column(
        Integer, ForeignKey("books.id", ondelete="CASCADE"), nullable=False
    )
    link = Column(String, unique=True, nullable=False)
    click_time = Column(DateTime, nullable=True)
    
    book = relationship("Book", back_populates="links")


class Room(Base):
    __tablename__ = "rooms"
    id = Column(Integer, primary_key=True)
    user_id = Column(
        Integer, ForeignKey("data_users.id", ondelete="CASCADE"), nullable=False
    )
    name = Column(String, nullable=False)
    enter_time = Column(DateTime, nullable=False)
    exit_time = Column(DateTime, nullable=False)
    previous_room_id = Column(Integer, ForeignKey("rooms.id"), nullable=True)
    next_room_id = Column(Integer, ForeignKey("rooms.id"), nullable=True)
    link_id = Column(Integer, nullable=True)

    previous_room = relationship(
        "Room",
        remote_side=[id],
        primaryjoin=previous_room_id == id,
        uselist=False,
        post_update=True,
    )

    next_room = relationship(
        "Room",
        remote_side=[id],
        primaryjoin=next_room_id == id,
        uselist=False,
        post_update=True,
    )

    # 1 -> N: room ma wiele books
    books = relationship("Book", back_populates="room", cascade="all, delete-orphan")
    link = relationship("BookLink", back_populates="room", cascade="all, delete-orphan")
