from sqlalchemy import Column, Integer, String
from sqlalchemy.orm import relationship
from database.engine import Base

class Group(Base):
    __tablename__ = "groups"

    id = Column(Integer, primary_key=True)
    group_name = Column(String, unique=True, nullable=False)

    users = relationship(
        "DataUser",
        back_populates="group",
        cascade="all, delete-orphan"
    )
