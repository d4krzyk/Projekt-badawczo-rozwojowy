from sqlalchemy import Column, Integer, String, DateTime, ForeignKey
from sqlalchemy.orm import relationship

from database.engine import Base


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
