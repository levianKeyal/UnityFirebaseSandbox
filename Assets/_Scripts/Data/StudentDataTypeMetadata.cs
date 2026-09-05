using System;
using System.Collections.Generic;
using System.Globalization;

internal static class StudentDataTypeMetadata
{
    private const string DateTimeRoundTripFormat = "O";

    private static readonly HashSet<string> ReservedDataKeys = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "schemaVersion",
        "updatedAt",
        "saveRevision"
    };

    public static bool IsReservedDataKey(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && ReservedDataKeys.Contains(key);
    }

    public static bool IsUpdateModeCompatible(
        StudentDataValueType valueType,
        StudentDataUpdateMode updateMode
    )
    {
        if (updateMode == StudentDataUpdateMode.Set)
        {
            return true;
        }

        return valueType == StudentDataValueType.Int
            || valueType == StudentDataValueType.Long
            || valueType == StudentDataValueType.Float
            || valueType == StudentDataValueType.Double;
    }

    public static bool TryValidateRuntimeValue(
        StudentDataValueType valueType,
        object value,
        out string error
    )
    {
        if (value == null)
        {
            if (valueType == StudentDataValueType.String)
            {
                error = null;
                return true;
            }

            error = $"Null is not valid for value type {valueType}.";
            return false;
        }

        Type expectedType = GetRuntimeType(valueType);
        if (value.GetType() != expectedType)
        {
            error = $"Expected runtime type {expectedType.Name}, received {value.GetType().Name}.";
            return false;
        }

        if (valueType == StudentDataValueType.Float)
        {
            float floatValue = (float)value;
            if (float.IsNaN(floatValue) || float.IsInfinity(floatValue))
            {
                error = "NaN and Infinity are not valid runtime float values.";
                return false;
            }
        }

        if (valueType == StudentDataValueType.Double)
        {
            double doubleValue = (double)value;
            if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue))
            {
                error = "NaN and Infinity are not valid runtime double values.";
                return false;
            }
        }

        if (valueType == StudentDataValueType.DateTime && ((DateTime)value).Kind != DateTimeKind.Utc)
        {
            error = "DateTime runtime values must use DateTimeKind.Utc.";
            return false;
        }

        error = null;
        return true;
    }

    public static Type GetRuntimeType(StudentDataValueType valueType)
    {
        switch (valueType)
        {
            case StudentDataValueType.String:
                return typeof(string);
            case StudentDataValueType.Bool:
                return typeof(bool);
            case StudentDataValueType.Int:
                return typeof(int);
            case StudentDataValueType.Long:
                return typeof(long);
            case StudentDataValueType.Float:
                return typeof(float);
            case StudentDataValueType.Double:
                return typeof(double);
            case StudentDataValueType.DateTime:
                return typeof(DateTime);
            default:
                throw new ArgumentOutOfRangeException(nameof(valueType), valueType, "Unknown value type.");
        }
    }

    public static bool TryGetDefaultValue(
        StudentDataValueType valueType,
        string defaultString,
        bool defaultBool,
        int defaultInt,
        long defaultLong,
        float defaultFloat,
        double defaultDouble,
        string defaultDateTime,
        out object value
    )
    {
        switch (valueType)
        {
            case StudentDataValueType.String:
                value = defaultString;
                return true;
            case StudentDataValueType.Bool:
                value = defaultBool;
                return true;
            case StudentDataValueType.Int:
                value = defaultInt;
                return true;
            case StudentDataValueType.Long:
                value = defaultLong;
                return true;
            case StudentDataValueType.Float:
                value = defaultFloat;
                return !float.IsNaN(defaultFloat) && !float.IsInfinity(defaultFloat);
            case StudentDataValueType.Double:
                value = defaultDouble;
                return !double.IsNaN(defaultDouble) && !double.IsInfinity(defaultDouble);
            case StudentDataValueType.DateTime:
                return TryParseDateTime(defaultDateTime, out value);
            default:
                value = null;
                return false;
        }
    }

    public static bool TryParseDateTime(string serializedValue, out object value)
    {
        DateTime parsedDateTime;
        bool parsed = DateTime.TryParseExact(
            serializedValue,
            DateTimeRoundTripFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out parsedDateTime
        );

        if (parsed && parsedDateTime.Kind != DateTimeKind.Utc)
        {
            parsed = false;
        }

        value = parsed ? parsedDateTime : null;
        return parsed;
    }
}
