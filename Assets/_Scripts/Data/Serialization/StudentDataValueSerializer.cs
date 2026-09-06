using System;
using Firebase.Firestore;

public sealed class StudentDataValueSerializer
{
    public bool TrySerialize(
        StudentDataDefinition definition,
        object runtimeValue,
        out object serializedValue,
        out string error
    )
    {
        serializedValue = null;
        if (!TryValidateDefinitionAndRuntimeValue(definition, runtimeValue, out error))
        {
            return false;
        }

        switch (definition.ValueType)
        {
            case StudentDataValueType.String:
            case StudentDataValueType.Bool:
            case StudentDataValueType.Long:
                serializedValue = runtimeValue;
                return true;
            case StudentDataValueType.Int:
                serializedValue = (long)(int)runtimeValue;
                return true;
            case StudentDataValueType.Float:
                serializedValue = (double)(float)runtimeValue;
                return true;
            case StudentDataValueType.Double:
                serializedValue = (double)runtimeValue;
                return true;
            case StudentDataValueType.DateTime:
                serializedValue = Timestamp.FromDateTime((DateTime)runtimeValue);
                return true;
            default:
                return Fail(definition, "a supported StudentDataValueType", definition.ValueType, out error);
        }
    }

    public bool TryDeserialize(
        StudentDataDefinition definition,
        object storedValue,
        out object runtimeValue,
        out string error
    )
    {
        runtimeValue = null;
        if (!TryValidateDefinition(definition, out error))
        {
            return false;
        }

        switch (definition.ValueType)
        {
            case StudentDataValueType.String:
                if (storedValue == null || storedValue is string)
                {
                    runtimeValue = storedValue;
                    return true;
                }

                return Fail(definition, "String or null", storedValue, out error);
            case StudentDataValueType.Bool:
                if (storedValue is bool)
                {
                    runtimeValue = storedValue;
                    return true;
                }

                return Fail(definition, "Boolean", storedValue, out error);
            case StudentDataValueType.Int:
                return TryDeserializeInt(definition, storedValue, out runtimeValue, out error);
            case StudentDataValueType.Long:
                if (storedValue is int || storedValue is long)
                {
                    runtimeValue = Convert.ToInt64(storedValue);
                    return true;
                }

                return Fail(definition, "Int32 or Int64", storedValue, out error);
            case StudentDataValueType.Float:
                return TryDeserializeFloat(definition, storedValue, out runtimeValue, out error);
            case StudentDataValueType.Double:
                return TryDeserializeDouble(definition, storedValue, out runtimeValue, out error);
            case StudentDataValueType.DateTime:
                if (storedValue is Timestamp)
                {
                    DateTime timestampValue = ((Timestamp)storedValue).ToDateTime();
                    runtimeValue = DateTime.SpecifyKind(timestampValue, DateTimeKind.Utc);
                    return true;
                }

                return Fail(definition, "Firebase Timestamp", storedValue, out error);
            default:
                return Fail(definition, "a supported StudentDataValueType", definition.ValueType, out error);
        }
    }

    private static bool TryDeserializeInt(
        StudentDataDefinition definition,
        object storedValue,
        out object runtimeValue,
        out string error
    )
    {
        runtimeValue = null;
        error = null;

        if (storedValue is int)
        {
            runtimeValue = storedValue;
            return true;
        }

        if (storedValue is long)
        {
            long longValue = (long)storedValue;
            if (longValue >= int.MinValue && longValue <= int.MaxValue)
            {
                runtimeValue = (int)longValue;
                return true;
            }

            return Fail(definition, "Int32-range Int64", storedValue, out error, "outside Int32 range");
        }

        return Fail(definition, "Int32 or Int64", storedValue, out error);
    }

    private static bool TryDeserializeFloat(
        StudentDataDefinition definition,
        object storedValue,
        out object runtimeValue,
        out string error
    )
    {
        runtimeValue = null;
        error = null;

        double doubleValue;
        if (storedValue is float)
        {
            doubleValue = (float)storedValue;
        }
        else if (storedValue is double)
        {
            doubleValue = (double)storedValue;
        }
        else
        {
            return Fail(definition, "Single or Double", storedValue, out error);
        }

        if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
        {
            return Fail(definition, "finite Single/Double", storedValue, out error, "NaN or Infinity");
        }

        float floatValue = (float)doubleValue;
        if (float.IsNaN(floatValue) || float.IsInfinity(floatValue))
        {
            return Fail(definition, "finite Double representable as Single", storedValue, out error, "conversion overflows Single");
        }

        runtimeValue = floatValue;
        return true;
    }

    private static bool TryDeserializeDouble(
        StudentDataDefinition definition,
        object storedValue,
        out object runtimeValue,
        out string error
    )
    {
        runtimeValue = null;
        error = null;

        if (!(storedValue is float) && !(storedValue is double))
        {
            return Fail(definition, "Single or Double", storedValue, out error);
        }

        double doubleValue = Convert.ToDouble(storedValue);
        if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
        {
            return Fail(definition, "finite Double", storedValue, out error, "NaN or Infinity");
        }

        runtimeValue = doubleValue;
        return true;
    }

    private static bool TryValidateDefinitionAndRuntimeValue(
        StudentDataDefinition definition,
        object runtimeValue,
        out string error
    )
    {
        if (!TryValidateDefinition(definition, out error))
        {
            return false;
        }

        string valueError;
        if (!StudentDataTypeMetadata.TryValidateRuntimeValue(definition.ValueType, runtimeValue, out valueError))
        {
            return Fail(definition, StudentDataTypeMetadata.GetRuntimeType(definition.ValueType).Name, runtimeValue, out error, valueError);
        }

        error = null;
        return true;
    }

    private static bool TryValidateDefinition(StudentDataDefinition definition, out string error)
    {
        if (definition == null)
        {
            error = "StudentDataDefinition is required.";
            return false;
        }

        try
        {
            StudentDataTypeMetadata.GetRuntimeType(definition.ValueType);
        }
        catch (ArgumentOutOfRangeException)
        {
            error = $"Definition '{definition.Key}' has unsupported value type '{definition.ValueType}'.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool Fail(
        StudentDataDefinition definition,
        string expectedType,
        object receivedValue,
        out string error,
        string reason = null
    )
    {
        string receivedType = receivedValue == null ? "null" : receivedValue.GetType().Name;
        error = $"Definition '{definition.Key}' expects {expectedType} but stored value is {receivedType}.";
        if (!string.IsNullOrEmpty(reason))
        {
            error += $" Reason: {reason}.";
        }

        return false;
    }
}
