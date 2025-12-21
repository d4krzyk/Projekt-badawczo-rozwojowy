from sqlalchemy import Column, Integer, String, DateTime, ForeignKey
from sqlalchemy.orm import relationship

from database.engine import Base


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
    book_links = relationship("BookLink", back_populates="room", cascade="all, delete-orphan")
