; Instalador de Dental Suite, con Inno Setup — la misma herramienta que
; usa el instalador original de Runyes (se identificó al extraerlo:
; "此安装程序由 Inno Setup 构建" / "This installer was built with Inno Setup").
;
; Cómo compilarlo:
;   1. Instala Inno Setup (jrsoftware.org/isinfo.php) — gratis.
;   2. Corre publish.bat (cliente) — ver README para el orden completo.
;      O simplemente corre build_all.bat desde la raíz del proyecto,
;      que hace todo.
;   3. Abre este archivo con el editor de Inno Setup y presiona "Compile"
;      (o desde línea de comandos: iscc installer.iss).
;   4. El instalador final queda en installer_output\DentalSuiteSetup.exe
;
; El instalador incluye el backend local (Python portátil) para quien
; quiera usar Dental Suite solo en esta PC, sin conectar nada más. Quien
; sí quiera conectar el celular u otra PC puede en cambio apuntar la
; app a un servidor central (por ejemplo Render, con PostgreSQL) — la
; app pregunta cuál de las dos formas quieres la primera vez que abre.

#define MyAppName "Dental Suite"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "Mark Inc"
#define MyAppExeName "DentalSuiteClient.exe"

[Setup]
AppId={{B9C2E4A0-5F1E-4C9E-9B7D-3D4A1E8F2C11}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=installer_output
OutputBaseFilename=DentalSuiteSetup
Compression=lzma
SolidCompression=yes
ArchitecturesInstallIn64BitMode=
; El cliente WPF es de 32 bits (por las DLLs del sensor Runyes).
PrivilegesRequired=admin
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Files]
; Cliente WPF publicado como single-file (ver publish.bat)
Source: "..\windows-client-prototype\DentalSuiteClient\publish\DentalSuiteClient.exe"; DestDir: "{app}"; Flags: ignoreversion

; Backend local (Python portátil + código) — solo se usa si la persona
; elige "Usar solo esta computadora" en vez de conectar un servidor en
; la nube. Mismo patrón que la carpeta "jre" del instalador de Ai-Dental
; (todo junto, sin que el usuario tenga que instalar Python aparte).
Source: "..\backend\python-runtime\*"; DestDir: "{app}\backend\python-runtime"; Flags: ignoreversion recursesubdirs createallsubdirs

; DLLs del sensor Runyes (sensor_io.dll, ImageCorrect.dll, Scilib21.dll):
; son propiedad de Runyes y vienen en SU instalador — no se redistribuyen
; en este repositorio. ANTES de compilar este instalador, cópialas a mano
; desde el instalador original de Runyes (carpeta app\Sensor\) a
; installer\sensor-dlls\ (junto a este script). Tienen que quedar en la
; raíz de la instalación ({app}), junto a DentalSuiteClient.exe, porque
; así es como Windows las busca cuando el código llama a P/Invoke.
Source: "sensor-dlls\*"; DestDir: "{app}"; Flags: ignoreversion

; DLLs del sensor/cámara Woodpecker (opción "Cámara intraoral" del
; selector de sensor), extraídas de tu propio instalador de Ai-Dental.
Source: "woodpecker-fpdsys-dlls\*"; DestDir: "{app}"; Flags: ignoreversion
Source: "woodpecker-dlls\*"; DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Desinstalar {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Abrir {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Se deja %LOCALAPPDATA%\DentalSuite intacto a propósito al desinstalar
; (ahí vive, en modo "solo esta PC", la base de datos SQLite con los
; pacientes — no queremos borrarla sin que la persona lo pida
; explícitamente).

[Code]
// Antes de copiar archivos, cierra cualquier instancia previa del
// cliente — si queda corriendo, Windows no deja reemplazar sus DLLs
// (error típico: "DeleteFile falló; código 5. Acceso denegado") y la
// instalación se traba pidiendo Reintentar/Omitir/Cancelar sin explicar
// por qué.
procedure CloseRunningInstance();
var
  ResultCode: Integer;
begin
  Exec('taskkill.exe', '/F /IM DentalSuiteClient.exe', '', SW_HIDE,
    ewWaitUntilTerminated, ResultCode);
  // Un margen breve para que Windows libere los archivos tras cerrar
  // el proceso, antes de que el instalador intente reemplazarlos.
  Sleep(1000);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    CloseRunningInstance();
end;
