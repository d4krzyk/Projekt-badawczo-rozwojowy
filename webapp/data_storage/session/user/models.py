from sqlalchemy import Column, Integer, String, ForeignKey
from sqlalchemy.orm import relationship

from database.engine import Base


class DataUser(Base):
    __tablename__ = "data_users"
    id = Column(Integer, primary_key=True)
    name = Column(String, unique=True, nullable=False)
    group_id = Column(
        Integer,
        ForeignKey("groups.id", ondelete="SET NULL"),
        nullable=True
    )
    group = relationship(
        "Group",
        back_populates="users"
    )
    
    user_sessions = relationship("UserSession", back_populates="data_user", cascade="all, delete-orphan")
