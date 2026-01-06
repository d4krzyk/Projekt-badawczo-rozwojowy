from pydantic import BaseModel
from datetime import datetime


class BookSessionEventInfoForBook(BaseModel):
    open_time: datetime
    close_time: datetime | None

    class Config:
        from_attributes = True
