"""Wikipedia Tracker router file."""

# Standard Library
from typing import List, Optional

# 3rd-Party
from fastapi import APIRouter, Request, Depends, Body, HTTPException, Query
from sqlalchemy.orm import Session

# Project
from database.engine import get_db

# Local
from .tracker import create_new_session
from .tracker import get_tracker_data
from .tracker import save_data_to_db
from .tracker import save_data_to_json_file
from .models import WikipediaUserSession
from .repository import WikipediaUserSessionRepository

router = APIRouter(prefix='/tracker', tags=['Wikipedia Tracker'])


@router.post('/create-session')
def create_session(username: str = Body(..., media_type="text/plain"), db: Session = Depends(get_db)):
    create_new_session(username, db)
    return {'status': 'OK'}


@router.post('/log')
async def log_data(request: Request, db: Session = Depends(get_db)):
    try:
        data = await request.json()
        # save_data_to_json_file(data)

        user_session = db.query(WikipediaUserSession).filter(WikipediaUserSession.active == True).first()
        if not user_session:
            return {'status': 'Error', 'message': 'Nie znaleziono aktywnej sesji. Rozpocznij nową sesję.'}
        save_data_to_db(data, user_session.id, db)
        return {'status': 'OK'}

    except Exception as e:
        print(f'Error: {e}')
        return {'status': 'Error', 'message': str(e)}
    

@router.get('/get_data')
def get_data(
        session_ids: Optional[str] = Query(None, description="ID sesji oddzielone przecinkami, np. 1,2,5"),
        db: Session = Depends(get_db)
):
    ids_list = []
    if session_ids:
        try:
            ids_list = [int(id_str.strip()) for id_str in session_ids.split(',') if id_str.strip()]
        except ValueError:
            raise HTTPException(status_code=400, detail='Parametr session_ids musi zawierać tylko liczby oddzielone przecinkami.')
    data = get_tracker_data(db, ids_list)
    return {'status': 'OK', 'data': data}


@router.delete('/sessions/')
def delete_multiple_sessions(
    ids: str = Query(..., description='Lista ID oddzielona przecinkami, np. \'1,2,3\''),
    db: Session = Depends(get_db)
):
    repo = WikipediaUserSessionRepository(db)

    try:
        id_list = [int(id_str.strip()) for id_str in ids.split(',') if id_str.strip()]
    except ValueError:
        raise HTTPException(status_code=400, detail='IDs muszą być liczbami całkowitymi oddzielonymi przecinkami')

    if not id_list:
        raise HTTPException(status_code=400, detail='Nie podano żadnych poprawnych ID')

    deleted_count = repo.delete_batch(id_list)

    return {
        'status': 'success',
        'deleted_count': deleted_count,
        'deleted_ids': id_list
    }
