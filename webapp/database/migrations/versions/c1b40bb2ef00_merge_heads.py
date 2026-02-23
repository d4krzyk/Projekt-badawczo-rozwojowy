"""merge heads

Revision ID: c1b40bb2ef00
Revises: 4f693eba4505, b722dd3b4e7f
Create Date: 2026-01-31 10:35:03.175762

"""
from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = 'c1b40bb2ef00'
down_revision: Union[str, None] = ('4f693eba4505', 'b722dd3b4e7f')
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    """Upgrade schema."""
    pass


def downgrade() -> None:
    """Downgrade schema."""
    pass
