from sqlalchemy.orm import Session
from datetime import datetime

from .models import Article
from .models import UserSession


class UserSessionRepository:
    def __init__(self, db: Session):
        self.db = db

    def create(self, name: str) -> UserSession:
        session = UserSession(name=name, start_date=datetime.now(), active=True)
        self.db.add(session)
        self.db.flush()
        return session


class ArticleRepository:
    def __init__(self, db: Session):
        self.db = db

    def create(self, name: str, url: str, start, end, navigation_type, entry_source, links, books, user_session_id) -> Article:
        article = Article(name=name, url=url, start=start, end=end, navigation_type=navigation_type, entry_source=entry_source, links=links)
        self.db.add(article)
        self.db.flush()
        return article
