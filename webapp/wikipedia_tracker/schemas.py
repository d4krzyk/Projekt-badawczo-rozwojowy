from datetime import datetime
from typing import List, Optional, Any, Union
from pydantic import BaseModel, Field


class ArticleBase(BaseModel):
    name: str
    url: str
    start: Optional[datetime] = None
    end: Optional[datetime] = None
    navigation_type: Optional[str] = None
    entry_source: Optional[str] = None

    links: Optional[Union[dict, list, str]] = None
    books: Optional[Union[dict, list, str]] = None


class ArticleCreate(ArticleBase):
    wiki_user_session_id: int


class ArticleResponse(ArticleBase):
    id: int
    created_at: datetime
    wiki_user_session_id: int

    class Config:
        from_attributes = True



class WikipediaUserSessionBase(BaseModel):
    name: str
    start_date: Optional[datetime] = None
    end_date: Optional[datetime] = None
    active: bool = False


class WikipediaUserSessionCreate(WikipediaUserSessionBase):
    pass


class WikipediaUserSessionResponse(WikipediaUserSessionBase):
    id: int
    start_date: datetime

    articles: List[ArticleResponse] = []

    class Config:
        from_attributes = True
