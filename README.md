# Dental Suite — arranque del proyecto

Software de gestión de clínica dental (pacientes, citas, facturación,
odontograma/estudios radiográficos) con soporte para sensores de rayos X
mediante una arquitectura de adaptadores por fabricante.

## Estructura

```
backend/                        Backend compartido (FastAPI + Postgres)
  app/
    models.py                   Modelo de datos completo
    schemas.py                  Validación de entrada/salida (Pydantic)
    database.py                 Conexión a Postgres/Neon
    main.py                     Punto de entrada de la API
    routers/
      patients.py
      appointments.py
      invoices.py
      studies.py                Odontograma / serie radiográfica

windows-client-prototype/
  RunyesSensorPrototype/        Prototipo de conexión al sensor Runyes
    IDentalSensor.cs            Interfaz común (patrón adaptador)
    RunyesSensorInterop.cs      P/Invoke sobre el SDK nativo de Runyes
    RunyesSensorAdapter.cs      Implementación del adaptador Runyes
    Program.cs                  Consola de prueba

  DentalSuiteClient/            Interfaz gráfica (WPF) del odontograma
    MainWindow.xaml             Layout: réplica de la pantalla de Runyes
    MainViewModel.cs            Arma la cuadrícula de 32 dientes
    Models/ToothSlot.cs         Una posición del odontograma
    Converters/                 Convertidores usados en el XAML
```

## Backend — cómo correrlo

```bash
cd backend
python -m venv venv
source venv/bin/activate  # En Windows: venv\Scripts\activate
pip install -r requirements.txt

export DATABASE_URL="postgresql://usuario:password@host:5432/dental_suite"
uvicorn app.main:app --reload
```

La API queda en `http://localhost:8000`, con documentación interactiva en
`http://localhost:8000/docs`.

**Pendiente antes de producción:**
- Autenticación real (JWT) — todavía no está implementada; los endpoints
  reciben `clinic_id` como parámetro para poder probarlos, pero en
  producción debe salir del token del usuario autenticado.
- Migraciones con Alembic en vez de `create_all` (que solo sirve para
  desarrollo).
- Subida de imágenes a un bucket (S3/Backblaze) — el modelo ya tiene el
  campo `image_url`, falta el endpoint de subida real.

## Prototipo Windows — cómo probarlo

**Nota:** `dotnet run`/`dotnet build` + ejecutar el .exe directamente
necesita el runtime x86 de .NET instalado (además del SDK, que suele
instalarse en x64) — si sale "You must install .NET to run this
application" con "Architecture: x86", corre:
```
winget install --id Microsoft.DotNet.Runtime.8 --architecture x86 -e
```
y abre una terminal nueva. Esto no afecta al instalador final de
Dental Suite (se publica self-contained, con su propio runtime
incluido) — solo hace falta para probar estos prototipos sueltos.


1. Copia estos archivos desde el instalador de Runyes (carpeta `app/Sensor/`
   dentro del instalador que ya analizamos) a la carpeta de salida del
   proyecto (`bin/Debug/net8.0/`), junto al `.exe` compilado:
   - `sensor_io.dll`
   - `ImageCorrect.dll`
   - `Scilib21.dll` (dependencia de las anteriores)
2. Conecta el sensor Runyes por USB a una PC Windows.
3. Compila y corre:
   ```
   dotnet build -p:Platform=x86
   dotnet run
   ```
4. El programa debe listar el dispositivo, esperar el disparo de rayos X,
   y guardar `captura_runyes.raw` con los bytes de la imagen capturada.

### ⚠️ Importante sobre las firmas P/Invoke

Los nombres de las funciones exportadas (`Sensor_OpenDevice`,
`Sensor_GetImageBits`, etc.) se confirmaron inspeccionando la tabla de
exportación real del DLL. **Los tipos de parámetro y su orden exacto no
vienen documentados por el fabricante** — se plantearon con las
convenciones más comunes en SDKs de sensores RVG (device handle como
puntero opaco, buffers de bytes con su tamaño, ancho/alto como enteros).

Es muy probable que al probar contra el sensor real algo no calce a la
primera (por ejemplo, que el ancho/alto venga en otro orden, o que
`Sensor_GetImageBits` espere un parámetro adicional). Es normal y
esperado — se ajusta iterando con el hardware real conectado, no hay
forma de confirmarlo sin el sensor físico en mano.

## Sensor Runyes — ya conectado de verdad

El adaptador `RunyesSensorAdapter` ya está enlazado al cliente WPF: doble
clic en una miniatura vacía dispara la captura real contra el sensor
conectado (ya no el aviso de antes). Las DLLs del sensor
(`sensor_io.dll`, `ImageCorrect.dll`, `Scilib21.dll`) ya vienen incluidas
en `installer\sensor-dlls\` — el instalador las coloca solas junto al
`.exe` del cliente, no hay que copiarlas a mano.

**Si el sensor no se detecta al probarlo:** lo más probable es que haya
que ajustar las firmas P/Invoke en `RunyesSensorInterop.cs` contra el
comportamiento real del hardware (ver advertencia en la sección
anterior) — es normal que la primera prueba no calce a la primera.

Para correr el cliente en modo desarrollo (sin pasar por el instalador
completo), hace falta compilar primero el proyecto del sensor y copiar
sus DLLs junto al ejecutable:

```
cd windows-client-prototype/DentalSuiteClient
dotnet build
copy ..\..\installer\sensor-dlls\*.dll bin\Debug\net8.0-windows\
dotnet run
```

## Próximos pasos sugeridos

1. Probar el prototipo de captura con el sensor Runyes físico y ajustar
   las firmas P/Invoke según el comportamiento real.
2. Conectar el prototipo de captura con el backend (subir automáticamente
   la imagen capturada al `POST /xray-studies/{id}/slots`).
3. Cuando llegue el segundo sensor (Dabi Atlante), crear
   `DabiAtlanteSensorAdapter : IDentalSensor` sin tocar nada de lo demás.
4. Generar el instalador único `.exe` — ver la siguiente sección.

## Generar el instalador único (.exe), paso a paso

Todo esto se corre **en una PC Windows** (no en Mac/Linux, porque tanto
el backend empaquetado como el cliente quedan compilados para Windows).

**Requisitos en esta PC:** .NET 8 SDK (con el workload de escritorio) e
Inno Setup. **Python NO hace falta tenerlo instalado** — `prepare_python_runtime.bat`
lo descarga por su cuenta como parte del runtime portátil.

```
1. build_all.bat
   → hace TODO en un solo paso: descarga el runtime portátil de Python,
     publica el cliente WPF, y genera el instalador final.
```

O paso a paso, si prefieres ver cada etapa por separado:

```
1. cd backend
   prepare_python_runtime.bat
   → descarga Python portátil, instala las dependencias adentro, y
     copia el código del backend. Genera backend\python-runtime\
     (self-contenida — la PC del cliente NO necesita Python instalado,
     igual que la carpeta "jre" que trae el instalador de Ai-Dental)

2. cd windows-client-prototype\DentalSuiteClient
   publish.bat
   → genera publish\DentalSuiteClient.exe

3. Instala Inno Setup (gratis, jrsoftware.org/isinfo.php)

4. Abre installer\installer.iss con Inno Setup y compílalo
   (o desde línea de comandos: iscc installer.iss)
   → genera installer\installer_output\DentalSuiteSetup.exe
```

Ese `DentalSuiteSetup.exe` final es el que le compartes a cualquier PC:
se instala con un asistente (español), crea accesos directos en
escritorio y menú inicio, y al abrir la app el backend arranca solo en
segundo plano — el usuario nunca ve una terminal ni configura nada.

**Nota sobre las DLLs del sensor:** por ser propiedad de Runyes, no las
incluyo dentro de este paquete. El script `installer.iss` tiene comentada
la línea para agregarlas — descoméntala una vez que las copies a
`installer\sensor-dlls\` (sacándolas del instalador original de Runyes,
carpeta `app\Sensor\`).
