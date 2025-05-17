import os
from sqlalchemy import create_engine, text
from sqlalchemy.orm import sessionmaker, declarative_base

def get_env_var(key: str) -> str:
    value = os.getenv(key)
    if value is None:
        raise RuntimeError(f"Brak wymaganej zmiennej środowiskowej: {key}")
    return value

DATABASE_URL = (
    # f"postgresql://essa:"      # Uwaga: bez "+asyncpg"
    f"postgresql://{get_env_var('ADMIN_USER')}:"      # Uwaga: bez "+asyncpg"
    f"{get_env_var('ADMIN_PASSWORD')}@"
    f"{get_env_var('POSTGRES_HOST')}:" 
    f"{get_env_var('POSTGRES_PORT')}/"
    f"{get_env_var('POSTGRES_DB')}"
)

# Tworzymy sync engine i sessionmaker
engine = create_engine(DATABASE_URL, future=True, echo=False)
SessionLocal = sessionmaker(
    bind=engine,
    expire_on_commit=False,
    autoflush=False,
    autocommit=False,
)
Base = declarative_base()

# Dependency do FastAPI
def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()

# Context manager do użycia poza FastAPI (np. w middleware)
from contextlib import contextmanager

@contextmanager
def db_session():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()