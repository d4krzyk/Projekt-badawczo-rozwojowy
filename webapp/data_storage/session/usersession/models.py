from sqlalchemy import Column, Integer, String, DateTime, ForeignKey, Boolean
from sqlalchemy.orm import relationship

from database.engine import Base


class UserSession(Base):
    __tablename__ = "user_sessions"
    id = Column(Integer, primary_key=True)
    data_user_id = Column(
        Integer, ForeignKey("data_users.id", ondelete="CASCADE", name="fk_user_sessions_data_user_id"), nullable=False
    )
    group_id = Column(Integer, ForeignKey("groups.id"), nullable=True)
    start_time = Column(DateTime, nullable=False)
    end_time = Column(DateTime, nullable=True)
    is_web = Column(Boolean, default=False, nullable=False)
    surrendered: bool = Column(Boolean, default=False, nullable=False)

    data_user = relationship("DataUser", back_populates="user_sessions")
    group = relationship("Group", back_populates="sessions")
    rooms = relationship("Room", back_populates="user_session", cascade="all, delete-orphan")
    book_session_events = relationship("BookSessionEvent", back_populates="user_session", cascade="all, delete-orphan")
    book_links = relationship("BookLink", back_populates="user_session", cascade="all, delete-orphan")

    @property
    def group_name(self) -> str | None:
        return self.group.group_name if self.group else None