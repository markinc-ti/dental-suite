"""Esquemas Pydantic (request/response) de la API."""

from datetime import datetime
from decimal import Decimal
from typing import List, Optional

from pydantic import BaseModel, EmailStr

from .models import (
    AppointmentStatus,
    DentitionType,
    ImageType,
    InvoiceStatus,
    Quadrant,
    SessionStatus,
    TreatmentStatus,
    UserRole,
)


# ---------------------------------------------------------------------------
# Pacientes
# ---------------------------------------------------------------------------

class PatientBase(BaseModel):
    first_name: str
    last_name: str
    date_of_birth: Optional[datetime] = None
    sex: Optional[str] = None
    phone: Optional[str] = None
    email: Optional[EmailStr] = None
    address: Optional[str] = None
    emergency_contact_name: Optional[str] = None
    emergency_contact_phone: Optional[str] = None
    emergency_contact_relationship: Optional[str] = None
    medical_notes: Optional[str] = None
    external_reference: Optional[str] = None
    primary_dentist_id: Optional[str] = None


class PatientCreate(PatientBase):
    pass


class PatientUpdate(BaseModel):
    first_name: Optional[str] = None
    last_name: Optional[str] = None
    date_of_birth: Optional[datetime] = None
    sex: Optional[str] = None
    phone: Optional[str] = None
    email: Optional[EmailStr] = None
    address: Optional[str] = None
    emergency_contact_name: Optional[str] = None
    emergency_contact_phone: Optional[str] = None
    emergency_contact_relationship: Optional[str] = None
    medical_notes: Optional[str] = None
    primary_dentist_id: Optional[str] = None


class PatientOut(PatientBase):
    id: str
    clinic_id: str
    is_active: bool
    created_at: datetime

    class Config:
        from_attributes = True


# ---------------------------------------------------------------------------
# Notas clínicas
# ---------------------------------------------------------------------------

class ClinicalNoteCreate(BaseModel):
    patient_id: str
    author_id: str
    tooth_number: Optional[int] = None
    content: str


class ClinicalNoteOut(BaseModel):
    id: str
    patient_id: str
    author_id: str
    tooth_number: Optional[int]
    content: str
    created_at: datetime

    class Config:
        from_attributes = True


# ---------------------------------------------------------------------------
# Citas
# ---------------------------------------------------------------------------

class AppointmentBase(BaseModel):
    patient_id: str
    dentist_id: str
    starts_at: datetime
    ends_at: datetime
    reason: Optional[str] = None
    notes: Optional[str] = None


class AppointmentCreate(AppointmentBase):
    pass


class AppointmentUpdate(BaseModel):
    dentist_id: Optional[str] = None
    starts_at: Optional[datetime] = None
    ends_at: Optional[datetime] = None
    reason: Optional[str] = None
    status: Optional[AppointmentStatus] = None
    notes: Optional[str] = None


class AppointmentOut(AppointmentBase):
    id: str
    clinic_id: str
    status: AppointmentStatus
    created_at: datetime

    class Config:
        from_attributes = True


# ---------------------------------------------------------------------------
# Facturación
# ---------------------------------------------------------------------------

class InvoiceItemCreate(BaseModel):
    description: str
    tooth_number: Optional[int] = None
    quantity: Decimal = Decimal("1")
    unit_price: Decimal


class InvoiceItemOut(InvoiceItemCreate):
    id: str
    total: Decimal

    class Config:
        from_attributes = True


class InvoiceCreate(BaseModel):
    patient_id: str
    notes: Optional[str] = None
    items: List[InvoiceItemCreate]


class InvoiceOut(BaseModel):
    id: str
    clinic_id: str
    patient_id: str
    folio: Optional[str]
    status: InvoiceStatus
    subtotal: Decimal
    tax: Decimal
    total: Decimal
    issued_at: Optional[datetime]
    paid_at: Optional[datetime]
    notes: Optional[str]
    created_at: datetime
    items: List[InvoiceItemOut] = []

    class Config:
        from_attributes = True


# ---------------------------------------------------------------------------
# Odontograma / Serie radiográfica
# ---------------------------------------------------------------------------

class ToothImageSlotOut(BaseModel):
    id: str
    tooth_number: int
    quadrant: Quadrant
    image_type: ImageType
    is_captured: bool
    image_url: Optional[str]
    image_order: Optional[int]
    captured_at: Optional[datetime]

    class Config:
        from_attributes = True


class ToothImageSlotUpload(BaseModel):
    """Payload que manda el cliente Windows al capturar una imagen del sensor."""
    tooth_number: int
    image_type: ImageType = ImageType.XRAY
    image_url: str
    image_order: Optional[int] = None


class XRayStudyCreate(BaseModel):
    patient_id: str
    dentition_type: DentitionType = DentitionType.PERMANENT
    description: Optional[str] = None
    sensor_brand: Optional[str] = None


class XRayStudyOut(BaseModel):
    id: str
    clinic_id: str
    patient_id: str
    dentition_type: DentitionType
    description: Optional[str]
    taken_at: datetime
    sensor_brand: Optional[str]
    slots: List[ToothImageSlotOut] = []

    class Config:
        from_attributes = True


# ---------------------------------------------------------------------------
# Usuarios
# ---------------------------------------------------------------------------

class UserOut(BaseModel):
    id: str
    clinic_id: str
    full_name: str
    username: str
    role: UserRole
    is_active: bool

    class Config:
        from_attributes = True


# ---------------------------------------------------------------------------
# Tratamientos
# ---------------------------------------------------------------------------

class TreatmentTypeCreate(BaseModel):
    name: str
    description: Optional[str] = None
    default_session_count: int = 1
    default_interval_days: int = 30
    default_cost: Optional[Decimal] = None


class TreatmentTypeOut(TreatmentTypeCreate):
    id: str
    clinic_id: str
    is_active: bool

    class Config:
        from_attributes = True


class TreatmentCreate(BaseModel):
    patient_id: str
    dentist_id: str
    treatment_type_id: Optional[str] = None
    name: str
    estimated_cost: Optional[Decimal] = None
    total_sessions: int
    interval_days: int = 30
    start_date: datetime
    start_time: str = "10:00"  # HH:mm, hora de cada sesión generada
    session_duration_minutes: int = 30
    notes: Optional[str] = None


class TreatmentSessionOut(BaseModel):
    id: str
    session_number: int
    scheduled_date: datetime
    status: SessionStatus
    appointment_id: Optional[str]
    completed_at: Optional[datetime]
    notes: Optional[str]

    class Config:
        from_attributes = True


class SessionReschedule(BaseModel):
    new_date: datetime


class ExtraSessionCreate(BaseModel):
    scheduled_date: datetime
    duration_minutes: int = 30
    notes: Optional[str] = None


class TreatmentOut(BaseModel):
    id: str
    clinic_id: str
    patient_id: str
    dentist_id: str
    treatment_type_id: Optional[str]
    name: str
    estimated_cost: Optional[Decimal]
    total_sessions: int
    interval_days: int
    start_date: datetime
    status: TreatmentStatus
    notes: Optional[str]
    created_at: datetime
    sessions: List[TreatmentSessionOut] = []

    class Config:
        from_attributes = True
