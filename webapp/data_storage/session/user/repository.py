from sqlalchemy.orm import Session
from sqlalchemy import select, func, case

from .models import DataUser
from ..usersession.models import UserSession


class UserRepository:
    def __init__(self, db: Session):
        self.db = db

    def get_all_with_session_counts(self):
        return self.db.execute(
            select(
                DataUser.id.label("id"),
                DataUser.name.label("name"),
                func.coalesce(
                    func.sum(case((UserSession.is_web.is_(False), 1), else_=0)),
                    0,
                ).label("app_sessions_count"),
                func.coalesce(
                    func.sum(case((UserSession.is_web.is_(True), 1), else_=0)),
                    0,
                ).label("web_sessions_count"),
                func.min(UserSession.end_time).label("first_session_date"),
                func.max(UserSession.end_time).label("last_session_date"),
            )
            .outerjoin(UserSession, UserSession.data_user_id == DataUser.id)
            .group_by(DataUser.id, DataUser.name)
            .order_by(DataUser.name)
        ).all()

    def get_by_name(self, user_name: str) -> DataUser | None:
        return self.db.execute(select(DataUser).where(DataUser.name == user_name)).scalars().first()

    def create(self, user_name: str) -> DataUser:
        user = DataUser(name=user_name)
        self.db.add(user)
        self.db.flush()
        return user
