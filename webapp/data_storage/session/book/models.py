from sqlalchemy import Column, Integer, String, ForeignKey
from sqlalchemy.orm import relationship

from database.engine import Base


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
