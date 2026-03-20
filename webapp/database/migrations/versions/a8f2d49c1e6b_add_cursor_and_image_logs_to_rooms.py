"""add cursor and image logs to rooms

Revision ID: a8f2d49c1e6b
Revises: d2b8f0a6c1d3
Create Date: 2026-03-15 10:00:00.000000
"""

from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = "a8f2d49c1e6b"
down_revision: Union[str, None] = "d2b8f0a6c1d3"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.add_column(
        "rooms",
        sa.Column("cursor_log", sa.JSON(), nullable=False, server_default=sa.text("'[]'::json")),
    )
    op.add_column(
        "rooms",
        sa.Column("image_logs", sa.JSON(), nullable=False, server_default=sa.text("'[]'::json")),
    )


def downgrade() -> None:
    op.drop_column("rooms", "image_logs")
    op.drop_column("rooms", "cursor_log")
