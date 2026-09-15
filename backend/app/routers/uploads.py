import os
import uuid

from fastapi import APIRouter, File, UploadFile

router = APIRouter(prefix="/uploads", tags=["uploads"])

UPLOADS_DIR = os.path.join(os.path.dirname(__file__), "..", "..", "uploads")
os.makedirs(UPLOADS_DIR, exist_ok=True)


@router.post("/image")
async def upload_image(file: UploadFile = File(...)):
    """
    Recibe los bytes de una imagen (radiografía o foto) y los guarda de
    verdad en el servidor, devolviendo la ruta pública con la que
    cualquier cliente (escritorio, navegador, app móvil) puede mostrarla
    después — a diferencia de guardar solo la ruta del archivo en la PC
    de quien la capturó, que nadie más puede ver.
    """
    extension = os.path.splitext(file.filename or "")[1] or ".png"
    unique_name = f"{uuid.uuid4().hex}{extension}"
    destination = os.path.join(UPLOADS_DIR, unique_name)

    contents = await file.read()
    with open(destination, "wb") as f:
        f.write(contents)

    return {"url": f"/uploads/{unique_name}"}
