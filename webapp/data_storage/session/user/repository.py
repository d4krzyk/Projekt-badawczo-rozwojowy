from sqlalchemy.orm import Session
from sqlalchemy import select

from .models import DataUser


class UserRepository:
    def __init__(self, db: Session):
        self.db = db

    def get_by_name(self, user_name: str) -> DataUser | None:
        return self.db.execute(select(DataUser).where(DataUser.name == user_name)).scalars().first()

    def create(self, user_name: str, group=None) -> DataUser:
        user = DataUser(name=user_name, group=group)
        self.db.add(user)
        self.db.flush()
        return user
