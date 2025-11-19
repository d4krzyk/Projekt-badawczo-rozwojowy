from pydantic import BaseModel
from datetime import datetime


class BookLinkInfo(BaseModel):
    link: str
    click_time: datetime | None

    class Config:
        from_attributes = True


class LinkLog(BaseModel):
    linkName: str
    clickTime: float
