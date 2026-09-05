using System;
using System.Collections.Generic;

public sealed class StudentDataRuntimeStore
{
    private readonly StudentDataRegistry registry;
    private readonly Dictionary<StudentDataDefinition, StudentDataRuntimeEntry> entries;

    public bool IsInitialized { get; }
    public StudentDataRegistry Registry => registry;
    public StudentDataValidationResult InitializationResult { get; }
    public bool HasDirtyData
    {
        get
        {
            foreach (StudentDataRuntimeEntry entry in entries.Values)
            {
                if (entry.IsDirty)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public event Action<StudentDataChange> ValueChanged;

    public StudentDataRuntimeStore(StudentDataRegistry registry)
    {
        this.registry = registry;
        entries = new Dictionary<StudentDataDefinition, StudentDataRuntimeEntry>();
        InitializationResult = new StudentDataValidationResult();

        if (registry == null)
        {
            InitializationResult.AddError("StudentDataRegistry is required.");
            return;
        }

        StudentDataValidationResult registryResult = registry.Validate();
        CopyValidationMessages(registryResult, InitializationResult);
        if (!registryResult.IsValid)
        {
            return;
        }

        Dictionary<StudentDataDefinition, StudentDataRuntimeEntry> pendingEntries =
            new Dictionary<StudentDataDefinition, StudentDataRuntimeEntry>();

        foreach (StudentDataDefinition definition in registry.Definitions)
        {
            object defaultValue;
            if (!definition.TryGetDefaultValue(out defaultValue))
            {
                InitializationResult.AddError(
                    $"Could not resolve default value for data definition '{definition.name}'."
                );
                continue;
            }

            string valueError;
            if (!StudentDataTypeMetadata.TryValidateRuntimeValue(
                definition.ValueType,
                defaultValue,
                out valueError
            ))
            {
                InitializationResult.AddError(
                    $"Invalid default for data definition '{definition.name}': {valueError}"
                );
                continue;
            }

            pendingEntries.Add(
                definition,
                new StudentDataRuntimeEntry(definition, defaultValue)
            );
        }

        if (!InitializationResult.IsValid)
        {
            return;
        }

        foreach (KeyValuePair<StudentDataDefinition, StudentDataRuntimeEntry> pair in pendingEntries)
        {
            entries.Add(pair.Key, pair.Value);
        }

        IsInitialized = true;
    }

    public bool TryGetValue(StudentDataDefinition definition, out object value)
    {
        StudentDataRuntimeEntry entry;
        if (!TryGetEntry(definition, out entry, out _))
        {
            value = null;
            return false;
        }

        value = entry.CurrentValue;
        return true;
    }

    public bool TryGetValue<T>(StudentDataDefinition definition, out T value)
    {
        StudentDataRuntimeEntry entry;
        string error;
        if (!TryGetEntry(definition, out entry, out error))
        {
            value = default(T);
            return false;
        }

        if (typeof(T) != StudentDataTypeMetadata.GetRuntimeType(definition.ValueType))
        {
            value = default(T);
            return false;
        }

        if (entry.CurrentValue == null)
        {
            value = default(T);
            return true;
        }

        value = (T)entry.CurrentValue;
        return true;
    }

    public StudentDataRuntimeEntry GetEntry(StudentDataDefinition definition)
    {
        StudentDataRuntimeEntry entry;
        string error;
        if (!TryGetEntry(definition, out entry, out error))
        {
            throw new InvalidOperationException(error);
        }

        return entry;
    }

    public void Set(StudentDataDefinition definition, object value)
    {
        string error;
        if (!TrySet(definition, value, out error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public bool TrySet(StudentDataDefinition definition, object value, out string error)
    {
        StudentDataRuntimeEntry entry;
        if (!TryGetEntry(definition, out entry, out error))
        {
            return false;
        }

        if (definition.UpdateMode != StudentDataUpdateMode.Set)
        {
            error = $"Definition '{definition.Key}' does not allow Set operations.";
            return false;
        }

        if (!StudentDataTypeMetadata.TryValidateRuntimeValue(definition.ValueType, value, out error))
        {
            return false;
        }

        return ApplyGameplayValue(entry, value, StudentDataChangeOrigin.Set, out error);
    }

    public void Increment(StudentDataDefinition definition, object amount)
    {
        string error;
        if (!TryIncrement(definition, amount, out error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public bool TryIncrement(StudentDataDefinition definition, object amount, out string error)
    {
        StudentDataRuntimeEntry entry;
        if (!TryGetEntry(definition, out entry, out error))
        {
            return false;
        }

        if (definition.UpdateMode != StudentDataUpdateMode.Increment)
        {
            error = $"Definition '{definition.Key}' does not allow Increment operations.";
            return false;
        }

        if (!StudentDataTypeMetadata.TryValidateRuntimeValue(definition.ValueType, amount, out error))
        {
            return false;
        }

        object newValue;
        if (!TryCalculateNumericValue(entry.CurrentValue, amount, definition.ValueType, out newValue, out error))
        {
            return false;
        }

        return ApplyGameplayValue(entry, newValue, StudentDataChangeOrigin.Increment, out error);
    }

    public void Maximum(StudentDataDefinition definition, object candidate)
    {
        string error;
        if (!TryMaximum(definition, candidate, out error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public bool TryMaximum(StudentDataDefinition definition, object candidate, out string error)
    {
        return TryApplyNumericComparison(
            definition,
            candidate,
            StudentDataChangeOrigin.Maximum,
            true,
            out error
        );
    }

    public void Minimum(StudentDataDefinition definition, object candidate)
    {
        string error;
        if (!TryMinimum(definition, candidate, out error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public bool TryMinimum(StudentDataDefinition definition, object candidate, out string error)
    {
        return TryApplyNumericComparison(
            definition,
            candidate,
            StudentDataChangeOrigin.Minimum,
            false,
            out error
        );
    }

    public void ResetToDefault(StudentDataDefinition definition)
    {
        string error;
        if (!TryResetToDefault(definition, out error))
        {
            throw new InvalidOperationException(error);
        }
    }

    public bool TryResetToDefault(StudentDataDefinition definition, out string error)
    {
        StudentDataRuntimeEntry entry;
        if (!TryGetEntry(definition, out entry, out error))
        {
            return false;
        }

        return ApplyGameplayValue(
            entry,
            entry.DefaultValue,
            StudentDataChangeOrigin.ResetToDefault,
            out error
        );
    }

    internal bool TryApplyLoadedValue(
        StudentDataDefinition definition,
        object value,
        out string error
    )
    {
        StudentDataRuntimeEntry entry;
        if (!TryGetEntry(definition, out entry, out error))
        {
            return false;
        }

        if (entry.IsDirty)
        {
            error = $"Cannot apply a loaded value while definition '{definition.Key}' is dirty.";
            return false;
        }

        if (!StudentDataTypeMetadata.TryValidateRuntimeValue(definition.ValueType, value, out error))
        {
            return false;
        }

        // Loading changes the baseline, not the local runtime revision.
        entry.ApplyValue(value, false, true, entry.Revision);
        return true;
    }

    public IReadOnlyList<StudentDataRuntimeEntry> GetDirtyEntries()
    {
        List<StudentDataRuntimeEntry> dirtyEntries = new List<StudentDataRuntimeEntry>();
        foreach (StudentDataRuntimeEntry entry in entries.Values)
        {
            if (entry.IsDirty)
            {
                dirtyEntries.Add(entry);
            }
        }

        return dirtyEntries;
    }

    public IReadOnlyList<StudentDataRuntimeSnapshot> GetDirtySnapshots()
    {
        List<StudentDataRuntimeSnapshot> snapshots = new List<StudentDataRuntimeSnapshot>();
        foreach (StudentDataRuntimeEntry entry in entries.Values)
        {
            if (entry.IsDirty)
            {
                snapshots.Add(
                    new StudentDataRuntimeSnapshot(
                        entry.Definition,
                        entry.CurrentValue,
                        entry.Revision
                    )
                );
            }
        }

        return snapshots;
    }

    public bool TryMarkClean(
        StudentDataDefinition definition,
        long expectedRevision,
        out string error
    )
    {
        StudentDataRuntimeEntry entry;
        if (!TryGetEntry(definition, out entry, out error))
        {
            return false;
        }

        if (entry.Revision != expectedRevision)
        {
            error =
                $"Revision mismatch for definition '{definition.Key}': "
                + $"expected {expectedRevision}, current {entry.Revision}.";
            return false;
        }

        entry.ApplyValue(entry.CurrentValue, false, entry.IsLoaded, entry.Revision);
        return true;
    }

    private bool TryApplyNumericComparison(
        StudentDataDefinition definition,
        object candidate,
        StudentDataChangeOrigin origin,
        bool selectMaximum,
        out string error
    )
    {
        StudentDataRuntimeEntry entry;
        if (!TryGetEntry(definition, out entry, out error))
        {
            return false;
        }

        if (
            (selectMaximum && definition.UpdateMode != StudentDataUpdateMode.Maximum)
            || (!selectMaximum && definition.UpdateMode != StudentDataUpdateMode.Minimum)
        )
        {
            error = $"Definition '{definition.Key}' does not allow {origin} operations.";
            return false;
        }

        if (!StudentDataTypeMetadata.TryValidateRuntimeValue(definition.ValueType, candidate, out error))
        {
            return false;
        }

        object newValue;
        if (!TryCompareNumericValue(entry.CurrentValue, candidate, definition.ValueType, selectMaximum, out newValue, out error))
        {
            return false;
        }

        return ApplyGameplayValue(entry, newValue, origin, out error);
    }

    private bool TryCalculateNumericValue(
        object currentValue,
        object amount,
        StudentDataValueType valueType,
        out object newValue,
        out string error
    )
    {
        newValue = null;
        error = null;

        try
        {
            switch (valueType)
            {
                case StudentDataValueType.Int:
                    newValue = checked((int)currentValue + (int)amount);
                    return true;
                case StudentDataValueType.Long:
                    newValue = checked((long)currentValue + (long)amount);
                    return true;
                case StudentDataValueType.Float:
                    newValue = (float)currentValue + (float)amount;
                    break;
                case StudentDataValueType.Double:
                    newValue = (double)currentValue + (double)amount;
                    break;
                default:
                    error = $"Value type {valueType} does not support numeric operations.";
                    return false;
            }
        }
        catch (OverflowException)
        {
            error = $"Numeric overflow while applying {valueType} increment.";
            return false;
        }

        return StudentDataTypeMetadata.TryValidateRuntimeValue(valueType, newValue, out error);
    }

    private bool TryCompareNumericValue(
        object currentValue,
        object candidate,
        StudentDataValueType valueType,
        bool selectMaximum,
        out object newValue,
        out string error
    )
    {
        newValue = null;
        error = null;

        switch (valueType)
        {
            case StudentDataValueType.Int:
                newValue = selectMaximum
                    ? Math.Max((int)currentValue, (int)candidate)
                    : Math.Min((int)currentValue, (int)candidate);
                return true;
            case StudentDataValueType.Long:
                newValue = selectMaximum
                    ? Math.Max((long)currentValue, (long)candidate)
                    : Math.Min((long)currentValue, (long)candidate);
                return true;
            case StudentDataValueType.Float:
                newValue = selectMaximum
                    ? Math.Max((float)currentValue, (float)candidate)
                    : Math.Min((float)currentValue, (float)candidate);
                return true;
            case StudentDataValueType.Double:
                newValue = selectMaximum
                    ? Math.Max((double)currentValue, (double)candidate)
                    : Math.Min((double)currentValue, (double)candidate);
                return true;
            default:
                error = $"Value type {valueType} does not support numeric comparisons.";
                return false;
        }
    }

    private bool ApplyGameplayValue(
        StudentDataRuntimeEntry entry,
        object newValue,
        StudentDataChangeOrigin origin,
        out string error
    )
    {
        error = null;
        if (object.Equals(entry.CurrentValue, newValue))
        {
            return true;
        }

        long nextRevision;
        try
        {
            nextRevision = checked(entry.Revision + 1);
        }
        catch (OverflowException)
        {
            error = $"Runtime revision overflow for definition '{entry.Definition.Key}'.";
            return false;
        }

        object previousValue = entry.CurrentValue;
        entry.ApplyValue(newValue, true, entry.IsLoaded, nextRevision);
        ValueChanged?.Invoke(
            new StudentDataChange(entry.Definition, previousValue, newValue, origin)
        );
        return true;
    }

    private bool TryGetEntry(
        StudentDataDefinition definition,
        out StudentDataRuntimeEntry entry,
        out string error
    )
    {
        if (!IsInitialized)
        {
            entry = null;
            error = "StudentDataRuntimeStore is not initialized.";
            return false;
        }

        if (definition == null)
        {
            entry = null;
            error = "StudentDataDefinition is required.";
            return false;
        }

        if (!entries.TryGetValue(definition, out entry))
        {
            error = $"StudentDataDefinition '{definition.name}' is not registered in this store.";
            return false;
        }

        error = null;
        return true;
    }

    private static void CopyValidationMessages(
        StudentDataValidationResult source,
        StudentDataValidationResult target
    )
    {
        foreach (string error in source.Errors)
        {
            target.AddError(error);
        }

        foreach (string warning in source.Warnings)
        {
            target.AddWarning(warning);
        }
    }
}
