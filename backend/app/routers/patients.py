from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from .. import models, schemas
from ..database import get_db

router = APIRouter(prefix="/patients", tags=["patients"])


@router.post("", response_model=schemas.PatientOut)
def create_patient(
    payload: schemas.PatientCreate,
    clinic_id: str,  # TODO: reemplazar por el clinic_id del usuario autenticado
    db: Session = Depends(get_db),
):
    patient = models.Patient(clinic_id=clinic_id, **payload.model_dump())
    db.add(patient)
    db.commit()
    db.refresh(patient)
    return patient


@router.get("", response_model=list[schemas.PatientOut])
def list_patients(
    clinic_id: str,
    search: str | None = None,
    db: Session = Depends(get_db),
):
    query = db.query(models.Patient).filter(
        models.Patient.clinic_id == clinic_id, models.Patient.is_active == True  # noqa: E712
    )
    if search:
        like = f"%{search}%"
        query = query.filter(
            (models.Patient.first_name.ilike(like))
            | (models.Patient.last_name.ilike(like))
        )
    return query.order_by(models.Patient.last_name).all()


@router.get("/{patient_id}", response_model=schemas.PatientOut)
def get_patient(patient_id: str, db: Session = Depends(get_db)):
    patient = db.query(models.Patient).filter(models.Patient.id == patient_id).first()
    if not patient:
        raise HTTPException(status_code=404, detail="Paciente no encontrado")
    return patient


@router.put("/{patient_id}", response_model=schemas.PatientOut)
def update_patient(
    patient_id: str, payload: schemas.PatientUpdate, db: Session = Depends(get_db)
):
    patient = db.query(models.Patient).filter(models.Patient.id == patient_id).first()
    if not patient:
        raise HTTPException(status_code=404, detail="Paciente no encontrado")
    for field, value in payload.model_dump(exclude_unset=True).items():
        setattr(patient, field, value)
    db.commit()
    db.refresh(patient)
    return patient


@router.delete("/{patient_id}")
def deactivate_patient(patient_id: str, db: Session = Depends(get_db)):
    patient = db.query(models.Patient).filter(models.Patient.id == patient_id).first()
    if not patient:
        raise HTTPException(status_code=404, detail="Paciente no encontrado")
    patient.is_active = False
    db.commit()
    return {"ok": True}
