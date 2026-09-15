"""
Punto de entrada para el backend empaquetado como .exe.

A diferencia de correr `uvicorn app.main:app --reload` en desarrollo,
este script está pensado para ejecutarse standalone (compilado con
PyInstaller) dentro de la carpeta de instalación en la PC del cliente:

  - Corre la base de datos SQLite junto al ejecutable (no en el
    directorio actual desde el que se lance), para que funcione sin
    importar desde dónde el instalador decida lanzarlo.
  - No usa --reload (eso es solo para desarrollo).
  - Escucha únicamente en localhost: 127.0.0.1 — el cliente WPF y el
    backend corren en la misma PC, no necesita exponerse a la red.
"""

import os
import sys

import uvicorn

def _resolve_base_dir() -> str:
    if getattr(sys, "frozen", False):
        # Empaquetado con PyInstaller: la carpeta del propio .exe.
        return os.path.dirname(sys.executable)
    return os.path.dirname(os.path.abspath(__file__))


def _resolve_data_dir() -> str:
    """
    Carpeta donde se guarda la base de datos SQLite. NO usamos la carpeta
    del programa (normalmente dentro de Program Files, protegida contra
    escritura sin permisos de administrador) — usamos el AppData del
    usuario actual, que siempre es escribible sin elevar permisos.
    """
    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        data_dir = os.path.join(local_app_data, "DentalSuite")
    else:
        # Fallback para desarrollo en Mac/Linux, donde LOCALAPPDATA no existe.
        data_dir = _resolve_base_dir()

    os.makedirs(data_dir, exist_ok=True)
    return data_dir


if __name__ == "__main__":
    data_dir = _resolve_data_dir()
    db_path = os.path.join(data_dir, "dental_suite.db")
    os.environ.setdefault("DATABASE_URL", f"sqlite:///{db_path}")

    from app.main import app  # importa después de fijar DATABASE_URL

    uvicorn.run(app, host="127.0.0.1", port=8003, log_level="info")
