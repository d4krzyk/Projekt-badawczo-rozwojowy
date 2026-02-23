from fastapi import APIRouter, Depends, HTTPException, status, Header
from sqlalchemy.orm import Session

from database.engine import get_db
from .usersession.schemas import FullSessionRequest, AllSessionsGroupedResponse, AllSessionsGroupedResponse_old
from .user.schemas import UserSessionsResponse, GroupSessionsResponse
from .service import SessionService

session_router = APIRouter(prefix="/session", tags=["Session"])


# ==================== POST ====================


@session_router.post("/", status_code=status.HTTP_201_CREATED)
def create_session_endpoint(
    request_body: FullSessionRequest,
    x_web: bool = Header(False, alias="X-Web"),
    db: Session = Depends(get_db),
):
    try:
        service = SessionService(db)
        service.create_full_session(request_body, is_web=x_web)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_409_CONFLICT, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


# ==================== GET ====================


@session_router.get("/all", response_model=AllSessionsGroupedResponse)
def get_all_sessions_endpoint(db: Session = Depends(get_db)):
    try:
        service = SessionService(db)
        return service.get_all_sessions()
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )

@session_router.get("/old", response_model=AllSessionsGroupedResponse_old)
def get_all_old_sessions_endpoint(db: Session = Depends(get_db)):
    try:
        service = SessionService(db)
        return service.get_all_sessions_old()
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


@session_router.get("/type/2d", response_model=AllSessionsGroupedResponse)
def get_2d_sessions_endpoint(db: Session = Depends(get_db)):
    try:
        service = SessionService(db)
        return service.get_sessions_by_type(is_web=True)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


@session_router.get("/type/3d", response_model=AllSessionsGroupedResponse)
def get_3d_sessions_endpoint(db: Session = Depends(get_db)):
    try:
        service = SessionService(db)
        return service.get_sessions_by_type(is_web=False)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


@session_router.get("/check-user/{user_name}", response_model=bool)
def check_user_exists_endpoint(
    user_name: str,
    x_web: bool = Header(False, alias="X-Web"),
    db: Session = Depends(get_db),
):
    try:
        service = SessionService(db)
        return service.check_user_exists(user_name, is_web=x_web)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )

@session_router.get("/check-group/{group_name}", response_model=bool)
def check_group_exists_endpoint(
    group_name: str,
    x_web: bool = Header(False, alias="X-Web"),
    db: Session = Depends(get_db)
):
    try:
        service = SessionService(db)
        return service.check_group_exists(group_name, is_web=x_web)
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


@session_router.get("/user/{user_name}", response_model=UserSessionsResponse)
def get_user_sessions_endpoint(user_name: str, db: Session = Depends(get_db)):
    try:
        service = SessionService(db)
        return service.get_user_sessions(user_name)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )

@session_router.get("/group/{group_name}", response_model=GroupSessionsResponse)
def get_group_sessions_endpoint(group_name: str, db: Session = Depends(get_db)):
    try:
        service = SessionService(db)
        return service.get_group_sessions(group_name)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


# ==================== DELETE ====================


@session_router.delete("/clear-all-data", status_code=status.HTTP_204_NO_CONTENT)
def clear_all_data_endpoint(db: Session = Depends(get_db)):
    try:
        service = SessionService(db)
        service.clear_all_data()
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


@session_router.delete("/user/{user_name}", status_code=status.HTTP_204_NO_CONTENT)
def delete_all_user_sessions_endpoint(user_name: str, db: Session = Depends(get_db)):
    try:
        service = SessionService(db)
        service.delete_all_user_sessions(user_name)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )


@session_router.delete("/{session_id}", status_code=status.HTTP_204_NO_CONTENT)
def delete_session_endpoint(session_id: int, db: Session = Depends(get_db)):
    try:
        service = SessionService(db)
        service.delete_session_by_id(session_id)
    except ValueError as e:
        raise HTTPException(status_code=status.HTTP_404_NOT_FOUND, detail=str(e))
    except Exception as e:
        raise HTTPException(
            status_code=status.HTTP_500_INTERNAL_SERVER_ERROR, detail=str(e)
        )
