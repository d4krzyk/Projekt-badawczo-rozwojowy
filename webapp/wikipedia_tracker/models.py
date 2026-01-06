from datetime import datetime
from typing import List, Optional, Any
from sqlalchemy import String, DateTime, ForeignKey, JSON, func, Boolean
from sqlalchemy.orm import Mapped, mapped_column, relationship
from sqlalchemy.sql import expression
from webapp.database.engine import Base

class UserSession(Base):
    __tablename__ = "user_sessions"

    id: Mapped[int] = mapped_column(primary_key=True, autoincrement=True)
    name: Mapped[str] = mapped_column(String(255))
    start_date: Mapped[datetime] = mapped_column(DateTime, default=func.now())
    end_date: Mapped[Optional[datetime]] = mapped_column(DateTime, nullable=True)
    active: Mapped[bool] = mapped_column(Boolean, default=False, server_default=expression.false())

    articles: Mapped[List["Article"]] = relationship(back_populates="session", cascade="all, delete-orphan")

    def __repr__(self) -> str:
        return f"<UserSession(id={self.id}, name='{self.name}')>"


class Article(Base):
    __tablename__ = "articles"

    id: Mapped[int] = mapped_column(primary_key=True, autoincrement=True)
    name: Mapped[str] = mapped_column(String(255))
    url: Mapped[str] = mapped_column(String(2048))

    start: Mapped[Optional[datetime]] = mapped_column(DateTime, nullable=True)
    end: Mapped[Optional[datetime]] = mapped_column(DateTime, nullable=True)

    navigation_type: Mapped[Optional[str]] = mapped_column(String(100), nullable=True)
    entry_source: Mapped[Optional[str]] = mapped_column(String(100), nullable=True)

    links: Mapped[Optional[Any]] = mapped_column(JSON, nullable=True)
    books: Mapped[Optional[Any]] = mapped_column(JSON, nullable=True)

    created_at: Mapped[datetime] = mapped_column(DateTime, server_default=func.now())

    user_session_id: Mapped[int] = mapped_column(ForeignKey("user_sessions.id"))

    session: Mapped["UserSession"] = relationship(back_populates="articles")

    def __repr__(self) -> str:
        return f"<Article(id={self.id}, name='{self.name}')>"