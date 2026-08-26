using System;

[Serializable]
public class UserProfile
{
    public string Uid;
    public string Email;
    public string DisplayName;
    public string PhotoUrl;
    public UserRole Role;
    public UserStatus Status;
    public DateTime CreatedAt;
    public DateTime LastLogin;

    public UserProfile()
    {
        Role = UserRole.User;
        Status = UserStatus.Active;
        CreatedAt = DateTime.UtcNow;
        LastLogin = DateTime.UtcNow;
    }
}
