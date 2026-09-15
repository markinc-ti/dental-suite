from datetime import datetime, timedelta

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from .. import models, schemas
from ..database import get_db

router = APIRouter(tags=["treatments"])


# ---------------------------------------------------------------------------
# Catálogo de tipos de tratamiento ("centro de costos")
# ---------------------------------------------------------------------------

@router.get("/treatment-types", response_model=list[schemas.TreatmentTypeOut])
def list_treatment_types(clinic_id: str, db: Session = Depends(get_db)):
    return (
        db.query(models.TreatmentType)
        .filter(models.TreatmentType.clinic_id == clinic_id, models.TreatmentType.is_active == True)  # noqa: E712
        .order_by(models.TreatmentType.name)
        .all()
    )


@router.post("/treatment-types", response_model=schemas.TreatmentTypeOut)
def create_treatment_type(
    payload: schemas.TreatmentTypeCreate, clinic_id: str, db: Session = Depends(get_db)
):
    treatment_type = models.TreatmentType(clinic_id=clinic_id, **payload.model_dump())
    db.add(treatment_type)
    db.commit()
    db.refresh(treatment_type)
    return treatment_type


# ---------------------------------------------------------------------------
# Tratamientos (planes por paciente)
# ---------------------------------------------------------------------------

@router.post("/treatments", response_model=schemas.TreatmentOut)
def create_treatment(payload: schemas.TreatmentCreate, clinic_id: str, db: Session = Depends(get_db)):
    if payload.total_sessions < 1:
        raise HTTPException(status_code=400, detail="El tratamiento necesita al menos 1 sesión.")

    try:
        hour, minute = (int(part) for part in payload.start_time.split(":"))
    except ValueError:
        raise HTTPException(status_code=400, detail="La hora debe tener el formato HH:mm.")

    treatment = models.Treatment(
        clinic_id=clinic_id,
        patient_id=payload.patient_id,
        dentist_id=payload.dentist_id,
        treatment_type_id=payload.treatment_type_id,
        name=payload.name,
        estimated_cost=payload.estimated_cost,
        total_sessions=payload.total_sessions,
        interval_days=payload.interval_days,
        start_date=payload.start_date,
        notes=payload.notes,
    )
    db.add(treatment)
    db.flush()  # para tener treatment.id antes de crear las sesiones

    # Genera automáticamente una cita + sesión por cada visita del plan,
    # espaciadas por interval_days (ej. mensual = 30).
    base_date = payload.start_date.replace(
        hour=hour, minute=minute, second=0, microsecond=0)

    for i in range(payload.total_sessions):
        session_date = base_date + timedelta(days=payload.interval_days * i)

        appointment = models.Appointment(
            clinic_id=clinic_id,
            patient_id=payload.patient_id,
            dentist_id=payload.dentist_id,
            starts_at=session_date,
            ends_at=session_date + timedelta(minutes=payload.session_duration_minutes),
            reason=f"{payload.name} — sesión {i + 1}/{payload.total_sessions}",
        )
        db.add(appointment)
        db.flush()

        db.add(models.TreatmentSession(
            treatment_id=treatment.id,
            appointment_id=appointment.id,
            session_number=i + 1,
            scheduled_date=session_date,
        ))

    db.commit()
    db.refresh(treatment)
    return treatment


@router.get("/treatments", response_model=list[schemas.TreatmentOut])
def list_treatments(
    clinic_id: str,
    patient_id: str | None = None,
    status: models.TreatmentStatus | None = None,
    db: Session = Depends(get_db),
):
    query = db.query(models.Treatment).filter(models.Treatment.clinic_id == clinic_id)
    if patient_id:
        query = query.filter(models.Treatment.patient_id == patient_id)
    if status:
        query = query.filter(models.Treatment.status == status)
    return query.order_by(models.Treatment.created_at.desc()).all()


@router.get("/treatments/{treatment_id}", response_model=schemas.TreatmentOut)
def get_treatment(treatment_id: str, db: Session = Depends(get_db)):
    treatment = db.query(models.Treatment).filter(models.Treatment.id == treatment_id).first()
    if not treatment:
        raise HTTPException(status_code=404, detail="Tratamiento no encontrado")
    return treatment


@router.put("/treatments/{treatment_id}/status", response_model=schemas.TreatmentOut)
def update_treatment_status(
    treatment_id: str, status: models.TreatmentStatus, db: Session = Depends(get_db)
):
    treatment = db.query(models.Treatment).filter(models.Treatment.id == treatment_id).first()
    if not treatment:
        raise HTTPException(status_code=404, detail="Tratamiento no encontrado")
    treatment.status = status
    db.commit()
    db.refresh(treatment)
    return treatment


@router.put("/treatment-sessions/{session_id}/reschedule", response_model=schemas.TreatmentSessionOut)
def reschedule_session(
    session_id: str, payload: schemas.SessionReschedule, db: Session = Depends(get_db)
):
    """Cambia la fecha/hora de una sesión ya programada — mueve también
    la cita ligada en la agenda, para que ambas queden siempre en sync."""
    session = (
        db.query(models.TreatmentSession)
        .filter(models.TreatmentSession.id == session_id)
        .first()
    )
    if not session:
        raise HTTPException(status_code=404, detail="Sesión no encontrada")

    duration = None
    if session.appointment is not None:
        duration = session.appointment.ends_at - session.appointment.starts_at

    session.scheduled_date = payload.new_date
    if session.appointment is not None:
        session.appointment.starts_at = payload.new_date
        session.appointment.ends_at = payload.new_date + (duration or timedelta(minutes=30))

    db.commit()
    db.refresh(session)
    return session


@router.post("/treatments/{treatment_id}/sessions", response_model=schemas.TreatmentSessionOut)
def add_extra_session(
    treatment_id: str, payload: schemas.ExtraSessionCreate, db: Session = Depends(get_db)
):
    """Agrega una sesión adicional a un tratamiento ya en curso — para
    algo que no estaba contemplado en el plan original (una visita
    extra), sin tener que crear un tratamiento nuevo aparte."""
    treatment = db.query(models.Treatment).filter(models.Treatment.id == treatment_id).first()
    if not treatment:
        raise HTTPException(status_code=404, detail="Tratamiento no encontrado")

    next_number = max((s.session_number for s in treatment.sessions), default=0) + 1

    appointment = models.Appointment(
        clinic_id=treatment.clinic_id,
        patient_id=treatment.patient_id,
        dentist_id=treatment.dentist_id,
        starts_at=payload.scheduled_date,
        ends_at=payload.scheduled_date + timedelta(minutes=payload.duration_minutes),
        reason=f"{treatment.name} — sesión extra {next_number}",
    )
    db.add(appointment)
    db.flush()

    session = models.TreatmentSession(
        treatment_id=treatment.id,
        appointment_id=appointment.id,
        session_number=next_number,
        scheduled_date=payload.scheduled_date,
        notes=payload.notes,
    )
    db.add(session)

    # La sesión extra también cuenta para el total, así la barra de
    # progreso sigue reflejando la realidad del tratamiento.
    treatment.total_sessions = next_number

    db.commit()
    db.refresh(session)
    return session


@router.put("/treatment-sessions/{session_id}/complete", response_model=schemas.TreatmentSessionOut)
def complete_session(session_id: str, db: Session = Depends(get_db)):
    session = (
        db.query(models.TreatmentSession)
        .filter(models.TreatmentSession.id == session_id)
        .first()
    )
    if not session:
        raise HTTPException(status_code=404, detail="Sesión no encontrada")

    session.status = models.SessionStatus.COMPLETED
    session.completed_at = datetime.utcnow()
    if session.appointment is not None:
        session.appointment.status = models.AppointmentStatus.COMPLETED

    db.commit()
    db.refresh(session)

    # Si ya se completaron todas las sesiones, el tratamiento completo
    # pasa a "completado" solo.
    treatment = session.treatment
    remaining = [s for s in treatment.sessions if s.status == models.SessionStatus.PENDING]
    if not remaining:
        treatment.status = models.TreatmentStatus.COMPLETED
        db.commit()

    return session
