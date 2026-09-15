from datetime import datetime

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from .. import models, schemas
from ..database import get_db

router = APIRouter(prefix="/appointments", tags=["appointments"])


@router.post("", response_model=schemas.AppointmentOut)
def create_appointment(
    payload: schemas.AppointmentCreate,
    clinic_id: str,
    db: Session = Depends(get_db),
):
    if payload.ends_at <= payload.starts_at:
        raise HTTPException(
            status_code=400, detail="La hora de fin debe ser posterior al inicio"
        )
    appointment = models.Appointment(clinic_id=clinic_id, **payload.model_dump())
    db.add(appointment)
    db.commit()
    db.refresh(appointment)
    return appointment


@router.get("", response_model=list[schemas.AppointmentOut])
def list_appointments(
    clinic_id: str,
    date_from: datetime | None = None,
    date_to: datetime | None = None,
    dentist_id: str | None = None,
    db: Session = Depends(get_db),
):
    query = db.query(models.Appointment).filter(
        models.Appointment.clinic_id == clinic_id
    )
    if date_from:
        query = query.filter(models.Appointment.starts_at >= date_from)
    if date_to:
        query = query.filter(models.Appointment.starts_at <= date_to)
    if dentist_id:
        query = query.filter(models.Appointment.dentist_id == dentist_id)
    return query.order_by(models.Appointment.starts_at).all()


@router.put("/{appointment_id}", response_model=schemas.AppointmentOut)
def update_appointment(
    appointment_id: str,
    payload: schemas.AppointmentUpdate,
    db: Session = Depends(get_db),
):
    appt = (
        db.query(models.Appointment)
        .filter(models.Appointment.id == appointment_id)
        .first()
    )
    if not appt:
        raise HTTPException(status_code=404, detail="Cita no encontrada")
    for field, value in payload.model_dump(exclude_unset=True).items():
        setattr(appt, field, value)
    db.commit()
    db.refresh(appt)
    return appt
