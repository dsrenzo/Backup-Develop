# main.py
from fastapi import FastAPI
from routers import operaciones
from db.manager import init_db
import uvicorn

app = FastAPI()
app.include_router(operaciones.router)
init_db()

if __name__ == "__main__":
    import uvicorn
    import webbrowser

    webbrowser.open("http://127.0.0.1:8000/docs")
    uvicorn.run("main:app", host="127.0.0.1", port=8000, reload=True)