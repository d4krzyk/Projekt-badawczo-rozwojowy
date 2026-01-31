from .repository import GroupRepository
from .models import Group


class GroupService:
    def __init__(self, repository: GroupRepository):
        self.repository = repository

    def get_or_create_group(self, group_name: str) -> Group:
        """Gets a group by name, creating one if it doesn't exist."""
        group = self.repository.get_by_name(group_name)
        if not group:
            group = self.repository.create(group_name)
        return group

    def get_group_by_name(self, group_name: str) -> Group:
        """Finds a group by name, raising an error if not found."""
        group = self.repository.get_by_name(group_name)
        if not group:
            raise ValueError(f"Group '{group_name}' not found")
        return group
