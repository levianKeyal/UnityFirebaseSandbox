public sealed class StudentDataRuntimeSnapshot
{
    public StudentDataDefinition Definition { get; }
    public StudentDataBlockDefinition Block => Definition != null ? Definition.Block : null;
    public object Value { get; }
    public long Revision { get; }

    internal StudentDataRuntimeSnapshot(
        StudentDataDefinition definition,
        object value,
        long revision
    )
    {
        Definition = definition;
        Value = value;
        Revision = revision;
    }
}
