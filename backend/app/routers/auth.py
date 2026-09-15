from fastapi import APIRouter, Depends, HTTPException
from pydantic import BaseModel
from sqlalchemy.orm import Session

from .. import models
from ..database import get_db
from ..security import verify_password

router = APIRouter(prefix="/auth", tags=["auth"])


class LoginRequest(BaseModel):
    username: str  # correo o teléfono celular, lo que se haya usado al crear la cuenta
    password: str


class LoginResponse(BaseModel):
    id: str
    clinic_id: str
    full_name: str
    username: str
    role: models.UserRole

    class Config:
        from_attributes = True


@router.post("/login", response_model=LoginResponse)
def login(payload: LoginRequest, db: Session = Depends(get_db)):
    user = db.query(models.User).filter(models.User.username == payload.username).first()

    # Mismo mensaje genérico tanto si el usuario no existe como si la
    # contraseña es incorrecta — no darle pistas a quien intenta entrar
    # sobre cuál de las dos cosas falló.
    invalid_credentials = HTTPException(status_code=401, detail="Usuario o contraseña incorrectos.")

    if not user or not user.is_active:
        raise invalid_credentials

    if not user.password_hash or not verify_password(payload.password, user.password_hash):
        raise invalid_credentials

    return user
