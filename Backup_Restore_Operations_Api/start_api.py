from main import app

if __name__ == "__main__":
    import uvicorn
    import webbrowser

    webbrowser.open("http://127.0.0.1:8000/docs")
    uvicorn.run("main:app", host="127.0.0.1", port=8000)
