# Dental Suite — App móvil (Android e iOS, un solo código)

Esta carpeta (`dental_suite_mobile/`) tiene el código Dart/Flutter ya
escrito: pantallas, modelos, y el cliente que habla con el mismo
backend que la app de escritorio. Lo que falta son las carpetas
`android/` e `ios/` — esas las genera Flutter automáticamente, con la
versión exacta de tus herramientas instaladas (si las escribiera a
mano aquí, corren el riesgo de no coincidir con tu Android
Studio/Xcode y no compilar).

## 1. Instalar Flutter

Si no lo tienes: https://docs.flutter.dev/get-started/install

Verifica que quedó bien instalado:
```
flutter doctor
```

## 2. Generar las carpetas de Android/iOS

Desde dentro de esta carpeta (`dental_suite_mobile/`):
```
flutter create .
```
Esto crea `android/`, `ios/`, y los demás archivos de cada plataforma
**sin tocar** el código que ya está en `lib/` ni `pubspec.yaml` — solo
rellena lo que falta.

## 3. Instalar las dependencias

```
flutter pub get
```

## 4. Compilar el APK de Android

```
flutter build apk --release
```
El archivo queda en:
```
build/app/outputs/flutter-apk/app-release.apk
```
Ese `.apk` ya lo puedes copiar a cualquier celular Android e instalarlo
directo (puede que tengas que permitir "instalar apps de orígenes
desconocidos" la primera vez).

## 5. Compilar para iOS

**Requiere una Mac con Xcode instalado — es un requisito de Apple, no
hay forma de evitarlo.** En esa Mac:
```
flutter build ios
```
Luego necesitas abrir el proyecto en Xcode (`ios/Runner.xcworkspace`)
para firmarlo con tu cuenta de Apple Developer y poder instalarlo en un
iPhone real o subirlo a la App Store.

## Primer uso

1. Al abrir la app por primera vez, te va a pedir la dirección del
   servidor — pega ahí la URL de Render (ver `DEPLOY.md` en la raíz del
   proyecto), por ejemplo `https://dental-suite-xxxx.onrender.com`.
2. Después, inicia sesión con el mismo correo/teléfono y contraseña que
   usas en la app de escritorio — es el mismo servidor, mismos
   usuarios.

## Qué incluye esta primera versión

- **Agenda**: citas del día, con flechas para moverte entre días.
- **Pacientes**: buscar y ver información básica de cada uno.
- **Tratamientos**: de un paciente en particular, o la lista general
  con su barra de progreso.
- **Fotos / radiografías**: galería de solo lectura del historial de
  un paciente (agrupado por diente, más reciente primero).

Esta primera versión es de **consulta** — no incluye captura de
radiografías/fotos con sensores ni edición, que se quedan en la app de
escritorio.
