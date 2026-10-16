"""
Auto-actualización del esquema de la base de datos.

`Base.metadata.create_all()` crea las tablas que faltan, pero NO agrega
columnas nuevas a tablas que ya existen. Sin esto, cada vez que una
versión nueva de Dental Suite agrega un campo (por ejemplo a pacientes),
la base de datos de una instalación anterior deja de funcionar y la
única "solución" sería borrarla y volver a capturar todo.

Aquí se compara cada tabla con el modelo y se agregan las columnas que
falten (siempre como opcionales, que es lo único que SQLite permite
agregar sin reconstruir la tabla). Nunca se borra ni se modifica nada
existente.
"""

import logging

from sqlalchemy import inspect, text
from sqlalchemy.engine import Engine
from sqlalchemy.sql.schema import MetaData

log = logging.getLogger("dentalsuite.schema")


def add_missing_columns(engine: Engine, metadata: MetaData) -> list[str]:
    added: list[str] = []
    inspector = inspect(engine)
    existing_tables = set(inspector.get_table_names())
    preparer = engine.dialect.identifier_preparer

    with engine.begin() as conn:
        for table in metadata.sorted_tables:
            if table.name not in existing_tables:
                continue  # create_all ya la creó completa

            existing_cols = {c["name"] for c in inspector.get_columns(table.name)}
            for column in table.columns:
                if column.name in existing_cols:
                    continue

                col_type = column.type.compile(dialect=engine.dialect)
                stmt = (
                    f"ALTER TABLE {preparer.quote(table.name)} "
                    f"ADD COLUMN {preparer.quote(column.name)} {col_type}"
                )
                conn.execute(text(stmt))
                added.append(f"{table.name}.{column.name}")
                log.info("Columna agregada: %s.%s (%s)", table.name, column.name, col_type)

    return added
