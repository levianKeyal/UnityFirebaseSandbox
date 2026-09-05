public sealed class StudentDataChange
{
    public StudentDataDefinition Definition { get; }
    public object PreviousValue { get; }
    public object NewValue { get; }
    public StudentDataChangeOrigin Origin { get; }

    public StudentDataChange(
        StudentDataDefinition definition,
        object previousValue,
        object newValue,
        StudentDataChangeOrigin origin
    )
    {
        Definition = definition;
        PreviousValue = previousValue;
        NewValue = newValue;
        Origin = origin;
    }
}
