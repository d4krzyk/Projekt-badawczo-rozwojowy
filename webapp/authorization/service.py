from sqlalchemy.orm import Session
from sqlalchemy.exc import IntegrityError
from passlib.hash import bcrypt
from typing import Optional
from . import repository
from .jwt_utils import create_token, validate_token

def register_user(
    username: Optional[str],
    email: Optional[str],
    password: str,
    db: Session
) -> str:
    if not username and not email:
        raise ValueError("Podaj co najmniej username lub email")
    try:
        hashed_password = bcrypt.hash(password)
        user = repository.create_user(db, username, email, hashed_password)
    except IntegrityError:
        db.rollback()
        raise ValueError("Użytkownik już istnieje")

    repository.delete_api_keys_for_user(db, str(user.id))
    token = create_token(user_id=str(user.id))
    repository.create_api_key(db, str(user.id), token)
    return token

def login_user(
    identifier: str,
    password: str,
    db: Session
) -> str:
    user = repository.get_user_by_username_or_email(db, identifier)
    if not user or not bcrypt.verify(password, user.hashed_password):
        raise ValueError("Nieprawidłowe dane logowania")

    repository.delete_api_keys_for_user(db, str(user.id))
    token = create_token(user_id=str(user.id))
    repository.create_api_key(db, str(user.id), token)
    return token

def logout_user(
    user_id: str,
    db: Session
) -> bool:
    return repository.deactivate_api_key(db, user_id)

def validate_token_against_db(
    token: str,
    db: Session
) -> Optional[str]:
    key = repository.get_apikey_by_token(db, token)
    if key:
        return key.user_id
    return None
