using System;
using System.IO;
using TMPro;
using UnityEngine;

public class AuthorizationTestUI : MonoBehaviour
{
    [SerializeField] private TMP_Text statusText;

    private bool subscribed;
    private Coroutine subscriptionCoroutine;
    private int refreshCount;
    private int buttonPressCount;
    private string lastEvent = "None";
    private string sessionLogPath;
    private bool sessionLogReady;

    private void OnEnable()
    {
        PrepareSessionLog();

        Debug.Log(
            $"[AuthorizationTestUI] OnEnable. GameObject={gameObject.name}, InstanceID={GetInstanceID()}, " +
            $"StatusText={(statusText != null ? statusText.gameObject.name : "NULL")}"
        );
        AppendSessionLog(
            $"[AuthorizationTestUI] OnEnable. GameObject={gameObject.name}, InstanceID={GetInstanceID()}, " +
            $"StatusText={(statusText != null ? statusText.gameObject.name : "NULL")}"
        );

        subscriptionCoroutine = StartCoroutine(WaitForServicesAndSubscribe());
        Refresh();
    }

    private void Start()
    {
        Refresh();
    }

    private void OnDisable()
    {
        UnsubscribeFromServices();

        if (subscriptionCoroutine != null)
        {
            StopCoroutine(subscriptionCoroutine);
            subscriptionCoroutine = null;
        }
    }

    public async void HandleRefreshAuthorizationClicked()
    {
        buttonPressCount++;
        lastEvent = "RefreshButton";

        Debug.Log(
            $"[AuthorizationTestUI] Refresh button clicked. InstanceID={GetInstanceID()}"
        );
        AppendSessionLog(
            $"[AuthorizationTestUI] Refresh button clicked. InstanceID={GetInstanceID()}"
        );

        AuthorizationService authorizationService = AuthorizationService.Instance;
        if (authorizationService == null)
        {
            Debug.LogWarning("[AuthorizationTestUI] AuthorizationService no disponible.");
            AppendSessionLog("[AuthorizationTestUI] AuthorizationService no disponible.");
            return;
        }

        try
        {
            if (statusText != null)
            {
                statusText.text = "REFRESH BUTTON PRESSED...";
            }

            Debug.Log("[AuthorizationTestUI] Starting forced authorization refresh.");
            AppendSessionLog("[AuthorizationTestUI] Starting forced authorization refresh.");
            await authorizationService.RefreshAuthorizationAsync(true);
            Debug.Log("[AuthorizationTestUI] Forced authorization refresh completed.");
            AppendSessionLog("[AuthorizationTestUI] Forced authorization refresh completed.");
            Refresh();
        }
        catch (System.Exception exception)
        {
            Debug.LogError(
                $"[AuthorizationTestUI] Forced authorization refresh FAILED: {exception.Message}"
            );
            AppendSessionLog(
                $"[AuthorizationTestUI] Forced authorization refresh FAILED: {exception.Message}"
            );
        }
    }

    private System.Collections.IEnumerator WaitForServicesAndSubscribe()
    {
        while (isActiveAndEnabled)
        {
            if (TrySubscribeToServices())
            {
                Refresh();
                subscriptionCoroutine = null;
                yield break;
            }

            yield return null;
        }

        subscriptionCoroutine = null;
    }

    private bool TrySubscribeToServices()
    {
        AuthorizationService authorizationService = AuthorizationService.Instance;
        UserService userService = UserService.Instance;

        if (authorizationService == null || userService == null || subscribed)
        {
            return authorizationService != null && userService != null && subscribed;
        }

        authorizationService.OnAuthorizationChanged += HandleAuthorizationChanged;
        userService.OnProfileLoaded += HandleProfileChanged;
        userService.OnProfileCleared += HandleProfileCleared;
        subscribed = true;

        Debug.Log(
            $"[AuthorizationTestUI] Subscribed to services. UserServiceInstanceID={userService.GetInstanceID()}, " +
            $"AuthorizationServiceInstanceID={authorizationService.GetInstanceID()}"
        );
        AppendSessionLog(
            $"[AuthorizationTestUI] Subscribed to services. UserServiceInstanceID={userService.GetInstanceID()}, " +
            $"AuthorizationServiceInstanceID={authorizationService.GetInstanceID()}"
        );

        return true;
    }

    private void UnsubscribeFromServices()
    {
        UserService userService = UserService.Instance;
        AuthorizationService authorizationService = AuthorizationService.Instance;

        if (authorizationService != null && subscribed)
        {
            authorizationService.OnAuthorizationChanged -= HandleAuthorizationChanged;
        }

        if (userService != null && subscribed)
        {
            userService.OnProfileLoaded -= HandleProfileChanged;
            userService.OnProfileCleared -= HandleProfileCleared;
        }

        subscribed = false;
    }

    private void HandleProfileChanged(UserProfile profile)
    {
        lastEvent = "OnProfileLoaded";
        string message =
            $"[AuthorizationTestUI] OnProfileLoaded received. Role={(profile != null ? profile.Role.ToString() : "NULL")}";
        Debug.Log(message);
        AppendSessionLog(message);
        Refresh();
    }

    private void HandleProfileCleared()
    {
        lastEvent = "OnProfileCleared";
        Debug.Log("[AuthorizationTestUI] OnProfileCleared received.");
        AppendSessionLog("[AuthorizationTestUI] OnProfileCleared received.");
        Refresh();
    }

    private void HandleAuthorizationChanged()
    {
        lastEvent = "OnAuthorizationChanged";
        Debug.Log("[AuthorizationTestUI] OnAuthorizationChanged received.");
        AppendSessionLog("[AuthorizationTestUI] OnAuthorizationChanged received.");
        Refresh();
    }

    private void Refresh()
    {
        refreshCount++;

        if (statusText == null)
        {
            return;
        }

        AuthorizationService authorizationService = AuthorizationService.Instance;
        UserService userService = UserService.Instance;

        Debug.Log(
            $"[AuthorizationTestUI] Refresh(). UIInstanceID={GetInstanceID()}, " +
            $"UserServiceInstanceID={(userService != null ? userService.GetInstanceID().ToString() : "NULL")}, " +
            $"AuthorizationServiceInstanceID={(authorizationService != null ? authorizationService.GetInstanceID().ToString() : "NULL")}"
        );
        AppendSessionLog(
            $"[AuthorizationTestUI] Refresh(). UIInstanceID={GetInstanceID()}, " +
            $"UserServiceInstanceID={(userService != null ? userService.GetInstanceID().ToString() : "NULL")}, " +
            $"AuthorizationServiceInstanceID={(authorizationService != null ? authorizationService.GetInstanceID().ToString() : "NULL")}"
        );

        string userServiceInstanceId = userService != null ? userService.GetInstanceID().ToString() : "NULL";
        string authorizationServiceInstanceId = authorizationService != null
            ? authorizationService.GetInstanceID().ToString()
            : "NULL";
        string profileLoaded = userService != null ? userService.IsProfileLoaded.ToString() : "NULL";
        string currentProfile = userService != null && userService.CurrentProfile != null ? "EXISTS" : "NULL";
        string profileUid = userService != null && userService.CurrentProfile != null
            ? userService.CurrentProfile.Uid
            : "NULL";
        string profileRole = userService != null && userService.CurrentProfile != null
            ? userService.CurrentProfile.Role.ToString()
            : "NULL";
        string authClaimRole = authorizationService != null
            ? authorizationService.AuthClaimRole.ToString()
            : "NULL";
        string effectiveRole = authorizationService != null
            ? authorizationService.CurrentRole.ToString()
            : "NULL";

        Debug.Log(
            $"[AuthorizationTestUI] Values -> ProfileLoaded={profileLoaded}, CurrentProfile={currentProfile}, " +
            $"ProfileRole={profileRole}, ClaimRole={authClaimRole}, EffectiveRole={effectiveRole}"
        );
        AppendSessionLog(
            $"[AuthorizationTestUI] Values -> ProfileLoaded={profileLoaded}, CurrentProfile={currentProfile}, " +
            $"ProfileRole={profileRole}, ClaimRole={authClaimRole}, EffectiveRole={effectiveRole}"
        );

        string panelText =
            "AUTHORIZATION DEBUG\n" +
            $"Refresh Count: {refreshCount}\n" +
            $"Button Count: {buttonPressCount}\n" +
            $"Subscribed: {subscribed}\n\n" +
            "UserService:\n" +
            $"Instance ID: {userServiceInstanceId}\n" +
            $"Profile Loaded: {profileLoaded}\n" +
            $"Current Profile: {currentProfile}\n" +
            $"Profile UID: {profileUid}\n" +
            $"Profile Role: {profileRole}\n\n" +
            "AuthorizationService:\n" +
            $"Instance ID: {authorizationServiceInstanceId}\n" +
            $"Auth Claim Role: {authClaimRole}\n" +
            $"Effective Role: {effectiveRole}\n\n" +
            $"Last Event: {lastEvent}";

        statusText.text = panelText;
        AppendSessionLog("---- PANEL TEXT ----");
        AppendSessionLog(panelText);
    }

    private void PrepareSessionLog()
    {
        if (sessionLogReady)
        {
            return;
        }

        string fileName = $"AuthorizationTestUI_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt";
        sessionLogPath = Path.Combine(Application.persistentDataPath, fileName);
        sessionLogReady = true;

        try
        {
            File.WriteAllText(
                sessionLogPath,
                $"AuthorizationTestUI session started: {DateTime.UtcNow:O}{Environment.NewLine}"
            );
            Debug.Log($"[AuthorizationTestUI] Session log file: {sessionLogPath}");
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[AuthorizationTestUI] No se pudo preparar el log de texto: {exception.Message}"
            );
        }
    }

    private void AppendSessionLog(string message)
    {
        if (!sessionLogReady || string.IsNullOrWhiteSpace(sessionLogPath))
        {
            return;
        }

        try
        {
            File.AppendAllText(
                sessionLogPath,
                $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}"
            );
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[AuthorizationTestUI] No se pudo escribir al log de texto: {exception.Message}"
            );
        }
    }
}
