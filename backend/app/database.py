import os

from sqlalchemy import create_engine
from sqlalchemy.orm import sessionmaker

# Por defecto usa SQLite local (un solo archivo, cero configuración) —
# ideal para una instalación de un solo consultorio con el instalador
# .exe. Si se define DATABASE_URL (por ejemplo apuntando a Postgres/Neon),
# se usa esa en su lugar — pensado para cuando varias sucursales necesiten
# compartir la misma base de datos en la nube.
DATABASE_URL = os.getenv("DATABASE_URL", "sqlite:///./dental_suite.db")

connect_args = {"check_same_thread": False} if DATABASE_URL.startswith("sqlite") else {}
engine = create_engine(DATABASE_URL, pool_pre_ping=True, connect_args=connect_args)
SessionLocal = sessionmaker(autocommit=False, autoflush=False, bind=engine)


def get_db():
    db = SessionLocal()
    try:
        yield db
    finally:
        db.close()
