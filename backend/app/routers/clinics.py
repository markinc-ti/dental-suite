from fastapi import APIRouter, Depends
from pydantic import BaseModel
from sqlalchemy.orm import Session

from .. import models
from ..database import get_db

router = APIRouter(prefix="/clinics", tags=["clinics"])


class ClinicCreate(BaseModel):
    name: str
    legal_name: str | None = None
    tax_id: str | None = None
    address: str | None = None
    phone: str | None = None
    email: str | None = None


class ClinicOut(BaseModel):
    id: str
    name: str

    class Config:
        from_attributes = True


@router.get("", response_model=list[ClinicOut])
def list_clinics(db: Session = Depends(get_db)):
    """
    El cliente de escritorio llama esto en su primer arranque: si viene
    vacío, crea la clínica por defecto con POST /clinics y guarda el id
    devuelto en su configuración local (ver ApiClient.EnsureClinicAsync
    en el cliente WPF).
    """
    return db.query(models.Clinic).all()


@router.post("", response_model=ClinicOut)
def create_clinic(payload: ClinicCreate, db: Session = Depends(get_db)):
    clinic = models.Clinic(**payload.model_dump())
    db.add(clinic)
    db.commit()
    db.refresh(clinic)
    return clinic
