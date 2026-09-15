"""
Hash y verificación de contraseñas con PBKDF2-HMAC-SHA256. Se usa la
librería estándar de Python (hashlib) a propósito, para no depender de
paquetes externos como bcrypt dentro del runtime portátil — con 200,000
iteraciones es una elección razonable y ampliamente recomendada cuando
no se quiere agregar una dependencia nativa adicional.

Formato de almacenamiento: "<salt_b64>$<hash_b64>"
"""

import base64
import hashlib
import hmac
import os

_ITERATIONS = 200_000


def hash_password(password: str) -> str:
    salt = os.urandom(16)
    derived = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, _ITERATIONS)
    return f"{base64.b64encode(salt).decode()}${base64.b64encode(derived).decode()}"


def verify_password(password: str, stored_hash: str) -> bool:
    try:
        salt_b64, hash_b64 = stored_hash.split("$", 1)
        salt = base64.b64decode(salt_b64)
        expected = base64.b64decode(hash_b64)
    except (ValueError, TypeError):
        return False

    derived = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, _ITERATIONS)
    return hmac.compare_digest(derived, expected)
