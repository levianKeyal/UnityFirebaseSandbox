using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using Firebase.Firestore;
using UnityEngine;

public sealed class StudentDataPersistenceLiveTest : MonoBehaviour
{
    [SerializeField] private StudentDataBlockDefinition testBlock;
    [SerializeField] private StudentDataBlockDefinition emptyTestBlock;
    [SerializeField] private StudentDataBlockDefinition missingTestBlock;
    [SerializeField] private StudentDataDefinition[] testDefinitions;

    private bool isRunning;
    private StringBuilder reportBuffer;
    private string reportFileName;

    [ContextMenu("Run Student Data Live Validation")]
    public void RunStudentDataLiveValidation()
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
            Debug.LogWarning("[StudentDataLiveTest] Enter Play Mode before running the validation.");
            return;
        }

        if (isRunning)
        {
            Debug.LogWarning("[StudentDataLiveTest] RUN IGNORED - validation already running");
            return;
        }

        isRunning = true;
        BeginReport();
        LogTest("========================================");
        LogTest("[StudentDataLiveTest] LIVE TEST START");
        LogTest("========================================");
        LogTest($"[StudentDataLiveTest] Trigger: {trigger}");
        _ = RunValidationAsync();
    }

    private async Task RunValidationAsync()
    {
        bool testPassed = false;
        try
        {
            if (!TryGetReadySession(out string uid))
            {
                return;
            }

            if (!ValidateTestAssets())
            {
                return;
            }

            StudentDataBlockPersistenceService persistence =
                new StudentDataBlockPersistenceService();

            StudentDataBlockLoadResult notFound = await persistence.LoadBlockAsync(
                uid,
                missingTestBlock
            );
            if (!notFound.IsNotFound)
            {
                LogTestError(
                    $"[StudentDataLiveTest] NOT FOUND FAIL - received {notFound.Status}: {notFound.Error}"
                );
                return;
            }

            LogTest("[StudentDataLiveTest] NOT FOUND PASS");

            LogTest("[StudentDataLiveTest] EMPTY VALUES - SAVE");
            StudentDataPersistenceResult emptySave = await persistence.SaveBlockAsync(
                uid,
                emptyTestBlock,
                new List<StudentDataRuntimeSnapshot>(),
                1
            );
            if (!emptySave.Success)
            {
                LogFailure("EMPTY_SAVE", emptySave.Error);
                return;
            }

            LogTest("[StudentDataLiveTest] EMPTY VALUES SAVE PASS");

            if (!await InspectEmptyDocumentAsync(uid, emptyTestBlock))
            {
                return;
            }

            StudentDataBlockLoadResult emptyLoad = await persistence.LoadBlockAsync(
                uid,
                emptyTestBlock
            );
            if (!emptyLoad.Success || emptyLoad.Document == null || emptyLoad.Document.StoredValues.Count != 0)
            {
                LogFailure("EMPTY_LOAD", "Expected an empty values map after loading the empty block.");
                return;
            }

            LogTest("[StudentDataLiveTest] EMPTY VALUES LOAD PASS");

            LogTest("[StudentDataLiveTest] SAVE");
            List<StudentDataRuntimeSnapshot> snapshots = BuildTestSnapshots();
            StudentDataPersistenceResult save = await persistence.SaveBlockAsync(
                uid,
                testBlock,
                snapshots,
                1
            );
            if (!save.Success)
            {
                LogFailure("SAVE", save.Error);
                return;
            }

            LogTest("[StudentDataLiveTest] SAVE PASS");

            LogTest("[StudentDataLiveTest] RAW READ");
            (bool Success, Dictionary<string, object> RawDocument) rawInspection =
                await InspectRawDocumentAsync(uid, testBlock);
            if (!rawInspection.Success)
            {
                return;
            }

            Dictionary<string, object> rawDocument = rawInspection.RawDocument;
            LogTest(
                $"[StudentDataLiveTest] RAW DOCUMENT runtimeType={rawDocument.GetType().FullName}"
            );

            LogTest("[StudentDataLiveTest] LOAD");
            StudentDataBlockLoadResult load = await persistence.LoadBlockAsync(uid, testBlock);
            if (!load.Success || load.Document == null)
            {
                LogFailure("LOAD", load.Error ?? $"Unexpected status: {load.Status}");
                return;
            }

            LogTest(
                $"[StudentDataLiveTest] LOAD PASS schemaVersion={load.Document.SchemaVersion} "
                + $"saveRevision={load.Document.SaveRevision} "
                + $"storedValues={load.Document.StoredValues.Count}"
            );

            LogTest("[StudentDataLiveTest] DESERIALIZATION");
            if (!RunRoundTripChecks(load.Document.StoredValues))
            {
                return;
            }

            testPassed = true;
            LogTest($"[StudentDataLiveTest] LIVE TEST PATH users/{uid}/data/{testBlock.Key}");
        }
        catch (Exception exception)
        {
            LogTestError(
                $"[StudentDataLiveTest] LIVE TEST EXCEPTION type={exception.GetType().FullName} "
                + $"message={exception.Message}"
            );
        }
        finally
        {
            LogTest("========================================");
            LogTest(
                testPassed
                    ? "[StudentDataLiveTest] LIVE TEST PASS"
                    : "[StudentDataLiveTest] LIVE TEST FAIL"
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
        UserService userService = UserService.Instance;
        AccountAccessService accountAccessService = AccountAccessService.Instance;

        if (authService == null || !authService.IsReady || authService.CurrentUser == null)
        {
            LogTestError(
                "[StudentDataLiveTest] TEST BLOCKED - AUTHENTICATED USER REQUIRED"
            );
            return false;
        }

        if (
            firestoreService == null
            || !firestoreService.IsReady
            || firestoreService.Database == null
        )
        {
            LogTestError("[StudentDataLiveTest] TEST BLOCKED - FIRESTORE NOT READY");
            return false;
        }

        if (userService == null || !userService.IsProfileLoaded || userService.CurrentProfile == null)
        {
            LogTestError("[StudentDataLiveTest] TEST BLOCKED - PROFILE NOT READY");
            return false;
        }

        if (accountAccessService == null || !accountAccessService.IsReady || !accountAccessService.CanUseApplication)
        {
            LogTestError("[StudentDataLiveTest] TEST BLOCKED - ACCOUNT NOT ACTIVE");
            return false;
        }

        uid = authService.CurrentUser.UserId;
        if (string.IsNullOrWhiteSpace(uid))
        {
            LogTestError("[StudentDataLiveTest] TEST BLOCKED - authenticated UID is empty.");
            return false;
        }

        LogTest("[StudentDataLiveTest] Readiness PASS; authenticated user and active profile confirmed.");
        return true;
    }

    private bool ValidateTestAssets()
    {
        if (testBlock == null || emptyTestBlock == null || missingTestBlock == null)
        {
            LogFailure("SETUP", "All three temporary block definitions are required.");
            return false;
        }

        if (testDefinitions == null || testDefinitions.Length != 12)
        {
            LogFailure("SETUP", "Exactly twelve temporary test definitions are required.");
            return false;
        }

        if (!testBlock.Validate().IsValid || !emptyTestBlock.Validate().IsValid || !missingTestBlock.Validate().IsValid)
        {
            LogFailure("SETUP", "A temporary block definition is invalid.");
            return false;
        }

        foreach (StudentDataDefinition definition in testDefinitions)
        {
            if (definition == null || !definition.Validate().IsValid || definition.Block != testBlock)
            {
                LogFailure("SETUP", "A temporary definition is null, invalid, or belongs to another block.");
                return false;
            }
        }

        return true;
    }

    private List<StudentDataRuntimeSnapshot> BuildTestSnapshots()
    {
        object[] values =
        {
            "hello",
            "",
            null,
            true,
            123,
            -456,
            9876543210L,
            12.5f,
            -3.25f,
            12345.6789,
            new DateTime(2026, 9, 8, 12, 34, 56, DateTimeKind.Utc),
            -9876.54321
        };

        if (values.Length != testDefinitions.Length)
        {
            throw new InvalidOperationException("Temporary definitions and test values must have the same length.");
        }

        List<StudentDataRuntimeSnapshot> snapshots = new List<StudentDataRuntimeSnapshot>();
        for (int index = 0; index < testDefinitions.Length; index++)
        {
            snapshots.Add(new StudentDataRuntimeSnapshot(testDefinitions[index], values[index], 0));
        }

        return snapshots;
    }

    private async Task<bool> InspectEmptyDocumentAsync(
        string uid,
        StudentDataBlockDefinition block
    )
    {
        DocumentSnapshot snapshot = await GetDocumentReference(uid, block).GetSnapshotAsync();
        if (!snapshot.Exists)
        {
            LogFailure("EMPTY_RAW", "Empty save document does not exist.");
            return false;
        }

        IDictionary<string, object> document = snapshot.ToDictionary();
        IDictionary<string, object> values = document["values"] as IDictionary<string, object>;
        if (values == null || values.Count != 0)
        {
            LogFailure("EMPTY_RAW", "Expected an empty values map.");
            return false;
        }

        LogTest("[StudentDataLiveTest] EMPTY VALUES RAW PASS");
        return true;
    }

    private async Task<(bool Success, Dictionary<string, object> RawDocument)> InspectRawDocumentAsync(
        string uid,
        StudentDataBlockDefinition block
    )
    {
        DocumentSnapshot snapshot = await GetDocumentReference(uid, block).GetSnapshotAsync();
        if (!snapshot.Exists)
        {
            LogFailure("RAW", "Saved document does not exist.");
            return (false, null);
        }

        IDictionary<string, object> dictionary = snapshot.ToDictionary();
        Dictionary<string, object> rawDocument = new Dictionary<string, object>(dictionary);
        string[] requiredFields = { "schemaVersion", "saveRevision", "updatedAt", "values" };
        LogTest("[StudentDataLiveTest] RAW ROOT TYPES");
        foreach (string field in requiredFields)
        {
            if (!dictionary.ContainsKey(field))
            {
                LogFailure("RAW", $"Missing root field '{field}'.");
                return (false, null);
            }

            LogTest(
                $"[StudentDataLiveTest] RAW ROOT key={field} "
                + $"value={FormatValue(dictionary[field])} "
                + $"runtimeType={GetTypeName(dictionary[field])}"
            );
        }

        if (!IsIntegralNumber(dictionary["schemaVersion"]))
        {
            LogFailure("RAW", "schemaVersion is not an integral numeric value.");
            return (false, null);
        }

        if (!IsIntegralNumber(dictionary["saveRevision"]))
        {
            LogFailure("RAW", "saveRevision is not an integral numeric value.");
            return (false, null);
        }

        if (!(dictionary["updatedAt"] is Timestamp))
        {
            LogFailure("RAW", "updatedAt is not a Firebase Timestamp.");
            return (false, null);
        }

        IDictionary<string, object> values = dictionary["values"] as IDictionary<string, object>;
        if (values == null)
        {
            LogFailure("RAW", "Root values is not a compatible dictionary.");
            return (false, null);
        }

        LogTest("[StudentDataLiveTest] RAW VALUE TYPES");
        foreach (KeyValuePair<string, object> pair in values)
        {
            LogTest(
                $"[StudentDataLiveTest] RAW VALUE key={pair.Key} "
                + $"value={FormatValue(pair.Value)} runtimeType={GetTypeName(pair.Value)}"
            );
        }

        return (ValidateRawValues(values), rawDocument);
    }

    private bool ValidateRawValues(IDictionary<string, object> values)
    {
        string nullableKey = testDefinitions[2].Key;
        string emptyKey = testDefinitions[1].Key;
        string dateTimeKey = testDefinitions[10].Key;

        if (!values.ContainsKey(nullableKey) || values[nullableKey] != null)
        {
            LogFailure("RAW", "Nullable string key is missing or not null.");
            return false;
        }

        LogTest(
            $"[StudentDataLiveTest] NULL STRING key={nullableKey} present={values.ContainsKey(nullableKey)} "
            + "value=null runtimeType=null"
        );

        if (!values.ContainsKey(emptyKey) || !(values[emptyKey] is string) || ((string)values[emptyKey]).Length != 0)
        {
            LogFailure("RAW", "Empty string key is missing or not empty.");
            return false;
        }

        LogTest(
            $"[StudentDataLiveTest] EMPTY STRING key={emptyKey} present=true length=0 runtimeType=String"
        );

        for (int index = 0; index < testDefinitions.Length; index++)
        {
            object rawValue;
            if (!values.TryGetValue(testDefinitions[index].Key, out rawValue))
            {
                LogFailure("RAW", $"Missing value key '{testDefinitions[index].Key}'.");
                return false;
            }

            if (!HasExpectedRawType(testDefinitions[index].ValueType, rawValue))
            {
                LogFailure(
                    "RAW",
                    $"Value '{testDefinitions[index].Key}' has unexpected type {GetTypeName(rawValue)}."
                );
                return false;
            }
        }

        object timestampValue = values[dateTimeKey];
        if (!(timestampValue is Timestamp))
        {
            LogFailure("RAW", "DateTime value is not a Firebase Timestamp.");
            return false;
        }

        DateTime timestampDateTime = ((Timestamp)timestampValue).ToDateTime();
        LogTest(
            $"[StudentDataLiveTest] DATETIME RAW value={timestampDateTime:o} "
            + $"rawType={GetTypeName(timestampValue)} timestampValue={timestampDateTime:o} "
            + $"timestampKind={timestampDateTime.Kind}"
        );
        return true;
    }

    private static bool HasExpectedRawType(StudentDataValueType valueType, object value)
    {
        switch (valueType)
        {
            case StudentDataValueType.String:
                return value == null || value is string;
            case StudentDataValueType.Bool:
                return value is bool;
            case StudentDataValueType.Int:
            case StudentDataValueType.Long:
                return IsIntegralNumber(value);
            case StudentDataValueType.Float:
            case StudentDataValueType.Double:
                return value is float || value is double;
            case StudentDataValueType.DateTime:
                return value is Timestamp;
            default:
                return false;
        }
    }

    private static bool IsIntegralNumber(object value)
    {
        return value is int || value is long;
    }

    private bool RunRoundTripChecks(IReadOnlyDictionary<string, object> storedValues)
    {
        StudentDataValueSerializer serializer = new StudentDataValueSerializer();
        object[] expectedValues =
        {
            "hello",
            "",
            null,
            true,
            123,
            -456,
            9876543210L,
            12.5f,
            -3.25f,
            12345.6789,
            new DateTime(2026, 9, 8, 12, 34, 56, DateTimeKind.Utc),
            -9876.54321
        };

        for (int index = 0; index < testDefinitions.Length; index++)
        {
            StudentDataDefinition definition = testDefinitions[index];
            object storedValue;
            if (!storedValues.TryGetValue(definition.Key, out storedValue))
            {
                LogFailure("ROUND_TRIP", $"Missing stored value '{definition.Key}'.");
                return false;
            }

            object runtimeValue;
            string error;
            if (!serializer.TryDeserialize(definition, storedValue, out runtimeValue, out error))
            {
                LogTestError(
                    $"[StudentDataLiveTest] ROUNDTRIP FAIL - {definition.Key} "
                    + $"expected={FormatValue(expectedValues[index])} "
                    + $"actual={FormatValue(runtimeValue)} "
                    + $"expectedType={GetTypeName(expectedValues[index])} "
                    + $"actualType={GetTypeName(runtimeValue)} serializerError={error}"
                );
                return false;
            }

            if (!ValuesEqual(expectedValues[index], runtimeValue))
            {
                LogTestError(
                    $"[StudentDataLiveTest] ROUNDTRIP FAIL - {definition.Key} "
                    + $"expected={FormatValue(expectedValues[index])} "
                    + $"actual={FormatValue(runtimeValue)} "
                    + $"expectedType={GetTypeName(expectedValues[index])} "
                    + $"actualType={GetTypeName(runtimeValue)} serializerError=<none>"
                );
                return false;
            }

            LogTest(
                $"[StudentDataLiveTest] ROUNDTRIP PASS - {definition.Key} "
                + $"original={FormatValue(expectedValues[index])} "
                + $"originalType={GetTypeName(expectedValues[index])} "
                + $"rawType={GetTypeName(storedValue)} "
                + $"deserialized={FormatValue(runtimeValue)} "
                + $"deserializedType={GetTypeName(runtimeValue)}"
            );

            if (expectedValues[index] is DateTime && runtimeValue is DateTime)
            {
                LogTest(
                    $"[StudentDataLiveTest] DATETIME ROUNDTRIP original={((DateTime)expectedValues[index]):o} "
                    + $"originalKind={((DateTime)expectedValues[index]).Kind} "
                    + $"rawType={GetTypeName(storedValue)} "
                    + $"deserialized={((DateTime)runtimeValue):o} "
                    + $"deserializedKind={((DateTime)runtimeValue).Kind}"
                );
            }
        }

        LogTest("[StudentDataLiveTest] ROUNDTRIP PASS for all test values.");
        return true;
    }

    private static DocumentReference GetDocumentReference(string uid, StudentDataBlockDefinition block)
    {
        return FirestoreService.Instance.Database
            .Collection("users")
            .Document(uid)
            .Collection("data")
            .Document(block.Key);
    }

    private void BeginReport()
    {
        reportBuffer = new StringBuilder();
        reportFileName =
            "StudentDataLiveTest_"
            + DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss", CultureInfo.InvariantCulture)
            + ".txt";

        reportBuffer.AppendLine("========================================");
        reportBuffer.AppendLine("Student Data Persistence Live Test");
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

    private void LogTestWarning(string message)
    {
        AppendReport(message);
        Debug.LogWarning(message);
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
                Debug.Log("[StudentDataLiveTest] REPORT SAVED");
                Debug.Log($"[StudentDataLiveTest] File: {reportFileName}");
                Debug.Log($"[StudentDataLiveTest] Location: {location}");
            }
            else
            {
                Debug.LogError($"[StudentDataLiveTest] REPORT EXPORT FAIL - {error}");
            }
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"[StudentDataLiveTest] REPORT EXPORT FAIL - "
                + $"{exception.GetType().FullName}: {exception.Message}"
            );
        }
    }

    private static bool ValuesEqual(object expected, object actual)
    {
        if (expected == null || actual == null)
        {
            return expected == actual;
        }

        if (expected is DateTime && actual is DateTime)
        {
            return (DateTime)expected == (DateTime)actual
                && ((DateTime)actual).Kind == DateTimeKind.Utc;
        }

        return expected.Equals(actual);
    }

    private void LogFailure(string stage, string error)
    {
        LogTestError($"[StudentDataLiveTest] {stage} FAIL: {error}");
    }

    private static string GetTypeName(object value)
    {
        return value == null ? "null" : value.GetType().FullName;
    }

    private static string FormatValue(object value)
    {
        if (value == null)
        {
            return "<null>";
        }

        if (value is string)
        {
            return $"\"{value}\"";
        }

        return value.ToString();
    }
}
