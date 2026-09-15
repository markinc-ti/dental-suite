from decimal import Decimal

from fastapi import APIRouter, Depends, HTTPException
from sqlalchemy.orm import Session

from .. import models, schemas
from ..database import get_db

router = APIRouter(prefix="/invoices", tags=["invoices"])

TAX_RATE = Decimal("0.16")  # IVA México; ajustar por clínica si aplica


@router.post("", response_model=schemas.InvoiceOut)
def create_invoice(
    payload: schemas.InvoiceCreate,
    clinic_id: str,
    db: Session = Depends(get_db),
):
    invoice = models.Invoice(
        clinic_id=clinic_id,
        patient_id=payload.patient_id,
        notes=payload.notes,
        status=models.InvoiceStatus.DRAFT,
    )

    subtotal = Decimal("0")
    for item_in in payload.items:
        line_total = item_in.unit_price * item_in.quantity
        subtotal += line_total
        invoice.items.append(
            models.InvoiceItem(
                description=item_in.description,
                tooth_number=item_in.tooth_number,
                quantity=item_in.quantity,
                unit_price=item_in.unit_price,
                total=line_total,
            )
        )

    invoice.subtotal = subtotal
    invoice.tax = subtotal * TAX_RATE
    invoice.total = invoice.subtotal + invoice.tax

    db.add(invoice)
    db.commit()
    db.refresh(invoice)
    return invoice


@router.get("", response_model=list[schemas.InvoiceOut])
def list_invoices(
    clinic_id: str,
    patient_id: str | None = None,
    status: models.InvoiceStatus | None = None,
    db: Session = Depends(get_db),
):
    query = db.query(models.Invoice).filter(models.Invoice.clinic_id == clinic_id)
    if patient_id:
        query = query.filter(models.Invoice.patient_id == patient_id)
    if status:
        query = query.filter(models.Invoice.status == status)
    return query.order_by(models.Invoice.created_at.desc()).all()


@router.post("/{invoice_id}/mark-paid", response_model=schemas.InvoiceOut)
def mark_invoice_paid(invoice_id: str, db: Session = Depends(get_db)):
    from datetime import datetime

    invoice = db.query(models.Invoice).filter(models.Invoice.id == invoice_id).first()
    if not invoice:
        raise HTTPException(status_code=404, detail="Factura no encontrada")
    invoice.status = models.InvoiceStatus.PAID
    invoice.paid_at = datetime.utcnow()
    db.commit()
    db.refresh(invoice)
    return invoice
