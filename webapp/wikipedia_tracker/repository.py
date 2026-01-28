from sqlalchemy.orm import Session
from datetime import datetime
from typing import List

from .models import Article
from .models import WikipediaUserSession


class WikipediaUserSessionRepository:
    def __init__(self, db: Session):
        self.db = db

    def create(self, name: str) -> WikipediaUserSession:
        # Set previous sessions as inactive
        self.db.query(WikipediaUserSession).filter(WikipediaUserSession.active == True).update({'active': False})

        # Create a new session
        session = WikipediaUserSession(name=name, start_date=datetime.now(), active=True)
        self.db.add(session)
        self.db.flush()
        return session

    def delete_batch(self, session_ids: List[int]) -> int:
        deleted_count = self.db.query(WikipediaUserSession) \
            .filter(WikipediaUserSession.id.in_(session_ids)) \
            .delete(synchronize_session=False)

        self.db.commit()
        return deleted_count


class ArticleRepository:
    def __init__(self, db: Session):
        self.db = db

    def create(self, name: str, url: str, start, end, navigation_type, entry_source, links, books, wiki_user_session_id) -> Article:
        article = Article(name=name, url=url, start=start, end=end, navigation_type=navigation_type, entry_source=entry_source, links=links, books=books, wiki_user_session_id=wiki_user_session_id)
        self.db.add(article)
        self.db.flush()
        return article
