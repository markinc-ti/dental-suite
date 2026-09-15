from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel, field_validator
from sqlalchemy.orm import Session

from .. import models
from ..database import get_db
from ..security import hash_password

router = APIRouter(prefix="/users", tags=["users"])


class UserCreate(BaseModel):
    full_name: str
    username: str  # correo o teléfono celular — lo que se use para iniciar sesión
    password: str
    role: models.UserRole = models.UserRole.DENTIST
    phone: str | None = None
    specialty: str | None = None
    professional_license: str | None = None

    @field_validator("username")
    @classmethod
    def username_not_blank(cls, value: str) -> str:
        value = value.strip()
        if not value:
            raise ValueError("El usuario (correo o teléfono) no puede estar vacío.")
        return value


class UserUpdate(BaseModel):
    full_name: str | None = None
    phone: str | None = None
    specialty: str | None = None
    professional_license: str | None = None
    is_active: bool | None = None


class UserOut(BaseModel):
    id: str
    clinic_id: str
    full_name: str
    username: str
    role: models.UserRole
    phone: str | None
    specialty: str | None
    professional_license: str | None
    is_active: bool

    class Config:
        from_attributes = True


@router.get("", response_model=list[UserOut])
def list_users(
    clinic_id: str,
    role: models.UserRole | None = None,
    db: Session = Depends(get_db),
):
    """
    El cliente de escritorio llama esto para llenar el listado de
    doctores (filtrando role=dentist), para revisar si ya existe un
    administrador (role=admin) en el primer arranque, o para el login.
    """
    query = db.query(models.User).filter(
        models.User.clinic_id == clinic_id, models.User.is_active == True  # noqa: E712
    )
    if role:
        query = query.filter(models.User.role == role)
    return query.order_by(models.User.full_name).all()


@router.post("", response_model=UserOut)
def create_user(payload: UserCreate, clinic_id: str, db: Session = Depends(get_db)):
    existing = db.query(models.User).filter(models.User.username == payload.username).first()
    if existing:
        raise HTTPException(
            status_code=400, detail="Ya existe un usuario con ese correo o teléfono."
        )

    user = models.User(
        clinic_id=clinic_id,
        full_name=payload.full_name,
        username=payload.username,
        role=payload.role,
        phone=payload.phone,
        specialty=payload.specialty,
        professional_license=payload.professional_license,
        password_hash=hash_password(payload.password),
    )
    db.add(user)
    db.commit()
    db.refresh(user)
    return user


@router.put("/{user_id}", response_model=UserOut)
def update_user(user_id: str, payload: UserUpdate, db: Session = Depends(get_db)):
    user = db.query(models.User).filter(models.User.id == user_id).first()
    if not user:
        raise HTTPException(status_code=404, detail="Doctor no encontrado")
    for field, value in payload.model_dump(exclude_unset=True).items():
        setattr(user, field, value)
    db.commit()
    db.refresh(user)
    return user


@router.delete("/{user_id}")
def deactivate_user(user_id: str, db: Session = Depends(get_db)):
    user = db.query(models.User).filter(models.User.id == user_id).first()
    if not user:
        raise HTTPException(status_code=404, detail="Doctor no encontrado")
    user.is_active = False
    db.commit()
    return {"ok": True}
