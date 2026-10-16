"""
Dónde viven los datos de Dental Suite.

Regla: NADA que el usuario genere (base de datos, fotos/radiografías
subidas, respaldos) se guarda dentro de la carpeta del programa
(C:\\Program Files (x86)\\Dental Suite\\...). Esa carpeta la reemplaza el
instalador en cada actualización/reinstalación, y además Windows no deja
escribir ahí sin permisos de administrador.

Todo va a la carpeta de datos:
  1. DENTALSUITE_DATA_DIR  (la fija el cliente WPF al arrancar el backend)
  2. %LOCALAPPDATA%\\DentalSuite   (Windows)
  3. la carpeta backend/        (desarrollo en Mac/Linux, o Render)
"""

import os
import shutil

_BACKEND_DIR = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))


def _resolve_data_dir() -> str:
    explicit = os.environ.get("DENTALSUITE_DATA_DIR")
    if explicit:
        return explicit
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        return os.path.join(local_app_data, "DentalSuite")
    return _BACKEND_DIR


DATA_DIR = _resolve_data_dir()
UPLOADS_DIR = os.path.join(DATA_DIR, "uploads")
DB_PATH = os.path.join(DATA_DIR, "dental_suite.db")

# Carpeta de respaldos visible para el usuario (Documentos), para que
# sobreviva incluso si se borra %LOCALAPPDATA% o se reinstala Windows y
# se copia la carpeta Documentos.
if os.environ.get("LOCALAPPDATA"):
    BACKUP_DIR = os.environ.get(
        "DENTALSUITE_BACKUP_DIR",
        os.path.join(os.path.expanduser("~"), "Documents", "Dental Suite - Respaldos"),
    )
else:
    BACKUP_DIR = os.path.join(DATA_DIR, "respaldos")

os.makedirs(DATA_DIR, exist_ok=True)
os.makedirs(UPLOADS_DIR, exist_ok=True)

# Versiones anteriores guardaban las imágenes subidas dentro de la
# carpeta del programa (backend\python-runtime\uploads). Si quedó alguna
# ahí, se copia una sola vez a la carpeta de datos para no perderla en
# la próxima reinstalación.
_LEGACY_UPLOADS = os.path.join(_BACKEND_DIR, "uploads")
if os.path.abspath(_LEGACY_UPLOADS) != os.path.abspath(UPLOADS_DIR) and os.path.isdir(_LEGACY_UPLOADS):
    for _name in os.listdir(_LEGACY_UPLOADS):
        _src = os.path.join(_LEGACY_UPLOADS, _name)
        _dst = os.path.join(UPLOADS_DIR, _name)
        if os.path.isfile(_src) and not os.path.exists(_dst):
            try:
                shutil.copy2(_src, _dst)
            except OSError:
                pass
