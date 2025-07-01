# Standard Library
from datetime import datetime
from datetime import timedelta
from typing import Any
from typing import Dict
from typing import Optional

# 3rd-Party
from jose import JWTError
from jose import jwt

ALGORITHM: str = "HS256"
SECRET: str = "sekretJWT123"


def create_token(user_id: str, expires_minutes: int = 60) -> str:
    payload: Dict[str, Any] = {
        "sub": user_id,
        "exp": datetime.utcnow() + timedelta(minutes=expires_minutes)
    }
    return jwt.encode(payload, SECRET, algorithm=ALGORITHM)


def decode_token(token: str) -> Optional[Dict[str, Any]]:
    try:
        return jwt.decode(token, SECRET, algorithms=[ALGORITHM])
    except JWTError:
        return None


def validate_token(token: str) -> Optional[str]:
    payload = decode_token(token)
    return payload.get("sub") if payload else None
