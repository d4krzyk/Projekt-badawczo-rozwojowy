from sqlalchemy.orm import Session

from .models import Book


class BookRepository:
    def __init__(self, db: Session):
        self.db = db

    def create(self, name: str, room_id: int) -> Book:
        book = Book(name=name, room_id=room_id)
        self.db.add(book)
        self.db.flush()
        return book
