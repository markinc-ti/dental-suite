"""
Punto de entrada del backend local (el que arranca el cliente WPF en modo
"solo esta PC", con el Python portátil de backend\\python-runtime).

  - Base de datos y fotos en la carpeta de DATOS (%LOCALAPPDATA%\\DentalSuite
    o la que indique DENTALSUITE_DATA_DIR), nunca en la carpeta del
    programa: reinstalar o actualizar Dental Suite no borra nada.
  - Respaldo automático diario en Documentos\\Dental Suite - Respaldos.
  - Escucha únicamente en 127.0.0.1: no necesita red ni internet.
"""

import logging
import os
import sys
import threading

import uvicorn


def _setup_logging(data_dir: str) -> None:
    logging.basicConfig(
        filename=os.path.join(data_dir, "backend.log"),
        level=logging.INFO,
        format="%(asctime)s %(levelname)s %(name)s: %(message)s",
    )


def _backup_in_background() -> None:
    from app.backup import run_daily_backup

    try:
        run_daily_backup()
    except Exception:  # un respaldo fallido nunca debe impedir trabajar
        logging.getLogger("dentalsuite.backup").exception("Falló el respaldo diario")


if __name__ == "__main__":
    # paths fija y crea la carpeta de datos (y rescata imágenes de
    # versiones anteriores que quedaron dentro de la carpeta del programa).
    from app import paths

    _setup_logging(paths.DATA_DIR)
    os.environ.setdefault("DATABASE_URL", f"sqlite:///{paths.DB_PATH}")

    # pythonw.exe no tiene consola: sin esto, cualquier print() truena.
    if sys.stdout is None:
        sys.stdout = open(os.devnull, "w")
    if sys.stderr is None:
        sys.stderr = open(os.devnull, "w")

    # El respaldo se hace ANTES de que la app toque la base (y antes de
    # que schema_upgrade agregue columnas), en un hilo para no retrasar
    # el arranque.
    t = threading.Thread(target=_backup_in_background, daemon=True)
    t.start()
    t.join(timeout=20)

    from app.main import app  # importa después de fijar DATABASE_URL

    uvicorn.run(app, host="127.0.0.1", port=8003, log_level="info", log_config=None)
