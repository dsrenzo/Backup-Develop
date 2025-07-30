### routers/operaciones.py
from fastapi import APIRouter, HTTPException, Request
from fastapi.responses import JSONResponse
from datetime import datetime
import os
import logging
import shutil
import time
from tqdm import tqdm
import psutil
import subprocess
import re
import hashlib
from db.manager import registrar_op, obtener_ops, detectar_entorno, existe_backup, obtener_config
from models.schemas import BackupModel, RestoreModel, DeleteModel, BootModel
from fastapi import Body

router = APIRouter()

# Configurar logging
log_dir = "G:/OperacionesPendientes/logs"
os.makedirs(log_dir, exist_ok=True)
log_file = os.path.join(log_dir, "api.log")
hot_log_file = os.path.join(log_dir, "backup_en_caliente.log")
logging.basicConfig(
    filename=log_file,
    level=logging.ERROR,
    format='%(asctime)s - %(levelname)s - %(message)s'
)

hot_logger = logging.getLogger("backup_en_caliente")
hot_handler = logging.FileHandler(hot_log_file)
hot_handler.setFormatter(logging.Formatter('%(asctime)s - %(levelname)s - %(message)s'))
hot_logger.setLevel(logging.INFO)
hot_logger.addHandler(hot_handler)


def validar_nombre(nombre: str):
    if not nombre.strip():
        raise HTTPException(status_code=400, detail="El nombre no puede estar vacío")
    if re.search(r'[<>:"/\\|?*]', nombre):
        raise HTTPException(status_code=400, detail="El nombre contiene caracteres no permitidos: <>:\"/\\|?*")

def validar_ruta(valor: str, campo: str):
    if not valor.strip():
        raise HTTPException(status_code=400, detail=f"El campo '{campo}' no puede estar vacío")

def tamano_directorio(path: str) -> int:
    total_size = 0
    stack = [path]
    while stack:
        current = stack.pop()
        try:
            with os.scandir(current) as entries:
                for entry in entries:
                    try:
                        if entry.is_file(follow_symlinks=False):
                            total_size += entry.stat(follow_symlinks=False).st_size
                        elif entry.is_dir(follow_symlinks=False):
                            stack.append(entry.path)
                    except Exception:
                        continue
        except Exception:
            continue
    return total_size

@router.post("/backup")
def crear_backup(data: BackupModel = Body(..., example={
    "origen": "C:/",
    "en_caliente": False
})):
    try:
        entorno = detectar_entorno()
        validar_ruta(data.origen, "origen")

        # Nombre automático
        nombre = datetime.now().strftime("backup_%Y%m%d_%H%M%S")

        # Obtener destino desde config
        destino_config = obtener_config("destino_backup")

        # Validación: recursión infinita
        origen_abs = os.path.abspath(data.origen)
        destino_abs = os.path.abspath(destino_config)
        if destino_abs.startswith(origen_abs):
            raise HTTPException(status_code=400, detail="El destino no puede estar dentro del origen (recursión infinita)")

        print(f"Origen: {data.origen}, Destino: {destino_config}, Entorno: {entorno}")
        archivo_final = os.path.normpath(os.path.join(destino_abs, f"{nombre}.img")).strip('"')

        # Obtener números de volumen reales (diskpart)
        def obtener_numero_volumen_diskpart(letra: str) -> int:
            letra = letra.strip().upper().replace(":", "")
            if not letra or not letra.isalpha():
                raise HTTPException(status_code=400, detail=f"Letra inválida: {letra}")

            script_path = "temp_diskpart_script.txt"
            with open(script_path, "w") as f:
                f.write("list volume\nexit\n")

            try:
                result = subprocess.run(["diskpart", "/s", script_path], capture_output=True, text=True)
                salida = result.stdout

                # Log en carpeta centralizada
                log_dir = "G:/OperacionesPendientes/logs"
                os.makedirs(log_dir, exist_ok=True)
                with open(os.path.join(log_dir, "diskpart_volumen.log"), "a", encoding="utf-8") as log:
                    log.write(f"\n--- {datetime.now()} ---\nLetra: {letra}\n{salida}\n")

            finally:
                if os.path.exists(script_path):
                    os.remove(script_path)

            # Buscar línea que empiece por Volume o Volumen y contenga la letra como columna
            for linea in salida.splitlines():
                linea_limpia = linea.strip().lower()
                if (linea_limpia.startswith("volume") or linea_limpia.startswith("volumen")) and letra in linea:
                    partes = linea.split()
                    # Buscar columna donde va la letra (ej. D, C, etc.)
                    for i, val in enumerate(partes):
                        if val == letra and i >= 2:
                            try:
                                return int(partes[1])  # Siempre está en index 1: Volume N
                            except ValueError:
                                break

            raise HTTPException(status_code=400, detail=f"No se pudo encontrar volumen para la letra {letra} usando diskpart")

        nvol_origen = None
        nvol_destino = None
        if entorno == "windows":
            letra_origen = data.origen.strip()[0]
            letra_destino = destino_config.strip()[0]
            nvol_origen = obtener_numero_volumen_diskpart(letra_origen)
            nvol_destino = obtener_numero_volumen_diskpart(letra_destino)

        # Backup en caliente
        if data.en_caliente:
            try:
                letra_origen = data.origen.strip().upper().replace("\\", "").replace("/", "")
                if not re.match(r"^[A-Z]:$", letra_origen):
                    raise HTTPException(status_code=400, detail="Origen debe ser una unidad válida como C:/ o D:/")

                if os.path.exists(archivo_final):
                    raise HTTPException(status_code=400, detail="Ya existe una imagen con ese nombre")

                os.makedirs(os.path.dirname(archivo_final), exist_ok=True)

                def obtener_espacio_disponible(path):
                    particion = os.path.splitdrive(path)[0] or path[0]
                    for p in psutil.disk_partitions():
                        if p.device.startswith(particion):
                            return psutil.disk_usage(p.mountpoint).free
                    return 0

                def obtener_tamano_particion(origen):
                    origen = origen.upper()
                    for p in psutil.disk_partitions():
                        if p.device.startswith(origen):
                            return psutil.disk_usage(p.mountpoint).used
                    return 0

                def hash_archivo(path):
                    import hashlib
                    h = hashlib.sha256()
                    with open(path, "rb") as f:
                        while chunk := f.read(8192):
                            h.update(chunk)
                    return h.hexdigest()

                tam_origen = obtener_tamano_particion(letra_origen)
                espacio_disp = obtener_espacio_disponible(destino_abs)

                if tam_origen > espacio_disp:
                    raise HTTPException(
                        status_code=400,
                        detail=f"Espacio insuficiente: origen ocupa {tam_origen} bytes y solo hay {espacio_disp} disponibles"
                    )

                inicio = time.time()
                """ with open(rf"\\.\{letra_origen}", "rb") as fsrc, open(archivo_final, "wb") as fdst:
                    shutil.copyfileobj(fsrc, fdst) """
                
                with open(rf"\\.\{letra_origen}", "rb") as fsrc, open(archivo_final, "wb") as fdst:
                    copied = 0
                    pbar = tqdm(total=tam_origen, unit="B", unit_scale=True, desc="Copiando")
                    while copied < tam_origen:
                        # leer solo lo que falta
                        to_read = min(16384, tam_origen - copied)
                        buf = fsrc.read(to_read)
                        if not buf:
                            break
                        fdst.write(buf)
                        copied += len(buf)
                        pbar.update(len(buf))
                    pbar.close()
                
                hot_logger.info(f"Backup en caliente completado: {archivo_final} ({copied} bytes copiados)") 
                duracion = round(time.time() - inicio, 2)

                hash_resultado = hash_archivo(archivo_final)
                with open(archivo_final + ".hash.txt", "w") as f:
                    f.write(f"SHA256: {hash_resultado}\n")

                return {
                    "mensaje": "Backup en caliente (.img) completado",
                    "duracion_segundos": duracion,
                    "tamano_bytes": os.path.getsize(archivo_final),
                    "archivo": archivo_final,
                    "hash": hash_resultado
                }

            except HTTPException:
                raise
            except Exception as e:
                raise HTTPException(status_code=500, detail=f"Error durante el backup en caliente: {str(e)}")

        # Backup normal
        if existe_backup(nombre):
            raise HTTPException(status_code=400, detail="Ya existe un backup con ese nombre")

        op = {
            "tipo": "backup",
            "nombre": nombre,
            "origen": data.origen,
            "destino": destino_config,
            "entorno": entorno,
            "fecha": datetime.now().isoformat(),
            "nvolumenorigen": nvol_origen,
            "nvolumendestino": nvol_destino
        }
        registrar_op(op)
        return {"mensaje": "Backup registrado correctamente"}

    except HTTPException as e:
        logging.error(f"/backup - {e.detail}")
        return JSONResponse(status_code=e.status_code, content={"error": e.detail})

""" @router.post("/restore")
def crear_restore(data: RestoreModel = Body(..., example={
    "nombre": "backup_2025_06_03",
    "destino": "C:/"
})):
    try:
        entorno = detectar_entorno()
        validar_nombre(data.nombre)
        validar_ruta(data.destino, "destino")
        if not existe_backup(data.nombre):
            raise HTTPException(status_code=404, detail="Backup no encontrado")
        op = {
            "tipo": "restore",
            "nombre": data.nombre,
            "origen": '',
            "destino": data.destino,
            "entorno": entorno,
            "fecha": datetime.now().isoformat()
        }
        registrar_op(op)
        return {"mensaje": "Restore registrado correctamente"}
    except HTTPException as e:
        logging.error(f"/restore - {e.detail}")
        return JSONResponse(status_code=e.status_code, content={"error": e.detail})
 """
@router.post("/delete")
def crear_delete(data: DeleteModel = Body(..., example={
    "nombre": "backup_2025_06_03"
})):
    try:
        entorno = detectar_entorno()
        validar_nombre(data.nombre)
        if not existe_backup(data.nombre):
            raise HTTPException(status_code=404, detail="Backup no encontrado")
        op = {
            "tipo": "delete",
            "nombre": data.nombre,
            "origen": "",
            "destino": "",
            "entorno": entorno,
            "fecha": datetime.now().isoformat()
        }
        registrar_op(op)
        return {"mensaje": "Eliminación registrada"}
    except HTTPException as e:
        logging.error(f"/delete - {e.detail}")
        return JSONResponse(status_code=e.status_code, content={"error": e.detail})

@router.post("/boot")
def cambiar_arranque(data: BootModel = Body(..., example={
    "modo": "windows"
})):
    try:
        entorno = detectar_entorno()
        if data.modo not in ["windows", "linux"]:
            raise HTTPException(status_code=400, detail="Modo inválido")
        op = {
            "tipo": "boot",
            "nombre": f"arranque_{data.modo}",
            "origen": "",
            "destino": "",
            "entorno": entorno,
            "fecha": datetime.now().isoformat()
        }
        registrar_op(op)
        return {"mensaje": "Cambio de inicio registrado"}
    except HTTPException as e:
        logging.error(f"/boot - {e.detail}")
        return JSONResponse(status_code=e.status_code, content={"error": e.detail})

@router.post("/shutdown")
def apagar():
    try:
        entorno = detectar_entorno()
        op = {
            "tipo": "shutdown",
            "nombre": "apagado_sistema",
            "origen": "",
            "destino": "",
            "entorno": entorno,
            "fecha": datetime.now().isoformat()
        }
        registrar_op(op)
        return {"mensaje": "Apagado registrado"}
    except HTTPException as e:
        logging.error(f"/shutdown - {e.detail}")
        return JSONResponse(status_code=e.status_code, content={"error": e.detail})

@router.get("/backups")
def listar_backups():
    backups = obtener_ops("backup")
    return [{"nombre": b["nombre"], "fecha": b["fecha"], "estado": b["estado"], "entorno": b.get("entorno", "") } for b in backups]

@router.get("/restores")
def listar_restores():
    return obtener_ops("restore")

@router.post("/edw_power")
def edw_power():
    try:
        entorno = detectar_entorno()
        if entorno != "windows":
            raise HTTPException(status_code=400, detail="Esta operación solo está disponible en Windows")

        def obtener_identificador_winpe() -> str:
            result = subprocess.run(["bcdedit", "/enum", "all"], capture_output=True, text=True, encoding="utf-8", errors="ignore")
            salida = result.stdout

            # Log para depuración
            log_dir = "G:/OperacionesPendientes/logs"
            os.makedirs(log_dir, exist_ok=True)
            with open(os.path.join(log_dir, "bcdedit.log"), "a", encoding="utf-8") as log:
                log.write(f"\n--- {datetime.now()} ---\n{salida}\n")

            bloques = salida.split("Identificador")
            descripciones_validas = ["windows pe", "winpe", "windows preinstallation environment"]

            for bloque in bloques:
                if any(desc in bloque.lower() for desc in descripciones_validas):
                    lineas = bloque.splitlines()
                    for linea in lineas:
                        if "description" in linea.lower():
                            break
                    for linea in lineas:
                        if "{" in linea and "}" in linea:
                            identificador = linea.strip()
                            if identificador.startswith("{") and identificador.endswith("}"):
                                return identificador

            raise HTTPException(
                status_code=400,
                detail="No se encontró entrada de arranque con descripción 'Windows PE', 'WinPE' o 'Windows PreInstallation Environment' en el BCD"
            )

        identificador_pe = obtener_identificador_winpe()

        resultado_bcdedit = subprocess.run(["bcdedit", "/default", identificador_pe], capture_output=True, text=True)
        if resultado_bcdedit.returncode != 0:
            raise HTTPException(status_code=500, detail=f"Error al ejecutar bcdedit: {resultado_bcdedit.stderr}")

        # Cambiar timeout a 5 segundos
        subprocess.run(["bcdedit", "/timeout", "5"], capture_output=True, text=True)

        resultado_shutdown = subprocess.run(["shutdown", "/r", "/f", "/t", "0"], capture_output=True, text=True)
        if resultado_shutdown.returncode != 0:
            raise HTTPException(status_code=500, detail=f"Error al ejecutar shutdown: {resultado_shutdown.stderr}")

        return {"mensaje": "Cambio de arranque a WinPE ejecutado. El sistema se está reiniciando."}

    except HTTPException as e:
        logging.error(f"/edw_power - {e.detail}")
        return JSONResponse(status_code=e.status_code, content={"error": e.detail})

