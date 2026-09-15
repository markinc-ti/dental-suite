"""
Endpoints para las sesiones de captura (radiografías y fotos) del
odontograma.

Cada vez que el cliente abre el odontograma de un paciente se crea una
XRayStudy nueva (una "sesión"), sin casillas pre-creadas — cada captura
(radiografía o foto) se registra sobre la marcha en /slots, indicando a
qué diente y de qué tipo (image_type) corresponde. Como el mismo diente
puede tener una radiografía Y una foto en la misma sesión, y además
volver a capturarse en sesiones futuras, esto arma solo un historial en
el tiempo por diente y por tipo — se consulta completo con
/patient/{patient_id}.
"""

from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from .. import models, schemas
from ..database import get_db

router = APIRouter(prefix="/xray-studies", tags=["xray-studies"])

# Mapeo número de diente -> cuadrante, sistema universal (1-32)
_QUADRANT_BY_TOOTH = {}
for n in range(1, 9):
    _QUADRANT_BY_TOOTH[n] = models.Quadrant.UPPER_RIGHT
for n in range(9, 17):
    _QUADRANT_BY_TOOTH[n] = models.Quadrant.UPPER_LEFT
for n in range(17, 25):
    _QUADRANT_BY_TOOTH[n] = models.Quadrant.LOWER_LEFT
for n in range(25, 33):
    _QUADRANT_BY_TOOTH[n] = models.Quadrant.LOWER_RIGHT


@router.post("", response_model=schemas.XRayStudyOut)
def create_study(
    payload: schemas.XRayStudyCreate,
    clinic_id: str,
    db: Session = Depends(get_db),
):
    study = models.XRayStudy(
        clinic_id=clinic_id,
        patient_id=payload.patient_id,
        dentition_type=payload.dentition_type,
        description=payload.description,
        sensor_brand=payload.sensor_brand,
    )
    db.add(study)
    db.commit()
    db.refresh(study)
    return study


@router.get("/{study_id}", response_model=schemas.XRayStudyOut)
def get_study(study_id: str, db: Session = Depends(get_db)):
    study = db.query(models.XRayStudy).filter(models.XRayStudy.id == study_id).first()
    if not study:
        raise HTTPException(status_code=404, detail="Estudio no encontrado")
    return study


@router.get("/patient/{patient_id}", response_model=list[schemas.XRayStudyOut])
def list_patient_studies(patient_id: str, db: Session = Depends(get_db)):
    """Historial completo del paciente: todas las sesiones, cada una con
    sus radiografías y fotos, ordenadas de la más reciente a la más
    antigua."""
    return (
        db.query(models.XRayStudy)
        .filter(models.XRayStudy.patient_id == patient_id)
        .order_by(models.XRayStudy.taken_at.desc())
        .all()
    )


@router.put("/slots/{slot_id}", response_model=schemas.ToothImageSlotOut)
def update_slot_image(
    slot_id: str, payload: schemas.ToothImageSlotUpload, db: Session = Depends(get_db)
):
    """Reemplaza la imagen de una captura ya existente (por ejemplo,
    después de anotarla en el visor) — a diferencia de /slots, aquí se
    apunta directo al registro por su propio id, sin necesitar
    study_id/tooth_number/image_type de nuevo."""
    slot = db.query(models.ToothImageSlot).filter(models.ToothImageSlot.id == slot_id).first()
    if not slot:
        raise HTTPException(status_code=404, detail="Captura no encontrada")

    slot.image_url = payload.image_url
    slot.captured_at = datetime.utcnow()
    db.commit()
    db.refresh(slot)
    return slot


@router.post("/{study_id}/slots", response_model=schemas.ToothImageSlotOut)
def upload_slot_image(
    study_id: str,
    payload: schemas.ToothImageSlotUpload,
    db: Session = Depends(get_db),
):
    """
    El cliente Windows llama este endpoint cada vez que captura una
    imagen (radiografía o foto) de un diente. Si ya existe una captura
    de ese mismo diente y tipo EN ESTA sesión, se actualiza (por ejemplo,
    si se vuelve a tomar); si no existe, se crea — así no hace falta
    pre-generar las 32 posiciones de antemano.
    """
    study = db.query(models.XRayStudy).filter(models.XRayStudy.id == study_id).first()
    if not study:
        raise HTTPException(status_code=404, detail="Estudio no encontrado")

    if payload.tooth_number not in _QUADRANT_BY_TOOTH:
        raise HTTPException(status_code=400, detail="Número de diente inválido")

    slot = (
        db.query(models.ToothImageSlot)
        .filter(
            models.ToothImageSlot.study_id == study_id,
            models.ToothImageSlot.tooth_number == payload.tooth_number,
            models.ToothImageSlot.image_type == payload.image_type,
        )
        .first()
    )
    if not slot:
        slot = models.ToothImageSlot(
            study_id=study_id,
            tooth_number=payload.tooth_number,
            quadrant=_QUADRANT_BY_TOOTH[payload.tooth_number],
            image_type=payload.image_type,
        )
        db.add(slot)

    slot.image_url = payload.image_url
    slot.image_order = payload.image_order
    slot.is_captured = True
    slot.captured_at = datetime.utcnow()
    db.commit()
    db.refresh(slot)
    return slot
