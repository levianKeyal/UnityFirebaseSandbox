# UnityFirebaseSandbox

Sandbox de autenticación y usuarios Firebase para una aplicación Unity destinada a Android y Chromebook.

## Estado actual

- Unity: `6000.3.8f1`
- Plataforma objetivo actual: `Android`
- Objetivo final actual: `Chromebook`
- Servicios Firebase usados: `Firebase Authentication` y `Cloud Firestore`
- Estado validado: el Hito 5 fue probado con éxito en una build Android real

## Flujo principal

`Google Sign-In`  
`→ Firebase Authentication`  
`→ Firebase UID`  
`→ UserService`  
`→ Firestore users/{UID}`

## Arquitectura

- `FirebaseBootstrap`
- `FirebaseAuthService`
- `FirestoreService`
- `UserService`
- `GoogleAuthService`
- `AuthTestUI`

## Google Sign-In

Implementación actual:

- Android Credential Manager
- Sign in with Google
- Google ID Token
- `GoogleAuthProvider`

Nota técnica importante:

- `Assets/Plugins/Android/com/ffds/unityauthsandbox/GoogleAuthBridge.java` usa `credentialManager.getCredentialAsync(...)`
- No cambiarlo a `getCredential(...)`

## Firebase Unity SDK

La versión instalada actualmente corresponde a `13.15.0`.

## Modelo de usuario

Los nuevos usuarios se crean con:

- `role = user`
- `status = active`

Enums actuales:

- `UserRole`: `User`, `Admin`, `SuperAdmin`
- `UserStatus`: `Active`, `Suspended`, `Disabled`

## Firestore

Colección:

- `users`

Documento:

- `users/{FirebaseUser.UserId}`

Campos actuales:

- `uid`
- `email`
- `displayName`
- `photoUrl`
- `role`
- `status`
- `createdAt`
- `lastLogin`

## Después de clonar

1. Clona el repositorio.
2. Asegúrate de tener Git LFS instalado.
3. Ejecuta `git lfs pull` si hace falta.
4. Abre el proyecto con Unity `6000.3.8f1`.
5. Restaura `Assets/google-services.json`.
6. Restaura `Assets/StreamingAssets/google-services-desktop.json` solo si aplica a tu entorno.
7. Abre Unity y espera la importación/recompilación.
8. Si hace falta, resuelve dependencias Android con External Dependency Manager.
9. Verifica que el target siga siendo Android.
10. Genera una build de prueba.

## Firebase config local

Estos archivos no están versionados por ahora:

- `Assets/google-services.json`
- `Assets/StreamingAssets/google-services-desktop.json`

Se deben descargar desde Firebase Console:

- Project Settings
- General
- Android app

Después de configurar Google Authentication y SHA-1.

## Seguridad

Las reglas actuales de Firestore son temporales de desarrollo y prueba. No usar este proyecto tal como está como configuración de producción.

## Archivos no versionados

- `Library/`
- `Temp/`
- `Obj/`
- `Logs/`
- `UserSettings/`
- `Build/`
- `Builds/`
- `Assets/google-services.json`
- `Assets/StreamingAssets/google-services-desktop.json`
- `*.apk`
- `*.aab`

## Estado validado

- Android build compila
- Google account picker funciona
- múltiples cuentas Google funcionan
- cada cuenta obtiene UID independiente
- Firestore crea `users/{UID}`
- Auth UID y Firestore UID coinciden
- logout/login funcionan
