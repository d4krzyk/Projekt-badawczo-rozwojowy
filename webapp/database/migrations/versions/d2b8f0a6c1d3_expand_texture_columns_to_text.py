"""expand texture columns to text

Revision ID: d2b8f0a6c1d3
Revises: 142a3f25b3a5
Create Date: 2026-02-28 12:10:00.000000
"""

from typing import Sequence, Union

from alembic import op
import sqlalchemy as sa


# revision identifiers, used by Alembic.
revision: str = "d2b8f0a6c1d3"
down_revision: Union[str, None] = "142a3f25b3a5"
branch_labels: Union[str, Sequence[str], None] = None
depends_on: Union[str, Sequence[str], None] = None


def upgrade() -> None:
    bind = op.get_bind()
    inspector = sa.inspect(bind)

    if not inspector.has_table("textures"):
        op.create_table(
            "textures",
            sa.Column("id", sa.Integer(), autoincrement=True, nullable=False),
            sa.Column("texture_wall", sa.Text(), nullable=False),
            sa.Column("texture_floor", sa.Text(), nullable=False),
            sa.Column("texture_bookcase", sa.Text(), nullable=False),
            sa.PrimaryKeyConstraint("id"),
        )
    else:
        op.alter_column("textures", "texture_wall", type_=sa.Text(), existing_nullable=False)
        op.alter_column("textures", "texture_floor", type_=sa.Text(), existing_nullable=False)
        op.alter_column("textures", "texture_bookcase", type_=sa.Text(), existing_nullable=False)

    if not inspector.has_table("texture_cache"):
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
    op.alter_column("textures", "texture_wall", type_=sa.String(length=65536), existing_nullable=False)
    op.alter_column("textures", "texture_floor", type_=sa.String(length=65536), existing_nullable=False)
    op.alter_column("textures", "texture_bookcase", type_=sa.String(length=65536), existing_nullable=False)
