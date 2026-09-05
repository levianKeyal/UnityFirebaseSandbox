using System;

internal static class StudentDataKeyValidation
{
    public static bool IsValid(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || !string.Equals(key, key.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        foreach (char character in key)
        {
            if (
                !char.IsLetterOrDigit(character)
                && character != '_'
                && character != '-'
            )
            {
                return false;
            }
        }

        return true;
    }
}
