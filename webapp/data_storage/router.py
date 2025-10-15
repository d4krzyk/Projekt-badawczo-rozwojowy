# 3rd-Party
from fastapi import APIRouter
from fastapi import Depends
from fastapi import Request
from fastapi.responses import JSONResponse
from fastapi.encoders import jsonable_encoder

from sqlalchemy.orm import Session
from .models import Room
# Project
from database.engine import get_db


router = APIRouter(prefix="/data_storage", tags=["Data storage"])


@router.post("/debug")
def list_rooms(db: Session = Depends(get_db)) -> JSONResponse:
    rooms = db.query(Room).all()
    return JSONResponse(content=jsonable_encoder(rooms))