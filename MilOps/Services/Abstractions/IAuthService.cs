using System.Threading.Tasks;
using MilOps.Models;

namespace MilOps.Services.Abstractions;

/// <summary>
/// 인증 서비스 인터페이스 - 테스트 가능하게 하기 위한 추상화
/// </summary>
public interface IAuthService
{
    User? CurrentUser { get; }
    UserRole CurrentUserRole { get; }
    bool IsLoggedIn { get; }
    bool IsSuperAdmin { get; }
    bool IsMiddleManager { get; }

    Task<(bool success, string? errorMessage)> LoginAsync(string loginId, string password);
    Task LogoutAsync();
    Task<bool> TryRestoreSessionAsync();
}

/// <summary>
/// 기존 static AuthService를 래핑하는 어댑터
/// 프로덕션에서 사용, 테스트에서는 Mock으로 대체
/// </summary>
public class AuthServiceAdapter : IAuthService
{
    public User? CurrentUser => AuthService.CurrentUser;
    public UserRole CurrentUserRole => AuthService.CurrentUserRole;
    public bool IsLoggedIn => AuthService.IsLoggedIn;
    public bool IsSuperAdmin => AuthService.IsSuperAdmin;
    public bool IsMiddleManager => AuthService.IsMiddleManager;

    public Task<(bool success, string? errorMessage)> LoginAsync(string loginId, string password)
        => AuthService.LoginAsync(loginId, password);

    public Task LogoutAsync()
        => AuthService.LogoutAsync();

    public Task<bool> TryRestoreSessionAsync()
        => AuthService.TryRestoreSessionAsync();
}
