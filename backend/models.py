"""
Modelo de datos del sistema de clínica dental.

Diseño multiempresa (varias clínicas/consultorios en la misma base de datos,
igual que en tickets-ti), con:
  - Pacientes
  - Citas
  - Facturación (facturas + partidas)
  - Series de radiografías / odontograma tipo "Runyes"
    (numeración universal 1-32, dentición permanente o temporal,
    organizada en cuadrantes R/L - superior/inferior, con una imagen
    asociada a cada posición dental)

Todas las fechas se guardan en UTC. Las tablas usan UUID como llave
primaria para poder sincronizar clientes offline sin colisiones de ID.
"""

import enum
import uuid
from datetime import datetime

from sqlalchemy import (
    Boolean,
    Column,
    DateTime,
    Enum,
    ForeignKey,
    Integer,
    Numeric,
    String,
    Text,
    UniqueConstraint,
)

from sqlalchemy.orm import declarative_base, relationship

Base = declarative_base()


def gen_uuid():
    return str(uuid.uuid4())


# ---------------------------------------------------------------------------
# Multiempresa
# ---------------------------------------------------------------------------

class Clinic(Base):
    """Una clínica/consultorio. Todo lo demás cuelga de aquí."""
    __tablename__ = "clinics"

    id = Column(String(36), primary_key=True, default=gen_uuid)
    name = Column(String(200), nullable=False)
    legal_name = Column(String(200))
    tax_id = Column(String(50))  # RFC u otro identificador fiscal
    address = Column(Text)
    phone = Column(String(50))
    email = Column(String(200))
    created_at = Column(DateTime, default=datetime.utcnow)
    is_active = Column(Boolean, default=True)

    users = relationship("User", back_populates="clinic")
    patients = relationship("Patient", back_populates="clinic")
    appointments = relationship("Appointment", back_populates="clinic")
    invoices = relationship("Invoice", back_populates="clinic")


class UserRole(str, enum.Enum):
    ADMIN = "admin"
    DENTIST = "dentist"
    ASSISTANT = "assistant"
    RECEPTION = "reception"


class User(Base):
    """Usuario del sistema (odontólogo, asistente, recepción, admin)."""
    __tablename__ = "users"

    id = Column(String(36), primary_key=True, default=gen_uuid)
    clinic_id = Column(String(36), ForeignKey("clinics.id"), nullable=False)
    full_name = Column(String(200), nullable=False)
    # Con qué inicia sesión: puede ser un correo o un número de celular
    # — lo que la clínica prefiera para ese usuario, sin formato fijo.
    username = Column(String(200), nullable=False, unique=True)
    password_hash = Column(String(255), nullable=False)
    role = Column(Enum(UserRole), nullable=False, default=UserRole.DENTIST)
    phone = Column(String(50))
    specialty = Column(String(150))
    professional_license = Column(String(50))  # cédula profesional / número de certificado
    is_active = Column(Boolean, default=True)
    created_at = Column(DateTime, default=datetime.utcnow)

    clinic = relationship("Clinic", back_populates="users")


# ---------------------------------------------------------------------------
# Pacientes
# ---------------------------------------------------------------------------

class Patient(Base):
    __tablename__ = "patients"

    id = Column(String(36), primary_key=True, default=gen_uuid)
    clinic_id = Column(String(36), ForeignKey("clinics.id"), nullable=False)

    first_name = Column(String(150), nullable=False)
    last_name = Column(String(150), nullable=False)
    date_of_birth = Column(DateTime)
    sex = Column(String(20))
    phone = Column(String(50))
    email = Column(String(200))
    address = Column(Text)

    # Contacto de emergencia
    emergency_contact_name = Column(String(150))
    emergency_contact_phone = Column(String(50))
    emergency_contact_relationship = Column(String(100))  # parentesco/relación

    # Referencia externa: útil si se migran datos desde el software Runyes
    # (que usa un "Paciente ID" propio, visible en la captura original,
    # p. ej. 20260808_101419).
    external_reference = Column(String(100))

    # Doctor principal a cargo de este paciente en general — puede
    # diferir del doctor asignado a un tratamiento específico (por
    # ejemplo, un especialista de ortodoncia distinto al doctor de
    # cabecera), útil tanto para clínicas con un solo doctor que hace
    # todo como para las que dividen por especialidad.
    primary_dentist_id = Column(String(36), ForeignKey("users.id"))

    medical_notes = Column(Text)  # alergias, condiciones relevantes, etc.

    created_at = Column(DateTime, default=datetime.utcnow)
    is_active = Column(Boolean, default=True)

    clinic = relationship("Clinic", back_populates="patients")
    appointments = relationship("Appointment", back_populates="patient")
    invoices = relationship("Invoice", back_populates="patient")
    xray_studies = relationship("XRayStudy", back_populates="patient")
    clinical_notes = relationship("ClinicalNote", back_populates="patient")


# ---------------------------------------------------------------------------
# Citas
# ---------------------------------------------------------------------------

class AppointmentStatus(str, enum.Enum):
    SCHEDULED = "scheduled"
    CONFIRMED = "confirmed"
    IN_PROGRESS = "in_progress"
    COMPLETED = "completed"
    CANCELLED = "cancelled"
    NO_SHOW = "no_show"


class Appointment(Base):
    __tablename__ = "appointments"

    id = Column(String(36), primary_key=True, default=gen_uuid)
    clinic_id = Column(String(36), ForeignKey("clinics.id"), nullable=False)
    patient_id = Column(String(36), ForeignKey("patients.id"), nullable=False)
    dentist_id = Column(String(36), ForeignKey("users.id"), nullable=False)

    starts_at = Column(DateTime, nullable=False)
    ends_at = Column(DateTime, nullable=False)
    reason = Column(String(300))
    status = Column(Enum(AppointmentStatus), default=AppointmentStatus.SCHEDULED)
    notes = Column(Text)

    created_at = Column(DateTime, default=datetime.utcnow)

    clinic = relationship("Clinic", back_populates="appointments")
    patient = relationship("Patient", back_populates="appointments")
    dentist = relationship("User")


# ---------------------------------------------------------------------------
# Facturación
# ---------------------------------------------------------------------------

class InvoiceStatus(str, enum.Enum):
    DRAFT = "draft"
    ISSUED = "issued"
    PAID = "paid"
    PARTIALLY_PAID = "partially_paid"
    CANCELLED = "cancelled"


class Invoice(Base):
    __tablename__ = "invoices"

    id = Column(String(36), primary_key=True, default=gen_uuid)
    clinic_id = Column(String(36), ForeignKey("clinics.id"), nullable=False)
    patient_id = Column(String(36), ForeignKey("patients.id"), nullable=False)

    folio = Column(String(50))  # folio/serie de facturación
    status = Column(Enum(InvoiceStatus), default=InvoiceStatus.DRAFT)
    subtotal = Column(Numeric(12, 2), default=0)
    tax = Column(Numeric(12, 2), default=0)
    total = Column(Numeric(12, 2), default=0)
    issued_at = Column(DateTime)
    paid_at = Column(DateTime)
    notes = Column(Text)

    created_at = Column(DateTime, default=datetime.utcnow)

    clinic = relationship("Clinic", back_populates="invoices")
    patient = relationship("Patient", back_populates="invoices")
    items = relationship("InvoiceItem", back_populates="invoice", cascade="all, delete-orphan")


class InvoiceItem(Base):
    __tablename__ = "invoice_items"

    id = Column(String(36), primary_key=True, default=gen_uuid)
    invoice_id = Column(String(36), ForeignKey("invoices.id"), nullable=False)

    description = Column(String(300), nullable=False)  # ej. "Limpieza dental"
    tooth_number = Column(Integer)  # opcional: procedimiento ligado a un diente
    quantity = Column(Numeric(10, 2), default=1)
    unit_price = Column(Numeric(12, 2), nullable=False)
    total = Column(Numeric(12, 2), nullable=False)

    invoice = relationship("Invoice", back_populates="items")


# ---------------------------------------------------------------------------
# Odontograma / Serie radiográfica (equivalente a la pantalla de Runyes)
# ---------------------------------------------------------------------------

class DentitionType(str, enum.Enum):
    PERMANENT = "permanent"   # "Diente" en la pantalla de Runyes
    DECIDUOUS = "deciduous"   # "Diente Infantil"


class Quadrant(str, enum.Enum):
    UPPER_RIGHT = "upper_right"   # 1-8   (dientes permanentes, sistema universal)
    UPPER_LEFT = "upper_left"     # 9-16
    LOWER_LEFT = "lower_left"     # 17-24
    LOWER_RIGHT = "lower_right"   # 25-32


class ImageType(str, enum.Enum):
    XRAY = "xray"      # Radiografía (sensor de rayos X)
    PHOTO = "photo"    # Foto (cámara intraoral)
    VIDEO = "video"    # Video corto (cámara intraoral)


class XRayStudy(Base):
    """
    Una sesión de captura radiográfica completa (una 'serie'), equivalente
    a la carpeta de 18 imágenes que se ve en la pantalla de Runyes
    (Paciente ID, fecha, y el set de posiciones 1-18).
    """
    __tablename__ = "xray_studies"

    id = Column(String(36), primary_key=True, default=gen_uuid)
    clinic_id = Column(String(36), ForeignKey("clinics.id"), nullable=False)
    patient_id = Column(String(36), ForeignKey("patients.id"), nullable=False)
    taken_by_id = Column(String(36), ForeignKey("users.id"))

    dentition_type = Column(Enum(DentitionType), default=DentitionType.PERMANENT)
    description = Column(String(300))  # ej. "18 pictures" en la captura original
    taken_at = Column(DateTime, default=datetime.utcnow)
    sensor_brand = Column(String(50))  # "Runyes", "Woodpecker", "DabiAtlante", etc.

    clinic = relationship("Clinic")
    patient = relationship("Patient", back_populates="xray_studies")
    taken_by = relationship("User")
    slots = relationship(
        "ToothImageSlot", back_populates="study", cascade="all, delete-orphan"
    )


class ToothImageSlot(Base):
    """
    Una captura concreta para un diente dentro de una sesión: puede ser
    una radiografía o una foto — ambas pueden existir para el mismo
    diente en la misma sesión (image_type las distingue), y el mismo
    diente puede volver a capturarse en sesiones futuras (cada sesión es
    un XRayStudy nuevo), formando un historial en el tiempo.
    """
    __tablename__ = "tooth_image_slots"
    __table_args__ = (
        UniqueConstraint("study_id", "tooth_number", "image_type", name="uq_study_tooth_type"),
    )

    id = Column(String(36), primary_key=True, default=gen_uuid)
    study_id = Column(String(36), ForeignKey("xray_studies.id"), nullable=False)

    tooth_number = Column(Integer, nullable=False)  # 1-32 (o 51-85 en temporales)
    quadrant = Column(Enum(Quadrant), nullable=False)
    image_type = Column(Enum(ImageType), nullable=False, default=ImageType.XRAY)
    is_captured = Column(Boolean, default=False)  # equivale al checkbox marcado

    image_url = Column(String(500))  # ruta/URL en el almacenamiento (bucket)
    image_order = Column(Integer)  # posición 1-18 tal como en la miniatura original
    captured_at = Column(DateTime)

    study = relationship("XRayStudy", back_populates="slots")


# ---------------------------------------------------------------------------
# Notas clínicas / historial (además del odontograma radiográfico)
# ---------------------------------------------------------------------------

class ClinicalNote(Base):
    __tablename__ = "clinical_notes"

    id = Column(String(36), primary_key=True, default=gen_uuid)
    patient_id = Column(String(36), ForeignKey("patients.id"), nullable=False)
    author_id = Column(String(36), ForeignKey("users.id"), nullable=False)

    tooth_number = Column(Integer)  # opcional: nota ligada a un diente específico
    content = Column(Text, nullable=False)
    created_at = Column(DateTime, default=datetime.utcnow)

    patient = relationship("Patient", back_populates="clinical_notes")
    author = relationship("User")


# ---------------------------------------------------------------------------
# Tratamientos: planes largos de varias sesiones (ortodoncia, brackets,
# etc.), con costo estimado, catálogo reutilizable ("centro de costos"),
# y sesiones que se programan automáticamente como citas.
# ---------------------------------------------------------------------------

class TreatmentStatus(str, enum.Enum):
    IN_PROGRESS = "in_progress"
    COMPLETED = "completed"
    CANCELLED = "cancelled"


class SessionStatus(str, enum.Enum):
    PENDING = "pending"
    COMPLETED = "completed"
    CANCELLED = "cancelled"


class TreatmentType(Base):
    """
    Catálogo de tipos de tratamiento de la clínica (el 'centro de
    costos'): p. ej. "Ortodoncia con brackets", con su número de
    sesiones y costo por defecto, reutilizable para cualquier paciente.
    """
    __tablename__ = "treatment_types"

    id = Column(String(36), primary_key=True, default=gen_uuid)
    clinic_id = Column(String(36), ForeignKey("clinics.id"), nullable=False)

    name = Column(String(150), nullable=False)
    description = Column(Text)
    default_session_count = Column(Integer, default=1)
    default_interval_days = Column(Integer, default=30)  # cada cuántos días la siguiente sesión
    default_cost = Column(Numeric(12, 2))
    is_active = Column(Boolean, default=True)

    clinic = relationship("Clinic")


class Treatment(Base):
    """Un plan de tratamiento concreto asignado a un paciente."""
    __tablename__ = "treatments"

    id = Column(String(36), primary_key=True, default=gen_uuid)
    clinic_id = Column(String(36), ForeignKey("clinics.id"), nullable=False)
    patient_id = Column(String(36), ForeignKey("patients.id"), nullable=False)
    dentist_id = Column(String(36), ForeignKey("users.id"), nullable=False)
    treatment_type_id = Column(String(36), ForeignKey("treatment_types.id"))

    name = Column(String(150), nullable=False)  # copiado del tipo, o personalizado
    estimated_cost = Column(Numeric(12, 2))
    total_sessions = Column(Integer, nullable=False, default=1)
    interval_days = Column(Integer, nullable=False, default=30)
    start_date = Column(DateTime, nullable=False)
    status = Column(Enum(TreatmentStatus), default=TreatmentStatus.IN_PROGRESS)
    notes = Column(Text)

    created_at = Column(DateTime, default=datetime.utcnow)

    clinic = relationship("Clinic")
    patient = relationship("Patient")
    dentist = relationship("User")
    treatment_type = relationship("TreatmentType")
    sessions = relationship(
        "TreatmentSession", back_populates="treatment",
        cascade="all, delete-orphan", order_by="TreatmentSession.session_number",
    )


class TreatmentSession(Base):
    """
    Una sesión/visita dentro de un tratamiento largo — se genera
    automáticamente al crear el tratamiento, junto con su cita
    correspondiente en la agenda.
    """
    __tablename__ = "treatment_sessions"

    id = Column(String(36), primary_key=True, default=gen_uuid)
    treatment_id = Column(String(36), ForeignKey("treatments.id"), nullable=False)
    appointment_id = Column(String(36), ForeignKey("appointments.id"))

    session_number = Column(Integer, nullable=False)
    scheduled_date = Column(DateTime, nullable=False)
    status = Column(Enum(SessionStatus), default=SessionStatus.PENDING)
    completed_at = Column(DateTime)
    notes = Column(Text)

    treatment = relationship("Treatment", back_populates="sessions")
    appointment = relationship("Appointment")
