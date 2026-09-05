using UnityEngine;

[CreateAssetMenu(fileName = "StudentDataBlockDefinition", menuName = "Student Data/Block Definition")]
public class StudentDataBlockDefinition : ScriptableObject
{
    [SerializeField] private string key;
    [SerializeField] private string displayName;
    [SerializeField] private int schemaVersion = 1;

    public string Key => key;
    public string DisplayName => displayName;
    public int SchemaVersion => schemaVersion;

    public StudentDataValidationResult Validate()
    {
        StudentDataValidationResult result = new StudentDataValidationResult();

        if (!StudentDataKeyValidation.IsValid(key))
        {
            result.AddError("Block key must be non-empty and contain only stable key characters.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            result.AddError("Block display name is required.");
        }

        if (schemaVersion < 1)
        {
            result.AddError("Block schema version must be at least 1.");
        }

        return result;
    }
}
