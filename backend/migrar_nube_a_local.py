"""
Copia TODOS los datos del servidor en la nube (Render / PostgreSQL) a la
base de datos local de esta PC, para pasar a trabajar sin internet.

Uso (en la PC del consultorio, con Dental Suite CERRADO):

    cd "C:\\Program Files (x86)\\Dental Suite\\backend\\python-runtime"
    python.exe migrar_nube_a_local.py ^
        --origen "postgresql://USUARIO:CLAVE@HOST.oregon-postgres.render.com/BASE" ^
        --nube https://dental-suite-k65o.onrender.com

  --origen  = "External Database URL" de tu base en Render
              (Dashboard → tu PostgreSQL → Connect → External).
  --nube    = dirección del backend en Render, para descargar las
              imágenes que todavía existan allá.

Qué hace:
  1. Respalda la base local actual (dental_suite.antes-de-migrar-FECHA.db).
  2. Vacía la base local y copia tabla por tabla desde la nube.
  3. Descarga cada imagen referenciada (/uploads/...) a la carpeta local
     de imágenes y reescribe las direcciones para que apunten a esta PC
     (http://127.0.0.1:8003/uploads/...).
  4. Deja la app configurada en modo local (server-config.json).

Las imágenes que Render ya haya borrado (su disco gratuito se reinicia en
cada despliegue) no se pueden recuperar: el script las cuenta y las
reporta al final, pero el resto de los datos se copia completo.
"""

import argparse
import json
import os
import shutil
import sys
import urllib.error
import urllib.request
from datetime import datetime

LOCAL_BASE_URL = "http://127.0.0.1:8003"


def main() -> int:
    parser = argparse.ArgumentParser(description="Copia los datos de Render a esta PC.")
    parser.add_argument("--origen", required=True, help="External Database URL de PostgreSQL en Render")
    parser.add_argument("--nube", default="https://dental-suite-k65o.onrender.com",
                        help="URL del backend en Render (para bajar imágenes)")
    parser.add_argument("--destino", default=None, help="Ruta del .db local (por defecto la de Dental Suite)")
    parser.add_argument("--sin-imagenes", action="store_true", help="No descargar imágenes")
    args = parser.parse_args()

    from app import paths

    dest_path = args.destino or paths.DB_PATH
    os.environ["DATABASE_URL"] = f"sqlite:///{dest_path}"

    from sqlalchemy import create_engine, select

    from app.models import Base
    from app.schema_upgrade import add_missing_columns

    origen_url = args.origen
    if origen_url.startswith("postgres://"):
        origen_url = "postgresql://" + origen_url[len("postgres://"):]

    print("Conectando a la base de datos en la nube...")
    src = create_engine(origen_url, pool_pre_ping=True)
    with src.connect():
        pass

    # 1) Respaldo de la base local actual
    if os.path.exists(dest_path):
        stamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        backup = dest_path.replace(".db", f".antes-de-migrar-{stamp}.db")
        shutil.copy2(dest_path, backup)
        print(f"Respaldo de la base local anterior: {backup}")

    dst = create_engine(f"sqlite:///{dest_path}")
    Base.metadata.create_all(dst)
    add_missing_columns(dst, Base.metadata)

    nube = args.nube.rstrip("/")
    copied: dict[str, int] = {}
    images_ok = 0
    images_missing: list[str] = []
    downloaded: dict[str, str] = {}

    def localize(value: str) -> str:
        """Si el valor es una ruta a /uploads/..., baja la imagen y
        devuelve la nueva dirección local."""
        nonlocal images_ok
        idx = value.find("/uploads/")
        if idx < 0:
            return value
        rel = value[idx:]  # /uploads/archivo.png
        if rel in downloaded:
            return downloaded[rel]

        name = os.path.basename(rel.split("?")[0])
        local_file = os.path.join(paths.UPLOADS_DIR, name)
        new_value = f"{LOCAL_BASE_URL}{rel}"

        if not args.sin_imagenes and not os.path.exists(local_file):
            source_url = f"{nube}{rel}"
            try:
                with urllib.request.urlopen(source_url, timeout=60) as resp, open(local_file, "wb") as out:
                    shutil.copyfileobj(resp, out)
                images_ok += 1
            except (urllib.error.URLError, OSError):
                images_missing.append(source_url)
                if os.path.exists(local_file) and os.path.getsize(local_file) == 0:
                    os.remove(local_file)
        elif os.path.exists(local_file):
            images_ok += 1

        downloaded[rel] = new_value
        return new_value

    # 2) Copiar tabla por tabla, respetando el orden de llaves foráneas
    tables = Base.metadata.sorted_tables
    with dst.begin() as dconn:
        for table in reversed(tables):
            dconn.execute(table.delete())

        for table in tables:
            with src.connect() as sconn:
                try:
                    rows = sconn.execute(select(table)).mappings().all()
                except Exception as exc:  # tabla que no existe en la nube
                    print(f"  (se omite {table.name}: {exc.__class__.__name__})")
                    continue

            batch = []
            for row in rows:
                data = dict(row)
                for key, val in data.items():
                    if isinstance(val, str) and "/uploads/" in val:
                        data[key] = localize(val)
                batch.append(data)

            if batch:
                dconn.execute(table.insert(), batch)
            copied[table.name] = len(batch)
            print(f"  {table.name}: {len(batch)} registros")

    # 3) Dejar la app en modo local
    config_path = os.path.join(paths.DATA_DIR, "server-config.json")
    with open(config_path, "w", encoding="utf-8") as f:
        json.dump({"ApiBaseUrl": LOCAL_BASE_URL}, f)

    print()
    print("=" * 60)
    print(f"LISTO. {sum(copied.values())} registros copiados a {dest_path}")
    print(f"Imágenes disponibles en esta PC: {images_ok}")
    if images_missing:
        print(f"Imágenes que ya no existían en Render: {len(images_missing)}")
        report = os.path.join(paths.DATA_DIR, "imagenes-no-recuperadas.txt")
        with open(report, "w", encoding="utf-8") as f:
            f.write("\n".join(images_missing))
        print(f"  (lista en {report})")
    print("Dental Suite quedó en modo LOCAL: ya no necesita internet.")
    print("=" * 60)
    return 0


if __name__ == "__main__":
    sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
    sys.exit(main())
