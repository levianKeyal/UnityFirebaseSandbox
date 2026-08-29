using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

public class AdminUserService : MonoBehaviour
{
    public static AdminUserService Instance { get; private set; }

    public bool IsReady { get; private set; }

    private FirebaseAuthService authService;
    private FirestoreService firestoreService;
    private AuthorizationService authorizationService;
    private AccountAccessService accountAccessService;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        RefreshCachedServices();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public Task<IReadOnlyList<UserProfile>> GetAllUsersAsync()
    {
        RefreshCachedServices();
        EnsureAdminReadAccess();

        return GetAllUsersInternalAsync();
    }

    private void RefreshCachedServices()
    {
        authService = FirebaseAuthService.Instance;
        firestoreService = FirestoreService.Instance;
        authorizationService = AuthorizationService.Instance;
        accountAccessService = AccountAccessService.Instance;

        IsReady =
            authService != null
            && authService.IsReady
            && firestoreService != null
            && firestoreService.IsReady
            && authorizationService != null
            && authorizationService.IsReady
            && accountAccessService != null
            && accountAccessService.IsReady;
    }

    private void EnsureAdminReadAccess()
    {
        if (authService == null || !authService.IsReady)
        {
            throw new InvalidOperationException("FirebaseAuthService no esta listo.");
        }

        FirebaseUser currentUser = authService.CurrentUser;
        if (currentUser == null)
        {
            throw new InvalidOperationException("No hay usuario autenticado.");
        }

        if (firestoreService == null || !firestoreService.IsReady || firestoreService.Database == null)
        {
            throw new InvalidOperationException("FirestoreService no esta listo.");
        }

        if (authorizationService == null || !authorizationService.IsReady)
        {
            throw new InvalidOperationException("AuthorizationService no esta listo.");
        }

        if (accountAccessService == null || !accountAccessService.IsReady)
        {
            throw new InvalidOperationException("AccountAccessService no esta listo.");
        }

        if (!accountAccessService.CanUseApplication)
        {
            throw new UnauthorizedAccessException(
                "Acceso administrativo denegado: la cuenta no tiene acceso activo."
            );
        }

        UserRole effectiveRole = authorizationService.CurrentRole;
        if (effectiveRole != UserRole.Admin && effectiveRole != UserRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException(
                $"Acceso administrativo denegado: rol insuficiente ({effectiveRole})."
            );
        }
    }

    private async Task<IReadOnlyList<UserProfile>> GetAllUsersInternalAsync()
    {
        FirebaseFirestore database = firestoreService.Database;
        QuerySnapshot snapshot = await database.Collection("users").GetSnapshotAsync();

        List<UserProfile> users = new List<UserProfile>();

        foreach (DocumentSnapshot document in snapshot.Documents)
        {
            try
            {
                users.Add(UserFirestoreMapper.FromDocument(document));
            }
            catch (Exception exception)
            {
                string message =
                    $"[AdminUserService] No se pudo convertir users/{document.Id} a UserProfile.";
                Debug.LogError($"{message} Detalle: {exception.Message}");
                throw new InvalidOperationException(message, exception);
            }
        }

        return users;
    }
}
