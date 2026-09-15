from fastapi import APIRouter, Depends
from sqlalchemy.orm import Session

from .. import models, schemas
from ..database import get_db

router = APIRouter(prefix="/clinical-notes", tags=["clinical-notes"])


@router.post("", response_model=schemas.ClinicalNoteOut)
def create_clinical_note(payload: schemas.ClinicalNoteCreate, db: Session = Depends(get_db)):
    note = models.ClinicalNote(**payload.model_dump())
    db.add(note)
    db.commit()
    db.refresh(note)
    return note


@router.get("/patient/{patient_id}", response_model=list[schemas.ClinicalNoteOut])
def list_patient_notes(patient_id: str, db: Session = Depends(get_db)):
    return (
        db.query(models.ClinicalNote)
        .filter(models.ClinicalNote.patient_id == patient_id)
        .order_by(models.ClinicalNote.created_at.desc())
        .all()
    )
