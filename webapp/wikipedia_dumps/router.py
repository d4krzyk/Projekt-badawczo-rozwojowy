# Standard Library
import time

# 3rd-Party
from fastapi import APIRouter
from fastapi import Query
from fastapi import Request

# Local
from .category import get_main_category_by_name

router = APIRouter(prefix='/dumps', tags=['Dumps'])


@router.get('/category', summary='Pobranie głównej kategorii')
def get_top_category(
    request: Request,
    category_name: str = Query(..., description='Nazwa kategorii')
):
    time_start = time.time()

    # Wstawiasz tutaj normalnie pobraną kategorię z wiki. np get_main_category_by_name('Minecraft')
    result = get_main_category_by_name(category_name, request)
    time_end = time.time()
    return {'category': category_name, 'top_category': result, 'time': time_end - time_start}
