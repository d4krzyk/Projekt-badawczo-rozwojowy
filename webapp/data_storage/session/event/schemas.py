from pydantic import BaseModel


class BookSessionEventInfoForBook(BaseModel):
    open_time: float
    close_time: float | None

    class Config:
        from_attributes = True
