public sealed class StudentDataRuntimeEntry
{
    public StudentDataDefinition Definition { get; }
    public object DefaultValue { get; }
    public object CurrentValue { get; private set; }
    public bool IsDirty { get; private set; }
    public bool IsLoaded { get; private set; }
    // Local in-memory revision; this is not the future persisted saveRevision metadata.
    public long Revision { get; private set; }
    public bool IsDefault => object.Equals(CurrentValue, DefaultValue);

    internal StudentDataRuntimeEntry(StudentDataDefinition definition, object defaultValue)
    {
        Definition = definition;
        DefaultValue = defaultValue;
        CurrentValue = defaultValue;
        IsDirty = false;
        IsLoaded = false;
        Revision = 0;
    }

    internal void ApplyValue(object value, bool isDirty, bool isLoaded, long revision)
    {
        CurrentValue = value;
        IsDirty = isDirty;
        IsLoaded = isLoaded;
        Revision = revision;
    }
}
