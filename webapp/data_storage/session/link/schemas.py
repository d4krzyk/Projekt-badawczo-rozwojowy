from pydantic import BaseModel, field_serializer
from datetime import datetime

from ..utils import strip_wikipedia_prefix


class BookLinkInfo(BaseModel):
    link: str
    click_time: datetime | None

    @field_serializer("link")
    def serialize_link(self, value: str) -> str:
        return strip_wikipedia_prefix(value)

    class Config:
        from_attributes = True


class LinkLog(BaseModel):
    linkName: str
    clickTime: float
