from sqlalchemy import Column, Integer, String, Boolean
from database.engine import Base

class User(Base):
    __tablename__ = "users"
    id: int = Column(Integer, primary_key=True)
    username: str = Column(String, unique=True, nullable=True)
    email: str = Column(String, unique=True, nullable=True)
    hashed_password: str = Column(String, nullable=False)

class APIKey(Base):
    __tablename__ = "api_keys"
    id: int = Column(Integer, primary_key=True)
    user_id: str = Column(String, nullable=False)
    token: str = Column(String, nullable=False)
    is_active: bool = Column(Boolean, default=True)
