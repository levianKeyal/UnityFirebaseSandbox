using System;
using Firebase.Firestore;
using UnityEngine;

public class FirestoreService : MonoBehaviour
{
    public static FirestoreService Instance { get; private set; }

    public FirebaseFirestore Database { get; private set; }

    public bool IsReady { get; private set; }

    public static event Action OnFirestoreReady;

    private bool initializationRequested;

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
    }

    private void TryInitialize()
    {
        if (IsReady || initializationRequested)
        {
            return;
        }

        initializationRequested = true;

        if (FirebaseBootstrap.IsReady)
        {
            InitializeFirestore();
            return;
        }

        Debug.Log("[Firestore] Esperando inicializacion de Firebase...");
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

        InitializeFirestore();
    }

    private void HandleFirebaseInitializationFailed(string message)
    {
        Debug.LogError($"[Firestore] No se pudo inicializar porque Firebase fallo antes: {message}");
    }

    private void InitializeFirestore()
    {
        if (IsReady)
        {
            return;
        }

        Debug.Log("[Firestore] Inicializando...");

        try
        {
            Database = FirebaseFirestore.DefaultInstance;
            IsReady = true;

            Debug.Log("[Firestore] Inicializado correctamente.");
            OnFirestoreReady?.Invoke();
        }
        catch (Exception exception)
        {
            Database = null;
            IsReady = false;
            Debug.LogError($"[Firestore] Error inicializando Firestore: {exception}");
        }
    }
}
