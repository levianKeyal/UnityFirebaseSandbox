using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class AdminUserListTest : MonoBehaviour
{
    [SerializeField] private TMP_Text resultText;

    [ContextMenu("Run Test")]
    public async void RunTest()
    {
        await RunTestAsync();
    }

    public async Task RunTestAsync()
    {
        SetResultText(BuildHeader(), false);

        AdminUserService adminUserService = AdminUserService.Instance;
        if (adminUserService == null)
        {
            WriteDenied("AdminUserService no disponible.");
            return;
        }

        FirebaseAuthService authService = FirebaseAuthService.Instance;
        AuthorizationService authorizationService = AuthorizationService.Instance;
        AccountAccessService accountAccessService = AccountAccessService.Instance;
        UserService userService = UserService.Instance;

        bool isAuthenticated = authService != null && authService.IsAuthenticated;
        string authLine = $"Auth: {(isAuthenticated ? "Logged In" : "Logged Out")}";
        string statusLine = BuildStatusLine(userService, isAuthenticated);
        bool hasAccess = accountAccessService != null && accountAccessService.CanUseApplication;
        string accessLine = $"Access: {hasAccess}";
        string roleLine = BuildRoleLine(authorizationService, isAuthenticated);

        if (!isAuthenticated)
        {
            WriteDenied("User not authenticated.", authLine, statusLine, accessLine, roleLine);
            return;
        }

        if (!hasAccess)
        {
            WriteDenied("Account access denied.", authLine, statusLine, accessLine, roleLine);
            return;
        }

        if (!IsAdministrativeRole(authorizationService))
        {
            WriteDenied(
                "Administrative role required.",
                authLine,
                statusLine,
                accessLine,
                roleLine
            );
            return;
        }

        try
        {
            IReadOnlyList<UserProfile> users = await adminUserService.GetAllUsersAsync();
            string output =
                BuildHeader()
                + "\n"
                + authLine
                + "\n"
                + statusLine
                + "\n"
                + accessLine
                + "\n"
                + roleLine
                + "\n\n"
                + "Result: SUCCESS\n"
                + $"Users Found: {users.Count}";

            SetResultText(output, true);
            Debug.Log($"[AdminUserListTest] Total users: {users.Count}");
        }
        catch (Exception exception)
        {
            string reason = GetSafeErrorReason(exception);
            WriteError(reason, authLine, statusLine, accessLine, roleLine);
        }
    }

    private string BuildHeader()
    {
        return "ADMIN USER LIST TEST";
    }

    private string BuildStatusLine(UserService userService, bool isAuthenticated)
    {
        if (!isAuthenticated)
        {
            return "Status: N/A";
        }

        if (userService == null || !userService.IsProfileLoaded || userService.CurrentProfile == null)
        {
            return "Status: N/A";
        }

        return $"Status: {userService.CurrentProfile.Status}";
    }

    private string BuildRoleLine(AuthorizationService authorizationService, bool isAuthenticated)
    {
        if (!isAuthenticated || authorizationService == null)
        {
            return "Role: N/A";
        }

        return $"Role: {authorizationService.CurrentRole}";
    }

    private static bool IsAdministrativeRole(AuthorizationService authorizationService)
    {
        if (authorizationService == null)
        {
            return false;
        }

        return authorizationService.CurrentRole == UserRole.Admin
            || authorizationService.CurrentRole == UserRole.SuperAdmin;
    }

    private void WriteDenied(
        string reason,
        string authLine = null,
        string statusLine = null,
        string accessLine = null,
        string roleLine = null
    )
    {
        string output =
            BuildHeader()
            + "\n"
            + (authLine ?? "Auth: Logged Out")
            + "\n"
            + (statusLine ?? "Status: N/A")
            + "\n"
            + (accessLine ?? "Access: False")
            + "\n"
            + (roleLine ?? "Role: N/A")
            + "\n\n"
            + "Result: DENIED\n"
            + $"Reason: {reason}";

        SetResultText(output, true);
    }

    private void WriteError(
        string reason,
        string authLine,
        string statusLine,
        string accessLine,
        string roleLine
    )
    {
        string output =
            BuildHeader()
            + "\n"
            + authLine
            + "\n"
            + statusLine
            + "\n"
            + accessLine
            + "\n"
            + roleLine
            + "\n\n"
            + "Result: ERROR\n"
            + $"Reason: {reason}";

        SetResultText(output, true);
    }

    private void SetResultText(string message, bool logToConsole)
    {
        if (resultText != null)
        {
            resultText.text = message;
        }

        if (logToConsole)
        {
            Debug.Log($"[AdminUserListTest]\n{message}");
        }
    }

    private static string GetSafeErrorReason(Exception exception)
    {
        if (exception == null)
        {
            return "Unknown error";
        }

        if (exception is UnauthorizedAccessException || exception is InvalidOperationException)
        {
            return exception.Message;
        }

        if (!string.IsNullOrWhiteSpace(exception.Message))
        {
            return exception.Message;
        }

        return exception.GetType().Name;
    }
}
