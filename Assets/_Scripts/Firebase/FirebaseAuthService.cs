using System;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

public class FirebaseAuthService : MonoBehaviour
{
    public static FirebaseAuthService Instance { get; private set; }

    public FirebaseUser CurrentUser => firebaseAuth != null ? firebaseAuth.CurrentUser : null;

    public bool IsAuthenticated => CurrentUser != null;
    public bool IsReady { get; private set; }

    public event Action<FirebaseUser> StateChanged;

    private Firebase.Auth.FirebaseAuth firebaseAuth;
    private bool initializationRequested;
    private bool isInitialized;
    private string lastKnownUserId = string.Empty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        TryInitialize();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }

        FirebaseBootstrap.OnFirebaseReady -= HandleFirebaseReady;
        FirebaseBootstrap.OnFirebaseInitializationFailed -= HandleFirebaseInitializationFailed;

        if (firebaseAuth != null)
        {
            firebaseAuth.StateChanged -= HandleAuthStateChanged;
        }
    }

    private void TryInitialize()
    {
        if (isInitialized || initializationRequested)
        {
            return;
        }

        initializationRequested = true;

        if (FirebaseBootstrap.IsReady)
        {
            InitializeFirebaseAuth();
            return;
        }

        Debug.Log("[FirebaseAuth] Esperando a FirebaseBootstrap...");
        FirebaseBootstrap.OnFirebaseReady += HandleFirebaseReady;
        FirebaseBootstrap.OnFirebaseInitializationFailed += HandleFirebaseInitializationFailed;

        if (FirebaseBootstrap.IsReady)
        {
            HandleFirebaseReady();
        }
    }

    private void HandleFirebaseReady()
    {
        FirebaseBootstrap.OnFirebaseReady -= HandleFirebaseReady;
        FirebaseBootstrap.OnFirebaseInitializationFailed -= HandleFirebaseInitializationFailed;

        InitializeFirebaseAuth();
    }

    private void HandleFirebaseInitializationFailed(string message)
    {
        Debug.LogError($"[FirebaseAuth] No se pudo inicializar porque Firebase fallo antes: {message}");
    }

    private void InitializeFirebaseAuth()
    {
        if (isInitialized)
        {
            return;
        }

        Debug.Log("[FirebaseAuth] Inicializando...");

        try
        {
            firebaseAuth = Firebase.Auth.FirebaseAuth.DefaultInstance;
            firebaseAuth.StateChanged += HandleAuthStateChanged;
            isInitialized = true;
            IsReady = true;
        }
        catch (Exception exception)
        {
            Debug.LogError($"[FirebaseAuth] Error al inicializar FirebaseAuth: {exception}");
            return;
        }

        Debug.Log("[FirebaseAuth] Inicializado.");
        HandleAuthStateChanged(this, EventArgs.Empty);
    }

    private void HandleAuthStateChanged(object sender, EventArgs eventArgs)
    {
        FirebaseUser currentUser = CurrentUser;
        string currentUid = currentUser != null ? currentUser.UserId : string.Empty;
        string previousUid = lastKnownUserId;

        Debug.Log("[FirebaseAuth] StateChanged");
        Debug.Log(
            $"[FirebaseAuth] Previous UID: {(!string.IsNullOrWhiteSpace(previousUid) ? previousUid : "null")}"
        );
        Debug.Log(
            $"[FirebaseAuth] Current UID: {(!string.IsNullOrWhiteSpace(currentUid) ? currentUid : "null")}"
        );
        lastKnownUserId = currentUid;

        if (currentUser == null)
        {
            Debug.Log("[FirebaseAuth] No hay ningun usuario autenticado.");
            StateChanged?.Invoke(null);
            return;
        }

        Debug.Log("[FirebaseAuth] Usuario autenticado.");
        Debug.Log($"[FirebaseAuth] Email: {currentUser.Email}");
        Debug.Log($"[FirebaseAuth] UID: {currentUser.UserId}");
        StateChanged?.Invoke(currentUser);
    }

    public void SignOut()
    {
        if (!isInitialized || firebaseAuth == null)
        {
            Debug.LogWarning("[FirebaseAuth] SignOut llamado antes de inicializar el servicio.");
            return;
        }

        firebaseAuth.SignOut();
        Debug.Log("[FirebaseAuth] Sesion cerrada.");
    }

    public Task<FirebaseUser> SignInWithCredentialAsync(Credential credential)
    {
        if (!isInitialized || firebaseAuth == null)
        {
            return Task.FromException<FirebaseUser>(
                new InvalidOperationException("FirebaseAuthService no esta listo.")
            );
        }

        if (credential == null)
        {
            return Task.FromException<FirebaseUser>(
                new ArgumentNullException(nameof(credential))
            );
        }

        return firebaseAuth.SignInWithCredentialAsync(credential);
    }
}
