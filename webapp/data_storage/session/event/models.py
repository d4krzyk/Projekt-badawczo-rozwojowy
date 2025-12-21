from sqlalchemy import Column, Integer, DateTime, ForeignKey
from sqlalchemy.orm import relationship

from database.engine import Base


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
