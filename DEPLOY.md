# Desplegar el backend de Dental Suite en Render

Esto pone el backend en internet (como ya hiciste con tickets-ti), para
que la app de escritorio, el navegador, y la futura app móvil hablen
todos con el mismo servidor — en vez de que cada PC tenga su propia
base de datos aislada.

## 1. Crear la base de datos PostgreSQL

1. En el dashboard de Render, **New +** → **PostgreSQL**.
2. Nombre: `dental-suite-db` (o el que quieras). Plan: el gratuito sirve
   para empezar.
3. Cuando termine de crearse, copia el valor de **Internal Database
   URL** (lo vas a necesitar en el paso 3).

## 2. Subir el código del backend

Si el backend (`dental-suite/backend/`) no está todavía en un
repositorio de GitHub, súbelo primero — Render despliega desde ahí,
igual que con tickets-ti.

## 3. Crear el servicio web

1. **New +** → **Web Service**.
2. Conecta el repositorio del backend.
3. **Root Directory**: `backend` (si el repo tiene más carpetas además
   del backend).
4. **Runtime**: Python 3.
5. **Build Command**:
   ```
   pip install -r requirements.txt
   ```
6. **Start Command**:
   ```
   uvicorn app.main:app --host 0.0.0.0 --port $PORT
   ```
7. **Variables de entorno** (pestaña Environment):
   - `DATABASE_URL` → pega ahí el **Internal Database URL** del paso 1.

8. Crea el servicio. Render le va a asignar una dirección como:
   ```
   https://dental-suite-xxxx.onrender.com
   ```
   Esa es la dirección que vas a pegar en la app de escritorio (te la
   va a pedir la primera vez que la abras en cada PC) y en la app
   móvil.

## 4. Confirmar que quedó funcionando

Abre en el navegador `https://dental-suite-xxxx.onrender.com/health` —
debe responder:
```json
{"status": "ok"}
```

## Notas

- **Plan gratuito de Render**: el servicio "se duerme" tras un rato sin
  uso, y la primera petición después de dormido tarda unos segundos
  extra en responder mientras despierta — normal, no es un error.
- **CORS** ya está abierto (`allow_origins=["*"]`) para que tanto la
  app de escritorio como la app móvil puedan conectarse sin problema.
  Si más adelante quieres restringirlo a dominios específicos, es en
  `app/main.py`.
- **Migraciones**: hoy la tabla se crea sola con `create_all` al
  arrancar (bien para empezar). Si el esquema crece mucho más adelante,
  conviene migrar a Alembic para no perder datos al cambiar columnas.
