using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

public class AdminUserDetailTest : MonoBehaviour
{
    [SerializeField] private string targetUid;
    [SerializeField] private TMP_InputField targetUidInputField;
    [SerializeField] private TMP_Text resultText;

    [ContextMenu("Run Test")]
    public void RunTest()
    {
        _ = RunTestAsync();
    }

    public async Task RunTestAsync()
    {
        string authLine = "Auth: Logged Out";
        string statusLine = "Status: N/A";
        string accessLine = "Access: False";
        string roleLine = "Role: N/A";
        string targetUidValue = GetTargetUid();
        string targetLine = $"Target UID: {GetTargetUidDisplay(targetUidValue)}";

        SetResultText(
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
            + targetLine
            + "\n\n"
            + "Result: RUNNING",
            true
        );

        AdminUserService adminUserService = AdminUserService.Instance;
        FirebaseAuthService authService = FirebaseAuthService.Instance;
        AuthorizationService authorizationService = AuthorizationService.Instance;
        AccountAccessService accountAccessService = AccountAccessService.Instance;
        UserService userService = UserService.Instance;

        bool isAuthenticated = authService != null && authService.IsAuthenticated;
        authLine = $"Auth: {(isAuthenticated ? "Logged In" : "Logged Out")}";
        statusLine = BuildStatusLine(userService, isAuthenticated);
        bool hasAccess = accountAccessService != null && accountAccessService.CanUseApplication;
        accessLine = $"Access: {hasAccess}";
        roleLine = BuildRoleLine(authorizationService, isAuthenticated);

        if (adminUserService == null)
        {
            WriteError(
                "AdminUserService no disponible.",
                authLine,
                statusLine,
                accessLine,
                roleLine,
                targetLine
            );
            return;
        }

        if (!isAuthenticated)
        {
            WriteDenied(
                "User not authenticated.",
                authLine,
                statusLine,
                accessLine,
                roleLine,
                targetLine
            );
            return;
        }

        if (!hasAccess)
        {
            WriteDenied(
                "Account access denied.",
                authLine,
                statusLine,
                accessLine,
                roleLine,
                targetLine
            );
            return;
        }

        if (!IsAdministrativeRole(authorizationService))
        {
            WriteDenied(
                "Administrative role required.",
                authLine,
                statusLine,
                accessLine,
                roleLine,
                targetLine
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(targetUidValue))
        {
            WriteError(
                "Target UID is required.",
                authLine,
                statusLine,
                accessLine,
                roleLine,
                targetLine
            );
            return;
        }

        try
        {
            UserProfile user = await adminUserService.GetUserByUidAsync(targetUidValue);

            if (user == null)
            {
                WriteNotFound(authLine, statusLine, accessLine, roleLine, targetLine);
                return;
            }

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
                + targetLine
                + "\n\n"
                + "Result: SUCCESS\n"
                + "User:\n"
                + $"Uid: {user.Uid}\n"
                + $"Name: {user.DisplayName}\n"
                + $"Email: {user.Email}\n"
                + $"Role: {user.Role}\n"
                + $"Status: {user.Status}";

            SetResultText(output, true);
            Debug.Log($"[AdminUserDetailTest] Uid={user.Uid}, Role={user.Role}, Status={user.Status}");
        }
        catch (ArgumentException exception)
        {
            WriteError(exception.Message, authLine, statusLine, accessLine, roleLine, targetLine);
        }
        catch (UnauthorizedAccessException exception)
        {
            WriteDenied(
                exception.Message,
                authLine,
                statusLine,
                accessLine,
                roleLine,
                targetLine
            );
        }
        catch (InvalidOperationException exception)
        {
            WriteError(exception.Message, authLine, statusLine, accessLine, roleLine, targetLine);
        }
        catch (Exception exception)
        {
            WriteError(
                GetSafeErrorReason(exception),
                authLine,
                statusLine,
                accessLine,
                roleLine,
                targetLine
            );
        }
    }

    private string BuildHeader()
    {
        return "ADMIN USER DETAIL TEST";
    }

    private string GetTargetUid()
    {
        if (targetUidInputField != null)
        {
            return targetUidInputField.text;
        }

        return targetUid;
    }

    private string GetTargetUidDisplay(string uid)
    {
        return string.IsNullOrWhiteSpace(uid) ? "<empty>" : uid.Trim();
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
        string authLine,
        string statusLine,
        string accessLine,
        string roleLine,
        string targetLine
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
            + targetLine
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
        string roleLine,
        string targetLine
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
            + targetLine
            + "\n\n"
            + "Result: ERROR\n"
            + $"Reason: {reason}";

        SetResultText(output, true);
    }

    private void WriteNotFound(
        string authLine,
        string statusLine,
        string accessLine,
        string roleLine,
        string targetLine
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
            + targetLine
            + "\n\n"
            + "Result: NOT FOUND";

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
            Debug.Log($"[AdminUserDetailTest]\n{message}");
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
