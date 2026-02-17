using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

/// <summary>
/// 일정 화면 분기 타입
/// </summary>
public enum ScheduleNavigationType
{
    Input,      // 지자체담당자용 입력 화면
    Reserve,    // 대대담당자용 예약 화면
    Confirm     // 양측 확정 화면
}

/// <summary>
/// 일정 화면 이동 요청 인자
/// </summary>
public class ScheduleNavigationArgs
{
    public Guid ScheduleId { get; set; }
    public ScheduleNavigationType NavigationType { get; set; }
    public string Mode { get; set; } = "view";
}

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _selectedTab = "home";

    [ObservableProperty]
    private string _currentPageTitle = "홈";

    [ObservableProperty]
    private bool _isHomeSelected = true;

    [ObservableProperty]
    private bool _isCalendarSelected = false;

    [ObservableProperty]
    private bool _isScheduleSelected = false;

    [ObservableProperty]
    private bool _isManagerSelected = false;

    [ObservableProperty]
    private bool _isNotificationSelected = false;

    [ObservableProperty]
    private bool _isChatSelected = false;

    [ObservableProperty]
    private bool _isSettingsSelected = false;

    // 헤더 알림 뱃지용 읽지않은 개수
    [ObservableProperty]
    private int _unreadNotificationCount = 0;

    [ObservableProperty]
    private bool _isDrawerOpen = false;

    // 역할별 플래그
    [ObservableProperty]
    private bool _isSuperAdmin = false;

    [ObservableProperty]
    private bool _isMiddleAdmin = false;

    [ObservableProperty]
    private bool _isUser = false;

    [ObservableProperty]
    private string _currentUserId = "";

    [ObservableProperty]
    private string _currentUserName = "";

    [ObservableProperty]
    private string _currentUserRole = "";

    [ObservableProperty]
    private string _currentUserRegion = "";

    // 오버레이 화면 열림 상태
    [ObservableProperty]
    private bool _isCompanyRegisterOpen = false;

    [ObservableProperty]
    private bool _isScheduleCreateOpen = false;

    [ObservableProperty]
    private bool _isScheduleInputOpen = false;

    [ObservableProperty]
    private bool _isScheduleReserveOpen = false;

    [ObservableProperty]
    private bool _isScheduleConfirmOpen = false;

    [ObservableProperty]
    private bool _isChatRoomOpen = false;

    // 비밀번호 변경
    [ObservableProperty]
    private bool _isPasswordChangeVisible = false;

    [ObservableProperty]
    private string _currentPassword = "";

    [ObservableProperty]
    private string _newPassword = "";

    [ObservableProperty]
    private string _confirmPassword = "";

    [ObservableProperty]
    private string _passwordChangeMessage = "";

    [ObservableProperty]
    private bool _isPasswordChangeError = false;

    [ObservableProperty]
    private string _passwordChangeMessageColor = "#00a872";

    // 일정 입력/예약/확정 화면에 전달할 데이터
    private Guid _scheduleInputId;
    private Guid _scheduleReserveId;
    private Guid _scheduleConfirmId;

    public Guid ScheduleInputId => _scheduleInputId;
    public Guid ScheduleReserveId => _scheduleReserveId;
    public Guid ScheduleConfirmId => _scheduleConfirmId;

    // 메뉴 표시 여부 (역할별)
    public bool ShowScheduleTab => IsUser || IsMiddleAdmin;  // 사용자, 중간관리자
    public bool ShowChatTab => IsUser || IsMiddleAdmin;      // 사용자, 중간관리자 (최종관리자 제외)
    public bool ShowManagerTab => true;  // 모든 역할에서 담당자 탭 접근 가능

    public MainViewModel()
    {
        UpdateUserRole();

        // 알림 읽음 이벤트 구독 → 헤더 뱃지 실시간 갱신
        NotificationService.UnreadCountChanged += OnUnreadCountChanged;
    }

    private void OnUnreadCountChanged(int delta)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            UnreadNotificationCount = Math.Max(0, UnreadNotificationCount + delta);
        });
    }

    public void RefreshUserRole()
    {
        UpdateUserRole();
        var user = AuthService.CurrentUser;

        CurrentUserId = user?.LoginId ?? "";
        CurrentUserName = user?.FullDisplayName ?? "";
        CurrentUserRole = user?.RoleDisplayName ?? "";

        // 지역 정보 로드 (비동기 fire-and-forget — 기존 호환성 유지)
        _ = LoadUserRegionAsync();

        // 현재 선택된 탭이 해당 역할에서 접근 불가능하면 기본 탭(캘린더)으로 리셋
        ResetToValidTab();
    }

    /// <summary>
    /// RefreshUserRole + LoadUserRegionAsync를 병렬화할 수 있도록 Task를 반환하는 버전.
    /// MainView.RefreshUserRoleAsync()에서 InitializeCurrentTabAsync()와 Task.WhenAll로 병렬 실행.
    /// </summary>
    public Task RefreshUserRoleAndGetRegionTask()
    {
        UpdateUserRole();
        var user = AuthService.CurrentUser;

        CurrentUserId = user?.LoginId ?? "";
        CurrentUserName = user?.FullDisplayName ?? "";
        CurrentUserRole = user?.RoleDisplayName ?? "";

        ResetToValidTab();

        // LoadUserRegionAsync의 Task를 반환하여 caller가 병렬화 가능
        return LoadUserRegionAsync();
    }

    private async Task LoadUserRegionAsync()
    {
        try
        {
            var user = AuthService.CurrentUser;
            if (user == null) return;

            var sw = System.Diagnostics.Stopwatch.StartNew();

            // 백그라운드 스레드에서 조직 캐시 조회 + 문자열 조합
            var regionString = await Task.Run(async () =>
            {
                // 공유 조직 캐시에서 조회 (Preload 완료 시 0ms, 5개 개별쿼리 제거)
                var (regions, districts, divisions, brigades, battalions) = await QueryHelper.GetOrgDataAsync();

                var parts = new List<string>();

                if (user.RegionId.HasValue)
                {
                    var region = regions.Find(r => r.Id == user.RegionId.Value);
                    if (region != null) parts.Add(region.Name);
                }
                if (user.DistrictId.HasValue)
                {
                    var district = districts.Find(d => d.Id == user.DistrictId.Value);
                    if (district != null) parts.Add(district.Name);
                }
                if (user.DivisionId.HasValue)
                {
                    var division = divisions.Find(d => d.Id == user.DivisionId.Value);
                    if (division != null) parts.Add(division.Name);
                }
                if (user.BrigadeId.HasValue)
                {
                    var brigade = brigades.Find(b => b.Id == user.BrigadeId.Value);
                    if (brigade != null) parts.Add(brigade.Name);
                }
                if (user.BattalionId.HasValue)
                {
                    var battalion = battalions.Find(b => b.Id == user.BattalionId.Value);
                    if (battalion != null) parts.Add(battalion.Name);
                }

                return string.Join(" ", parts);
            });

            // UI 프로퍼티 업데이트는 호출 스레드(UI)에서 실행
            CurrentUserRegion = regionString;
            System.Diagnostics.Debug.WriteLine($"[PERF][MainVM] LoadUserRegionAsync (BG 스레드): {sw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadUserRegionAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 역할에서 접근 불가능한 탭이 선택되어 있으면 캘린더로 리셋
    /// </summary>
    private void ResetToValidTab()
    {
        // 일정 탭은 사용자, 중간관리자만 접근 가능
        if (IsScheduleSelected && !ShowScheduleTab)
        {
            SelectTab("calendar");
            return;
        }

        // 채팅 탭은 사용자, 중간관리자만 접근 가능
        if (IsChatSelected && !ShowChatTab)
        {
            SelectTab("calendar");
            return;
        }
    }

    private void UpdateUserRole()
    {
        var role = AuthService.CurrentUserRole;
        var roleString = AuthService.CurrentUser?.Role ?? "null";

        IsSuperAdmin = role.IsSuperAdmin();
        IsMiddleAdmin = role.IsMiddleManager() || role.IsViewer();
        IsUser = role.IsUser();

        // 역할이 None인 경우 기본값으로 사용자 처리 (담당자 메뉴 숨김)
        if (role == UserRole.None && AuthService.IsLoggedIn)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] WARNING: Role is None but user is logged in. DB role string: {roleString}");
            // 기본적으로 가장 낮은 권한으로 처리
            IsUser = true;
        }

        // 속성 변경 알림 (메뉴 표시 여부 갱신)
        OnPropertyChanged(nameof(ShowScheduleTab));
        OnPropertyChanged(nameof(ShowChatTab));
        OnPropertyChanged(nameof(ShowManagerTab));
    }

    // 탭 변경 이벤트 (View에서 구독하여 초기화 처리)
    public event Action<string>? TabChanged;

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        SelectedTab = tabName;

        // 모든 탭 선택 해제
        IsHomeSelected = false;
        IsCalendarSelected = false;
        IsScheduleSelected = false;
        IsChatSelected = false;
        IsManagerSelected = false;
        IsNotificationSelected = false;
        IsSettingsSelected = false;

        // 선택된 탭 활성화
        switch (tabName)
        {
            case "home":
                IsHomeSelected = true;
                CurrentPageTitle = "홈";
                break;
            case "calendar":
                IsCalendarSelected = true;
                CurrentPageTitle = "자원조사 일정표";
                break;
            case "schedule":
                IsScheduleSelected = true;
                CurrentPageTitle = "일정";
                break;
            case "chat":
                IsChatSelected = true;
                CurrentPageTitle = "메시지";
                break;
            case "notification":
                IsNotificationSelected = true;
                CurrentPageTitle = "알림";
                break;
            case "manager":
                IsManagerSelected = true;
                CurrentPageTitle = "담당자";
                break;
            case "settings":
                IsSettingsSelected = true;
                CurrentPageTitle = "설정";
                break;
        }

        // 탭 변경 이벤트 발생
        TabChanged?.Invoke(tabName);
    }

    [RelayCommand]
    private void ToggleDrawer()
    {
        IsDrawerOpen = !IsDrawerOpen;
    }

    [RelayCommand]
    private void CloseDrawer()
    {
        IsDrawerOpen = false;
        ResetPasswordChangeForm();
    }

    [RelayCommand]
    private void ShowPasswordChange()
    {
        // 드로어를 먼저 닫고 모달을 표시
        IsDrawerOpen = false;
        IsPasswordChangeVisible = true;
        PasswordChangeMessage = "";
        IsPasswordChangeError = false;
    }

    [RelayCommand]
    private void OpenManual()
    {
        IsDrawerOpen = false;
        PdfViewerService.OpenManual();
    }

    [RelayCommand]
    private void HidePasswordChange()
    {
        ResetPasswordChangeForm();
    }

    [RelayCommand]
    private async Task SubmitPasswordChange()
    {
        // 입력 검증
        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            SetPasswordError("현재 비밀번호를 입력해주세요");
            return;
        }
        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            SetPasswordError("새 비밀번호를 입력해주세요");
            return;
        }
        if (NewPassword.Length < 6)
        {
            SetPasswordError("비밀번호는 6자 이상이어야 합니다");
            return;
        }
        if (NewPassword != ConfirmPassword)
        {
            SetPasswordError("새 비밀번호가 일치하지 않습니다");
            return;
        }

        var (success, errorMessage) = await AuthService.ChangePasswordAsync(CurrentPassword, NewPassword);
        if (success)
        {
            PasswordChangeMessage = "비밀번호가 변경되었습니다";
            IsPasswordChangeError = false;
            PasswordChangeMessageColor = "#00a872";
            // 1.5초 후 폼 닫기
            await Task.Delay(1500);
            ResetPasswordChangeForm();
        }
        else
        {
            SetPasswordError(errorMessage ?? "비밀번호 변경에 실패했습니다");
        }
    }

    private void SetPasswordError(string message)
    {
        PasswordChangeMessage = message;
        IsPasswordChangeError = true;
        PasswordChangeMessageColor = "#D32F2F";
    }

    private void ResetPasswordChangeForm()
    {
        IsPasswordChangeVisible = false;
        CurrentPassword = "";
        NewPassword = "";
        ConfirmPassword = "";
        PasswordChangeMessage = "";
        IsPasswordChangeError = false;
        PasswordChangeMessageColor = "#00a872";
    }

    /// <summary>
    /// 헤더 알림 뱃지용 읽지않은 알림 개수 로드
    /// </summary>
    public async Task LoadUnreadNotificationCountAsync()
    {
        try
        {
            UnreadNotificationCount = await NotificationService.GetUnreadCountAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadUnreadNotificationCountAsync error: {ex.Message}");
        }
    }

    // 로그아웃 완료 이벤트
    public event Action? LogoutCompleted;

    [RelayCommand]
    private async Task Logout()
    {
        IsDrawerOpen = false;

        try
        {
            // 1. 정리 작업 (타이머, Realtime 구독 해제)
            try
            {
                AppRestartService.CleanupBeforeLogout?.Invoke();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] Cleanup error (continuing): {ex.Message}");
            }

            // 2. 조직 데이터 공유 캐시 초기화
            QueryHelper.ClearOrgCache();

            // 3. 세션 저장소 클리어
            SessionStorageService.ClearSession();

            // 4. AuthService 로그아웃
            await AuthService.LogoutAsync();

            // 5. 로그아웃 완료 이벤트 발생 (UI에서 로그인 화면으로 전환)
            LogoutCompleted?.Invoke();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Logout error: {ex.Message}");
        }
    }

    // === 업체 등록 화면 ===
    [RelayCommand]
    private void OpenCompanyRegister()
    {
        IsCompanyRegisterOpen = true;
    }

    [RelayCommand]
    private void CloseCompanyRegister()
    {
        IsCompanyRegisterOpen = false;
    }

    // === 일정 생성 화면 ===
    [RelayCommand]
    private void OpenScheduleCreate()
    {
        IsScheduleCreateOpen = true;
    }

    [RelayCommand]
    private void CloseScheduleCreate()
    {
        IsScheduleCreateOpen = false;
    }

    // === 일정 상세 화면 열기 (역할/상태에 따라 분기) ===

    /// <summary>
    /// 일정 상세 화면 열기 전 이벤트 (View에서 구독하여 실제 화면 전환 처리)
    /// </summary>
    public event Action<ScheduleNavigationArgs>? ScheduleNavigationRequested;

    /// <summary>
    /// 일정 상세 화면 열기 요청 (MVVM 패턴: View에서 직접 DB 호출하지 않고 ViewModel에서 처리)
    /// 백그라운드에서 일정 상태를 조회하고, 역할/상태에 따라 적절한 화면으로 분기
    /// </summary>
    public void RequestOpenScheduleDetail(Guid scheduleId, string mode = "view")
    {
        // 백그라운드에서 일정 조회 및 화면 분기 처리
        _ = Task.Run(async () =>
        {
            try
            {
                var currentUser = AuthService.CurrentUser;
                if (currentUser == null)
                    return;

                var client = SupabaseService.Client;
                if (client == null)
                    return;

                // 백그라운드에서 일정 조회
                var schedule = await client.From<Models.Schedule>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, scheduleId.ToString())
                    .Single();

                if (schedule == null)
                    return;

                // 화면 분기 로직 (비즈니스 로직)
                ScheduleNavigationType navigationType;

                // 예약됨/확정됨 상태 -> 확정 화면
                if (schedule.Status == "reserved" || schedule.Status == "confirmed")
                {
                    navigationType = ScheduleNavigationType.Confirm;
                }
                // 중간관리자/최종관리자/여단총괄은 모든 상태의 일정을 확정 화면(뷰어 모드)으로 조회
                else if (currentUser.Role == "middle_local" || currentUser.Role == "middle_military" ||
                         currentUser.Role == "viewer_military" ||
                         currentUser.Role == "super_admin_mois" || currentUser.Role == "super_admin_army")
                {
                    navigationType = ScheduleNavigationType.Confirm;
                }
                // 역할에 따라 분기
                else if (currentUser.Role == "user_local")
                {
                    // 지자체담당자 -> 일정 입력 화면
                    navigationType = ScheduleNavigationType.Input;
                }
                else if (currentUser.Role == "user_military")
                {
                    // 대대담당자 -> 입력됨 상태일 때만 예약 화면
                    if (schedule.Status == "inputted")
                    {
                        navigationType = ScheduleNavigationType.Reserve;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }

                // UI 스레드에서 화면 전환 이벤트 발생
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ScheduleNavigationRequested?.Invoke(new ScheduleNavigationArgs
                    {
                        ScheduleId = scheduleId,
                        NavigationType = navigationType,
                        Mode = mode
                    });
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainViewModel] RequestOpenScheduleDetail error: {ex.Message}");
            }
        });
    }

    // === 일정 입력 화면 (지자체담당자용) ===
    public void OpenScheduleInput(Guid scheduleId)
    {
        _scheduleInputId = scheduleId;
        IsScheduleInputOpen = true;
    }

    [RelayCommand]
    private void CloseScheduleInput()
    {
        IsScheduleInputOpen = false;
    }

    // === 일정 예약 화면 (대대담당자용) ===
    public void OpenScheduleReserve(Guid scheduleId)
    {
        _scheduleReserveId = scheduleId;
        IsScheduleReserveOpen = true;
    }

    [RelayCommand]
    private void CloseScheduleReserve()
    {
        IsScheduleReserveOpen = false;
    }

    // === 일정 확정 화면 (양측 공통) ===
    public void OpenScheduleConfirm(Guid scheduleId)
    {
        _scheduleConfirmId = scheduleId;
        IsScheduleConfirmOpen = true;
    }

    [RelayCommand]
    private void CloseScheduleConfirm()
    {
        IsScheduleConfirmOpen = false;
    }

    // === 채팅방 화면 ===
    public void OpenChatRoom()
    {
        IsChatRoomOpen = true;
    }

    [RelayCommand]
    private void CloseChatRoom()
    {
        IsChatRoomOpen = false;
    }
}
