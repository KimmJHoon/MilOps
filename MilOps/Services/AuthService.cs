using System;
using System.IO;

namespace MilOps.Services;

public enum UserRole
{
    None,
    SuperAdmin,  // 최종관리자 (SW0001)
    Manager      // 일반 담당자
}

public static class AuthService
{
    private static readonly string AuthFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MilOps",
        "auth.txt"
    );

    public static string? CurrentUserId { get; private set; }
    public static UserRole CurrentUserRole { get; private set; } = UserRole.None;

    public static bool IsLoggedIn => !string.IsNullOrEmpty(CurrentUserId);
    public static bool IsSuperAdmin => CurrentUserRole == UserRole.SuperAdmin;

    public static void Login(string userId)
    {
        CurrentUserId = userId;
        CurrentUserRole = userId == "SW0001" ? UserRole.SuperAdmin : UserRole.Manager;
        SaveAuthState();
    }

    public static void Logout()
    {
        CurrentUserId = null;
        CurrentUserRole = UserRole.None;
        DeleteAuthState();
    }

    public static bool TryRestoreSession()
    {
        try
        {
            if (File.Exists(AuthFilePath))
            {
                var userId = File.ReadAllText(AuthFilePath).Trim();
                if (!string.IsNullOrEmpty(userId))
                {
                    CurrentUserId = userId;
                    CurrentUserRole = userId == "SW0001" ? UserRole.SuperAdmin : UserRole.Manager;
                    return true;
                }
            }
        }
        catch
        {
            // Ignore errors
        }
        return false;
    }

    private static void SaveAuthState()
    {
        try
        {
            var directory = Path.GetDirectoryName(AuthFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            File.WriteAllText(AuthFilePath, CurrentUserId ?? "");
        }
        catch
        {
            // Ignore errors
        }
    }

    private static void DeleteAuthState()
    {
        try
        {
            if (File.Exists(AuthFilePath))
            {
                File.Delete(AuthFilePath);
            }
        }
        catch
        {
            // Ignore errors
        }
    }
}
