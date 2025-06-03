# Standard Library
from typing import Optional

# 3rd-Party
from pydantic import BaseModel
from pydantic import EmailStr
from pydantic import Field


class RegisterRequest(BaseModel):
    username: Optional[str] = Field(None, min_length=1, description="Nazwa użytkownika")
    email: Optional[EmailStr] = Field(None, min_length=1, description="Adres email")
    password: str = Field(..., min_length=1, description="Hasło")


class LoginRequest(BaseModel):
    identifier: str = Field(..., min_length=1, description="Nazwa użytkownika lub email")
    password: str = Field(..., min_length=1, description="Hasło")
