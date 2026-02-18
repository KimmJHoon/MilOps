using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Supabase.Gotrue;
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
    ViewerMilitary,    // 여단총괄 (뷰어 전용)
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
    public static bool IsViewer => CurrentUserRole == UserRole.ViewerMilitary;

    // 하위 호환용 프로퍼티
    public static string? CurrentUserId => CurrentUser?.LoginId;

    /// <summary>
    /// login_id와 password로 로그인
    /// </summary>
    public static async Task<(bool success, string? errorMessage)> LoginAsync(string loginId, string password)
    {
        var totalSw = Stopwatch.StartNew();
        try
        {
            var sw = Stopwatch.StartNew();

            // 1. 캐시된 email 확인 → 있으면 GetUserByLoginId 스킵 (856ms 절감)
            string? cachedEmail = null;
            var (_, _, cachedLoginId, email) = await SessionStorageService.LoadSessionAsync();
            if (cachedLoginId == loginId && !string.IsNullOrEmpty(email))
            {
                cachedEmail = email;
                Debug.WriteLine($"[PERF][Auth] 캐시된 email 사용 (GetUserByLoginId 스킵): {sw.ElapsedMilliseconds}ms");
            }

            // 2. 캐시 없으면 서버 조회
            UserModel? userInfo = null;
            if (cachedEmail == null)
            {
                sw.Restart();
                userInfo = await SupabaseService.GetUserByLoginIdAsync(loginId);
                Debug.WriteLine($"[PERF][Auth] GetUserByLoginId: {sw.ElapsedMilliseconds}ms");
                if (userInfo == null || string.IsNullOrEmpty(userInfo.Email))
                {
                    Debug.WriteLine("[AuthService] LoginAsync: User not found");
                    return (false, "존재하지 않는 아이디입니다");
                }
                cachedEmail = userInfo.Email;
            }

            // 3. Supabase Auth로 로그인
            sw.Restart();
            var session = await SupabaseService.Client.Auth.SignIn(cachedEmail, password);
            Debug.WriteLine($"[PERF][Auth] Auth.SignIn: {sw.ElapsedMilliseconds}ms");
            if (session?.User == null)
            {
                Debug.WriteLine("[AuthService] LoginAsync: Sign in failed - no session");
                return (false, "로그인에 실패했습니다");
            }

            // 4. 사용자 정보 조회 (캐시 히트 시에만 필요)
            if (userInfo == null)
            {
                userInfo = await SupabaseService.GetUserByLoginIdAsync(loginId);
                if (userInfo == null)
                {
                    return (false, "사용자 정보를 불러올 수 없습니다");
                }
            }

            CurrentUser = userInfo;
            CurrentUserRole = ParseRole(userInfo.Role);

            // 5. 세션 토큰 + email 캐시 저장
            sw.Restart();
            if (session.AccessToken != null && session.RefreshToken != null)
            {
                await SessionStorageService.SaveSessionAsync(session.AccessToken, session.RefreshToken, loginId, cachedEmail);
            }
            Debug.WriteLine($"[PERF][Auth] 세션 저장: {sw.ElapsedMilliseconds}ms");

            totalSw.Stop();
            Debug.WriteLine($"[PERF][Auth] ===== 로그인 총 소요: {totalSw.ElapsedMilliseconds}ms =====");
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
    /// 비밀번호 변경
    /// </summary>
    public static async Task<(bool success, string? errorMessage)> ChangePasswordAsync(
        string currentPassword, string newPassword)
    {
        try
        {
            if (CurrentUser == null || string.IsNullOrEmpty(CurrentUser.Email))
            {
                return (false, "로그인 상태가 아닙니다");
            }

            // 1. 현재 비밀번호 검증 (재인증)
            try
            {
                await SupabaseService.Client.Auth.SignIn(CurrentUser.Email, currentPassword);
            }
            catch (GotrueException)
            {
                return (false, "현재 비밀번호가 일치하지 않습니다");
            }

            // 2. 새 비밀번호로 업데이트
            var attrs = new UserAttributes { Password = newPassword };
            await SupabaseService.Client.Auth.Update(attrs);

            // 3. 세션 토큰 갱신 — 기존 cachedEmail 보존
            var session = SupabaseService.Client.Auth.CurrentSession;
            if (session?.AccessToken != null && session.RefreshToken != null && CurrentUser.LoginId != null)
            {
                var (_, _, _, existingEmail) = await SessionStorageService.LoadSessionAsync();
                await SessionStorageService.SaveSessionAsync(
                    session.AccessToken, session.RefreshToken, CurrentUser.LoginId, existingEmail);
            }

            return (true, null);
        }
        catch (GotrueException ex)
        {
            return (false, $"비밀번호 변경 실패: {ex.Message}");
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
            // Supabase 초기화
            try
            {
                if (!SupabaseService.IsInitialized)
                {
                    await SupabaseService.InitializeAsync();
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
            string? cachedEmail = null;

            try
            {
                (accessToken, refreshToken, loginId, cachedEmail) = await SessionStorageService.LoadSessionAsync();
            }
            catch (Exception loadEx)
            {
                System.Diagnostics.Debug.WriteLine($"[AuthService] TryRestoreSessionAsync: Load session failed - {loadEx.Message}");
                return false;
            }

            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken) || string.IsNullOrEmpty(loginId))
            {
                return false;
            }

            // 2. 토큰으로 세션 복원 시도
            try
            {
                var session = await SupabaseService.Client.Auth.SetSession(accessToken, refreshToken);

                if (session?.User == null)
                {
                    System.Diagnostics.Debug.WriteLine("[AuthService] TryRestoreSessionAsync: SetSession failed - no user");
                    SessionStorageService.ClearSession();
                    return false;
                }

                // 3. 새 토큰 저장 (리프레시된 경우) — 기존 cachedEmail 보존
                if (session.AccessToken != null && session.RefreshToken != null)
                {
                    await SessionStorageService.SaveSessionAsync(session.AccessToken, session.RefreshToken, loginId, cachedEmail);
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
                var userProfile = await SupabaseService.GetUserByLoginIdAsync(loginId);

                if (userProfile == null)
                {
                    System.Diagnostics.Debug.WriteLine("[AuthService] TryRestoreSessionAsync: User profile not found");
                    SessionStorageService.ClearSession();
                    return false;
                }

                CurrentUser = userProfile;
                CurrentUserRole = ParseRole(userProfile.Role);
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
            "viewer_military" => UserRole.ViewerMilitary,
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
