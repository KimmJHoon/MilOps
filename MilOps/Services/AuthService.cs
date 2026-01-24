using System;
using System.IO;

namespace MilOps.Services;

public static class AuthService
{
    private static readonly string AuthFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MilOps",
        "auth.txt"
    );

    public static string? CurrentUserId { get; private set; }

    public static bool IsLoggedIn => !string.IsNullOrEmpty(CurrentUserId);

    public static void Login(string userId)
    {
        CurrentUserId = userId;
        SaveAuthState();
    }

    public static void Logout()
    {
        CurrentUserId = null;
        DeleteAuthState();
    }

    public static bool TryRestoreSession()
    {
        try
        {
            if (File.Exists(AuthFilePath))
            {
                CurrentUserId = File.ReadAllText(AuthFilePath).Trim();
                return !string.IsNullOrEmpty(CurrentUserId);
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
