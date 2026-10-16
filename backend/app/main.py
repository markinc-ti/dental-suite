from fastapi import FastAPI
from fastapi.middleware.cors import CORSMiddleware
from fastapi.staticfiles import StaticFiles

from . import paths
from .database import engine
from .schema_upgrade import add_missing_columns
from .models import Base
from .routers import appointments, auth, clinical_notes, clinics, invoices, patients, studies, treatments, uploads, users

app = FastAPI(
    title="Dental Suite API",
    description="Backend de gestión de clínica dental: pacientes, citas, "
    "facturación y estudios radiográficos.",
    version="0.1.0",
)

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # restringir a los dominios reales en producción
    allow_methods=["*"],
    allow_headers=["*"],
)

# Carpeta donde se guardan las imágenes subidas (radiografías/fotos) —
# se sirve tal cual como archivos estáticos, para que cualquier cliente
# (escritorio, navegador, o la app móvil) las pueda ver con solo la URL,
# sin necesitar autenticación especial para cargarlas.
#
# Nota sobre Render: el disco del plan gratuito es efímero — el
# contenido se puede perder en cada redeploy. Para producción de verdad
# conviene mover esto a un almacenamiento persistente (S3, Cloudinary,
# un Persistent Disk de Render, etc.). Para empezar y probar, esto
# ya deja el flujo completo funcionando de punta a punta.
# Las imágenes viven en la carpeta de datos (ver app/paths.py), NO en la
# carpeta del programa — así una reinstalación no las borra.
UPLOADS_DIR = paths.UPLOADS_DIR

app.include_router(clinics.router)
app.include_router(auth.router)
app.include_router(users.router)
app.include_router(patients.router)
app.include_router(clinical_notes.router)
app.include_router(treatments.router)
app.include_router(appointments.router)
app.include_router(invoices.router)
app.include_router(studies.router)
# El router de /uploads/image (POST) se registra ANTES del mount de
# archivos estáticos en la misma ruta — si no, el mount le gana el paso
# a la ruta POST y responde 405 Method Not Allowed en vez de subir el
# archivo, porque Starlette resuelve las rutas en el orden en que se
# registran.
app.include_router(uploads.router)
app.mount("/uploads", StaticFiles(directory=UPLOADS_DIR), name="uploads")


@app.on_event("startup")
def on_startup():
    # En desarrollo crea las tablas automáticamente; en producción usar
    # migraciones (Alembic) en vez de create_all.
    Base.metadata.create_all(bind=engine)
    # Columnas nuevas de versiones recientes se agregan solas a una base
    # de datos existente, sin perder nada de lo capturado.
    add_missing_columns(engine, Base.metadata)


@app.get("/health")
def health_check():
    return {"status": "ok"}
