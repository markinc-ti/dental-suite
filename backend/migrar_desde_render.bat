@echo off
chcp 65001 >nul
REM ============================================================
REM  Pasa los datos que estaban en el servidor de Render a ESTA PC,
REM  para que Dental Suite funcione sin internet.
REM  Se instala junto al programa; doble clic para correrlo.
REM ============================================================

echo.
echo  ===  Dental Suite: traer datos de la nube a esta PC  ===
echo.
echo  Necesitas la "External Database URL" de tu base en Render:
echo    Render ^> tu PostgreSQL ^> Connect ^> External
echo  Empieza con postgresql://...
echo.
set /p ORIGEN=Pega aqui la URL y presiona Enter:
if "%ORIGEN%"=="" (
    echo No se escribio ninguna URL. Cancelado.
    pause
    exit /b 1
)

echo.
echo Cerrando Dental Suite para que la base de datos no este en uso...
taskkill /F /IM DentalSuiteClient.exe >nul 2>nul
powershell -NoProfile -Command "Get-Process pythonw -ErrorAction SilentlyContinue | Where-Object { $_.Path -like '%~dp0*' } | Stop-Process -Force"
timeout /t 2 >nul

cd /d "%~dp0backend\python-runtime"
python.exe migrar_nube_a_local.py --origen "%ORIGEN%" --nube https://dental-suite-k65o.onrender.com

echo.
echo Ya puedes abrir Dental Suite normalmente.
pause
