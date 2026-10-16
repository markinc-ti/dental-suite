@echo off
REM ============================================================
REM  Prepara una carpeta de Python PORTATIL con el backend y sus
REM  dependencias ya instaladas adentro — el mismo patrón que la
REM  carpeta "jre" del instalador de Ai-Dental, pero con Python.
REM
REM  Resultado: backend\python-runtime\  (self-contenida, se puede
REM  copiar tal cual dentro del instalador — la PC del cliente NO
REM  necesita tener Python instalado).
REM ============================================================

set PYTHON_VERSION=3.11.9
set EMBED_ZIP=python-%PYTHON_VERSION%-embed-amd64.zip
set EMBED_URL=https://www.python.org/ftp/python/%PYTHON_VERSION%/%EMBED_ZIP%
set RUNTIME_DIR=python-runtime

echo.
echo [1/6] Descargando Python %PYTHON_VERSION% embeddable...
if exist %EMBED_ZIP% del %EMBED_ZIP%
powershell -Command "Invoke-WebRequest -Uri '%EMBED_URL%' -OutFile '%EMBED_ZIP%'"
if not exist %EMBED_ZIP% (
    echo ERROR: no se pudo descargar Python embeddable. Revisa tu conexión.
    pause
    exit /b 1
)

echo.
echo [2/6] Extrayendo a %RUNTIME_DIR%\...
if exist %RUNTIME_DIR% rmdir /s /q %RUNTIME_DIR%
powershell -Command "Expand-Archive -Path '%EMBED_ZIP%' -DestinationPath '%RUNTIME_DIR%'"
del %EMBED_ZIP%

echo.
echo [3/6] Habilitando site-packages en el Python embeddable...
REM El embeddable trae desactivado "import site" por defecto (y sin
REM ruta a Lib\site-packages) — hay que habilitarlo para que pip y las
REM dependencias instaladas se puedan importar normalmente.
powershell -Command ^
  "$pth = Get-ChildItem '%RUNTIME_DIR%\python*._pth' | Select-Object -First 1; " ^
  "(Get-Content $pth.FullName) -replace '#import site','import site' | Set-Content $pth.FullName; " ^
  "Add-Content $pth.FullName 'Lib\site-packages'"

echo.
echo [4/6] Instalando pip dentro del runtime portátil...
if exist get-pip.py del get-pip.py
powershell -Command "Invoke-WebRequest -Uri 'https://bootstrap.pypa.io/get-pip.py' -OutFile 'get-pip.py'"
%RUNTIME_DIR%\python.exe get-pip.py --no-warn-script-location
del get-pip.py

echo.
echo [5/6] Instalando las dependencias del backend dentro del runtime...
%RUNTIME_DIR%\python.exe -m pip install --no-warn-script-location -r requirements.txt
if %errorlevel% neq 0 (
    echo ERROR instalando dependencias dentro del runtime portátil.
    pause
    exit /b 1
)

echo.
echo [6/6] Copiando el código del backend (app\ y run_server.py) al runtime...
xcopy /e /i /y app %RUNTIME_DIR%\app
copy /y run_server.py %RUNTIME_DIR%\run_server.py
copy /y migrar_nube_a_local.py %RUNTIME_DIR%\migrar_nube_a_local.py

echo.
echo ============================================================
echo  LISTO. Carpeta portátil generada en: backend\%RUNTIME_DIR%\
echo  Contiene Python + todas las dependencias + el código del
echo  backend. Se ejecuta con:
echo    python-runtime\pythonw.exe run_server.py
echo  (pythonw.exe = sin ventana de consola visible)
echo ============================================================

echo.
pause
