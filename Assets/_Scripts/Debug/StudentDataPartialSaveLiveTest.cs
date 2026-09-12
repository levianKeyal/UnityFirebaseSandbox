using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

public sealed class StudentDataPartialSaveLiveTest : MonoBehaviour
{
    private const string TestDocumentId = "livePartialSave";

    private bool isRunning;
    private StringBuilder reportBuffer;
    private string reportFileName;

    [ContextMenu("Run Student Data Partial Save Live Test")]
    public void RunStudentDataPartialSaveLiveTest()
    {
        StartValidation("Context Menu");
    }

    public void HandleRunTestClicked()
    {
        StartValidation("UI Button");
    }

    private void StartValidation(string trigger)
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[StudentDataPartialSaveTest] Enter Play Mode before running the test.");
            return;
        }

        if (isRunning)
        {
            Debug.LogWarning("[StudentDataPartialSaveTest] RUN IGNORED - test already running");
            return;
        }

        isRunning = true;
        BeginReport();
        LogTest("========================================");
        LogTest("[StudentDataPartialSaveTest] LIVE PARTIAL SAVE TEST START");
        LogTest("========================================");
        LogTest($"[StudentDataPartialSaveTest] Trigger: {trigger}");
        _ = RunValidationAsync();
    }

    private async Task RunValidationAsync()
    {
        bool testPassed = false;
        try
        {
            string uid;
            if (!TryGetReadySession(out uid))
            {
                return;
            }

            DocumentReference document = GetDocumentReference(uid);

            Dictionary<string, object> initialValues = new Dictionary<string, object>
            {
                { "exerciseCorrect", 10L },
                { "exerciseErrors", 3L },
                { "optionalNote", "initial" }
            };
            Dictionary<string, object> initialDocument = CreatePartialDocument(
                1L,
                1L,
                initialValues
            );

            await document.SetAsync(
                initialDocument,
                SetOptions.MergeFields(
                    new FieldPath("schemaVersion"),
                    new FieldPath("saveRevision"),
                    new FieldPath("updatedAt"),
                    new FieldPath("values", "exerciseCorrect"),
                    new FieldPath("values", "exerciseErrors"),
                    new FieldPath("values", "optionalNote")
                )
            );

            DocumentSnapshot createSnapshot = await document.GetSnapshotAsync();
            if (!createSnapshot.Exists)
            {
                LogFailure("CREATE", "The partial save document does not exist after creation.");
                return;
            }

            IDictionary<string, object> createValues;
            if (!TryGetValues(createSnapshot, out createValues)
                || !HasValue(createValues, "exerciseCorrect", 10L)
                || !HasValue(createValues, "exerciseErrors", 3L)
                || !HasValue(createValues, "optionalNote", "initial"))
            {
                LogFailure("CREATE", "The initial values were not stored as expected.");
                return;
            }

            LogTest("[StudentDataPartialSaveTest] CREATE PASS");

            Dictionary<string, object> updateValues = new Dictionary<string, object>
            {
                { "exerciseCorrect", 11L }
            };
            Dictionary<string, object> partialDocument = CreatePartialDocument(
                1L,
                2L,
                updateValues
            );

            await document.SetAsync(
                partialDocument,
                SetOptions.MergeFields(
                    new FieldPath("schemaVersion"),
                    new FieldPath("saveRevision"),
                    new FieldPath("updatedAt"),
                    new FieldPath("values", "exerciseCorrect")
                )
            );

            DocumentSnapshot partialSnapshot = await document.GetSnapshotAsync();
            IDictionary<string, object> partialValues;
            if (!partialSnapshot.Exists || !TryGetValues(partialSnapshot, out partialValues))
            {
                LogFailure("PARTIAL WRITE", "The document or values map is missing after the partial update.");
                return;
            }

            LogTest("[StudentDataPartialSaveTest] PARTIAL WRITE PASS");
            if (!HasValue(partialValues, "exerciseCorrect", 11L))
            {
                LogFailure("UPDATED VALUE", "exerciseCorrect was not updated to 11.");
                return;
            }

            LogTest("[StudentDataPartialSaveTest] UPDATED VALUE PASS");
            if (!HasValue(partialValues, "exerciseErrors", 3L))
            {
                LogFailure("UNTOUCHED VALUE", "exerciseErrors was not preserved.");
                return;
            }

            LogTest("[StudentDataPartialSaveTest] UNTOUCHED VALUE PASS");
            if (!HasValue(partialValues, "optionalNote", "initial"))
            {
                LogFailure("UNTOUCHED STRING", "optionalNote was not preserved.");
                return;
            }

            LogTest("[StudentDataPartialSaveTest] UNTOUCHED STRING PASS");

            Dictionary<string, object> nullValues = new Dictionary<string, object>
            {
                { "optionalNote", null }
            };
            Dictionary<string, object> nullDocument = CreatePartialDocument(
                null,
                3L,
                nullValues
            );

            await document.SetAsync(
                nullDocument,
                SetOptions.MergeFields(
                    new FieldPath("saveRevision"),
                    new FieldPath("updatedAt"),
                    new FieldPath("values", "optionalNote")
                )
            );

            DocumentSnapshot nullSnapshot = await document.GetSnapshotAsync();
            IDictionary<string, object> finalValues;
            if (!nullSnapshot.Exists || !TryGetValues(nullSnapshot, out finalValues))
            {
                LogFailure("NULL VALUE", "The document or values map is missing after the null update.");
                return;
            }

            object optionalNote;
            if (!finalValues.TryGetValue("optionalNote", out optionalNote) || optionalNote != null)
            {
                LogFailure("NULL VALUE", "optionalNote is missing or is not present with a null value.");
                return;
            }

            LogTest("[StudentDataPartialSaveTest] NULL VALUE PASS");
            testPassed = true;
        }
        catch (Exception exception)
        {
            LogTestError(
                "[StudentDataPartialSaveTest] LIVE PARTIAL SAVE TEST EXCEPTION "
                + $"type={exception.GetType().FullName} message={exception.Message}"
            );
        }
        finally
        {
            LogTest("========================================");
            LogTest(
                testPassed
                    ? "[StudentDataPartialSaveTest] LIVE PARTIAL SAVE TEST PASS"
                    : "[StudentDataPartialSaveTest] LIVE PARTIAL SAVE TEST FAIL"
            );
            LogTest("========================================");
            TryExportReport();
            isRunning = false;
        }
    }

    private bool TryGetReadySession(out string uid)
    {
        uid = null;
        FirebaseAuthService authService = FirebaseAuthService.Instance;
        FirestoreService firestoreService = FirestoreService.Instance;

        if (authService == null || !authService.IsReady || authService.CurrentUser == null)
        {
            LogTestError("[StudentDataPartialSaveTest] TEST BLOCKED - AUTHENTICATED USER REQUIRED");
            return false;
        }

        if (firestoreService == null || !firestoreService.IsReady || firestoreService.Database == null)
        {
            LogTestError("[StudentDataPartialSaveTest] TEST BLOCKED - FIRESTORE NOT READY");
            return false;
        }

        uid = authService.CurrentUser.UserId;
        if (string.IsNullOrWhiteSpace(uid))
        {
            LogTestError("[StudentDataPartialSaveTest] TEST BLOCKED - authenticated UID is empty.");
            return false;
        }

        LogTest("[StudentDataPartialSaveTest] Readiness PASS; authenticated user confirmed.");
        return true;
    }

    private static Dictionary<string, object> CreatePartialDocument(
        object schemaVersion,
        long saveRevision,
        IDictionary<string, object> values
    )
    {
        Dictionary<string, object> document = new Dictionary<string, object>
        {
            { "saveRevision", saveRevision },
            { "updatedAt", Timestamp.GetCurrentTimestamp() },
            { "values", new Dictionary<string, object>(values) }
        };

        if (schemaVersion != null)
        {
            document.Add("schemaVersion", schemaVersion);
        }

        return document;
    }

    private static DocumentReference GetDocumentReference(string uid)
    {
        return FirestoreService.Instance.Database
            .Collection("users")
            .Document(uid)
            .Collection("data")
            .Document(TestDocumentId);
    }

    private static bool TryGetValues(
        DocumentSnapshot snapshot,
        out IDictionary<string, object> values
    )
    {
        values = null;
        IDictionary<string, object> document = snapshot.ToDictionary();
        object rawValues;
        if (!document.TryGetValue("values", out rawValues))
        {
            return false;
        }

        values = rawValues as IDictionary<string, object>;
        return values != null;
    }

    private static bool HasValue(
        IDictionary<string, object> values,
        string key,
        object expected
    )
    {
        object actual;
        return values.TryGetValue(key, out actual) && Equals(actual, expected);
    }

    private void BeginReport()
    {
        reportBuffer = new StringBuilder();
        reportFileName =
            "StudentDataPartialSaveTest_"
            + DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)
            + ".txt";

        reportBuffer.AppendLine("========================================");
        reportBuffer.AppendLine("Student Data Partial Save Live Test");
        reportBuffer.AppendLine("========================================");
        reportBuffer.AppendLine(
            $"Timestamp UTC: {DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)}"
        );
        reportBuffer.AppendLine($"Platform: {Application.platform}");
        reportBuffer.AppendLine($"Unity Version: {Application.unityVersion}");
        reportBuffer.AppendLine($"Application Version: {Application.version}");
        reportBuffer.AppendLine("========================================");
    }

    private void LogTest(string message)
    {
        AppendReport(message);
        Debug.Log(message);
    }

    private void LogTestError(string message)
    {
        AppendReport(message);
        Debug.LogError(message);
    }

    private void AppendReport(string message)
    {
        if (reportBuffer != null)
        {
            reportBuffer.AppendLine(message ?? string.Empty);
        }
    }

    private void LogFailure(string stage, string error)
    {
        LogTestError($"[StudentDataPartialSaveTest] {stage} FAIL: {error}");
    }

    private void TryExportReport()
    {
        if (reportBuffer == null || string.IsNullOrEmpty(reportFileName))
        {
            return;
        }

        try
        {
            string location;
            string error;
            if (AndroidDownloadFileWriter.TrySaveText(
                reportFileName,
                reportBuffer.ToString(),
                out location,
                out error
            ))
            {
                Debug.Log("[StudentDataPartialSaveTest] REPORT SAVED");
                Debug.Log($"[StudentDataPartialSaveTest] File: {reportFileName}");
                Debug.Log($"[StudentDataPartialSaveTest] Location: {location}");
            }
            else
            {
                Debug.LogError($"[StudentDataPartialSaveTest] REPORT EXPORT FAIL - {error}");
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[StudentDataPartialSaveTest] REPORT EXPORT FAIL - "
                + $"{exception.GetType().FullName}: {exception.Message}"
            );
        }
    }
}
