from .repository import UserRepository
from .models import DataUser
from ..group.models import Group


class UserService:
    def __init__(self, repository: UserRepository):
        self.repository = repository

    def get_all_users(self):
        users = self.repository.get_all_with_session_counts()
        return [
            {
                "id": user.id,
                "name": user.name,
                "app_sessions_count": user.app_sessions_count,
                "web_sessions_count": user.web_sessions_count,
                "first_session_date": user.first_session_date,
                "last_session_date": user.last_session_date,
            }
            for user in users
        ]

    def get_or_create_user(self, user_name: str) -> DataUser:
        """Gets a user by name, creating one if it doesn't exist."""
        user = self.repository.get_by_name(user_name)
        if not user:
            user = self.repository.create(user_name)
        return user

    def get_user_by_name(self, user_name: str) -> DataUser:
        """Finds a user by name, raising an error if not found."""
        user = self.repository.get_by_name(user_name)
        if not user:
            raise ValueError(f"User '{user_name}' not found")
        return user
