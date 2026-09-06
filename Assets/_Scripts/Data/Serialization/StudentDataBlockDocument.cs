using System.Collections.Generic;
using Firebase.Firestore;

public sealed class StudentDataBlockDocument
{
    public StudentDataBlockDefinition Block { get; }
    public long SchemaVersion { get; }
    public long SaveRevision { get; }
    public Timestamp UpdatedAt { get; }
    public IReadOnlyDictionary<string, object> StoredValues { get; }
    public IReadOnlyDictionary<string, object> Values => StoredValues;

    internal StudentDataBlockDocument(
        StudentDataBlockDefinition block,
        long schemaVersion,
        long saveRevision,
        Timestamp updatedAt,
        IDictionary<string, object> storedValues
    )
    {
        Block = block;
        SchemaVersion = schemaVersion;
        SaveRevision = saveRevision;
        UpdatedAt = updatedAt;
        StoredValues = new Dictionary<string, object>(storedValues);
    }
}
