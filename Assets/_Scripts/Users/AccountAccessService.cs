using System;
using UnityEngine;

public class AccountAccessService : MonoBehaviour
{
    public static AccountAccessService Instance { get; private set; }

    public bool IsReady { get; private set; }
    public bool HasResolvedStatus { get; private set; }
    public bool CanUseApplication { get; private set; }
    public UserStatus CurrentStatus { get; private set; } = UserStatus.Active;
    public string LastAccessMessage { get; private set; } = "Esperando perfil...";
    public string LastRestrictionMessage { get; private set; } = string.Empty;

    public event Action OnAccountAccessChanged;

    private UserService userService;
    private bool initializationRequested;
    private bool subscribedToUserService;
    private Coroutine initializationCoroutine;
    private string lastAccessSignature = string.Empty;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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

        UnsubscribeFromUserService();

        if (initializationCoroutine != null)
        {
            StopCoroutine(initializationCoroutine);
            initializationCoroutine = null;
        }
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

    private System.Collections.IEnumerator InitializeWhenReady()
    {
        Debug.Log("[AccountAccess] Esperando a UserService...");

        while (UserService.Instance == null)
        {
            yield return null;
        }

        userService = UserService.Instance;
        SubscribeToUserService();

        IsReady = true;
        Debug.Log("[AccountAccess] Inicializado.");
        SyncWithCurrentProfile();

        initializationCoroutine = null;
    }

    private void SubscribeToUserService()
    {
        if (userService == null || subscribedToUserService)
        {
            return;
        }

        userService.OnProfileLoaded += HandleProfileLoaded;
        userService.OnProfileCleared += HandleProfileCleared;
        subscribedToUserService = true;
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

    private void SyncWithCurrentProfile()
    {
        if (userService == null)
        {
            ApplyNoProfileState("Sin perfil de cuenta.");
            return;
        }

        if (userService.CurrentProfile != null)
        {
            ApplyProfileState(userService.CurrentProfile);
            return;
        }

        if (userService.IsProfileLoaded)
        {
            ApplyNoProfileState("Perfil no disponible.");
            return;
        }

        ApplyNoProfileState("Esperando perfil...");
    }

    private void HandleProfileLoaded(UserProfile profile)
    {
        if (profile == null)
        {
            ApplyNoProfileState("Perfil no disponible.");
            return;
        }

        ApplyProfileState(profile);
    }

    private void HandleProfileCleared()
    {
        bool preserveRestrictionMessage = HasResolvedStatus && !CanUseApplication;
        ApplyNoProfileState("Sin perfil autenticado.", preserveRestrictionMessage);
    }

    private void ApplyProfileState(UserProfile profile)
    {
        CurrentStatus = profile.Status;
        HasResolvedStatus = true;
        CanUseApplication = profile.Status == UserStatus.Active;
        LastAccessMessage = GetAccessMessage(profile.Status);
        LastRestrictionMessage = CanUseApplication ? string.Empty : LastAccessMessage;

        string signature = BuildSignature();
        if (string.Equals(lastAccessSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        lastAccessSignature = signature;

        OnAccountAccessChanged?.Invoke();
        if (CanUseApplication)
        {
            Debug.Log(
                $"[AccountAccess] Acceso permitido. UID={profile.Uid}, Status={profile.Status}"
            );
            return;
        }

        Debug.LogWarning(
            $"[AccountAccess] Acceso bloqueado. UID={profile.Uid}, Status={profile.Status}"
        );
    }

    private void ApplyNoProfileState(string message, bool preserveRestrictionMessage = false)
    {
        if (!preserveRestrictionMessage)
        {
            LastRestrictionMessage = string.Empty;
        }

        HasResolvedStatus = false;
        CanUseApplication = false;
        LastAccessMessage = message;

        string signature = BuildSignature();
        if (string.Equals(lastAccessSignature, signature, StringComparison.Ordinal))
        {
            return;
        }

        lastAccessSignature = signature;
        Debug.Log($"[AccountAccess] {message}");
        OnAccountAccessChanged?.Invoke();
    }

    private static string GetAccessMessage(UserStatus status)
    {
        switch (status)
        {
            case UserStatus.Suspended:
                return "Cuenta suspendida.";
            case UserStatus.Disabled:
                return "Cuenta deshabilitada.";
            case UserStatus.Active:
            default:
                return "Cuenta activa.";
        }
    }

    private string BuildSignature()
    {
        string statusSignature = HasResolvedStatus ? CurrentStatus.ToString() : "NoProfile";
        return $"{statusSignature}:{CanUseApplication}:{LastAccessMessage}";
    }
}
