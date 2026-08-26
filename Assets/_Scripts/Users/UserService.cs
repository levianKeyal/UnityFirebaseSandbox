using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

public class UserService : MonoBehaviour
{
    public static UserService Instance { get; private set; }

    public UserProfile CurrentProfile { get; private set; }

    public bool IsProfileLoaded { get; private set; }

    public event Action<UserProfile> OnProfileLoaded;
    public event Action OnProfileCleared;

    private FirebaseAuthService authService;
    private FirestoreService firestoreService;
    private bool initializationRequested;
    private bool isInitialized;
    private bool subscribedToAuthStateChanged;
    private bool isLoadingProfile;
    private int profileRequestVersion;
    private string requestedUid;
    private Coroutine initializationCoroutine;

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

        UnsubscribeFromAuthService();

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
        Debug.Log("[UserService] Esperando a los servicios de Firebase...");

        while (true)
        {
            authService = FirebaseAuthService.Instance;
            firestoreService = FirestoreService.Instance;

            if (
                authService != null
                && authService.IsReady
                && firestoreService != null
                && firestoreService.IsReady
            )
            {
                break;
            }

            yield return null;
        }

        if (!subscribedToAuthStateChanged)
        {
            authService.StateChanged += HandleAuthStateChanged;
            subscribedToAuthStateChanged = true;
        }

        isInitialized = true;
        Debug.Log("[UserService] Inicializado.");
        SyncWithCurrentAuthState(initialSync: true);

        initializationCoroutine = null;
    }

    private void HandleAuthStateChanged(FirebaseUser currentUser)
    {
        if (currentUser == null)
        {
            ClearProfile(logIfAlreadyEmpty: false, fromLogout: true);
            return;
        }

        Debug.Log($"[UserService] Usuario autenticado detectado. UID: {currentUser.UserId}");
        RequestProfileSync(currentUser);
    }

    private void SyncWithCurrentAuthState(bool initialSync = false)
    {
        if (!isInitialized)
        {
            return;
        }

        FirebaseUser currentUser = authService != null ? authService.CurrentUser : null;
        if (currentUser == null)
        {
            ClearProfile(logIfAlreadyEmpty: initialSync, fromLogout: false);
            return;
        }

        RequestProfileSync(currentUser);
    }

    private void RequestProfileSync(FirebaseUser currentUser)
    {
        if (firestoreService == null || !firestoreService.IsReady)
        {
            Debug.Log("[UserService] Esperando a FirestoreService...");
            return;
        }

        if (string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            Debug.LogWarning("[UserService] El usuario autenticado no tiene UID valido.");
            ClearProfile(logIfAlreadyEmpty: false, fromLogout: false);
            return;
        }

        if (isLoadingProfile && string.Equals(requestedUid, currentUser.UserId, StringComparison.Ordinal))
        {
            return;
        }

        if (
            IsProfileLoaded
            && CurrentProfile != null
            && string.Equals(CurrentProfile.Uid, currentUser.UserId, StringComparison.Ordinal)
            && !isLoadingProfile
        )
        {
            return;
        }

        int requestVersion = ++profileRequestVersion;
        requestedUid = currentUser.UserId;
        isLoadingProfile = true;

        _ = LoadOrCreateProfileAsync(currentUser, requestVersion);
    }

    private async Task LoadOrCreateProfileAsync(FirebaseUser currentUser, int requestVersion)
    {
        string uid = currentUser.UserId;
        Debug.Log($"[UserService] Documento solicitado: users/{uid}");

        DocumentReference documentReference = firestoreService.Database.Collection("users").Document(uid);
        DateTime utcNow = DateTime.UtcNow;

        try
        {
            Debug.Log($"[UserService] Cargando perfil: {uid}");

            DocumentSnapshot snapshot = await documentReference.GetSnapshotAsync();
            if (!IsCurrentRequest(uid, requestVersion))
            {
                return;
            }

            if (!snapshot.Exists)
            {
                Debug.Log("[UserService] El perfil no existe. Creando nuevo usuario...");
                Debug.Log($"[UserService] Creando documento para UID: {uid}");

                if (!IsCurrentRequest(uid, requestVersion) || !IsLatestFirebaseUser(uid))
                {
                    return;
                }

                Dictionary<string, object> newUserDocument = UserFirestoreMapper.CreateNewUserDocument(
                    currentUser,
                    utcNow
                );

                await documentReference.SetAsync(newUserDocument);

                if (!IsCurrentRequest(uid, requestVersion))
                {
                    return;
                }

                UserProfile createdProfile = new UserProfile
                {
                    Uid = uid,
                    Email = UserFirestoreMapper.NormalizeEmail(currentUser.Email),
                    DisplayName = UserFirestoreMapper.NormalizeDisplayName(currentUser.DisplayName),
                    PhotoUrl = UserFirestoreMapper.NormalizePhotoUrl(currentUser.PhotoUrl),
                    Role = UserRole.User,
                    Status = UserStatus.Active,
                    CreatedAt = utcNow,
                    LastLogin = utcNow
                };

                SetLoadedProfile(createdProfile);
                Debug.Log("[UserService] Perfil creado correctamente.");
                return;
            }

            Debug.Log("[UserService] Perfil encontrado.");

            UserProfile loadedProfile = UserFirestoreMapper.FromDocument(snapshot);
            if (!IsCurrentRequest(uid, requestVersion))
            {
                return;
            }

            Dictionary<string, object> loginUpdate = UserFirestoreMapper.CreateExistingUserUpdateDocument(
                currentUser,
                utcNow
            );

            if (!IsCurrentRequest(uid, requestVersion) || !IsLatestFirebaseUser(uid))
            {
                return;
            }

            await documentReference.UpdateAsync(loginUpdate);

            if (!IsCurrentRequest(uid, requestVersion))
            {
                return;
            }

            loadedProfile.Email = UserFirestoreMapper.NormalizeEmail(currentUser.Email);
            loadedProfile.DisplayName = UserFirestoreMapper.NormalizeDisplayName(currentUser.DisplayName);
            loadedProfile.PhotoUrl = UserFirestoreMapper.NormalizePhotoUrl(currentUser.PhotoUrl);
            loadedProfile.LastLogin = utcNow;

            SetLoadedProfile(loadedProfile);
            Debug.Log("[UserService] Perfil cargado correctamente.");
        }
        catch (Exception exception)
        {
            if (IsCurrentRequest(uid, requestVersion))
            {
                Debug.LogError($"[UserService] Error cargando perfil: {exception}");
                ClearProfile(logIfAlreadyEmpty: false, fromLogout: false);
            }
        }
        finally
        {
            if (IsCurrentRequest(uid, requestVersion))
            {
                isLoadingProfile = false;
                requestedUid = null;
            }
        }
    }

    private bool IsLatestFirebaseUser(string uid)
    {
        FirebaseUser latestUser = authService != null ? authService.CurrentUser : null;

        return latestUser != null
            && string.Equals(latestUser.UserId, uid, StringComparison.Ordinal);
    }

    private bool IsCurrentRequest(string uid, int requestVersion)
    {
        return profileRequestVersion == requestVersion
            && string.Equals(requestedUid, uid, StringComparison.Ordinal);
    }

    private void UnsubscribeFromAuthService()
    {
        if (!subscribedToAuthStateChanged)
        {
            authService = null;
            return;
        }

        if (authService != null)
        {
            authService.StateChanged -= HandleAuthStateChanged;
        }

        subscribedToAuthStateChanged = false;
        authService = null;
    }

    private void SetLoadedProfile(UserProfile profile)
    {
        CurrentProfile = profile;
        IsProfileLoaded = true;
        OnProfileLoaded?.Invoke(profile);
    }

    private void ClearProfile(bool logIfAlreadyEmpty, bool fromLogout)
    {
        bool hadProfile = CurrentProfile != null || IsProfileLoaded;

        if (!hadProfile)
        {
            if (logIfAlreadyEmpty)
            {
                Debug.Log("[UserService] No hay usuario autenticado.");
            }

            return;
        }

        profileRequestVersion++;
        requestedUid = null;
        isLoadingProfile = false;

        CurrentProfile = null;
        IsProfileLoaded = false;

        if (fromLogout)
        {
            Debug.Log("[UserService] Perfil limpiado por logout.");
        }
        else
        {
            Debug.Log("[UserService] Perfil limpiado.");
        }

        OnProfileCleared?.Invoke();
    }
}
