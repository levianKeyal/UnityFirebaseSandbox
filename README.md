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
- `AuthorizationService`
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

### Roles y autoridad

- `Firestore role` es solo dato de perfil y UI.
- `Firebase Custom Claims` son la autoridad real para autorización.
- La app nunca debe elevar permisos por un `role` falsificado en Firestore.
- Si el `role` de Firestore y el claim difieren, la app usa el claim y solo registra un warning.

## Authorization / Roles

El sistema de autorización final del proyecto usa `Firebase Custom Claims` como autoridad real.

- Roles soportados:
  - `user`
  - `admin`
  - `superAdmin`
- `AuthorizationService` transforma el claim `role` en `UserRole`.
- `Firestore role` sigue siendo información de perfil y UI.
- Si Firestore y el claim difieren, el claim manda.
- El fallback seguro sigue siendo `User`.
- Cambios de claim pueden requerir renovación de token.
- El flujo normal ya resuelve correctamente el rol después de la autenticación.
- Para diagnóstico o refresco explícito existe `RefreshAuthorizationAsync(true)`.

## Required Scene Services

El GameObject persistente de servicios debe contener:

- `FirebaseBootstrap`
- `FirebaseAuthService`
- `FirestoreService`
- `UserService`
- `GoogleAuthService`
- `AuthorizationService`

`AuthorizationService` es obligatorio desde el Hito 6.

Si `AuthorizationService.Instance == null`, primero verifica que el componente esté agregado al GameObject persistente de servicios.

## Hito 6 Validado

El Hito 6 ya fue validado en una build Android real con:

- `Profile Role: SuperAdmin`
- `Auth Claim Role: SuperAdmin`
- `Effective Role: SuperAdmin`

### Primer SuperAdmin

El primer `SuperAdmin` se asigna con tooling administrativo externo, fuera de Unity.
El cliente Unity nunca contiene credenciales secretas ni el Firebase Admin SDK.

### Tooling administrativo

El script `Tools/FirebaseAdmin/set-role.js` usa `firebase-admin` para asignar claims como:

- `user`
- `admin`
- `superAdmin`

El archivo `Tools/FirebaseAdmin/serviceAccountKey.json` debe existir solo de forma local y nunca versionarse.

### Renovación de token

Los custom claims no aparecen en un token ya emitido.
Después de asignar o cambiar un claim:

1. cerrar sesión y volver a iniciar;
2. o forzar refresh de token si el código administrativo o de diagnóstico lo pide.

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
- `Tools/FirebaseAdmin/serviceAccountKey.json`
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
