using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using TMPro;
using UnityEngine;

public class FirestoreSecurityTest : MonoBehaviour
{
    [SerializeField] private TMP_InputField otherUserUid;
    [SerializeField] private TMP_InputField unauthorizedTargetUid;
    [SerializeField] private TMP_Text resultText;

    public async void TestReadOtherUser()
    {
        await TestReadOtherUserAsync();
    }

    public async void TestChangeOwnRole()
    {
        await TestChangeOwnRoleAsync();
    }

    public async void TestChangeOwnStatus()
    {
        await TestChangeOwnStatusAsync();
    }

    public async void TestWriteOtherUser()
    {
        await TestWriteOtherUserAsync();
    }

    private async Task TestReadOtherUserAsync()
    {
        if (!TryGetReadyServices(out FirebaseFirestore firestore, out FirebaseUser currentUser))
        {
            return;
        }

        if (!TryValidateTargetUid(otherUserUid, currentUser.UserId, "otherUserUid"))
        {
            return;
        }

        try
        {
            string targetUid = ReadInputValue(otherUserUid);
            Debug.Log($"[SecurityTest] Intentando leer users/{targetUid}...");
            DocumentSnapshot snapshot = await firestore.Collection("users").Document(targetUid).GetSnapshotAsync();

            if (snapshot != null && snapshot.Exists)
            {
                Report("[SecurityTest] FAIL: El usuario pudo leer otro perfil.", true);
                return;
            }

            Report("[SecurityTest] FAIL: La lectura no fue denegada y no se obtuvo un perfil valido.", true);
        }
        catch (Exception exception)
        {
            if (IsPermissionDenied(exception))
            {
                Report("[SecurityTest] PASS: Lectura de otro usuario bloqueada.");
                return;
            }

            LogUnexpectedFailure("Lectura de otro usuario bloqueada", exception);
        }
    }

    private async Task TestChangeOwnRoleAsync()
    {
        if (!TryGetReadyServices(out FirebaseFirestore firestore, out FirebaseUser currentUser))
        {
            return;
        }

        try
        {
            Debug.Log($"[SecurityTest] Intentando modificar role en users/{currentUser.UserId}...");
            await firestore
                .Collection("users")
                .Document(currentUser.UserId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    { "role", "superAdmin" }
                });

            Report("[SecurityTest] FAIL: El usuario pudo modificar role.", true);
        }
        catch (Exception exception)
        {
            if (IsPermissionDenied(exception))
            {
                Report("[SecurityTest] PASS: Cambio de role bloqueado.");
                return;
            }

            LogUnexpectedFailure("Cambio de role bloqueado", exception);
        }
    }

    private async Task TestChangeOwnStatusAsync()
    {
        if (!TryGetReadyServices(out FirebaseFirestore firestore, out FirebaseUser currentUser))
        {
            return;
        }

        try
        {
            Debug.Log($"[SecurityTest] Intentando modificar status en users/{currentUser.UserId}...");
            await firestore
                .Collection("users")
                .Document(currentUser.UserId)
                .UpdateAsync(new Dictionary<string, object>
                {
                    { "status", "disabled" }
                });

            Report("[SecurityTest] FAIL: El usuario pudo modificar status.", true);
        }
        catch (Exception exception)
        {
            if (IsPermissionDenied(exception))
            {
                Report("[SecurityTest] PASS: Cambio de status bloqueado.");
                return;
            }

            LogUnexpectedFailure("Cambio de status bloqueado", exception);
        }
    }

    private async Task TestWriteOtherUserAsync()
    {
        if (!TryGetReadyServices(out FirebaseFirestore firestore, out FirebaseUser currentUser))
        {
            return;
        }

        if (!TryValidateTargetUid(unauthorizedTargetUid, currentUser.UserId, "unauthorizedTargetUid"))
        {
            return;
        }

        try
        {
            string targetUid = ReadInputValue(unauthorizedTargetUid);
            Debug.Log($"[SecurityTest] Intentando escribir users/{targetUid}...");
            await firestore
                .Collection("users")
                .Document(targetUid)
                .SetAsync(CreateUnauthorizedCreatePayload(currentUser, targetUid));

            Report("[SecurityTest] FAIL: El usuario pudo crear el documento.", true);
        }
        catch (Exception exception)
        {
            if (IsPermissionDenied(exception))
            {
                Report("[SecurityTest] PASS: Escritura para otro UID bloqueada.");
                return;
            }

            LogUnexpectedFailure("Escritura para otro UID bloqueada", exception);
        }
    }

    private static Dictionary<string, object> CreateUnauthorizedCreatePayload(
        FirebaseUser currentUser,
        string targetUid
    )
    {
        DateTime utcNow = DateTime.UtcNow;

        return new Dictionary<string, object>
        {
            { "uid", targetUid },
            { "email", currentUser.Email ?? string.Empty },
            { "displayName", currentUser.DisplayName ?? string.Empty },
            { "photoUrl", currentUser.PhotoUrl != null ? currentUser.PhotoUrl.ToString() : string.Empty },
            { "role", "user" },
            { "status", "active" },
            { "createdAt", Timestamp.FromDateTime(utcNow) },
            { "lastLogin", Timestamp.FromDateTime(utcNow) }
        };
    }

    private bool TryGetReadyServices(out FirebaseFirestore firestore, out FirebaseUser currentUser)
    {
        firestore = null;
        currentUser = null;

        if (FirestoreService.Instance == null || !FirestoreService.Instance.IsReady)
        {
            Report("[SecurityTest] FirestoreService no esta listo.", true);
            return false;
        }

        if (FirebaseAuthService.Instance == null || !FirebaseAuthService.Instance.IsReady)
        {
            Report("[SecurityTest] FirebaseAuthService no esta listo.", true);
            return false;
        }

        firestore = FirestoreService.Instance.Database;
        currentUser = FirebaseAuthService.Instance.CurrentUser;

        if (firestore == null)
        {
            Report("[SecurityTest] Firestore no esta disponible.", true);
            return false;
        }

        if (currentUser == null)
        {
            Report("[SecurityTest] No hay usuario autenticado.", true);
            return false;
        }

        return true;
    }

    private bool TryValidateTargetUid(TMP_InputField targetUidInput, string currentUid, string fieldName)
    {
        string targetUid = ReadInputValue(targetUidInput);

        if (string.IsNullOrWhiteSpace(targetUid))
        {
            Report($"[SecurityTest] '{fieldName}' esta vacio.", true);
            return false;
        }

        if (string.Equals(targetUid, currentUid, StringComparison.Ordinal))
        {
            Report($"[SecurityTest] '{fieldName}' debe ser distinto del usuario actual.", true);
            return false;
        }

        return true;
    }

    private static string ReadInputValue(TMP_InputField inputField)
    {
        return inputField != null ? inputField.text?.Trim() ?? string.Empty : string.Empty;
    }

    private void LogUnexpectedFailure(string operationName, Exception exception)
    {
        Report(
            $"[SecurityTest] FAIL: {operationName} fallo por un error distinto a permisos. " +
            $"Tipo={exception.GetType().Name} Mensaje={exception.Message}",
            true
        );
    }

    private void Report(string message, bool isError = false)
    {
        if (resultText != null)
        {
            if (!string.IsNullOrWhiteSpace(resultText.text))
            {
                resultText.text += Environment.NewLine;
            }

            resultText.text += message;
        }

        if (isError)
        {
            Debug.LogError(message);
            return;
        }

        Debug.Log(message);
    }

    private static bool IsPermissionDenied(Exception exception)
    {
        if (exception == null)
        {
            return false;
        }

        foreach (Exception candidate in EnumerateExceptions(exception))
        {
            if (HasPermissionDeniedErrorCode(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<Exception> EnumerateExceptions(Exception exception)
    {
        if (exception == null)
        {
            yield break;
        }

        yield return exception;

        if (exception is AggregateException aggregateException)
        {
            foreach (Exception inner in aggregateException.InnerExceptions)
            {
                foreach (Exception nested in EnumerateExceptions(inner))
                {
                    yield return nested;
                }
            }
        }

        if (exception.InnerException != null)
        {
            foreach (Exception nested in EnumerateExceptions(exception.InnerException))
            {
                yield return nested;
            }
        }
    }

    private static bool HasPermissionDeniedErrorCode(Exception exception)
    {
        if (exception == null)
        {
            return false;
        }

        Type exceptionType = exception.GetType();
        string typeName = exceptionType.FullName ?? exceptionType.Name;
        string message = exception.Message ?? string.Empty;

        if (typeName.IndexOf("PermissionDenied", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (message.IndexOf("PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (message.IndexOf("Permission denied", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        object errorCodeValue = ReadPropertyValue(exception, "ErrorCode");
        if (errorCodeValue != null)
        {
            string errorCodeText = errorCodeValue.ToString();
            if (string.Equals(errorCodeText, "PermissionDenied", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (errorCodeText.IndexOf("PermissionDenied", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static object ReadPropertyValue(object target, string propertyName)
    {
        if (target == null)
        {
            return null;
        }

        return target.GetType().GetProperty(propertyName)?.GetValue(target);
    }
}
