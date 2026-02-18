namespace MilOps.Services;

/// <summary>
/// UserRole 확장 메서드 — 역할 체크 로직 통합
/// 16곳 이상에서 반복되던 if/switch 체인을 단일 메서드로 통합
/// </summary>
public static class UserRoleExtensions
{
    /// <summary>최종관리자 (행안부 또는 육본)</summary>
    public static bool IsSuperAdmin(this UserRole role)
        => role is UserRole.SuperAdminMois or UserRole.SuperAdminArmy;

    /// <summary>중간관리자 (지자체 또는 군부대)</summary>
    public static bool IsMiddleManager(this UserRole role)
        => role is UserRole.MiddleLocal or UserRole.MiddleMilitary;

    /// <summary>관리자급 전체 (최종 + 중간 + 뷰어)</summary>
    public static bool IsManager(this UserRole role)
        => role is UserRole.SuperAdminMois or UserRole.SuperAdminArmy
            or UserRole.MiddleLocal or UserRole.MiddleMilitary
            or UserRole.ViewerMilitary;

    /// <summary>뷰어 (여단총괄)</summary>
    public static bool IsViewer(this UserRole role)
        => role is UserRole.ViewerMilitary;

    /// <summary>일반 사용자 (지자체 또는 군부대)</summary>
    public static bool IsUser(this UserRole role)
        => role is UserRole.UserLocal or UserRole.UserMilitary;

    /// <summary>군 계열 역할 (군부대 실무자, 중간관리자, 뷰어, 육본)</summary>
    public static bool IsMilitary(this UserRole role)
        => role is UserRole.UserMilitary or UserRole.MiddleMilitary
            or UserRole.ViewerMilitary or UserRole.SuperAdminArmy;

    /// <summary>지자체 계열 역할 (지자체 실무자, 중간관리자, 행안부)</summary>
    public static bool IsLocal(this UserRole role)
        => role is UserRole.UserLocal or UserRole.MiddleLocal or UserRole.SuperAdminMois;

    /// <summary>군 중간관리자 + 뷰어 (사단/여단 관리자)</summary>
    public static bool IsMilitaryManager(this UserRole role)
        => role is UserRole.MiddleMilitary or UserRole.ViewerMilitary;

    /// <summary>그룹 뱃지 표시 대상 (최종관리자 + 중간관리자 + 뷰어)</summary>
    public static bool ShowsGroupBadges(this UserRole role)
        => role.IsSuperAdmin() || role.IsMiddleManager() || role.IsViewer();

    /// <summary>DB role 문자열로 변환</summary>
    public static string ToRoleString(this UserRole role) => role switch
    {
        UserRole.UserLocal => "user_local",
        UserRole.UserMilitary => "user_military",
        UserRole.MiddleLocal => "middle_local",
        UserRole.MiddleMilitary => "middle_military",
        UserRole.ViewerMilitary => "viewer_military",
        UserRole.SuperAdminMois => "super_admin_mois",
        UserRole.SuperAdminArmy => "super_admin_army",
        _ => "user_local"
    };
}
