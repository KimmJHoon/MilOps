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

            // 4. 세션 토큰 저장 (자동 로그인용)
            if (session.AccessToken != null && session.RefreshToken != null)
            {
                await SessionStorageService.SaveSessionAsync(session.AccessToken, session.RefreshToken, loginId);
            }

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
            System.Diagnostics.Debug.WriteLine("[AuthService] TryRestoreSessionAsync: Starting...");

            // Supabase 초기화
            try
            {
                if (!SupabaseService.IsInitialized)
                {
                    System.Diagnostics.Debug.WriteLine("[AuthService] TryRestoreSessionAsync: Initializing Supabase...");
                    await SupabaseService.InitializeAsync();
                    System.Diagnostics.Debug.WriteLine("[AuthService] TryRestoreSessionAsync: Supabase initialized");
                }
            }
            catch (Exception initEx)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthService] TryRestoreSessionAsync: Supabase init failed - {initEx.Message}");
                return false;
            }

            // 1. 저장된 세션 토큰 로드
            string? accessToken = null;
            string? refreshToken = null;
            string? loginId = null;

            try
            {
                System.Diagnostics.Debug.WriteLine("[AuthService] TryRestoreSessionAsync: Loading session...");
                (accessToken, refreshToken, loginId) = await SessionStorageService.LoadSessionAsync();
                System.Diagnostics.Debug.WriteLine($"[AuthService] TryRestoreSessionAsync: Session loaded - hasAccess={!string.IsNullOrEmpty(accessToken)}, hasRefresh={!string.IsNullOrEmpty(refreshToken)}, loginId={loginId ?? "null"}");
            }
            catch (Exception loadEx)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthService] TryRestoreSessionAsync: Load session failed - {loadEx.Message}");
                return false;
            }

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(loginId))
            {
                System.Diagnostics.Debug.WriteLine("[AuthService] TryRestoreSessionAsync: No saved session");
                return false;
            }

            System.Diagnostics.Debug.WriteLine($"[AuthService] TryRestoreSessionAsync: Found saved session for {loginId}");

            // 2. 토큰으로 세션 복원 시도
            try
            {
                System.Diagnostics.Debug.WriteLine("[AuthService] TryRestoreSessionAsync: Setting session...");
                var session = await SupabaseService.Client.Auth.SetSession(accessToken, refreshToken);

                if (session?.User == null)
                {
                    System.Diagnostics.Debug.WriteLine("[AuthService] TryRestoreSessionAsync: SetSession failed - no user");
                    SessionStorageService.ClearSession();
                    return false;
                }

                System.Diagnostics.Debug.WriteLine($"[AuthService] TryRestoreSessionAsync: Session restored, User ID={session.User.Id}");

                // 3. 새 토큰 저장 (리프레시된 경우)
                if (session.AccessToken != null && session.RefreshToken != null)
                {
                    await SessionStorageService.SaveSessionAsync(session.AccessToken, session.RefreshToken, loginId);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthService] TryRestoreSessionAsync: SetSession error - {ex.GetType().Name}: {ex.Message}");
                SessionStorageService.ClearSession();
                return false;
            }

            // 4. 사용자 프로필 조회
            try
            {
                System.Diagnostics.Debug.WriteLine("[AuthService] TryRestoreSessionAsync: Loading user profile...");
                var userProfile = await SupabaseService.GetUserByLoginIdAsync(loginId);

                if (userProfile == null)
                {
                    System.Diagnostics.Debug.WriteLine("[AuthService] TryRestoreSessionAsync: User profile not found");
                    SessionStorageService.ClearSession();
                    return false;
                }

                CurrentUser = userProfile;
                CurrentUserRole = ParseRole(userProfile.Role);
                System.Diagnostics.Debug.WriteLine($"[AuthService] TryRestoreSessionAsync: Success - {loginId}, Role={CurrentUserRole}");
                return true;
            }
            catch (Exception profileEx)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthService] TryRestoreSessionAsync: Profile load error - {profileEx.Message}");
                SessionStorageService.ClearSession();
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AuthService] TryRestoreSessionAsync: Unexpected error - {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            try
            {
                SessionStorageService.ClearSession();
            }
            catch { }
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
        // UI 스레드 데드락 방지를 위해 동기적으로 처리
        CurrentUser = null;
        CurrentUserRole = UserRole.None;

        // 저장된 세션 삭제
        SessionStorageService.ClearSession();

        // 백그라운드에서 Supabase 로그아웃 처리
        _ = Task.Run(async () =>
        {
            try
            {
                await SupabaseService.Client.Auth.SignOut();
            }
            catch
            {
                // Ignore errors
            }
        });
    }
}
