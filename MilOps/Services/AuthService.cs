using System;
using System.Threading.Tasks;
using Supabase.Gotrue.Exceptions;
using UserModel = MilOps.Models.User;

namespace MilOps.Services;

public enum UserRole
{
    None,
    SuperAdminMois,    // 행정안전부 최종관리자
    SuperAdminArmy,    // 육군본부 최종관리자
    MiddleLocal,       // 지자체 중간관리자
    MiddleMilitary,    // 군부대 중간관리자
    UserLocal,         // 지자체 실무자
    UserMilitary       // 군부대 실무자
}

public static class AuthService
{
    public static UserModel? CurrentUser { get; private set; }
    public static UserRole CurrentUserRole { get; private set; } = UserRole.None;

    public static bool IsLoggedIn => CurrentUser != null;
    public static bool IsSuperAdmin => CurrentUserRole == UserRole.SuperAdminMois ||
                                       CurrentUserRole == UserRole.SuperAdminArmy;
    public static bool IsMiddleManager => CurrentUserRole == UserRole.MiddleLocal ||
                                          CurrentUserRole == UserRole.MiddleMilitary;

    // 하위 호환용 프로퍼티
    public static string? CurrentUserId => CurrentUser?.LoginId;

    /// <summary>
    /// login_id와 password로 로그인
    /// </summary>
    public static async Task<(bool success, string? errorMessage)> LoginAsync(string loginId, string password)
    {
        try
        {
            // 1. login_id로 사용자 전체 정보 조회 (로그인 전이므로 anon key로 접근)
            System.Diagnostics.Debug.WriteLine($"[AuthService] LoginAsync: Step 1 - Getting user info for login_id={loginId}");
            var userInfo = await SupabaseService.GetUserByLoginIdAsync(loginId);
            if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
            {
                System.Diagnostics.Debug.WriteLine("[AuthService] LoginAsync: User not found");
                return (false, "존재하지 않는 아이디입니다");
            }
            System.Diagnostics.Debug.WriteLine($"[AuthService] LoginAsync: Found user - Email={userInfo.Email}, Role={userInfo.Role}");

            // 2. Supabase Auth로 로그인
            System.Diagnostics.Debug.WriteLine("[AuthService] LoginAsync: Step 2 - Signing in with Supabase Auth");
            var session = await SupabaseService.Client.Auth.SignIn(userInfo.Email, password);
            if (session?.User == null)
            {
                System.Diagnostics.Debug.WriteLine("[AuthService] LoginAsync: Sign in failed - no session");
                return (false, "로그인에 실패했습니다");
            }
            System.Diagnostics.Debug.WriteLine($"[AuthService] LoginAsync: Sign in successful, User ID={session.User.Id}");

            // 3. 이미 조회한 사용자 정보 사용 (RLS 정책 우회)
            System.Diagnostics.Debug.WriteLine($"[AuthService] LoginAsync: Using pre-fetched profile - LoginId={userInfo.LoginId}, Role={userInfo.Role}");
            CurrentUser = userInfo;
            CurrentUserRole = ParseRole(userInfo.Role);
            System.Diagnostics.Debug.WriteLine($"[AuthService] LoginAsync: ParsedRole={CurrentUserRole}, IsSuperAdmin={IsSuperAdmin}");

            return (true, null);
        }
        catch (GotrueException ex)
        {
            return (false, ex.Message switch
            {
                var m when m.Contains("Invalid login credentials") => "아이디 또는 비밀번호가 일치하지 않습니다",
                var m when m.Contains("Email not confirmed") => "이메일 인증이 필요합니다",
                _ => $"로그인 오류: {ex.Message}"
            });
        }
        catch (Exception ex)
        {
            return (false, $"오류가 발생했습니다: {ex.Message}");
        }
    }

    /// <summary>
    /// 로그아웃
    /// </summary>
    public static async Task LogoutAsync()
    {
        try
        {
            await SupabaseService.Client.Auth.SignOut();
        }
        catch
        {
            // Ignore errors
        }
        finally
        {
            CurrentUser = null;
            CurrentUserRole = UserRole.None;
        }
    }

    /// <summary>
    /// 세션 복원 시도
    /// </summary>
    public static async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            if (!SupabaseService.IsInitialized)
            {
                await SupabaseService.InitializeAsync();
            }

            var session = SupabaseService.Client.Auth.CurrentSession;
            if (session?.User == null)
            {
                return false;
            }

            // 세션이 있으면 프로필 조회
            var userProfile = await SupabaseService.GetCurrentUserProfileAsync();
            if (userProfile == null)
            {
                return false;
            }

            CurrentUser = userProfile;
            CurrentUserRole = ParseRole(userProfile.Role);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static UserRole ParseRole(string? role)
    {
        return role switch
        {
            "super_admin_mois" => UserRole.SuperAdminMois,
            "super_admin_army" => UserRole.SuperAdminArmy,
            "middle_local" => UserRole.MiddleLocal,
            "middle_military" => UserRole.MiddleMilitary,
            "user_local" => UserRole.UserLocal,
            "user_military" => UserRole.UserMilitary,
            _ => UserRole.None
        };
    }

    // 하위 호환용 동기 메서드들
    public static bool TryRestoreSession()
    {
        try
        {
            return TryRestoreSessionAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return false;
        }
    }

    public static void Logout()
    {
        try
        {
            LogoutAsync().GetAwaiter().GetResult();
        }
        catch
        {
            CurrentUser = null;
            CurrentUserRole = UserRole.None;
        }
    }
}
