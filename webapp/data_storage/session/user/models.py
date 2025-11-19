from sqlalchemy import Column, Integer, String
from sqlalchemy.orm import relationship

from database.engine import Base


class DataUser(Base):
    __tablename__ = "data_users"
    id = Column(Integer, primary_key=True)
    name = Column(String, unique=True, nullable=False)
    
    user_sessions = relationship("UserSession", back_populates="data_user", cascade="all, delete-orphan")
