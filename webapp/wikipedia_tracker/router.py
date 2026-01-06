# Standard Library
import json
from pathlib import Path

# 3rd-Party
from fastapi import APIRouter, Request

router = APIRouter(prefix="/tracker", tags=["Wikipedia Tracker"])
DATA_FILE = Path("wikipedia_tracker/user_data.jsonl")

@router.post("/log")
async def log_data(request: Request):
    try:
        data = await request.json()

        # TODO: save to the db

        with open(DATA_FILE, "a", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False)
            f.write("\n")

        return {"status": "OK"}

    except Exception as e:
        print(f"Error: {e}")
        return {"status": "Error", "message": str(e)}
