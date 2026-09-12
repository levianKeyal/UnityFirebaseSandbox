using System;
using System.Threading.Tasks;
using Firebase.Auth;
using UnityEngine;

public sealed class StudentDataSessionService : MonoBehaviour
{
    [SerializeField] private StudentDataRegistry studentDataRegistry;

    public bool IsDataReady { get; private set; }
    public StudentDataRuntimeStore RuntimeStore { get; private set; }
    public event Action<bool> OnDataReadyChanged;

    private FirebaseAuthService authService;
    private UserService userService;
    private AccountAccessService accountAccessService;
    private FirestoreService firestoreService;
    private StudentDataSaveCoordinator saveCoordinator;
    private StudentDataLoadCoordinator loadCoordinator;
    private string currentUid;
    private string failedUid;
    private string loadingUid;
    private int sessionVersion;
    private bool isLoading;
    private bool subscribedToFirebaseReady;
    private bool subscribedToAuthService;
    private bool subscribedToUserService;
    private bool subscribedToAccountAccess;

    private void Start()
    {
        FirebaseBootstrap.OnFirebaseReady += HandleFirebaseReady;
        subscribedToFirebaseReady = true;
        ResolveServicesAndSubscribe();
        EvaluateDataSession();
    }

    private void OnDestroy()
    {
        if (subscribedToFirebaseReady)
        {
            FirebaseBootstrap.OnFirebaseReady -= HandleFirebaseReady;
            subscribedToFirebaseReady = false;
        }

        UnsubscribeFromServices();
        InvalidateSession();
    }

    public async Task<bool> FlushAsync()
    {
        ResolveServicesAndSubscribe();
        string uid;
        if (!IsDataReady
            || RuntimeStore == null
            || !TryGetValidSessionUid(out uid)
            || !string.Equals(currentUid, uid, StringComparison.Ordinal))
        {
            return false;
        }

        StudentDataSaveCoordinator coordinator = saveCoordinator;
        if (coordinator == null)
        {
            return false;
        }

        return await coordinator.SaveDirtyDataAsync(uid, RuntimeStore);
    }

    private void HandleFirebaseReady()
    {
        ResolveServicesAndSubscribe();
        EvaluateDataSession();
    }

    private void HandleAuthStateChanged(FirebaseUser currentUser)
    {
        ResolveServicesAndSubscribe();
        EvaluateDataSession();
    }

    private void HandleProfileLoaded(UserProfile profile)
    {
        ResolveServicesAndSubscribe();
        EvaluateDataSession();
    }

    private void HandleProfileCleared()
    {
        InvalidateSession();
        EvaluateDataSession();
    }

    private void HandleAccountAccessChanged()
    {
        ResolveServicesAndSubscribe();
        EvaluateDataSession();
    }

    private void HandleFirestoreReady()
    {
        ResolveServicesAndSubscribe();
        EvaluateDataSession();
    }

    private void EvaluateDataSession()
    {
        ResolveServicesAndSubscribe();

        string uid;
        if (!TryGetValidSessionUid(out uid))
        {
            InvalidateSession();
            return;
        }

        if (IsDataReady && string.Equals(currentUid, uid, StringComparison.Ordinal))
        {
            return;
        }

        if (isLoading && string.Equals(loadingUid, uid, StringComparison.Ordinal))
        {
            return;
        }

        if (failedUid != null && string.Equals(failedUid, uid, StringComparison.Ordinal))
        {
            return;
        }

        StartDataLoad(uid);
    }

    private void StartDataLoad(string uid)
    {
        if (studentDataRegistry == null)
        {
            Debug.LogError("[StudentDataSession] StudentDataRegistry is required.");
            failedUid = uid;
            SetDataReady(false);
            return;
        }

        StudentDataValidationResult registryValidation = studentDataRegistry.Validate();
        if (!registryValidation.IsValid)
        {
            Debug.LogError(
                "[StudentDataSession] Registry invalid: "
                + string.Join(" ", registryValidation.Errors)
            );
            failedUid = uid;
            SetDataReady(false);
            return;
        }

        StudentDataRuntimeStore runtimeStore = new StudentDataRuntimeStore(studentDataRegistry);
        if (!runtimeStore.IsInitialized)
        {
            Debug.LogError("[StudentDataSession] RuntimeStore initialization failed.");
            failedUid = uid;
            SetDataReady(false);
            return;
        }

        currentUid = uid;
        loadingUid = uid;
        failedUid = null;
        isLoading = true;
        int loadVersion = ++sessionVersion;
        RuntimeStore = runtimeStore;
        StudentDataBlockPersistenceService persistenceService =
            new StudentDataBlockPersistenceService();
        saveCoordinator = new StudentDataSaveCoordinator(persistenceService);
        loadCoordinator = new StudentDataLoadCoordinator(persistenceService);
        SetDataReady(false);
        _ = LoadSessionAsync(uid, runtimeStore, loadVersion);
    }

    private async Task LoadSessionAsync(
        string uid,
        StudentDataRuntimeStore runtimeStore,
        int loadVersion
    )
    {
        bool loadSucceeded = false;
        try
        {
            loadSucceeded = await loadCoordinator.LoadInitialDataAsync(
                uid,
                studentDataRegistry,
                runtimeStore
            );
        }
        catch (Exception exception)
        {
            Debug.LogError($"[StudentDataSession] Data load exception: {exception.Message}");
        }

        if (loadVersion != sessionVersion || !IsCurrentSession(uid))
        {
            return;
        }

        isLoading = false;
        loadingUid = null;
        if (!loadSucceeded)
        {
            Debug.LogError("[StudentDataSession] Initial data load failed.");
            failedUid = uid;
            RuntimeStore = null;
            saveCoordinator = null;
            loadCoordinator = null;
            SetDataReady(false);
            return;
        }

        SetDataReady(true);
    }

    private bool IsCurrentSession(string uid)
    {
        string currentSessionUid;
        return TryGetValidSessionUid(out currentSessionUid)
            && string.Equals(currentSessionUid, uid, StringComparison.Ordinal)
            && string.Equals(currentUid, uid, StringComparison.Ordinal);
    }

    private bool TryGetValidSessionUid(out string uid)
    {
        uid = null;
        if (authService == null
            || !authService.IsReady
            || authService.CurrentUser == null
            || userService == null
            || !userService.IsProfileLoaded
            || userService.CurrentProfile == null
            || accountAccessService == null
            || !accountAccessService.IsReady
            || !accountAccessService.CanUseApplication
            || firestoreService == null
            || !firestoreService.IsReady)
        {
            return false;
        }

        uid = authService.CurrentUser.UserId;
        return !string.IsNullOrWhiteSpace(uid)
            && string.Equals(userService.CurrentProfile.Uid, uid, StringComparison.Ordinal);
    }

    private void ResolveServicesAndSubscribe()
    {
        if (authService == null)
        {
            authService = FirebaseAuthService.Instance;
        }

        if (userService == null)
        {
            userService = UserService.Instance;
        }

        if (accountAccessService == null)
        {
            accountAccessService = AccountAccessService.Instance;
        }

        if (firestoreService == null)
        {
            firestoreService = FirestoreService.Instance;
        }

        if (!subscribedToAuthService && authService != null)
        {
            authService.StateChanged += HandleAuthStateChanged;
            subscribedToAuthService = true;
        }

        if (!subscribedToUserService && userService != null)
        {
            userService.OnProfileLoaded += HandleProfileLoaded;
            userService.OnProfileCleared += HandleProfileCleared;
            subscribedToUserService = true;
        }

        if (!subscribedToAccountAccess && accountAccessService != null)
        {
            accountAccessService.OnAccountAccessChanged += HandleAccountAccessChanged;
            subscribedToAccountAccess = true;
        }

        FirestoreService.OnFirestoreReady -= HandleFirestoreReady;
        FirestoreService.OnFirestoreReady += HandleFirestoreReady;
    }

    private void UnsubscribeFromServices()
    {
        if (authService != null && subscribedToAuthService)
        {
            authService.StateChanged -= HandleAuthStateChanged;
        }

        if (userService != null && subscribedToUserService)
        {
            userService.OnProfileLoaded -= HandleProfileLoaded;
            userService.OnProfileCleared -= HandleProfileCleared;
        }

        if (accountAccessService != null && subscribedToAccountAccess)
        {
            accountAccessService.OnAccountAccessChanged -= HandleAccountAccessChanged;
        }

        FirestoreService.OnFirestoreReady -= HandleFirestoreReady;
        subscribedToAuthService = false;
        subscribedToUserService = false;
        subscribedToAccountAccess = false;
    }

    private void InvalidateSession()
    {
        sessionVersion++;
        isLoading = false;
        loadingUid = null;
        currentUid = null;
        failedUid = null;
        RuntimeStore = null;
        saveCoordinator = null;
        loadCoordinator = null;
        SetDataReady(false);
    }

    private void SetDataReady(bool value)
    {
        if (IsDataReady == value)
        {
            return;
        }

        IsDataReady = value;
        OnDataReadyChanged?.Invoke(value);
    }
}
