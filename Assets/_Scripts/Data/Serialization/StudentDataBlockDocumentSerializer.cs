using System;
using System.Collections.Generic;
using Firebase.Firestore;

public sealed class StudentDataBlockDocumentSerializer
{
    public const string SchemaVersionField = "schemaVersion";
    public const string SaveRevisionField = "saveRevision";
    public const string UpdatedAtField = "updatedAt";
    public const string ValuesField = "values";

    private readonly StudentDataValueSerializer valueSerializer;

    public StudentDataBlockDocumentSerializer()
        : this(new StudentDataValueSerializer())
    {
    }

    internal StudentDataBlockDocumentSerializer(StudentDataValueSerializer valueSerializer)
    {
        this.valueSerializer = valueSerializer;
    }

    public bool TrySerializeBlock(
        StudentDataBlockDefinition block,
        IEnumerable<StudentDataRuntimeSnapshot> snapshots,
        long saveRevision,
        Timestamp updatedAt,
        out Dictionary<string, object> document,
        out string error
    )
    {
        document = null;
        if (!TryValidateBlock(block, out error))
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

        if (updatedAt == null)
        {
            error = $"Block '{block.Key}' updatedAt must be a Firebase Timestamp.";
            return false;
        }

        Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
        HashSet<string> keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (StudentDataRuntimeSnapshot snapshot in snapshots)
        {
            if (snapshot == null)
            {
                error = $"Block '{block.Key}' cannot serialize a null snapshot.";
                return false;
            }

            if (snapshot.Definition == null)
            {
                error = $"Block '{block.Key}' cannot serialize a snapshot without a definition.";
                return false;
            }

            if (snapshot.Block != block)
            {
                error =
                    $"Definition '{snapshot.Definition.Key}' belongs to block "
                    + $"'{GetBlockKey(snapshot.Block)}', not '{block.Key}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(snapshot.Definition.Key))
            {
                error = $"Block '{block.Key}' cannot serialize a definition with an empty key.";
                return false;
            }

            if (!keys.Add(snapshot.Definition.Key))
            {
                error =
                    $"Block '{block.Key}' contains duplicate definition key "
                    + $"'{snapshot.Definition.Key}'.";
                return false;
            }

            object serializedValue;
            string valueError;
            if (!valueSerializer.TrySerialize(
                snapshot.Definition,
                snapshot.Value,
                out serializedValue,
                out valueError
            ))
            {
                error =
                    $"Could not serialize definition '{snapshot.Definition.Key}' "
                    + $"in block '{block.Key}': {valueError}";
                return false;
            }

            values.Add(snapshot.Definition.Key, serializedValue);
        }

        document = new Dictionary<string, object>
        {
            { SchemaVersionField, (long)block.SchemaVersion },
            { SaveRevisionField, saveRevision },
            { UpdatedAtField, updatedAt },
            { ValuesField, values }
        };
        return true;
    }

    public bool TryDeserializeBlock(
        StudentDataBlockDefinition block,
        IDictionary<string, object> document,
        out StudentDataBlockDocument result,
        out string error
    )
    {
        result = null;
        if (!TryValidateBlock(block, out error))
        {
            return false;
        }

        if (document == null)
        {
            error = $"Block '{block.Key}' document is required.";
            return false;
        }

        object schemaVersionValue;
        if (!TryGetRequiredField(document, SchemaVersionField, out schemaVersionValue, out error))
        {
            return false;
        }

        long schemaVersion;
        if (!TryReadInt64Metadata(
            block,
            SchemaVersionField,
            schemaVersionValue,
            out schemaVersion,
            out error
        ))
        {
            return false;
        }

        if (schemaVersion <= 0)
        {
            error = $"Block '{block.Key}' schemaVersion must be greater than zero.";
            return false;
        }

        object saveRevisionValue;
        if (!TryGetRequiredField(document, SaveRevisionField, out saveRevisionValue, out error))
        {
            return false;
        }

        long saveRevision;
        if (!TryReadInt64Metadata(
            block,
            SaveRevisionField,
            saveRevisionValue,
            out saveRevision,
            out error
        ))
        {
            return false;
        }

        if (saveRevision < 0)
        {
            error = $"Block '{block.Key}' saveRevision cannot be negative.";
            return false;
        }

        object updatedAtValue;
        if (!TryGetRequiredField(document, UpdatedAtField, out updatedAtValue, out error))
        {
            return false;
        }

        Timestamp updatedAt = updatedAtValue as Timestamp;
        if (updatedAt == null)
        {
            error =
                $"Block '{block.Key}' expects updatedAt to be Firebase Timestamp "
                + $"but received {GetTypeName(updatedAtValue)}.";
            return false;
        }

        object valuesValue;
        if (!TryGetRequiredField(document, ValuesField, out valuesValue, out error))
        {
            return false;
        }

        IDictionary<string, object> storedValues = valuesValue as IDictionary<string, object>;
        if (storedValues == null)
        {
            error =
                $"Block '{block.Key}' expects values to be Dictionary<String, Object> "
                + $"but received {GetTypeName(valuesValue)}.";
            return false;
        }

        result = new StudentDataBlockDocument(
            block,
            schemaVersion,
            saveRevision,
            updatedAt,
            storedValues
        );
        return true;
    }

    private static bool TryValidateBlock(StudentDataBlockDefinition block, out string error)
    {
        if (block == null)
        {
            error = "StudentDataBlockDefinition is required.";
            return false;
        }

        if (block.SchemaVersion <= 0)
        {
            error = $"Block '{block.Key}' schemaVersion must be greater than zero.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryGetRequiredField(
        IDictionary<string, object> document,
        string fieldName,
        out object value,
        out string error
    )
    {
        if (!document.TryGetValue(fieldName, out value))
        {
            error = $"Document is missing required field '{fieldName}'.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryReadInt64Metadata(
        StudentDataBlockDefinition block,
        string fieldName,
        object value,
        out long result,
        out string error
    )
    {
        if (value is int)
        {
            result = (long)(int)value;
            error = null;
            return true;
        }

        if (value is long)
        {
            result = (long)value;
            error = null;
            return true;
        }

        result = 0;
        error =
            $"Block '{block.Key}' expects {fieldName} to be Int32 or Int64 "
            + $"but received {GetTypeName(value)}.";
        return false;
    }

    private static string GetBlockKey(StudentDataBlockDefinition block)
    {
        return block == null ? "<null>" : block.Key;
    }

    private static string GetTypeName(object value)
    {
        return value == null ? "null" : value.GetType().Name;
    }
}
