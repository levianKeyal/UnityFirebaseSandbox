using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AdminUserWriteTest : MonoBehaviour
{
    private const float DropdownFontSize = 32f;
    private const float DropdownItemHeight = 72f;
    private const float DropdownVisibleItemCount = 5f;

    private static readonly UserStatus[] SupportedStatuses =
    {
        UserStatus.Active,
        UserStatus.Suspended,
        UserStatus.Disabled
    };

    private static readonly UserRole[] SupportedRoles =
    {
        UserRole.User,
        UserRole.Admin,
        UserRole.SuperAdmin
    };

    [SerializeField] private string targetUid;
    [SerializeField] private TMP_InputField targetUidInput;
    [SerializeField] private UserStatus targetStatus = UserStatus.Active;
    [SerializeField] private UserRole targetRole = UserRole.User;
    [SerializeField] private TMP_Dropdown statusDropdown;
    [SerializeField] private TMP_Dropdown roleDropdown;
    [SerializeField] private TMP_Dropdown userDropdown;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private AuthTestUI authTestUI;

    private readonly List<UserProfile> loadedUsers = new List<UserProfile>();
    private bool isRefreshingUsers;
    private bool autoRefreshStarted;
    private string autoRefreshSessionUid;
    private bool subscribedToAuthService;
    private bool subscribedToUserService;
    private bool subscribedToAuthorizationService;
    private bool subscribedToAccountAccessService;
    private Coroutine subscriptionBootstrapCoroutine;

    private void Awake()
    {
        if (authTestUI == null)
        {
            authTestUI = FindFirstObjectByType<AuthTestUI>();
        }

        ConfigureStatusDropdown();
        ConfigureRoleDropdown();
        ConfigureUserDropdown();
        ConfigureDropdownVisuals(statusDropdown);
        ConfigureDropdownVisuals(roleDropdown);
        ConfigureDropdownVisuals(userDropdown);
    }

    private void OnEnable()
    {
        SubscribeToReadinessEvents();
        TryAutoRefreshUsers();
        StartSubscriptionBootstrapIfNeeded();
    }

    private void OnDisable()
    {
        if (subscriptionBootstrapCoroutine != null)
        {
            StopCoroutine(subscriptionBootstrapCoroutine);
            subscriptionBootstrapCoroutine = null;
        }

        UnsubscribeFromReadinessEvents();
    }

    public void SetStatus()
    {
        _ = SetStatusAsync();
    }

    public void SetRole()
    {
        _ = SetRoleAsync();
    }

    private async Task SetStatusAsync()
    {
        try
        {
            AdminUserService adminUserService = AdminUserService.Instance;
            string effectiveTargetUid = GetTargetUid();
            UserStatus requestedStatus = GetRequestedStatus();
            if (adminUserService == null)
            {
                WriteResult("ERROR", "AdminUserService no disponible.", "Status");
                return;
            }

            await adminUserService.SetUserStatusAsync(effectiveTargetUid, requestedStatus);
            WriteResult("SUCCESS", "Status actualizado correctamente.", "Status", requestedStatus.ToString());
        }
        catch (Exception exception)
        {
            WriteExceptionResult(exception, "Status");
        }
    }

    private async Task SetRoleAsync()
    {
        try
        {
            AdminUserService adminUserService = AdminUserService.Instance;
            string effectiveTargetUid = GetTargetUid();
            UserRole requestedRole = GetRequestedRole();
            if (adminUserService == null)
            {
                WriteResult("ERROR", "AdminUserService no disponible.", "Role");
                return;
            }

            await adminUserService.SetUserRoleAsync(effectiveTargetUid, requestedRole);
            WriteResult("SUCCESS", "Role actualizado correctamente.", "Role", requestedRole.ToString());
        }
        catch (Exception exception)
        {
            WriteExceptionResult(exception, "Role");
        }
    }

    public void RefreshUsers()
    {
        _ = RefreshUsersAsync();
    }

    public void OnUserSelected(int index)
    {
        if (index <= 0)
        {
            return;
        }

        int userIndex = index - 1;
        if (userIndex < 0 || userIndex >= loadedUsers.Count)
        {
            WriteResult("ERROR", "Seleccion de usuario invalida.", "Selection");
            return;
        }

        UserProfile selectedUser = loadedUsers[userIndex];
        if (selectedUser == null || string.IsNullOrWhiteSpace(selectedUser.Uid))
        {
            WriteResult("ERROR", "El usuario seleccionado no tiene UID.", "Selection");
            return;
        }

        targetUid = selectedUser.Uid.Trim();
        if (targetUidInput != null)
        {
            targetUidInput.text = targetUid;
        }

        string displayName = string.IsNullOrWhiteSpace(selectedUser.DisplayName)
            ? "N/A"
            : selectedUser.DisplayName;
        string email = string.IsNullOrWhiteSpace(selectedUser.Email) ? "N/A" : selectedUser.Email;
        WriteResult(
            "SUCCESS",
            $"Selected User\nDisplayName: {displayName}\nEmail: {email}\nUID: {targetUid}",
            "Selection"
        );
    }

    private async Task RefreshUsersAsync()
    {
        if (isRefreshingUsers)
        {
            return;
        }

        if (userDropdown == null)
        {
            WriteResult("ERROR", "userDropdown no esta asignado.", "Users");
            return;
        }

        isRefreshingUsers = true;

        try
        {
            loadedUsers.Clear();
            ResetUserDropdown();

            AdminUserService adminUserService = AdminUserService.Instance;
            if (adminUserService == null)
            {
                WriteResult("ERROR", "AdminUserService no disponible.", "Users");
                return;
            }

            IReadOnlyList<UserProfile> users = await adminUserService.GetAllUsersAsync();
            if (users != null)
            {
                foreach (UserProfile user in users)
                {
                    if (user != null)
                    {
                        loadedUsers.Add(user);
                    }
                }
            }

            loadedUsers.Sort(CompareUsers);
            PopulateUserDropdown();
            WriteResult("SUCCESS", $"Users loaded: {loadedUsers.Count}", "Users");
        }
        catch (Exception exception)
        {
            WriteExceptionResult(exception, "Users");
        }
        finally
        {
            isRefreshingUsers = false;
        }
    }

    private void WriteExceptionResult(Exception exception, string operation)
    {
        string result = IsDeniedException(exception) ? "DENIED" : "ERROR";
        string reason = GetSafeErrorReason(exception);
        WriteResult(result, reason, operation);
    }

    private void WriteResult(string result, string reason, string operation, string requestedValue = null)
    {
        string output =
            "ADMIN USER WRITE TEST"
            + "\n"
            + BuildActorDiagnostics()
            + "\nTarget UID: "
            + GetTargetUidDisplay()
            + BuildRequestedLine(operation, requestedValue)
            + "\nResult: "
            + result
            + "\nReason: "
            + reason;

        if (resultText != null)
        {
            resultText.text = output;
        }

        if (result == "SUCCESS")
        {
            Debug.Log($"[AdminUserWriteTest]\n{output}");
        }
        else
        {
            Debug.LogError($"[AdminUserWriteTest]\n{output}");
        }
    }

    private void ConfigureStatusDropdown()
    {
        if (statusDropdown == null)
        {
            return;
        }

        statusDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        foreach (UserStatus status in SupportedStatuses)
        {
            options.Add(new TMP_Dropdown.OptionData(status.ToString()));
        }

        statusDropdown.AddOptions(options);
        statusDropdown.SetValueWithoutNotify(GetStatusIndex(targetStatus));
        statusDropdown.RefreshShownValue();
    }

    private void ConfigureRoleDropdown()
    {
        if (roleDropdown == null)
        {
            return;
        }

        roleDropdown.ClearOptions();
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        foreach (UserRole role in SupportedRoles)
        {
            options.Add(new TMP_Dropdown.OptionData(role.ToString()));
        }

        roleDropdown.AddOptions(options);
        roleDropdown.SetValueWithoutNotify(GetRoleIndex(targetRole));
        roleDropdown.RefreshShownValue();
    }

    private void ConfigureUserDropdown()
    {
        ResetUserDropdown();
    }

    private void ConfigureDropdownVisuals(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
        {
            return;
        }

        if (dropdown.captionText != null)
        {
            dropdown.captionText.fontSize = DropdownFontSize;
            dropdown.captionText.enableAutoSizing = false;
        }

        if (dropdown.itemText != null)
        {
            dropdown.itemText.fontSize = DropdownFontSize;
            dropdown.itemText.enableAutoSizing = false;
            ConfigureDropdownItemHeight(dropdown);
        }

        if (dropdown.template != null)
        {
            Vector2 templateSize = dropdown.template.sizeDelta;
            templateSize.y = DropdownItemHeight * DropdownVisibleItemCount;
            dropdown.template.sizeDelta = templateSize;
        }
    }

    private void ConfigureDropdownItemHeight(TMP_Dropdown dropdown)
    {
        Transform current = dropdown.itemText.transform.parent;
        while (current != null && current != dropdown.template)
        {
            LayoutElement layoutElement = current.GetComponent<LayoutElement>();
            if (layoutElement != null)
            {
                layoutElement.preferredHeight = DropdownItemHeight;
                return;
            }

            current = current.parent;
        }

        RectTransform itemTransform = dropdown.itemText.transform.parent as RectTransform;
        if (itemTransform != null)
        {
            Vector2 itemSize = itemTransform.sizeDelta;
            itemSize.y = DropdownItemHeight;
            itemTransform.sizeDelta = itemSize;
        }
    }

    private UserStatus GetRequestedStatus()
    {
        if (statusDropdown != null && statusDropdown.value >= 0 && statusDropdown.value < SupportedStatuses.Length)
        {
            return SupportedStatuses[statusDropdown.value];
        }

        return targetStatus;
    }

    private UserRole GetRequestedRole()
    {
        if (roleDropdown != null && roleDropdown.value >= 0 && roleDropdown.value < SupportedRoles.Length)
        {
            return SupportedRoles[roleDropdown.value];
        }

        return targetRole;
    }

    private void ResetUserDropdown()
    {
        if (userDropdown == null)
        {
            return;
        }

        userDropdown.ClearOptions();
        userDropdown.AddOptions(new List<string> { "Seleccionar usuario..." });
        userDropdown.SetValueWithoutNotify(0);
        userDropdown.RefreshShownValue();
    }

    private void SubscribeToReadinessEvents()
    {
        FirebaseAuthService authService = FirebaseAuthService.Instance;
        if (authService != null && !subscribedToAuthService)
        {
            authService.StateChanged += HandleAuthStateChanged;
            subscribedToAuthService = true;
        }

        UserService userService = UserService.Instance;
        if (userService != null && !subscribedToUserService)
        {
            userService.OnProfileLoaded += HandleProfileLoaded;
            userService.OnProfileCleared += HandleProfileCleared;
            subscribedToUserService = true;
        }

        AuthorizationService authorizationService = AuthorizationService.Instance;
        if (authorizationService != null && !subscribedToAuthorizationService)
        {
            authorizationService.OnAuthorizationChanged += HandleAuthorizationChanged;
            subscribedToAuthorizationService = true;
        }

        AccountAccessService accountAccessService = AccountAccessService.Instance;
        if (accountAccessService != null && !subscribedToAccountAccessService)
        {
            accountAccessService.OnAccountAccessChanged += HandleAccountAccessChanged;
            subscribedToAccountAccessService = true;
        }
    }

    private void StartSubscriptionBootstrapIfNeeded()
    {
        if (AreRequiredServicesSubscribed())
        {
            return;
        }

        if (subscriptionBootstrapCoroutine == null)
        {
            Debug.Log("[AdminUserWriteTest] Waiting for services...");
            subscriptionBootstrapCoroutine = StartCoroutine(BootstrapServiceSubscriptions());
        }
    }

    private IEnumerator BootstrapServiceSubscriptions()
    {
        while (!AreRequiredServicesSubscribed())
        {
            SubscribeToReadinessEvents();

            if (AreRequiredServicesSubscribed())
            {
                break;
            }

            yield return null;
        }

        TryAutoRefreshUsers();
        Debug.Log("[AdminUserWriteTest] Service subscriptions ready.");
        subscriptionBootstrapCoroutine = null;
    }

    private bool AreRequiredServicesSubscribed()
    {
        return subscribedToAuthService
            && subscribedToUserService
            && subscribedToAuthorizationService
            && subscribedToAccountAccessService;
    }

    private void UnsubscribeFromReadinessEvents()
    {
        if (subscribedToAuthService && FirebaseAuthService.Instance != null)
        {
            FirebaseAuthService.Instance.StateChanged -= HandleAuthStateChanged;
        }

        if (subscribedToUserService && UserService.Instance != null)
        {
            UserService.Instance.OnProfileLoaded -= HandleProfileLoaded;
            UserService.Instance.OnProfileCleared -= HandleProfileCleared;
        }

        if (subscribedToAuthorizationService && AuthorizationService.Instance != null)
        {
            AuthorizationService.Instance.OnAuthorizationChanged -= HandleAuthorizationChanged;
        }

        if (subscribedToAccountAccessService && AccountAccessService.Instance != null)
        {
            AccountAccessService.Instance.OnAccountAccessChanged -= HandleAccountAccessChanged;
        }

        subscribedToAuthService = false;
        subscribedToUserService = false;
        subscribedToAuthorizationService = false;
        subscribedToAccountAccessService = false;
    }

    private void HandleAuthStateChanged(Firebase.Auth.FirebaseUser currentUser)
    {
        string currentUid = currentUser != null ? currentUser.UserId : string.Empty;
        if (!string.Equals(autoRefreshSessionUid, currentUid, StringComparison.Ordinal))
        {
            autoRefreshSessionUid = currentUid;
            autoRefreshStarted = false;
            ClearLoadedUsers();
        }

        SubscribeToReadinessEvents();
        TryAutoRefreshUsers();
    }

    private void HandleProfileLoaded(UserProfile profile)
    {
        SubscribeToReadinessEvents();
        TryAutoRefreshUsers();
    }

    private void HandleProfileCleared()
    {
        autoRefreshSessionUid = string.Empty;
        autoRefreshStarted = false;
        ClearLoadedUsers();
    }

    private void ClearLoadedUsers()
    {
        loadedUsers.Clear();
        ResetUserDropdown();
        targetUid = string.Empty;

        if (targetUidInput != null)
        {
            targetUidInput.text = string.Empty;
        }
    }

    private void HandleAuthorizationChanged()
    {
        SubscribeToReadinessEvents();
        TryAutoRefreshUsers();
    }

    private void HandleAccountAccessChanged()
    {
        SubscribeToReadinessEvents();
        TryAutoRefreshUsers();
    }

    private void TryAutoRefreshUsers()
    {
        FirebaseAuthService authService = FirebaseAuthService.Instance;
        UserService userService = UserService.Instance;
        AuthorizationService authorizationService = AuthorizationService.Instance;
        AccountAccessService accountAccessService = AccountAccessService.Instance;

        if (
            authService == null
            || !authService.IsReady
            || !authService.IsAuthenticated
            || userService == null
            || !userService.IsProfileLoaded
            || userService.CurrentProfile == null
            || authorizationService == null
            || !authorizationService.IsReady
            || accountAccessService == null
            || !accountAccessService.IsReady
            || !accountAccessService.CanUseApplication
        )
        {
            return;
        }

        UserRole currentRole = authorizationService.CurrentRole;
        if (currentRole != UserRole.Admin && currentRole != UserRole.SuperAdmin)
        {
            return;
        }

        string currentUid = authService.CurrentUser.UserId;
        if (autoRefreshStarted && string.Equals(autoRefreshSessionUid, currentUid, StringComparison.Ordinal))
        {
            return;
        }

        autoRefreshSessionUid = currentUid;
        autoRefreshStarted = true;
        _ = RefreshUsersAsync();
    }

    private void PopulateUserDropdown()
    {
        if (userDropdown == null)
        {
            return;
        }

        List<string> options = new List<string> { "Seleccionar usuario..." };
        foreach (UserProfile user in loadedUsers)
        {
            options.Add(GetUserDisplayName(user));
        }

        userDropdown.ClearOptions();
        userDropdown.AddOptions(options);
        userDropdown.SetValueWithoutNotify(0);
        userDropdown.RefreshShownValue();
    }

    private static string GetUserDisplayName(UserProfile user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return $"{user.DisplayName} - {user.Email}";
            }

            return user.DisplayName;
        }

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            return user.Email;
        }

        return user.Uid;
    }

    private static int GetStatusIndex(UserStatus status)
    {
        for (int index = 0; index < SupportedStatuses.Length; index++)
        {
            if (SupportedStatuses[index] == status)
            {
                return index;
            }
        }

        return 0;
    }

    private static int GetRoleIndex(UserRole role)
    {
        for (int index = 0; index < SupportedRoles.Length; index++)
        {
            if (SupportedRoles[index] == role)
            {
                return index;
            }
        }

        return 0;
    }

    private static int CompareUsers(UserProfile first, UserProfile second)
    {
        int displayNameComparison = string.Compare(
            first.DisplayName,
            second.DisplayName,
            StringComparison.OrdinalIgnoreCase
        );
        if (displayNameComparison != 0)
        {
            return displayNameComparison;
        }

        return string.Compare(first.Email, second.Email, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildRequestedLine(string operation, string requestedValue)
    {
        if (operation != "Status" && operation != "Role")
        {
            return string.Empty;
        }

        return "\nRequested " + operation + ": " + (requestedValue ?? "N/A");
    }

    private string BuildActorDiagnostics()
    {
        FirebaseAuthService authService = FirebaseAuthService.Instance;
        AuthorizationService authorizationService = AuthorizationService.Instance;
        AccountAccessService accountAccessService = AccountAccessService.Instance;
        UserService userService = UserService.Instance;

        bool isAuthenticated = authService != null && authService.IsAuthenticated;
        string actorRole =
            isAuthenticated && authorizationService != null
                ? authorizationService.CurrentRole.ToString()
                : "N/A";
        string actorStatus = "N/A";
        if (isAuthenticated && userService != null && userService.IsProfileLoaded && userService.CurrentProfile != null)
        {
            actorStatus = userService.CurrentProfile.Status.ToString();
        }

        bool hasAccess = accountAccessService != null && accountAccessService.CanUseApplication;

        return "Actor authenticated: "
            + isAuthenticated
            + "\nActor role: "
            + actorRole
            + "\nActor status: "
            + actorStatus
            + "\nActor access: "
            + hasAccess;
    }

    private string GetTargetUid()
    {
        if (targetUidInput != null && !string.IsNullOrWhiteSpace(targetUidInput.text))
        {
            return targetUidInput.text.Trim();
        }

        return targetUid == null ? null : targetUid.Trim();
    }

    private string GetTargetUidDisplay()
    {
        string effectiveTargetUid = GetTargetUid();
        return string.IsNullOrWhiteSpace(effectiveTargetUid) ? "<empty>" : effectiveTargetUid;
    }

    private static bool IsDeniedException(Exception exception)
    {
        if (exception is UnauthorizedAccessException || exception is ArgumentOutOfRangeException)
        {
            return true;
        }

        return exception is InvalidOperationException
            && exception.Message.IndexOf("propio usuario", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string GetSafeErrorReason(Exception exception)
    {
        if (exception == null)
        {
            return "Unknown error";
        }

        return string.IsNullOrWhiteSpace(exception.Message)
            ? exception.GetType().Name
            : exception.Message;
    }
}
