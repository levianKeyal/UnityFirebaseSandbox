using System.Collections;
using Firebase.Auth;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AuthTestUI : MonoBehaviour
{
    [Header("Status Texts")]
    [SerializeField] private TMP_Text firebaseText;
    [SerializeField] private TMP_Text firestoreText;
    [SerializeField] private TMP_Text authText;
    [SerializeField] private TMP_Text profileText;
    [SerializeField] private TMP_Text googleText;

    [Header("Buttons")]
    [SerializeField] private Button signInButton;
    [SerializeField] private Button signOutButton;

    private bool authSubscribed;
    private bool userSubscribed;
    private bool googleSubscribed;

    private Coroutine subscriptionCoroutine;

    private void OnEnable()
    {
        FirebaseBootstrap.OnFirebaseReady += HandleFirebaseReady;
        FirebaseBootstrap.OnFirebaseInitializationFailed += HandleFirebaseInitializationFailed;

        FirestoreService.OnFirestoreReady += HandleFirestoreReady;

        subscriptionCoroutine = StartCoroutine(WaitForServicesAndSubscribe());

        RefreshUI();
    }

    private void Start()
    {
        RefreshUI();
    }

    private void OnDisable()
    {
        FirebaseBootstrap.OnFirebaseReady -= HandleFirebaseReady;
        FirebaseBootstrap.OnFirebaseInitializationFailed -= HandleFirebaseInitializationFailed;

        FirestoreService.OnFirestoreReady -= HandleFirestoreReady;

        UnsubscribeFromServiceEvents();

        if (subscriptionCoroutine != null)
        {
            StopCoroutine(subscriptionCoroutine);
            subscriptionCoroutine = null;
        }
    }

    public void HandleSignInClicked()
    {
        if (GoogleAuthService.Instance == null)
        {
            Debug.LogWarning("[AuthTestUI] GoogleAuthService no esta disponible.");
            return;
        }

        GoogleAuthService.Instance.SignInWithGoogle();
    }

    public void HandleSignOutClicked()
    {
        if (FirebaseAuthService.Instance == null)
        {
            Debug.LogWarning("[AuthTestUI] FirebaseAuthService no esta disponible.");
            return;
        }

        FirebaseAuthService.Instance.SignOut();
    }

    private IEnumerator WaitForServicesAndSubscribe()
    {
        while (isActiveAndEnabled)
        {
            TrySubscribeToServiceEvents();

            bool allAvailable =
                FirebaseAuthService.Instance != null &&
                UserService.Instance != null &&
                GoogleAuthService.Instance != null;

            if (allAvailable)
            {
                RefreshUI();
                subscriptionCoroutine = null;
                yield break;
            }

            yield return null;
        }

        subscriptionCoroutine = null;
    }

    private void TrySubscribeToServiceEvents()
    {
        if (!authSubscribed && FirebaseAuthService.Instance != null)
        {
            FirebaseAuthService.Instance.StateChanged += HandleAuthStateChanged;
            authSubscribed = true;

            Debug.Log("[AuthTestUI] Suscrito a FirebaseAuthService.");
        }

        if (!userSubscribed && UserService.Instance != null)
        {
            UserService.Instance.OnProfileLoaded += HandleProfileLoaded;
            UserService.Instance.OnProfileCleared += HandleProfileCleared;

            userSubscribed = true;

            Debug.Log("[AuthTestUI] Suscrito a UserService.");
        }

        if (!googleSubscribed && GoogleAuthService.Instance != null)
        {
            GoogleAuthService.Instance.StateChanged += HandleGoogleAuthStateChanged;
            googleSubscribed = true;

            Debug.Log("[AuthTestUI] Suscrito a GoogleAuthService.");
        }
    }

    private void UnsubscribeFromServiceEvents()
    {
        if (authSubscribed && FirebaseAuthService.Instance != null)
        {
            FirebaseAuthService.Instance.StateChanged -= HandleAuthStateChanged;
        }

        if (userSubscribed && UserService.Instance != null)
        {
            UserService.Instance.OnProfileLoaded -= HandleProfileLoaded;
            UserService.Instance.OnProfileCleared -= HandleProfileCleared;
        }

        if (googleSubscribed && GoogleAuthService.Instance != null)
        {
            GoogleAuthService.Instance.StateChanged -= HandleGoogleAuthStateChanged;
        }

        authSubscribed = false;
        userSubscribed = false;
        googleSubscribed = false;
    }

    private void HandleFirebaseReady()
    {
        TrySubscribeToServiceEvents();
        RefreshUI();
    }

    private void HandleFirestoreReady()
    {
        TrySubscribeToServiceEvents();
        RefreshUI();
    }

    private void HandleFirebaseInitializationFailed(string message)
    {
        RefreshUI();
    }

    private void HandleAuthStateChanged(FirebaseUser user)
    {
        Debug.Log(
            user != null
                ? $"[AuthTestUI] Auth cambiado. UID: {user.UserId}"
                : "[AuthTestUI] Auth cambiado. Sin usuario."
        );

        RefreshUI();
    }

    private void HandleProfileLoaded(UserProfile profile)
    {
        Debug.Log(
            $"[AuthTestUI] Perfil cargado: {profile?.Uid ?? "null"}"
        );

        RefreshUI();
    }

    private void HandleProfileCleared()
    {
        Debug.Log("[AuthTestUI] Perfil limpiado.");

        RefreshUI();
    }

    private void HandleGoogleAuthStateChanged(GoogleAuthState state)
    {
        Debug.Log($"[AuthTestUI] GoogleAuth: {state}");

        RefreshUI();
    }

    private void RefreshUI()
    {
        RefreshFirebaseStatus();
        RefreshFirestoreStatus();
        RefreshAuthStatus();
        RefreshProfile();
        RefreshGoogleStatus();
        RefreshButtons();
    }

    private void RefreshFirebaseStatus()
    {
        if (firebaseText == null)
            return;

        firebaseText.text =
            $"Firebase: {(FirebaseBootstrap.IsReady ? "READY" : "WAITING")}";
    }

    private void RefreshFirestoreStatus()
    {
        if (firestoreText == null)
            return;

        bool ready =
            FirestoreService.Instance != null &&
            FirestoreService.Instance.IsReady;

        firestoreText.text =
            $"Firestore: {(ready ? "READY" : "WAITING")}";
    }

    private void RefreshAuthStatus()
    {
        if (authText == null)
            return;

        FirebaseAuthService authService = FirebaseAuthService.Instance;

        if (authService == null || !authService.IsReady)
        {
            authText.text = "Auth: WAITING";
            return;
        }

        if (!authService.IsAuthenticated || authService.CurrentUser == null)
        {
            authText.text = "Auth: NO USER";
            return;
        }

        authText.text =
            "Auth: LOGGED IN\n" +
            $"Email: {authService.CurrentUser.Email}\n" +
            $"UID: {authService.CurrentUser.UserId}";
    }

    private void RefreshProfile()
    {
        if (profileText == null)
            return;

        UserService userService = UserService.Instance;

        if (userService == null ||
            !userService.IsProfileLoaded ||
            userService.CurrentProfile == null)
        {
            profileText.text = "Profile: NOT LOADED";
            return;
        }

        UserProfile profile = userService.CurrentProfile;

        profileText.text =
            $"Nombre: {profile.DisplayName}\n" +
            $"Email: {profile.Email}\n" +
            $"UID: {profile.Uid}\n" +
            $"Role: {profile.Role}\n" +
            $"Status: {profile.Status}";
    }

    private void RefreshGoogleStatus()
    {
        if (googleText == null)
            return;

        GoogleAuthService googleService = GoogleAuthService.Instance;

        if (googleService == null)
        {
            googleText.text = "GoogleAuth: NOT READY";
            return;
        }

#if UNITY_ANDROID && !UNITY_EDITOR

        googleText.text =
            $"GoogleAuth: {googleService.State}";

        if (googleService.State == GoogleAuthState.Error &&
            !string.IsNullOrWhiteSpace(googleService.LastError))
        {
            googleText.text +=
                "\n" + googleService.LastError;
        }

#else

        googleText.text =
            "GoogleAuth: EDITOR\n" +
            "Login disponible en Android";

#endif
    }

    private void RefreshButtons()
    {
        bool authenticated =
            FirebaseAuthService.Instance != null &&
            FirebaseAuthService.Instance.IsAuthenticated;

        if (signInButton != null)
        {
#if UNITY_ANDROID && !UNITY_EDITOR

            signInButton.interactable =
                !authenticated &&
                GoogleAuthService.Instance != null &&
                GoogleAuthService.Instance.IsReady &&
                GoogleAuthService.Instance.State != GoogleAuthState.SigningIn;

#else

            signInButton.interactable = false;

#endif
        }

        if (signOutButton != null)
        {
            signOutButton.interactable = authenticated;
        }
    }
}