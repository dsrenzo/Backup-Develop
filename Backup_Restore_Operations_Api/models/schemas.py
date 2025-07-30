### models/schemas.py
from pydantic import BaseModel

class BackupModel(BaseModel):
    origen: str
    en_caliente: bool = False

class RestoreModel(BaseModel):
    nombre: str
    destino: str

class DeleteModel(BaseModel):
    nombre: str

class BootModel(BaseModel):
    modo: str  # "windows" o "linux"