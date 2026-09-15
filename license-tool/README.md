# Sistema de licencias — Dental Suite

Control de quién puede usar el software mediante llaves de licencia
firmadas criptográficamente (RSA-2048 + SHA256). No necesita internet
para validar — funciona igual de bien sin conexión, consistente con el
resto del sistema.

## Cómo funciona

1. Tú generas una llave con `LicenseGenerator.exe`, indicando el
   nombre del cliente y (opcionalmente) una fecha de vencimiento.
2. Le entregas esa llave de texto al cliente (por correo, WhatsApp,
   como prefieras — no es secreta en sí misma, lo que la hace válida
   es la firma).
3. La primera vez que la clínica abre Dental Suite, si no hay ninguna
   licencia activada, aparece una ventana pidiendo la llave.
4. El cliente la pega, el software la valida contra la clave pública
   (que sí va incluida en el instalador) y la guarda en
   `%LOCALAPPDATA%\DentalSuite\license.key`. De ahí en adelante abre
   directo, sin volver a pedirla — hasta que la licencia venza (si le
   pusiste fecha de vencimiento).

## ⚠️ Seguridad — muy importante

- **`license-keys/private_key.pem` y toda la carpeta `license-tool/`
  son de uso exclusivo tuyo.** Si alguien más consigue la clave
  privada, puede generar licencias válidas para tu software sin tu
  permiso — no la subas a ningún repositorio compartido, no la mandes
  por correo sin cifrar, no la incluyas en ningún instalador que le
  des a una clínica.
- La clave PÚBLICA (`license-keys/public_key.pem`, y ya integrada
  dentro de `DentalSuiteClient/Licensing/LicenseFormat.cs`) sí es
  segura de distribuir — de hecho ya viaja dentro del instalador que
  reciben tus clientes. Solo sirve para *verificar* licencias, no para
  crearlas.

## Generar una licencia

```
cd license-tool/LicenseGenerator
dotnet run -- "Consultorio Dental García" 2027-12-31
```

O sin fecha de vencimiento (licencia perpetua):

```
dotnet run -- "Consultorio Dental García"
```

Esto imprime la llave lista para copiar y entregar al cliente.

## Limitaciones de esta primera versión

- **No está atada a una PC específica** — la misma llave técnicamente
  podría copiarse a otra instalación. Si más adelante quieres evitar
  eso, se puede agregar un "ID de máquina": la app mostraría un código
  único en su primer arranque, el cliente te lo manda, y generas la
  licencia ya amarrada a ese equipo — es un paso más de fricción para
  el cliente, pero cierra ese hueco. Avísame si quieres que lo agregue.
- **La verificación de fecha usa el reloj de la PC del cliente** — si
  alguien atrasa la fecha del sistema a propósito, podría seguir
  usando una licencia "vencida". Para el caso de uso actual (control
  de clientes de buena fe, no protección contra piratería agresiva)
  esto es aceptable; una protección más fuerte requeriría validar
  contra un servidor con hora real, lo cual reintroduce la dependencia
  de internet que evitamos a propósito.
