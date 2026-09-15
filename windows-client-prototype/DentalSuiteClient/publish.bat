@echo off
REM Publica el cliente WPF como ejecutable autocontenido para win-x86
REM (arquitectura x86 porque el sensor Runyes usa DLLs de 32 bits).
REM Esto incluye el runtime de .NET dentro del propio .exe, para que la
REM PC del cliente NO necesite tener .NET instalado.

REM Borramos la carpeta de publicación anterior primero: si dotnet
REM publish falla a medias, un .exe viejo que quede ahí de una
REM compilación anterior podría hacer parecer que todo salió bien
REM cuando en realidad el instalador está empaquetando código viejo.
if exist publish rmdir /s /q publish

dotnet publish ^
  -c Release ^
  -r win-x86 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -o publish

if not exist publish\DentalSuiteClient.exe (
    echo.
    echo ERROR: dotnet publish falló ^(revisa los mensajes en rojo arriba^).
    echo No se generó publish\DentalSuiteClient.exe.
    pause
    exit /b 1
)

echo.
echo Listo. El ejecutable queda en publish\DentalSuiteClient.exe

echo.
pause
