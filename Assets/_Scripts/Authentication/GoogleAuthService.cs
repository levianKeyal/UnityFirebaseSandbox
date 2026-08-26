using System;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

public class GoogleAuthService : MonoBehaviour
{
    public static GoogleAuthService Instance { get; private set; }

    [SerializeField]
    private string webClientIdOverride = string.Empty;

    public GoogleAuthState State { get; private set; } = GoogleAuthState.Idle;

    public bool IsReady { get; private set; }

    public string LastError { get; private set; }

    public event Action<GoogleAuthState> StateChanged;

    private FirebaseAuthService firebaseAuthService;
    private bool initializationRequested;
    private bool subscribedToBootstrapEvents;
    private bool subscribedToAuthStateChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        SetState(GoogleAuthState.Idle);
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

        UnsubscribeFromBootstrapEvents();
        UnsubscribeFromAuthService();
    }

    public void SignInWithGoogle()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        _ = SignInWithGoogleAsync();
#else
        SetError("Google Sign-In solo esta disponible en Android.");
#endif
    }

    private void TryInitialize()
    {
        if (initializationRequested)
        {
            return;
        }

        initializationRequested = true;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (TryBindFirebaseAuthService())
        {
            return;
        }

        Debug.Log("[GoogleAuth] Esperando a FirebaseAuthService...");
        SubscribeToBootstrapEvents();
#else
        SetError("Google Sign-In solo esta disponible en Android.");
#endif
    }

    private bool TryBindFirebaseAuthService()
    {
        firebaseAuthService = FirebaseAuthService.Instance;

        if (firebaseAuthService == null)
        {
            IsReady = false;
            return false;
        }

        if (!subscribedToAuthStateChanged)
        {
            firebaseAuthService.StateChanged += HandleFirebaseAuthStateChanged;
            subscribedToAuthStateChanged = true;
        }

        IsReady = firebaseAuthService.IsReady;

        if (IsReady)
        {
            Debug.Log("[GoogleAuth] Servicio listo.");
        }

        return IsReady;
    }

    private void SubscribeToBootstrapEvents()
    {
        if (subscribedToBootstrapEvents)
        {
            return;
        }

        FirebaseBootstrap.OnFirebaseReady += HandleFirebaseReady;
        FirebaseBootstrap.OnFirebaseInitializationFailed += HandleFirebaseInitializationFailed;
        subscribedToBootstrapEvents = true;
    }

    private void UnsubscribeFromBootstrapEvents()
    {
        if (!subscribedToBootstrapEvents)
        {
            return;
        }

        FirebaseBootstrap.OnFirebaseReady -= HandleFirebaseReady;
        FirebaseBootstrap.OnFirebaseInitializationFailed -= HandleFirebaseInitializationFailed;
        subscribedToBootstrapEvents = false;
    }

    private void UnsubscribeFromAuthService()
    {
        if (firebaseAuthService != null && subscribedToAuthStateChanged)
        {
            firebaseAuthService.StateChanged -= HandleFirebaseAuthStateChanged;
        }

        subscribedToAuthStateChanged = false;
        firebaseAuthService = null;
    }

    private void HandleFirebaseReady()
    {
        if (TryBindFirebaseAuthService())
        {
            UnsubscribeFromBootstrapEvents();
        }
    }

    private void HandleFirebaseInitializationFailed(string message)
    {
        SetError($"Firebase fallo antes de iniciar GoogleAuth: {message}");
    }

    private void HandleFirebaseAuthStateChanged(FirebaseUser user)
    {
        if (!IsReady && firebaseAuthService != null && firebaseAuthService.IsReady)
        {
            IsReady = true;
            Debug.Log("[GoogleAuth] Servicio listo.");
            UnsubscribeFromBootstrapEvents();
        }
    }

    #if UNITY_ANDROID && !UNITY_EDITOR
    private async Task SignInWithGoogleAsync()
    {
        if (!EnsureReady())
        {
            return;
        }

        if (State == GoogleAuthState.SigningIn)
        {
            return;
        }

        string resolvedWebClientId = ResolveWebClientId();
        if (string.IsNullOrWhiteSpace(resolvedWebClientId))
        {
            SetError(
                "No se encontro el Web Client ID. Configura el valor en el inspector o verifica google-services.json."
            );
            return;
        }

        SetState(GoogleAuthState.SigningIn);
        Debug.Log("[GoogleAuth] Iniciando Google Sign-In...");

        try
        {
            string idToken = await GoogleAuthBridge.RequestIdTokenAsync(resolvedWebClientId);
            if (string.IsNullOrWhiteSpace(idToken))
            {
                SetState(GoogleAuthState.Canceled);
                Debug.Log("[GoogleAuth] Inicio de sesion cancelado.");
                return;
            }

            Debug.Log("[GoogleAuth] ID token obtenido.");
            Debug.Log("[GoogleAuth] Autenticando con Firebase...");

            Credential credential = GoogleAuthProvider.GetCredential(idToken, null);
            FirebaseUser signedInUser = await firebaseAuthService.SignInWithCredentialAsync(
                credential
            );

            if (signedInUser != null)
            {
                Debug.Log("[GoogleAuth] Firebase Authentication completado.");
                SetState(GoogleAuthState.Success);
            }
            else
            {
                SetError("Firebase retorno un usuario nulo.");
            }
        }
        catch (OperationCanceledException)
        {
            SetState(GoogleAuthState.Canceled);
            Debug.Log("[GoogleAuth] Inicio de sesion cancelado.");
        }
        catch (Exception exception)
        {
            SetError($"Error durante Google Sign-In: {exception.Message}");
        }
    }
#endif

    private bool EnsureReady()
    {
        if (IsReady && firebaseAuthService != null && firebaseAuthService.IsReady)
        {
            return true;
        }

        if (firebaseAuthService == null)
        {
            TryBindFirebaseAuthService();
        }

        if (firebaseAuthService == null || !firebaseAuthService.IsReady)
        {
            SetError("FirebaseAuthService no esta listo todavia.");
            return false;
        }

        IsReady = true;
        return true;
    }

    private string ResolveWebClientId()
    {
        if (!string.IsNullOrWhiteSpace(webClientIdOverride))
        {
            return webClientIdOverride.Trim();
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using (AndroidJavaClass unityPlayerClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (AndroidJavaObject activity = unityPlayerClass.GetStatic<AndroidJavaObject>("currentActivity"))
            using (AndroidJavaObject resources = activity.Call<AndroidJavaObject>("getResources"))
            {
                string packageName = activity.Call<string>("getPackageName");
                int resourceId = resources.Call<int>(
                    "getIdentifier",
                    "default_web_client_id",
                    "string",
                    packageName
                );

                if (resourceId != 0)
                {
                    return resources.Call<string>("getString", resourceId);
                }
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[GoogleAuth] No se pudo leer default_web_client_id: {exception.Message}"
            );
        }
#endif

        return string.Empty;
    }

    private void SetState(GoogleAuthState state)
    {
        State = state;
        if (state != GoogleAuthState.Error)
        {
            LastError = string.Empty;
        }

        StateChanged?.Invoke(state);
    }

    private void SetError(string message)
    {
        LastError = message;
        State = GoogleAuthState.Error;
        Debug.LogError($"[GoogleAuth] {message}");
        StateChanged?.Invoke(State);
    }
}
