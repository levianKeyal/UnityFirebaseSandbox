using System;
using System.Collections.Generic;
using Firebase.Auth;
using Firebase.Firestore;
using UnityEngine;

public static class UserFirestoreMapper
{
    private const string FieldUid = "uid";
    private const string FieldEmail = "email";
    private const string FieldDisplayName = "displayName";
    private const string FieldPhotoUrl = "photoUrl";
    private const string FieldRole = "role";
    private const string FieldStatus = "status";
    private const string FieldCreatedAt = "createdAt";
    private const string FieldLastLogin = "lastLogin";

    public static Dictionary<string, object> CreateNewUserDocument(FirebaseUser firebaseUser, DateTime utcNow)
    {
        return new Dictionary<string, object>
        {
            { FieldUid, firebaseUser.UserId },
            { FieldEmail, NormalizeEmail(firebaseUser.Email) },
            { FieldDisplayName, NormalizeDisplayName(firebaseUser.DisplayName) },
            { FieldPhotoUrl, NormalizePhotoUrl(firebaseUser.PhotoUrl) },
            { FieldRole, ToFirestoreRole(UserRole.User) },
            { FieldStatus, ToFirestoreStatus(UserStatus.Active) },
            { FieldCreatedAt, ToTimestamp(utcNow) },
            { FieldLastLogin, ToTimestamp(utcNow) }
        };
    }

    public static Dictionary<string, object> CreateExistingUserUpdateDocument(
        FirebaseUser firebaseUser,
        DateTime utcNow
    )
    {
        return new Dictionary<string, object>
        {
            { FieldEmail, NormalizeEmail(firebaseUser.Email) },
            { FieldDisplayName, NormalizeDisplayName(firebaseUser.DisplayName) },
            { FieldPhotoUrl, NormalizePhotoUrl(firebaseUser.PhotoUrl) },
            { FieldLastLogin, ToTimestamp(utcNow) }
        };
    }

    public static UserProfile FromDocument(DocumentSnapshot snapshot)
    {
        UserProfile profile = new UserProfile
        {
            Uid = ReadString(snapshot, FieldUid, snapshot.Id, true),
            Email = ReadString(snapshot, FieldEmail, string.Empty, false),
            DisplayName = ReadString(snapshot, FieldDisplayName, string.Empty, false),
            PhotoUrl = ReadString(snapshot, FieldPhotoUrl, string.Empty, false),
            Role = ReadRole(snapshot),
            Status = ReadStatus(snapshot),
            CreatedAt = ReadDateTime(snapshot, FieldCreatedAt, DateTime.UtcNow, true),
            LastLogin = ReadDateTime(snapshot, FieldLastLogin, DateTime.UtcNow, true)
        };

        return profile;
    }

    public static string NormalizeEmail(string email)
    {
        return string.IsNullOrWhiteSpace(email) ? string.Empty : email;
    }

    public static string NormalizeDisplayName(string displayName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName;
    }

    public static string NormalizePhotoUrl(Uri photoUrl)
    {
        return photoUrl != null ? photoUrl.ToString() : string.Empty;
    }

    public static string ToFirestoreRole(UserRole role)
    {
        switch (role)
        {
            case UserRole.Admin:
                return "admin";
            case UserRole.SuperAdmin:
                return "superAdmin";
            case UserRole.User:
            default:
                return "user";
        }
    }

    public static string ToFirestoreStatus(UserStatus status)
    {
        switch (status)
        {
            case UserStatus.Suspended:
                return "suspended";
            case UserStatus.Disabled:
                return "disabled";
            case UserStatus.Active:
            default:
                return "active";
        }
    }

    public static Timestamp ToTimestamp(DateTime utcDateTime)
    {
        DateTime utcValue = utcDateTime.Kind == DateTimeKind.Utc
            ? utcDateTime
            : DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

        return Timestamp.FromDateTime(utcValue);
    }

    private static string ReadString(
        DocumentSnapshot snapshot,
        string fieldName,
        string fallback,
        bool useWarning
    )
    {
        if (!snapshot.ContainsField(fieldName))
        {
            if (useWarning)
            {
                Debug.LogWarning(
                    $"[UserMapper] Campo faltante '{fieldName}' en documento '{snapshot.Id}'."
                );
            }

            return fallback;
        }

        string value = snapshot.GetValue<string>(fieldName);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static UserRole ReadRole(DocumentSnapshot snapshot)
    {
        if (!snapshot.ContainsField(FieldRole))
        {
            Debug.LogWarning(
                $"[UserMapper] Campo faltante '{FieldRole}' en documento '{snapshot.Id}'. Usando User."
            );
            return UserRole.User;
        }

        string rawValue = snapshot.GetValue<string>(FieldRole);

        if (string.Equals(rawValue, "user", StringComparison.OrdinalIgnoreCase))
        {
            return UserRole.User;
        }

        if (string.Equals(rawValue, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return UserRole.Admin;
        }

        if (string.Equals(rawValue, "superAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return UserRole.SuperAdmin;
        }

        Debug.LogWarning(
            $"[UserMapper] Valor de rol invalido '{rawValue}' en documento '{snapshot.Id}'. Usando User."
        );
        return UserRole.User;
    }

    private static UserStatus ReadStatus(DocumentSnapshot snapshot)
    {
        if (!snapshot.ContainsField(FieldStatus))
        {
            Debug.LogWarning(
                $"[UserMapper] Campo faltante '{FieldStatus}' en documento '{snapshot.Id}'. Usando Disabled."
            );
            return UserStatus.Disabled;
        }

        string rawValue = snapshot.GetValue<string>(FieldStatus);

        if (string.Equals(rawValue, "active", StringComparison.OrdinalIgnoreCase))
        {
            return UserStatus.Active;
        }

        if (string.Equals(rawValue, "suspended", StringComparison.OrdinalIgnoreCase))
        {
            return UserStatus.Suspended;
        }

        if (string.Equals(rawValue, "disabled", StringComparison.OrdinalIgnoreCase))
        {
            return UserStatus.Disabled;
        }

        Debug.LogWarning(
            $"[UserMapper] Valor de estado invalido '{rawValue}' en documento '{snapshot.Id}'. Usando Disabled."
        );
        return UserStatus.Disabled;
    }

    private static DateTime ReadDateTime(
        DocumentSnapshot snapshot,
        string fieldName,
        DateTime fallback,
        bool useWarning
    )
    {
        if (!snapshot.ContainsField(fieldName))
        {
            if (useWarning)
            {
                Debug.LogWarning(
                    $"[UserMapper] Campo de fecha faltante '{fieldName}' en documento '{snapshot.Id}'."
                );
            }

            return fallback;
        }

        Timestamp timestamp = snapshot.GetValue<Timestamp>(fieldName);
        return DateTime.SpecifyKind(timestamp.ToDateTime(), DateTimeKind.Utc);
    }
}
