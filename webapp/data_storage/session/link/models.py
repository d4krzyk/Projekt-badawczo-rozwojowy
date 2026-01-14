from sqlalchemy import Column, Integer, String, DateTime, ForeignKey
from sqlalchemy.orm import relationship

from database.engine import Base


class BookLink(Base):
    __tablename__ = "book_links"
    id = Column(Integer, primary_key=True)
    link = Column(String, nullable=False)
    click_time = Column(DateTime, nullable=True)

    book_id = Column(
        Integer, ForeignKey("books.id", ondelete="CASCADE", name="fk_book_links_book_id"), nullable=True
    )
    user_session_id = Column(
        Integer, ForeignKey("user_sessions.id", ondelete="CASCADE", name="fk_book_links_user_session_id"), nullable=False
    )
    room_id = Column(
        Integer, ForeignKey("rooms.id", ondelete="CASCADE", name="fk_book_links_room_id"), nullable=False
    )
    book = relationship("Book", back_populates="links")
    user_session = relationship("UserSession", back_populates="book_links")
    room = relationship("Room", back_populates="book_links")
