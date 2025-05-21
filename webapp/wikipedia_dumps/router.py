import time
from fastapi import APIRouter
from fastapi import Query
from .category import get_main_category_by_name


router = APIRouter(prefix='/dumps', tags=['Dumps'])


@router.get('/category', summary='Pobranie głównej kategorii')
def get_top_category(category_name: str = Query(..., description='Nazwa kategorii')):
    time_start = time.time()
    # Wstawiasz tutaj normalnie pobraną kategorię z wiki. np get_main_category_by_name('Minecraft')
    # TODO: przyspieszyć wczytywanie plików, teraz te jsony się ładują długo.
    result = get_main_category_by_name(category_name)
    time_end = time.time()
    return {'category': category_name, 'top_category': result, 'time': time_end - time_start}

