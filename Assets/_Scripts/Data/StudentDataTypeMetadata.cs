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
