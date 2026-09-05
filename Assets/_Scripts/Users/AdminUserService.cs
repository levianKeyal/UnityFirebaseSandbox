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

    public async Task<UserProfile> GetUserByUidAsync(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            throw new ArgumentException("Target UID is required.", nameof(uid));
        }

        RefreshCachedServices();
        EnsureAdminReadAccess();

        string normalizedUid = uid.Trim();
        return await GetUserByUidInternalAsync(normalizedUid);
    }

    public async Task SetUserStatusAsync(string uid, UserStatus status)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            throw new ArgumentException("Target UID is required.", nameof(uid));
        }

        RefreshCachedServices();
        EnsureAdminWriteAccess(requireSuperAdmin: false);

        string normalizedUid = uid.Trim();
        EnsureNotSelfModification(normalizedUid);
        EnsureValidStatus(status);

        FirebaseFirestore database = firestoreService.Database;
        DocumentReference userReference = database.Collection("users").Document(normalizedUid);

        await userReference.UpdateAsync(
            new Dictionary<string, object>
            {
                { "status", UserFirestoreMapper.ToFirestoreStatus(status) }
            }
        );
    }

    public async Task SetUserRoleAsync(string uid, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            throw new ArgumentException("Target UID is required.", nameof(uid));
        }

        RefreshCachedServices();
        EnsureAdminWriteAccess(requireSuperAdmin: true);

        string normalizedUid = uid.Trim();
        EnsureNotSelfModification(normalizedUid);
        EnsureValidRole(role);

        FirebaseFirestore database = firestoreService.Database;
        DocumentReference userReference = database.Collection("users").Document(normalizedUid);

        await userReference.UpdateAsync(
            new Dictionary<string, object>
            {
                { "role", UserFirestoreMapper.ToFirestoreRole(role) }
            }
        );
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

    private void EnsureAdminWriteAccess(bool requireSuperAdmin)
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
        if (requireSuperAdmin)
        {
            if (effectiveRole != UserRole.SuperAdmin)
            {
                throw new UnauthorizedAccessException(
                    $"Acceso administrativo denegado: se requiere SuperAdmin ({effectiveRole})."
                );
            }

            return;
        }

        if (effectiveRole != UserRole.Admin && effectiveRole != UserRole.SuperAdmin)
        {
            throw new UnauthorizedAccessException(
                $"Acceso administrativo denegado: rol insuficiente ({effectiveRole})."
            );
        }
    }

    private void EnsureNotSelfModification(string targetUid)
    {
        FirebaseUser currentUser = authService != null ? authService.CurrentUser : null;
        if (currentUser != null && string.Equals(currentUser.UserId, targetUid, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("No puedes modificar tu propio usuario.");
        }
    }

    private static void EnsureValidStatus(UserStatus status)
    {
        switch (status)
        {
            case UserStatus.Active:
            case UserStatus.Suspended:
            case UserStatus.Disabled:
                return;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(status),
                    status,
                    "Estado de usuario invalido."
                );
        }
    }

    private static void EnsureValidRole(UserRole role)
    {
        switch (role)
        {
            case UserRole.User:
            case UserRole.Admin:
                return;
            case UserRole.SuperAdmin:
                throw new ArgumentOutOfRangeException(
                    nameof(role),
                    role,
                    "No se permite asignar SuperAdmin desde Unity."
                );
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(role),
                    role,
                    "Rol de usuario invalido."
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

    private async Task<UserProfile> GetUserByUidInternalAsync(string uid)
    {
        FirebaseFirestore database = firestoreService.Database;
        DocumentReference userReference = database.Collection("users").Document(uid);
        DocumentSnapshot snapshot = await userReference.GetSnapshotAsync();

        if (!snapshot.Exists)
        {
            Debug.LogWarning($"[AdminUserService] users/{uid} no existe.");
            return null;
        }

        try
        {
            return UserFirestoreMapper.FromDocument(snapshot);
        }
        catch (Exception exception)
        {
            string message = $"[AdminUserService] No se pudo convertir users/{uid} a UserProfile.";
            Debug.LogError($"{message} Detalle: {exception.Message}");
            throw new InvalidOperationException(message, exception);
        }
    }
}
