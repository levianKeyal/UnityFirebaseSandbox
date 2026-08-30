using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

public class AuthorizationService : MonoBehaviour
{
    public static AuthorizationService Instance { get; private set; }

    public UserRole CurrentRole { get; private set; } = UserRole.User;
    public UserRole AuthClaimRole { get; private set; } = UserRole.User; // Legacy/diagnostic only.
    public UserRole FirestoreRole { get; private set; } = UserRole.User; // Profile role from Firestore.

    public bool HasFirestoreProfile { get; private set; }
    public bool IsReady { get; private set; }

    public bool IsUser => CurrentRole == UserRole.User;
    public bool IsAdmin => CurrentRole == UserRole.Admin;
    public bool IsSuperAdmin => CurrentRole == UserRole.SuperAdmin;

    public event Action OnAuthorizationChanged;

    private FirebaseAuthService authService;
    private UserService userService;
    private bool initializationRequested;
    private bool subscribedToAuthService;
    private bool subscribedToUserService;
    private Coroutine initializationCoroutine;
    private Coroutine userServiceCoroutine;
    private int refreshVersion;
    private string requestedUid;
    private string lastMismatchLog = string.Empty;

    private static MethodInfo tokenAsyncMethod;
    private static PropertyInfo tokenTaskResultProperty;

    [Serializable]
    private class JwtClaimsPayload
    {
        public string role;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResetAuthorizationState();
    }

    private void Start()
    {
        TryInitialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        UnsubscribeFromAuthService();
        UnsubscribeFromUserService();

        if (initializationCoroutine != null)
        {
            StopCoroutine(initializationCoroutine);
            initializationCoroutine = null;
        }

        if (userServiceCoroutine != null)
        {
            StopCoroutine(userServiceCoroutine);
            userServiceCoroutine = null;
        }
    }

    public Task RefreshAuthorizationAsync(bool forceRefresh = false)
    {
        return RefreshAuthorizationInternalAsync(forceRefresh);
    }

    private void TryInitialize()
    {
        if (initializationRequested)
        {
            return;
        }

        initializationRequested = true;
        initializationCoroutine = StartCoroutine(InitializeWhenReady());
    }

    private IEnumerator InitializeWhenReady()
    {
        Debug.Log("[Authorization] Esperando a FirebaseAuthService...");

        while (true)
        {
            authService = FirebaseAuthService.Instance;

            if (authService != null && authService.IsReady)
            {
                break;
            }

            yield return null;
        }

        SubscribeToAuthService();
        TrySubscribeToUserService();

        IsReady = true;
        Debug.Log("[Authorization] Inicializado.");
        _ = RefreshAuthorizationInternalAsync(forceRefresh: false);

        initializationCoroutine = null;
    }

    private void SubscribeToAuthService()
    {
        if (authService == null || subscribedToAuthService)
        {
            return;
        }

        authService.StateChanged += HandleAuthStateChanged;
        subscribedToAuthService = true;
    }

    private void TrySubscribeToUserService()
    {
        if (subscribedToUserService)
        {
            return;
        }

        userService = UserService.Instance;
        if (userService == null)
        {
            if (userServiceCoroutine == null)
            {
                userServiceCoroutine = StartCoroutine(WaitForUserServiceAndSubscribe());
            }

            return;
        }

        userService.OnProfileLoaded += HandleProfileLoaded;
        userService.OnProfileCleared += HandleProfileCleared;
        subscribedToUserService = true;

        if (userService.CurrentProfile != null)
        {
            HandleProfileLoaded(userService.CurrentProfile);
        }
        else if (userService.IsProfileLoaded)
        {
            HandleProfileCleared();
        }
    }

    private IEnumerator WaitForUserServiceAndSubscribe()
    {
        while (UserService.Instance == null)
        {
            yield return null;
        }

        userService = UserService.Instance;
        userServiceCoroutine = null;
        TrySubscribeToUserService();
    }

    private void UnsubscribeFromAuthService()
    {
        if (authService != null && subscribedToAuthService)
        {
            authService.StateChanged -= HandleAuthStateChanged;
        }

        subscribedToAuthService = false;
        authService = null;
    }

    private void UnsubscribeFromUserService()
    {
        if (userService != null && subscribedToUserService)
        {
            userService.OnProfileLoaded -= HandleProfileLoaded;
            userService.OnProfileCleared -= HandleProfileCleared;
        }

        subscribedToUserService = false;
        userService = null;
    }

    private void HandleAuthStateChanged(FirebaseUser currentUser)
    {
        TrySubscribeToUserService();

        if (currentUser == null)
        {
            ResetAuthorizationState();
            Debug.Log("[Authorization] Sin usuario autenticado. Usando User.");
            OnAuthorizationChanged?.Invoke();
            return;
        }

        _ = RefreshAuthorizationInternalAsync(forceRefresh: false);
    }

    private void HandleProfileLoaded(UserProfile profile)
    {
        TrySubscribeToUserService();

        HasFirestoreProfile = profile != null;
        FirestoreRole = profile != null ? profile.Role : UserRole.User;
        UpdateEffectiveRole();
        WarnIfRoleMismatch();
        OnAuthorizationChanged?.Invoke();
    }

    private void HandleProfileCleared()
    {
        HasFirestoreProfile = false;
        FirestoreRole = UserRole.User;
        UpdateEffectiveRole();
        OnAuthorizationChanged?.Invoke();
    }

    private async Task RefreshAuthorizationInternalAsync(bool forceRefresh)
    {
        if (authService == null)
        {
            authService = FirebaseAuthService.Instance;
        }

        FirebaseUser currentUser = authService != null ? authService.CurrentUser : null;
        if (currentUser == null)
        {
            ResetAuthorizationState();
            OnAuthorizationChanged?.Invoke();
            return;
        }

        string uid = currentUser.UserId;
        if (string.IsNullOrWhiteSpace(uid))
        {
            Debug.LogWarning("[Authorization] El usuario autenticado no tiene UID valido. Usando User.");
            ResetAuthorizationState();
            OnAuthorizationChanged?.Invoke();
            return;
        }

        int requestVersion = ++refreshVersion;
        requestedUid = uid;

        TrySubscribeToUserService();

        try
        {
            string idToken = await GetFirebaseTokenAsync(currentUser, forceRefresh);
            if (!IsCurrentRequest(uid, requestVersion))
            {
                return;
            }

            UserRole claimRole = ParseClaimRole(idToken);
            AuthClaimRole = claimRole;
            UpdateEffectiveRole();
            WarnIfRoleMismatch();

            Debug.Log(
                $"[Authorization] Claim role: {AuthClaimRole}, Firestore role: {FirestoreRole}, Effective role: {CurrentRole}"
            );

            OnAuthorizationChanged?.Invoke();
        }
        catch (Exception exception)
        {
            if (!IsCurrentRequest(uid, requestVersion))
            {
                return;
            }

            Debug.LogWarning(
                $"[Authorization] No se pudo resolver el claim de rol. Usando User. Detalle: {exception.Message}"
            );
            AuthClaimRole = UserRole.User;
            UpdateEffectiveRole();
            OnAuthorizationChanged?.Invoke();
        }
        finally
        {
            if (IsCurrentRequest(uid, requestVersion))
            {
                requestedUid = null;
            }
        }
    }

    private bool IsCurrentRequest(string uid, int version)
    {
        return refreshVersion == version
            && string.Equals(requestedUid, uid, StringComparison.Ordinal);
    }

    private void ResetAuthorizationState()
    {
        refreshVersion++;
        requestedUid = null;

        AuthClaimRole = UserRole.User;
        FirestoreRole = UserRole.User;
        HasFirestoreProfile = false;
        UpdateEffectiveRole();
    }

    private void UpdateEffectiveRole()
    {
        CurrentRole = HasFirestoreProfile ? FirestoreRole : UserRole.User;
    }

    private void WarnIfRoleMismatch()
    {
        if (!HasFirestoreProfile)
        {
            return;
        }

        if (FirestoreRole == AuthClaimRole)
        {
            lastMismatchLog = string.Empty;
            return;
        }

        string mismatchKey = $"{AuthClaimRole}:{FirestoreRole}";
        if (string.Equals(lastMismatchLog, mismatchKey, StringComparison.Ordinal))
        {
            return;
        }

        lastMismatchLog = mismatchKey;
        Debug.LogWarning(
            $"[Authorization] Firestore role differs from auth claim. Firestore={FirestoreRole}, Claim={AuthClaimRole}. Using Firestore role for authorization."
        );
    }

    private static UserRole ParseClaimRole(string jwtToken)
    {
        if (string.IsNullOrWhiteSpace(jwtToken))
        {
            return UserRole.User;
        }

        string[] parts = jwtToken.Split('.');
        if (parts.Length < 2)
        {
            Debug.LogWarning("[Authorization] Token invalido o incompleto. Usando User.");
            return UserRole.User;
        }

        try
        {
            string payloadJson = DecodeBase64Url(parts[1]);
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                Debug.LogWarning("[Authorization] No se pudo leer el payload del token. Usando User.");
                return UserRole.User;
            }

            JwtClaimsPayload payload = JsonUtility.FromJson<JwtClaimsPayload>(payloadJson);
            if (payload == null || string.IsNullOrWhiteSpace(payload.role))
            {
                return UserRole.User;
            }

            string normalizedRole = payload.role.Trim();
            if (string.Equals(normalizedRole, "user", StringComparison.OrdinalIgnoreCase))
            {
                return UserRole.User;
            }

            if (string.Equals(normalizedRole, "admin", StringComparison.OrdinalIgnoreCase))
            {
                return UserRole.Admin;
            }

            if (string.Equals(normalizedRole, "superAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return UserRole.SuperAdmin;
            }

            Debug.LogWarning(
                $"[Authorization] Claim role invalido '{normalizedRole}'. Usando User."
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[Authorization] Error leyendo claims del token: {exception.Message}. Usando User."
            );
        }

        return UserRole.User;
    }

    private static string DecodeBase64Url(string base64Url)
    {
        string normalized = base64Url.Replace('-', '+').Replace('_', '/');

        switch (normalized.Length % 4)
        {
            case 2:
                normalized += "==";
                break;
            case 3:
                normalized += "=";
                break;
            case 0:
                break;
            default:
                throw new FormatException("Base64Url invalido.");
        }

        byte[] bytes = Convert.FromBase64String(normalized);
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    private static async Task<string> GetFirebaseTokenAsync(FirebaseUser firebaseUser, bool forceRefresh)
    {
        if (firebaseUser == null)
        {
            return null;
        }

        if (!TryInitializeTokenReflection(firebaseUser))
        {
            return null;
        }

        object taskObject = tokenAsyncMethod.Invoke(firebaseUser, new object[] { forceRefresh });
        if (taskObject is not Task tokenTask)
        {
            throw new InvalidOperationException("TokenAsync no devolvio un Task valido.");
        }

        await tokenTask;

        if (tokenTask.IsFaulted)
        {
            Exception exception = tokenTask.Exception?.GetBaseException()
                ?? new InvalidOperationException("TokenAsync fallo sin excepcion detallada.");
            throw exception;
        }

        return tokenTaskResultProperty.GetValue(tokenTask) as string;
    }

    private static bool TryInitializeTokenReflection(FirebaseUser firebaseUser)
    {
        if (tokenAsyncMethod != null && tokenTaskResultProperty != null)
        {
            return true;
        }

        Type userType = firebaseUser.GetType();
        tokenAsyncMethod = userType.GetMethod(
            "TokenAsync",
            BindingFlags.Instance | BindingFlags.Public,
            null,
            new[] { typeof(bool) },
            null
        );

        if (tokenAsyncMethod == null)
        {
            Debug.LogWarning("[Authorization] No se encontro FirebaseUser.TokenAsync(bool).");
            return false;
        }

        Type tokenTaskType = tokenAsyncMethod.ReturnType;
        tokenTaskResultProperty = tokenTaskType.GetProperty("Result");

        if (tokenTaskResultProperty == null || tokenTaskResultProperty.PropertyType != typeof(string))
        {
            Debug.LogWarning("[Authorization] No se pudo preparar acceso al resultado de TokenAsync.");
            return false;
        }

        return true;
    }
}
