using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Firestore;

public sealed class StudentDataBlockPersistenceService
{
    private readonly FirestoreService configuredFirestoreService;
    private readonly StudentDataBlockDocumentSerializer documentSerializer;

    public StudentDataBlockPersistenceService()
        : this(null, new StudentDataBlockDocumentSerializer())
    {
    }

    internal StudentDataBlockPersistenceService(
        FirestoreService firestoreService,
        StudentDataBlockDocumentSerializer documentSerializer
    )
    {
        configuredFirestoreService = firestoreService;
        this.documentSerializer = documentSerializer;
    }

    public async Task<StudentDataPersistenceResult> SaveBlockAsync(
        string uid,
        StudentDataBlockDefinition block,
        IEnumerable<StudentDataRuntimeSnapshot> snapshots,
        long saveRevision
    )
    {
        string validationError;
        if (!TryValidateArguments(uid, block, snapshots, saveRevision, out validationError))
        {
            return StudentDataPersistenceResult.CreateFailure(validationError);
        }

        Timestamp updatedAt = Timestamp.GetCurrentTimestamp();
        Dictionary<string, object> document;
        string serializationError;
        if (!documentSerializer.TrySerializeBlock(
            block,
            snapshots,
            saveRevision,
            updatedAt,
            out document,
            out serializationError
        ))
        {
            return StudentDataPersistenceResult.CreateFailure(
                $"Could not serialize block '{block.Key}': {serializationError}"
            );
        }

        FirestoreService firestoreService;
        string firestoreError;
        if (!TryGetReadyFirestoreService(out firestoreService, out firestoreError))
        {
            return StudentDataPersistenceResult.CreateFailure(firestoreError);
        }

        DocumentReference documentReference = GetBlockDocumentReference(
            firestoreService.Database,
            uid,
            block.Key
        );

        try
        {
            await documentReference.SetAsync(document);
        }
        catch (Exception exception)
        {
            return StudentDataPersistenceResult.CreateFailure(
                $"Firestore save failed for block '{block.Key}': {exception.Message}"
            );
        }

        return StudentDataPersistenceResult.CreateSuccess(block, saveRevision);
    }

    public async Task<StudentDataBlockLoadResult> LoadBlockAsync(
        string uid,
        StudentDataBlockDefinition block
    )
    {
        string validationError;
        if (!TryValidateArguments(uid, block, out validationError))
        {
            return StudentDataBlockLoadResult.CreateFailure(validationError);
        }

        FirestoreService firestoreService;
        string firestoreError;
        if (!TryGetReadyFirestoreService(out firestoreService, out firestoreError))
        {
            return StudentDataBlockLoadResult.CreateFailure(firestoreError);
        }

        DocumentReference documentReference = GetBlockDocumentReference(
            firestoreService.Database,
            uid,
            block.Key
        );

        DocumentSnapshot snapshot;
        try
        {
            snapshot = await documentReference.GetSnapshotAsync();
        }
        catch (Exception exception)
        {
            return StudentDataBlockLoadResult.CreateFailure(
                $"Firestore load failed for block '{block.Key}': {exception.Message}"
            );
        }

        if (!snapshot.Exists)
        {
            return StudentDataBlockLoadResult.CreateNotFound(block);
        }

        IDictionary<string, object> document;
        try
        {
            document = snapshot.ToDictionary();
        }
        catch (Exception exception)
        {
            return StudentDataBlockLoadResult.CreateFailure(
                $"Could not convert Firestore document for block '{block.Key}' to a dictionary: {exception.Message}"
            );
        }

        StudentDataBlockDocument blockDocument;
        string deserializationError;
        if (!documentSerializer.TryDeserializeBlock(
            block,
            document,
            out blockDocument,
            out deserializationError
        ))
        {
            return StudentDataBlockLoadResult.CreateFailure(
                $"Could not deserialize block '{block.Key}': {deserializationError}"
            );
        }

        return StudentDataBlockLoadResult.CreateSuccess(block, blockDocument);
    }

    private bool TryGetReadyFirestoreService(
        out FirestoreService firestoreService,
        out string error
    )
    {
        firestoreService = configuredFirestoreService ?? FirestoreService.Instance;
        if (firestoreService == null)
        {
            error = "FirestoreService is not available.";
            return false;
        }

        if (!firestoreService.IsReady || firestoreService.Database == null)
        {
            error = "FirestoreService is not ready.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateArguments(
        string uid,
        StudentDataBlockDefinition block,
        out string error
    )
    {
        if (string.IsNullOrWhiteSpace(uid))
        {
            error = "Student data UID is required.";
            return false;
        }

        if (!string.Equals(uid, uid.Trim(), StringComparison.Ordinal))
        {
            error = "Student data UID must not contain leading or trailing whitespace.";
            return false;
        }

        if (!TryValidateBlock(block, out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateArguments(
        string uid,
        StudentDataBlockDefinition block,
        IEnumerable<StudentDataRuntimeSnapshot> snapshots,
        long saveRevision,
        out string error
    )
    {
        if (!TryValidateArguments(uid, block, out error))
        {
            return false;
        }

        if (snapshots == null)
        {
            error = $"Block '{block.Key}' requires a snapshots collection.";
            return false;
        }

        if (saveRevision < 0)
        {
            error = $"Block '{block.Key}' saveRevision cannot be negative.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryValidateBlock(
        StudentDataBlockDefinition block,
        out string error
    )
    {
        if (block == null)
        {
            error = "StudentDataBlockDefinition is required.";
            return false;
        }

        StudentDataValidationResult validation = block.Validate();
        if (!validation.IsValid)
        {
            error = $"Block '{block.Key}' is invalid: {string.Join(" ", validation.Errors)}";
            return false;
        }

        error = null;
        return true;
    }

    private static DocumentReference GetBlockDocumentReference(
        FirebaseFirestore database,
        string uid,
        string blockKey
    )
    {
        return database
            .Collection("users")
            .Document(uid)
            .Collection("data")
            .Document(blockKey);
    }
}
