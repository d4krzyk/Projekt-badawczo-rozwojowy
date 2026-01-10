'''Wikipedia Tracker router file.'''

# 3rd-Party
from fastapi import APIRouter, Request, Depends, Body
from sqlalchemy.orm import Session

# Project
from database.engine import get_db

# Local
from .tracker import create_new_session
from .tracker import get_tracker_data
from .tracker import save_data_to_db
from .tracker import save_data_to_json_file

router = APIRouter(prefix='/tracker', tags=['Wikipedia Tracker'])


@router.post('/create-session')
def create_session(username: str = Body(..., media_type="text/plain"), db: Session = Depends(get_db)):
    create_new_session(username, db)
    return {'status': 'OK'}


@router.post('/log')
async def log_data(request: Request):
    try:
        data = await request.json()
        save_data_to_json_file(data)
        return {'status': 'OK'}

    except Exception as e:
        print(f'Error: {e}')
        return {'status': 'Error', 'message': str(e)}
    

@router.get('/get_data')
def get_data():
    data = get_tracker_data()
    return data
            
