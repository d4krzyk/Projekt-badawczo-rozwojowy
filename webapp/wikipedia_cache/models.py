from datetime import datetime
from typing import List, Optional, Any
from sqlalchemy import String, DateTime, ForeignKey, JSON, func, Boolean
from sqlalchemy.orm import Mapped, mapped_column, relationship
from sqlalchemy.sql import expression
from database.engine import Base

class TextureCache(Base):
    __tablename__ = "texture_cache"

    article_name: Mapped[str] = mapped_column(primary_key=True, autoincrement=False)
    category: Mapped[str] = mapped_column(String(255))
    texture_id: Mapped[int] = mapped_column(ForeignKey("textures.id"), unique=True, nullable=False)

    def __repr__(self) -> str:
        return f"<TextureCache(id={self.article_name}, category='{self.category}')>"
    
class Texture(Base):
    __tablename__ = "textures"

    id: Mapped[int] = mapped_column(primary_key=True, autoincrement=True)
    texture_wall: Mapped[str] = mapped_column(String(4096))
    texture_floor: Mapped[str] = mapped_column(String(4096))
    texture_bookcase: Mapped[str] = mapped_column(String(4096))

    def __repr__(self) -> str:
        return f"<Texture(id={self.id})>"
