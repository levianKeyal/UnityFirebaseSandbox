using UnityEngine;

[CreateAssetMenu(fileName = "StudentDataDefinition", menuName = "Student Data/Data Definition")]
public class StudentDataDefinition : ScriptableObject
{
    // This key is a persistent identifier. Do not rename it after real data is persisted without an explicit migration.
    [SerializeField] private string key;
    [SerializeField] private StudentDataBlockDefinition block;
    [SerializeField] private StudentDataValueType valueType = StudentDataValueType.String;
    [SerializeField] private StudentDataUpdateMode updateMode = StudentDataUpdateMode.Set;
    [SerializeField] private StudentDataSavePolicy savePolicy = StudentDataSavePolicy.Buffered;
    [SerializeField] private StudentDataLoadPolicy loadPolicy = StudentDataLoadPolicy.LoadNormally;

    [Header("Typed Defaults")]
    [SerializeField] private string defaultString;
    [SerializeField] private bool defaultBool;
    [SerializeField] private int defaultInt;
    [SerializeField] private long defaultLong;
    [SerializeField] private float defaultFloat;
    [SerializeField] private double defaultDouble;
    [SerializeField] private string defaultDateTime = "1970-01-01T00:00:00.0000000Z";

    public string Key => key;
    public StudentDataBlockDefinition Block => block;
    public StudentDataValueType ValueType => valueType;
    public StudentDataUpdateMode UpdateMode => updateMode;
    public StudentDataSavePolicy SavePolicy => savePolicy;
    public StudentDataLoadPolicy LoadPolicy => loadPolicy;

    public bool TryGetDefaultValue(out object value)
    {
        return StudentDataTypeMetadata.TryGetDefaultValue(
            valueType,
            defaultString,
            defaultBool,
            defaultInt,
            defaultLong,
            defaultFloat,
            defaultDouble,
            defaultDateTime,
            out value
        );
    }

    public StudentDataValidationResult Validate()
    {
        StudentDataValidationResult result = new StudentDataValidationResult();

        if (!StudentDataKeyValidation.IsValid(key))
        {
            result.AddError("Data key must be non-empty and contain only stable key characters.");
        }

        if (StudentDataTypeMetadata.IsReservedDataKey(key))
        {
            result.AddError($"Data key '{key}' is reserved for future persistence metadata.");
        }

        if (block == null)
        {
            result.AddError("Data definition must reference a block.");
        }

        if (!StudentDataTypeMetadata.IsUpdateModeCompatible(valueType, updateMode))
        {
            result.AddError($"Update mode {updateMode} is incompatible with value type {valueType}.");
        }

        if (!TryGetDefaultValue(out _))
        {
            result.AddError($"Default value is invalid for value type {valueType}.");
        }

        return result;
    }

}
