using System;
using Firebase;
using Firebase.Extensions;
using UnityEngine;

public class FirebaseBootstrap : MonoBehaviour
{
    public static FirebaseBootstrap Instance { get; private set; }

    public static bool IsReady { get; private set; }
    public static event Action OnFirebaseReady;
    public static event Action<string> OnFirebaseInitializationFailed;

    private FirebaseApp firebaseApp;
    private bool initializationStarted;

    private void Awake()
    {
        // Evita tener más de un FirebaseBootstrap activo.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Lo conservamos si posteriormente cambiamos de escena.
        DontDestroyOnLoad(gameObject);

        if (!IsReady && !initializationStarted)
        {
            InitializeFirebase();
        }
    }

    private void InitializeFirebase()
    {
        initializationStarted = true;

        Debug.Log("[Firebase] Comprobando dependencias...");

        try
        {
            FirebaseApp.CheckAndFixDependenciesAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCanceled)
                    {
                        HandleInitializationFailure(
                            "La comprobacion de dependencias fue cancelada.",
                            null
                        );
                        return;
                    }

                    if (task.IsFaulted)
                    {
                        HandleInitializationFailure(
                            "Error comprobando dependencias.",
                            task.Exception
                        );
                        return;
                    }

                    DependencyStatus dependencyStatus = task.Result;

                    if (dependencyStatus != DependencyStatus.Available)
                    {
                        HandleInitializationFailure(
                            $"Dependencias no disponibles: {dependencyStatus}",
                            null
                        );
                        return;
                    }

                    firebaseApp = FirebaseApp.DefaultInstance;
                    IsReady = true;

                    Debug.Log("[Firebase] Inicializado correctamente.");
                    Debug.Log($"[Firebase] App Name: {firebaseApp.Name}");

                    OnFirebaseReady?.Invoke();
                });
        }
        catch (Exception exception)
        {
            HandleInitializationFailure(
                "Excepcion inesperada al inicializar Firebase.",
                exception
            );
        }
    }

    private void HandleInitializationFailure(string message, Exception exception)
    {
        IsReady = false;

        if (exception != null)
        {
            Debug.LogError($"[Firebase] {message} {exception}");
            OnFirebaseInitializationFailed?.Invoke($"{message} {exception.Message}");
            return;
        }

        Debug.LogError($"[Firebase] {message}");
        OnFirebaseInitializationFailed?.Invoke(message);
    }
}
