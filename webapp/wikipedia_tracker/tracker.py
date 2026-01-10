"""Wikipedia tracker functions."""

# Standard Library
import json
from datetime import datetime
from pathlib import Path
from typing import Optional, List

# 3rd-Party
from sqlalchemy.orm import Session

# Local
from .models import WikipediaUserSession
from .repository import ArticleRepository
from .repository import WikipediaUserSessionRepository

DATA_JSON_FILE = Path('wikipedia_tracker/user_data.jsonl')


def create_new_session(name: str, db: Session):
    """Create a new Wikipedia user session."""
    repo = WikipediaUserSessionRepository(db)
    new_session = repo.create(name)
    db.commit()
    db.refresh(new_session)
    return new_session


def save_data_to_json_file(data):
    """Save session data to the JSON file specified in DATA_JSON_FILE const."""
    with open(DATA_JSON_FILE, 'a', encoding='utf-8') as f:
        json.dump(data, f, ensure_ascii=False)
        f.write('\n')


def save_data_to_db(data: dict, user_session_id, db: Session):
    repo = ArticleRepository(db)
    new_article = repo.create(
        wiki_user_session_id=user_session_id,
        name=data['name'],
        url=data['name'],
        start=data['enter_time'],
        end=data['exit_time'],
        navigation_type=data.get('extra_data', {}).get('navigationType', ''),
        entry_source=data.get('extra_data', {}).get('entrySource', ''),
        links=data.get('book_links', []),
        books=data.get('books', [])
    )
    db.commit()
    db.refresh(new_article)
    return new_article


def get_seconds_diff(start_ref: datetime, target_time) -> float:
    if not target_time or not start_ref:
        return 0.0

    dt = target_time
    if isinstance(target_time, str):
        dt = datetime.fromisoformat(target_time.replace('Z', '+00:00'))

    if start_ref.tzinfo and not dt.tzinfo:
        dt = dt.replace(tzinfo=start_ref.tzinfo)
    elif not start_ref.tzinfo and dt.tzinfo:
        dt = dt.replace(tzinfo=None)

    delta = dt - start_ref
    return delta.total_seconds()


def get_tracker_data(db: Session, ids_list: Optional[List[int]] = None):
    query = db.query(WikipediaUserSession)

    if ids_list:
        query = query.filter(WikipediaUserSession.id.in_(ids_list))

    sessions = query.all()

    results = []
    for session in sessions:
        session_start = session.start_date

        session_logs = []

        sorted_articles = sorted(session.articles, key=lambda x: x.start if x.start else datetime.min)

        for article in sorted_articles:

            book_logs = []
            if article.books:
                for book_entry in article.books:
                    b_name = book_entry.get('name', 'Unknown')
                    events = book_entry.get('session_events', [])

                    for event in events:
                        book_logs.append({
                            'bookName': b_name,
                            'openTime': get_seconds_diff(session_start, event.get('open_time')),
                            'closeTime': get_seconds_diff(session_start, event.get('close_time'))
                        })

            link_logs = []
            if article.links:
                for link_entry in article.links:
                    link_logs.append({
                        'linkName': link_entry.get('link', ''),
                        'clickTime': get_seconds_diff(session_start, link_entry.get('click_time'))
                    })

            enter_t = get_seconds_diff(session_start, article.start)
            exit_t = get_seconds_diff(session_start, article.end) if article.end else enter_t

            session_logs.append({
                'roomName': article.url,
                'enterTime': enter_t,
                'exitTime': exit_t,
                'bookLogs': book_logs,
                'linkLogs': link_logs
            })

        results.append({
            'id': session.id,
            'user_name': session.name,
            'session_logs': session_logs
        })

    return results