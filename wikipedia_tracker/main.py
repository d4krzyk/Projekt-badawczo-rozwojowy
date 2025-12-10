from fastapi import FastAPI, Request
import json
from pathlib import Path
import uvicorn

app = FastAPI()

DATA_FILE = Path("user_data.jsonl")
DATA_FILE.parent.mkdir(parents=True, exist_ok=True)


@app.post("/log")
async def log_data(request: Request):
    try:
        raw_data = await request.body()
        data = json.loads(raw_data.decode("utf-8"))

        # TODO: save to the db

        with open(DATA_FILE, "a", encoding="utf-8") as f:
            json.dump(data, f, ensure_ascii=False)
            f.write("\n")

        return {"status": "OK"}

    except Exception as e:
        print(f"Error: {e}")
        return {"status": "Error", "message": str(e)}


@app.get("/")
def home():
    return {"message": "Wikipedia Tracker."}


if __name__ == "__main__":
    uvicorn.run("main:app", host="127.0.0.1", port=5000, reload=True)
