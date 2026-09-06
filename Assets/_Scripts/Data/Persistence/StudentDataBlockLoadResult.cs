public enum StudentDataBlockLoadStatus
{
    Success,
    NotFound,
    Failure
}

public sealed class StudentDataBlockLoadResult
{
    public StudentDataBlockLoadStatus Status { get; }
    public StudentDataBlockDefinition Block { get; }
    public StudentDataBlockDocument Document { get; }
    public string Error { get; }
    public bool Success => Status == StudentDataBlockLoadStatus.Success;
    public bool IsNotFound => Status == StudentDataBlockLoadStatus.NotFound;

    private StudentDataBlockLoadResult(
        StudentDataBlockLoadStatus status,
        StudentDataBlockDefinition block,
        StudentDataBlockDocument document,
        string error
    )
    {
        Status = status;
        Block = block;
        Document = document;
        Error = error;
    }

    internal static StudentDataBlockLoadResult CreateSuccess(
        StudentDataBlockDefinition block,
        StudentDataBlockDocument document
    )
    {
        return new StudentDataBlockLoadResult(
            StudentDataBlockLoadStatus.Success,
            block,
            document,
            null
        );
    }

    internal static StudentDataBlockLoadResult CreateNotFound(
        StudentDataBlockDefinition block
    )
    {
        return new StudentDataBlockLoadResult(
            StudentDataBlockLoadStatus.NotFound,
            block,
            null,
            null
        );
    }

    internal static StudentDataBlockLoadResult CreateFailure(string error)
    {
        return new StudentDataBlockLoadResult(
            StudentDataBlockLoadStatus.Failure,
            null,
            null,
            error
        );
    }
}
