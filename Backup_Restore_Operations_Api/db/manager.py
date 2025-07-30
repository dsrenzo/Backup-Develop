import os
import sqlite3
from fastapi import HTTPException

DB_PATH = "G:/OperacionesPendientes/operaciones.db"

def init_db():
    os.makedirs(os.path.dirname(DB_PATH), exist_ok=True)
    with sqlite3.connect(DB_PATH) as conn:
        conn.execute("""
            CREATE TABLE IF NOT EXISTS operaciones (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                tipo TEXT,
                nombre TEXT,
                origen TEXT,
                destino TEXT,
                entorno TEXT,
                fecha TEXT,
                estado TEXT DEFAULT 'pendiente',
                nvolumenorigen INTEGER,
                nvolumendestino INTEGER
            )
        """)
        conn.execute("""
            CREATE TABLE IF NOT EXISTS config (
                clave TEXT PRIMARY KEY,
                valor TEXT NOT NULL
            )
        """)

def registrar_op(op: dict):
    with sqlite3.connect(DB_PATH) as conn:
        conn.execute("""
            INSERT INTO operaciones (tipo, nombre, origen, destino, entorno, fecha, estado, nvolumenorigen, nvolumendestino)
            VALUES (?, ?, ?, ?, ?, ?, 'pendiente', ?, ?)
        """, (
            op["tipo"], op["nombre"], op.get("origen", ""), op.get("destino", ""),
            op["entorno"], op["fecha"], op.get("nvolumenorigen"), op.get("nvolumendestino")
        ))

def obtener_ops(tipo: str):
    with sqlite3.connect(DB_PATH) as conn:
        cur = conn.cursor()
        cur.execute("SELECT nombre, fecha, estado, entorno FROM operaciones WHERE tipo = ?", (tipo,))
        return [{"nombre": n, "fecha": f, "estado": e, "entorno": ent} for n, f, e, ent in cur.fetchall()]

def existe_backup(nombre: str) -> bool:
    with sqlite3.connect(DB_PATH) as conn:
        cur = conn.cursor()
        cur.execute("SELECT 1 FROM operaciones WHERE tipo = 'backup' AND nombre = ?", (nombre,))
        return cur.fetchone() is not None

def detectar_entorno() -> str:
    if os.name == "nt" and os.path.exists("C:/"):
        return "windows"
    elif os.path.exists("/"):
        return "linux"
    return "desconocido"

def obtener_config(clave: str) -> str:
    try:
        with sqlite3.connect(DB_PATH) as conn:
            cur = conn.cursor()
            cur.execute("SELECT valor FROM config WHERE clave = ?", (clave,))
            row = cur.fetchone()
            if row:
                return row[0]
            else:
                raise HTTPException(
                    status_code=400,
                    detail=f"No está definido el valor para '{clave}' en la tabla de configuración"
                )
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error al acceder a la configuración: {str(e)}")