@echo off
REM ============================================================
REM  Script maestro: revisa/instala lo que falte, y compila TODO
REM  hasta dejar listo el instalador final.
REM
REM  Correr desde la raíz del proyecto (dental-suite\) en Windows,
REM  como Administrador (clic derecho > "Ejecutar como administrador"
REM  en cmd o PowerShell) — hace falta para instalar herramientas
REM  del sistema si llegan a faltar.
REM ============================================================

echo.
echo [0/3] Revisando herramientas necesarias...

REM ---- .NET SDK ----
where dotnet >nul 2>nul
if %errorlevel% neq 0 goto :install_dotnet
dotnet --list-sdks 2>nul | findstr /r "^8\." >nul
if %errorlevel% neq 0 goto :install_dotnet
echo   - .NET 8 SDK: OK
goto :check_innosetup

:install_dotnet
echo   - .NET 8 SDK: no encontrado. Instalando con winget...
where winget >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: no se encontró "winget" en esta PC para instalar el
    echo .NET SDK automáticamente. Instálalo a mano desde:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo y vuelve a correr este script.
    pause
    exit /b 1
)
winget install --id Microsoft.DotNet.SDK.8 -e --silent --accept-package-agreements --accept-source-agreements
if %errorlevel% neq 0 (
    echo ERROR: winget no pudo instalar el .NET SDK. Instálalo a mano desde:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    pause
    exit /b 1
)
REM Añadir la ruta típica al PATH de esta sesión, para no tener que
REM cerrar y volver a abrir la terminal.
set "PATH=%PATH%;C:\Program Files\dotnet"
where dotnet >nul 2>nul
if %errorlevel% neq 0 (
    echo AVISO: el .NET SDK se instaló, pero esta terminal no lo detecta
    echo todavía. Cierra esta ventana, abre una nueva, y vuelve a correr
    echo build_all.bat.
    pause
    exit /b 1
)
echo   - .NET 8 SDK: instalado correctamente.

:check_innosetup
REM ---- Inno Setup ----
REM Revisamos 3 formas distintas de encontrarlo, porque winget a veces
REM no agrega su carpeta al PATH del sistema aunque sí lo instale bien.
where iscc >nul 2>nul
if %errorlevel% equ 0 goto :innosetup_ok

if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "PATH=%PATH%;C:\Program Files (x86)\Inno Setup 6"
    goto :innosetup_ok
)
if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "PATH=%PATH%;C:\Program Files\Inno Setup 6"
    goto :innosetup_ok
)
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" (
    set "PATH=%PATH%;%LOCALAPPDATA%\Programs\Inno Setup 6"
    goto :innosetup_ok
)

echo   - Inno Setup: no encontrado. Instalando con winget...
where winget >nul 2>nul
if %errorlevel% neq 0 (
    echo ERROR: no se encontró "winget" en esta PC para instalar Inno
    echo Setup automáticamente. Instálalo a mano desde:
    echo https://jrsoftware.org/isinfo.php
    echo y vuelve a correr este script.
    pause
    exit /b 1
)
winget install --id JRSoftware.InnoSetup -e --silent --accept-package-agreements --accept-source-agreements

REM Sin importar si winget dijo "instalado" o "ya estaba instalado",
REM revisamos de nuevo en las rutas conocidas antes de rendirnos.
if exist "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" (
    set "PATH=%PATH%;C:\Program Files (x86)\Inno Setup 6"
    goto :innosetup_ok
)
if exist "C:\Program Files\Inno Setup 6\ISCC.exe" (
    set "PATH=%PATH%;C:\Program Files\Inno Setup 6"
    goto :innosetup_ok
)
if exist "%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe" (
    set "PATH=%PATH%;%LOCALAPPDATA%\Programs\Inno Setup 6"
    goto :innosetup_ok
)
where iscc >nul 2>nul
if %errorlevel% equ 0 goto :innosetup_ok

echo ERROR: Inno Setup parece estar instalado pero no se pudo localizar
echo ISCC.exe automáticamente. Ábrelo a mano: busca "Inno Setup Compiler"
echo en el menú inicio, abre installer\installer.iss ahí, y compílalo
echo con el botón "Compile" o la tecla F9.
pause
exit /b 1

:innosetup_ok
echo   - Inno Setup: OK

:prereqs_done

echo.
echo [1/3] Preparando el runtime portátil de Python (para el modo "solo esta PC")...
cd backend
call prepare_python_runtime.bat
if not exist python-runtime\pythonw.exe (
    echo ERROR: no se generó el runtime portátil. Abortando.
    pause
    exit /b 1
)
cd ..

echo.
echo [2/3] Publicando el cliente WPF como .exe autocontenido...
cd windows-client-prototype\DentalSuiteClient
call publish.bat
if not exist publish\DentalSuiteClient.exe (
    echo ERROR: no se generó DentalSuiteClient.exe. Abortando.
    pause
    exit /b 1
)
cd ..\..

echo.
echo [3/3] Generando el instalador final con Inno Setup...
cd installer
iscc installer.iss
cd ..
if not exist installer\installer_output\DentalSuiteSetup.exe (
    echo ERROR: Inno Setup no generó el instalador. Revisa los mensajes arriba.
    pause
    exit /b 1
)

echo.
echo ============================================================
echo  LISTO. Instalador generado en:
echo  installer\installer_output\DentalSuiteSetup.exe
echo ============================================================

echo.
pause
