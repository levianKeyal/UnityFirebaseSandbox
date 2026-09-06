public enum StudentDataPersistenceStatus
{
    Success,
    Failure
}

public sealed class StudentDataPersistenceResult
{
    public StudentDataPersistenceStatus Status { get; }
    public StudentDataBlockDefinition Block { get; }
    public long SaveRevision { get; }
    public string Error { get; }
    public bool Success => Status == StudentDataPersistenceStatus.Success;

    private StudentDataPersistenceResult(
        StudentDataPersistenceStatus status,
        StudentDataBlockDefinition block,
        long saveRevision,
        string error
    )
    {
        Status = status;
        Block = block;
        SaveRevision = saveRevision;
        Error = error;
    }

    internal static StudentDataPersistenceResult CreateSuccess(
        StudentDataBlockDefinition block,
        long saveRevision
    )
    {
        return new StudentDataPersistenceResult(
            StudentDataPersistenceStatus.Success,
            block,
            saveRevision,
            null
        );
    }

    internal static StudentDataPersistenceResult CreateFailure(string error)
    {
        return new StudentDataPersistenceResult(
            StudentDataPersistenceStatus.Failure,
            null,
            0,
            error
        );
    }
}
