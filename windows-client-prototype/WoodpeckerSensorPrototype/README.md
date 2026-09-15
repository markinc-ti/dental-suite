# Prototipo de sensor Woodpecker / DBA

Adaptador para el sensor Woodpecker/DBA usando `XVDetector.dll`,
extraído del instalador `Ai-Dental.rar` que compartiste (carpeta
`Ai-Dental-Client`).

## Diferencia clave con Runyes

`XVDetector.dll` tiene exports decorados al estilo clásico de stdcall
de 32 bits (`_XVD_InitDetector@4`) — el número después de la `@` es la
cantidad exacta de bytes de parámetros, así que el conteo de
parámetros de cada función es un hecho confirmado por el propio
compilador, no una suposición nuestra. Lo que sigue siendo una
suposición razonable (pendiente de confirmar con el hardware real) es
el tipo exacto de cada parámetro y el significado de los códigos de
retorno.

Además, este SDK entrega la imagen por **callback**
(`RegisterImageValidCallBack`), no por una función que hay que llamar
en bucle como en Runyes — el propio sensor avisa cuando la imagen está
lista.

## Antes de probar: copiar las DLLs necesarias

`XVDetector.dll` necesita estos archivos en la misma carpeta que el
`.exe` (ya están en `installer/woodpecker-dlls/`, extraídos de tu
propio instalador):

- `XVDetector.dll`
- `DataProc.dll`
- `opencv_world341.dll`

Si en la PC de prueba no abre por falta de runtime de Visual C++,
correr `VC_redist.x86.exe` (también incluido) una sola vez.

## Cómo probar

**Nota:** `dotnet run` necesita el runtime x86 de .NET instalado además
del SDK normal (que suele venir en x64) — si sale el error "You must
install .NET to run this application" con "Architecture: x86", instala:
```
winget install --id Microsoft.DotNet.Runtime.8 --architecture x86 -e
```
(y abre una terminal nueva después). Esto no afecta al instalador final
de Dental Suite — ese se publica self-contained, con su propio runtime
incluido; este paso extra solo hace falta para probar estos prototipos
sueltos con `dotnet run`.

```
cd windows-client-prototype/WoodpeckerSensorTestConsole
dotnet build
copy ..\..\installer\woodpecker-dlls\*.* bin\Debug\net8.0\
dotnet run
```

Con el sensor Woodpecker/DBA conectado por USB. El programa te va a
preguntar por la ruta de un archivo de calibración — si el proveedor
o el software original ya generó uno, indícalo aquí; si no lo tienes,
presiona Enter para omitirlo (es probable que sin calibración la
imagen salga inválida, igual que aprendimos con Runyes).

## Cosas que probablemente haya que ajustar

- **`XVD_InitDetector`**: se asume que el parámetro es un índice de
  dispositivo (int). Si no conecta, puede que en realidad espere otra
  cosa (por ejemplo un handle de ventana, dado que este SDK usa
  callbacks).
- **La firma del callback de imagen** (`ImageValidCallback`): se
  asume `(puntero_a_datos, ancho, alto)`. Si el callback nunca se
  dispara, o llega con valores sin sentido, este es el primer lugar a
  revisar — podría tener un parámetro adicional (por ejemplo, un
  puntero a contexto de usuario, patrón común en callbacks de C).
- **Profundidad de bits del píxel**: se asume 16 bits por píxel (igual
  que Runyes), sin confirmar contra el hardware real todavía.

## Segundo intento: FpdSys.dll (la ruta real que usa tu sensor E4S)

Con el depurador confirmamos que tu sensor específico (identificado en
la etiqueta como algo similar a `PF6440...`) es reconocido internamente
como modelo **E4S**, y que la app real usa la capa genérica
`FpdSys.dll` (que internamente crea el objeto `E4S.dll` sin que
nosotros necesitemos tocarlo directo) — no `XVDetector.dll` como se
pensó al principio.

**DLLs necesarias** (ya están en
`installer/woodpecker-fpdsys-dlls/`, extraídas de tu propio instalador):
`FpdSys.dll`, `DetFinder.dll`, `E4S.dll`, `ConnUSBfifo.dll`,
`CaliImpl.dll`, `isecurity.dll`, `fpd_boost.dll`, `fpd_yall.dll`,
`msvcp120.dll`, `msvcr120.dll`.

```
cd windows-client-prototype/WoodpeckerSensorTestConsole
dotnet build
copy ..\..\installer\woodpecker-fpdsys-dlls\*.dll bin\Debug\net8.0\
dotnet run
```

**Importante:** a diferencia de `XVDetector.dll`, los nombres
exportados de `FpdSys.dll` NO vienen decorados con `@N` — así que
volvemos a no tener certeza sobre la cantidad exacta de parámetros de
cada función. Es muy probable que la primera prueba falle o incluso
truene la consola. Si eso pasa, seguimos con el mismo depurador
(x32dbg) que ya tienes abierto y configurado, esta vez apuntando los
breakpoints a `Create`, `RegisterScanNotify`, `ScanOnce`,
`QueryImageBuf` y `GetImageFromBuf` dentro de `fpdsys.dll` en vez de
`XVDetector.dll`.


## Integración con el cliente WPF

Una vez validado el prototipo por consola, el patrón para conectarlo
a `DentalSuiteClient` es el mismo que con Runyes: agregar una
referencia de proyecto a `WoodpeckerSensorPrototype`, y elegir qué
`IDentalSensor` instanciar (Runyes o Woodpecker) según el sensor
configurado — no hace falta tocar la ventana principal, ya está
escrita contra la interfaz genérica, no contra un fabricante
específico.
