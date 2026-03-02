from fastapi import APIRouter, Request, Depends, Body, HTTPException, Query
from sqlalchemy.orm import Session

from database.engine import get_db
from wikipedia_cache.cache import save_texture_to_db, get_cached_texture as get_cached_texture_from_db


router = APIRouter(prefix='/cache', tags=['Wikipedia Cache'])

@router.post('/cache_texture')
async def cache_texture(request: Request, db: Session = Depends(get_db)):
    try:
        data = await request.json()
        save_texture_to_db(data, db)
        return {'status': 'OK'}
    except Exception as e:
        print(f'Error: {e}')
        return {'status': 'Error', 'message': str(e)}

@router.get('/get_cached_texture')
def get_cached_texture_endpoint(article: str = Query(...), category: str = Query(...), db: Session = Depends(get_db)):
    try:
        cache_texture = get_cached_texture_from_db(article, category, db)
        if cache_texture:
            return {'status': 'OK', 'data': cache_texture}
        else:
            return {'status': 'Not Found', 'message': 'Nie znaleziono tekstury dla podanego artykułu lub kategorii.'}
    except HTTPException:
        raise
    except Exception as e:
        print(f'Error: {e}')
        raise HTTPException(status_code=500, detail=str(e))

