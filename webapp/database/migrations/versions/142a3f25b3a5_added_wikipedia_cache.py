"""Added wikipedia_cache

Revision ID: 142a3f25b3a5
Revises: 8c47a2afe3ae
Create Date: 2026-02-01 12:00:00.000000
"""

from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = "142a3f25b3a5"
down_revision: Union[str, None] = "8c47a2afe3ae"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    op.create_table(
        "textures",
        sa.Column("id", sa.Integer(), autoincrement=True, nullable=False),
        sa.Column("texture_wall", sa.Text(), nullable=False),
        sa.Column("texture_floor", sa.Text(), nullable=False),
        sa.Column("texture_bookcase", sa.Text(), nullable=False),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_table(
        "texture_cache",
        sa.Column("article_name", sa.String(), nullable=False),
        sa.Column("category", sa.String(length=512), nullable=False),
        sa.Column("texture_id", sa.Integer(), nullable=False),
        sa.ForeignKeyConstraint(["texture_id"], ["textures.id"]),
        sa.PrimaryKeyConstraint("article_name"),
        sa.UniqueConstraint("texture_id"),
    )


def downgrade() -> None:
    op.drop_table("texture_cache")
    op.drop_table("textures")
