"""Wikipedia tracker functions."""

# Standard Library
import json
from pathlib import Path

# 3rd-Party
from sqlalchemy.orm import Session

# Local
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
        f.write("\n")


def save_data_to_db(data):
    pass


def get_tracker_data():
    pass
