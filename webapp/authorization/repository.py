# Standard Library
from typing import Optional

# 3rd-Party
from sqlalchemy.orm import Session

# Local
from .models import APIKey
from .models import User


def get_user_by_username_or_email(db: Session, identifier: str) -> Optional[User]:
    return db.query(User).filter(
        (User.username == identifier) | (User.email == identifier)
    ).first()


def create_user(db: Session, username: str, email: str, hashed_password: str) -> User:
    user = User(username=username, email=email, hashed_password=hashed_password)
    db.add(user)
    db.commit()
    db.refresh(user)
    return user


def delete_api_keys_for_user(db: Session, user_id: str) -> None:
    db.query(APIKey).filter_by(user_id=user_id).delete()
    db.commit()


def create_api_key(db: Session, user_id: str, token: str) -> APIKey:
    key = APIKey(user_id=user_id, token=token, is_active=True)
    db.add(key)
    db.commit()
    return key


def get_active_apikey(db: Session, user_id: str) -> Optional[APIKey]:
    return db.query(APIKey).filter_by(user_id=user_id, is_active=True).first()


def get_apikey_by_token(db: Session, token: str) -> Optional[APIKey]:
    return db.query(APIKey).filter_by(token=token, is_active=True).first()


def deactivate_api_key(db: Session, user_id: str) -> bool:
    key = db.query(APIKey).filter_by(user_id=user_id, is_active=True).first()
    if not key:
        return False
    key.is_active = False
    db.commit()
    return True
