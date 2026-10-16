"""
Respaldos automáticos de la base de datos local y de las imágenes.

Cada vez que arranca el backend local (es decir, cada día que se abre
Dental Suite) se hace, como máximo una vez al día:

  - una copia consistente de dental_suite.db (con la API de respaldo de
    SQLite, segura aunque la base esté en uso), y
  - una copia de las imágenes nuevas (solo las que todavía no estaban
    respaldadas, para no duplicar gigas cada día).

Se conservan los últimos KEEP_DAYS respaldos diarios de la base.

La carpeta de respaldos está en Documentos ("Dental Suite - Respaldos"),
fuera de la carpeta del programa y fuera de AppData, para poder copiarla
a una USB o a la nube y restaurarla en otra PC.
"""

import logging
import os
import shutil
import sqlite3
from datetime import datetime

from . import paths

log = logging.getLogger("dentalsuite.backup")

KEEP_DAYS = 30


def run_daily_backup() -> str | None:
    if not os.path.exists(paths.DB_PATH):
        return None

    db_dir = os.path.join(paths.BACKUP_DIR, "base-de-datos")
    img_dir = os.path.join(paths.BACKUP_DIR, "imagenes")
    os.makedirs(db_dir, exist_ok=True)
    os.makedirs(img_dir, exist_ok=True)

    today = datetime.now().strftime("%Y-%m-%d")
    target = os.path.join(db_dir, f"dental_suite_{today}.db")

    if not os.path.exists(target):
        tmp = target + ".tmp"
        src = sqlite3.connect(paths.DB_PATH)
        dst = sqlite3.connect(tmp)
        try:
            src.backup(dst)
        finally:
            dst.close()
            src.close()
        os.replace(tmp, target)
        log.info("Respaldo de base de datos: %s", target)

    # Imágenes: solo las que falten en el respaldo.
    for name in os.listdir(paths.UPLOADS_DIR):
        s = os.path.join(paths.UPLOADS_DIR, name)
        d = os.path.join(img_dir, name)
        if os.path.isfile(s) and not os.path.exists(d):
            try:
                shutil.copy2(s, d)
            except OSError as exc:
                log.warning("No se pudo respaldar %s: %s", name, exc)

    # Limpieza: conservar solo los últimos KEEP_DAYS respaldos de la base.
    backups = sorted(
        f for f in os.listdir(db_dir) if f.startswith("dental_suite_") and f.endswith(".db")
    )
    for old in backups[:-KEEP_DAYS]:
        try:
            os.remove(os.path.join(db_dir, old))
        except OSError:
            pass

    return target


def restore_from_backup(backup_file: str) -> None:
    """Restaura la base desde un respaldo. Llamar con el backend
    DETENIDO. Guarda primero la base actual por si acaso."""
    if os.path.exists(paths.DB_PATH):
        stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        shutil.copy2(paths.DB_PATH, paths.DB_PATH + f".antes-de-restaurar-{stamp}")
    shutil.copy2(backup_file, paths.DB_PATH)
