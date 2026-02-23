from sqlalchemy.orm import Session
from sqlalchemy import select

from .models import Group


class GroupRepository:
    def __init__(self, db: Session):
        self.db = db

    def get_by_name(self, group_name: str) -> Group | None:
        return self.db.execute(
            select(Group).where(Group.group_name == group_name)
        ).scalars().first()

    def create(self, group_name: str) -> Group:
        group = Group(group_name=group_name)
        self.db.add(group)
        self.db.flush()
        return group

