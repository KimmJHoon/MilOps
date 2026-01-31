using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using System;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

/// <summary>
/// 일정 상태 변경 이벤트 인자
/// </summary>
public class ScheduleStatusChangedEventArgs : EventArgs
{
    public Guid ScheduleId { get; }
    public string NewStatus { get; }
    public int NewStatusOrder { get; }

    public ScheduleStatusChangedEventArgs(Guid scheduleId, string newStatus, int newStatusOrder)
    {
        ScheduleId = scheduleId;
        NewStatus = newStatus;
        NewStatusOrder = newStatusOrder;
    }
}

/// <summary>
/// 일정 확정 ViewModel
/// - 예약된 일정에 대해 지자체담당자와 대대담당자 양측이 확정
/// - 양측 모두 확정 시 status가 "confirmed"로 변경
/// </summary>
public partial class ScheduleConfirmViewModel : ViewModelBase
{
    private Schedule? _schedule;
    private Guid _scheduleId;
    private string _currentUserRole = "";

    // 로딩 상태
    [ObservableProperty]
    private bool _isLoading = false;

    // 현재 사용자 정보
    [ObservableProperty]
    private string _currentUserDisplay = "";

    // 상태 표시
    [ObservableProperty]
    private string _statusDisplay = "예약됨 (확정 대기)";

    // 예약된 일정 정보
    [ObservableProperty]
    private string _reservedDateDisplay = "";

    [ObservableProperty]
    private string _reservedTimeDisplay = "";

    // 업체 정보
    [ObservableProperty]
    private string _regionName = "";

    [ObservableProperty]
    private string _districtName = "";

    [ObservableProperty]
    private string _companyName = "";

    [ObservableProperty]
    private string _companyAddress = "";

    [ObservableProperty]
    private string _companyProducts = "";

    // 지자체담당자 정보
    [ObservableProperty]
    private string _localUserName = "";

    [ObservableProperty]
    private string _localUserRank = "";

    [ObservableProperty]
    private string _localUserPhone = "";

    [ObservableProperty]
    private bool _localConfirmed = false;

    [ObservableProperty]
    private string _localConfirmedText = "미확정";

    [ObservableProperty]
    private IBrush _localConfirmedBg = new SolidColorBrush(Color.Parse("#333333"));

    [ObservableProperty]
    private IBrush _localConfirmedFg = new SolidColorBrush(Color.Parse("#888888"));

    // 대대담당자 정보
    [ObservableProperty]
    private string _militaryUserName = "";

    [ObservableProperty]
    private string _militaryUserRank = "";

    [ObservableProperty]
    private string _militaryUserPhone = "";

    [ObservableProperty]
    private bool _militaryConfirmed = false;

    [ObservableProperty]
    private string _militaryConfirmedText = "미확정";

    [ObservableProperty]
    private IBrush _militaryConfirmedBg = new SolidColorBrush(Color.Parse("#333333"));

    [ObservableProperty]
    private IBrush _militaryConfirmedFg = new SolidColorBrush(Color.Parse("#888888"));

    // 메모
    [ObservableProperty]
    private string _memo = "";

    [ObservableProperty]
    private bool _hasMemo = false;

    // 메시지
    [ObservableProperty]
    private string _waitingMessage = "";

    [ObservableProperty]
    private bool _showWaitingMessage = false;

    [ObservableProperty]
    private string _confirmRequestMessage = "";

    [ObservableProperty]
    private bool _showConfirmRequestMessage = false;

    // 에러/성공 메시지
    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _successMessage = "";

    // 확정 버튼 상태
    [ObservableProperty]
    private bool _canConfirm = false;

    [ObservableProperty]
    private bool _alreadyConfirmedByMe = false;

    // 확정 확인 모달
    [ObservableProperty]
    private bool _showConfirmModal = false;

    [ObservableProperty]
    private string _modalDateDisplay = "";

    [ObservableProperty]
    private string _modalTimeDisplay = "";

    [ObservableProperty]
    private string _modalCompanyDisplay = "";

    // 이벤트
    public event EventHandler? CloseRequested;
    public event EventHandler<ScheduleStatusChangedEventArgs>? ScheduleStatusChanged;

    public ScheduleConfirmViewModel()
    {
    }

    /// <summary>
    /// 일정 ID로 초기화
    /// </summary>
    public async Task InitializeAsync(Guid scheduleId)
    {
        _scheduleId = scheduleId;

        var currentUser = AuthService.CurrentUser;
        if (currentUser == null) return;

        _currentUserRole = currentUser.Role ?? "";

        await LoadScheduleAsync();
    }

    private async Task LoadScheduleAsync()
    {
        IsLoading = true;
        ErrorMessage = "";

        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            // 일정 로드
            var scheduleResponse = await client.From<Schedule>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
                .Single();

            _schedule = scheduleResponse;
            if (_schedule == null)
            {
                ErrorMessage = "일정을 찾을 수 없습니다.";
                return;
            }

            // 관련 데이터 로드
            var companyTask = client.From<Company>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _schedule.CompanyId.ToString())
                .Single();

            var localUserTask = client.From<User>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _schedule.LocalUserId.ToString())
                .Single();

            var militaryUserTask = client.From<User>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _schedule.MilitaryUserId.ToString())
                .Single();

            await Task.WhenAll(companyTask, localUserTask, militaryUserTask);

            var company = companyTask.Result;
            var localUser = localUserTask.Result;
            var militaryUser = militaryUserTask.Result;

            // 업체 정보 설정
            if (company != null)
            {
                CompanyName = company.Name;
                CompanyAddress = company.Address ?? "";
                CompanyProducts = company.Products ?? "";

                // District 로드
                if (company.DistrictId != Guid.Empty)
                {
                    var district = await client.From<District>()
                        .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, company.DistrictId.ToString())
                        .Single();

                    if (district != null)
                    {
                        DistrictName = district.Name;

                        // Region 로드
                        var region = await client.From<Region>()
                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, district.RegionId.ToString())
                            .Single();

                        if (region != null)
                        {
                            RegionName = region.Name;
                        }
                    }
                }
            }

            // 지자체담당자 정보
            if (localUser != null)
            {
                LocalUserName = localUser.Name;
                LocalUserRank = localUser.Position ?? "";
                LocalUserPhone = localUser.Phone ?? "";
            }

            // 대대담당자 정보
            if (militaryUser != null)
            {
                MilitaryUserName = militaryUser.Name;
                MilitaryUserRank = militaryUser.MilitaryRank ?? "";
                MilitaryUserPhone = militaryUser.Phone ?? "";
            }

            // 현재 사용자 표시 설정
            await SetCurrentUserDisplayAsync();

            // 예약된 일정 정보 설정
            if (_schedule.ReservedDate.HasValue)
            {
                ReservedDateDisplay = _schedule.ReservedDate.Value.ToString("M월 d일");
            }
            if (_schedule.ReservedStartTime.HasValue && _schedule.ReservedEndTime.HasValue)
            {
                ReservedTimeDisplay = $"{_schedule.ReservedStartTime.Value:hh\\:mm}-{_schedule.ReservedEndTime.Value:hh\\:mm}";
            }

            // 메모
            Memo = _schedule.Memo ?? "";
            HasMemo = !string.IsNullOrEmpty(Memo);

            // 확정 상태 업데이트
            UpdateConfirmationStatus();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] LoadScheduleAsync error: {ex.Message}");
            ErrorMessage = "데이터 로드 중 오류가 발생했습니다.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SetCurrentUserDisplayAsync()
    {
        var currentUser = AuthService.CurrentUser;
        if (currentUser == null) return;

        var client = SupabaseService.Client;
        if (client == null) return;

        if (_currentUserRole == "user_local")
        {
            // 지자체담당자
            string districtDisplay = "";
            if (currentUser.DistrictId.HasValue)
            {
                var district = await client.From<District>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.DistrictId.Value.ToString())
                    .Single();

                if (district != null)
                {
                    var region = await client.From<Region>()
                        .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, district.RegionId.ToString())
                        .Single();

                    districtDisplay = region != null
                        ? $"{region.Name} {district.Name}"
                        : district.Name;
                }
            }
            CurrentUserDisplay = $"{currentUser.FullDisplayName} ({districtDisplay} 지자체담당자)";
        }
        else if (_currentUserRole == "user_military")
        {
            // 대대담당자
            string battalionDisplay = "";
            if (currentUser.BattalionId.HasValue)
            {
                var battalion = await client.From<Battalion>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.BattalionId.Value.ToString())
                    .Single();

                if (battalion != null)
                {
                    var division = await client.From<Division>()
                        .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, battalion.DivisionId.ToString())
                        .Single();

                    battalionDisplay = division != null
                        ? $"{division.Name} {battalion.Name}"
                        : battalion.Name;
                }
            }
            CurrentUserDisplay = $"{currentUser.FullDisplayName} ({battalionDisplay} 대대담당자)";
        }
    }

    private void UpdateConfirmationStatus()
    {
        if (_schedule == null) return;

        System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] UpdateConfirmationStatus - Role: {_currentUserRole}");
        System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] UpdateConfirmationStatus - Schedule.LocalConfirmed: {_schedule.LocalConfirmed}, Schedule.MilitaryConfirmed: {_schedule.MilitaryConfirmed}");
        System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] UpdateConfirmationStatus - Schedule.Status: {_schedule.Status}");

        // 지자체담당자 확정 상태
        LocalConfirmed = _schedule.LocalConfirmed;
        if (LocalConfirmed)
        {
            LocalConfirmedText = "확정함";
            LocalConfirmedBg = new SolidColorBrush(Color.Parse("#2A5F2A"));
            LocalConfirmedFg = new SolidColorBrush(Color.Parse("#88FF88"));
        }
        else
        {
            LocalConfirmedText = "미확정";
            LocalConfirmedBg = new SolidColorBrush(Color.Parse("#333333"));
            LocalConfirmedFg = new SolidColorBrush(Color.Parse("#888888"));
        }

        // 대대담당자 확정 상태
        MilitaryConfirmed = _schedule.MilitaryConfirmed;
        if (MilitaryConfirmed)
        {
            MilitaryConfirmedText = "확정함";
            MilitaryConfirmedBg = new SolidColorBrush(Color.Parse("#2A5F2A"));
            MilitaryConfirmedFg = new SolidColorBrush(Color.Parse("#88FF88"));
        }
        else
        {
            MilitaryConfirmedText = "미확정";
            MilitaryConfirmedBg = new SolidColorBrush(Color.Parse("#333333"));
            MilitaryConfirmedFg = new SolidColorBrush(Color.Parse("#888888"));
        }

        // 상태 표시
        if (_schedule.Status == "confirmed")
        {
            StatusDisplay = "확정됨";
        }
        else
        {
            StatusDisplay = "예약됨 (확정 대기)";
        }

        // 현재 사용자의 확정 여부
        bool isLocalUser = _currentUserRole == "user_local";
        bool isMilitaryUser = _currentUserRole == "user_military";

        AlreadyConfirmedByMe = (isLocalUser && LocalConfirmed) || (isMilitaryUser && MilitaryConfirmed);

        // 확정 버튼 활성화 (예약 상태이고 아직 확정하지 않은 경우)
        CanConfirm = _schedule.Status == "reserved" && !AlreadyConfirmedByMe;

        System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] UpdateConfirmationStatus - isLocalUser: {isLocalUser}, isMilitaryUser: {isMilitaryUser}");
        System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] UpdateConfirmationStatus - AlreadyConfirmedByMe: {AlreadyConfirmedByMe}, CanConfirm: {CanConfirm}");

        // 메시지 설정
        ShowWaitingMessage = false;
        ShowConfirmRequestMessage = false;
        WaitingMessage = "";
        ConfirmRequestMessage = "";

        if (_schedule.Status == "reserved")
        {
            if (AlreadyConfirmedByMe)
            {
                // 내가 확정했고 상대방이 아직 안 한 경우
                bool otherConfirmed = isLocalUser ? MilitaryConfirmed : LocalConfirmed;
                if (!otherConfirmed)
                {
                    ShowWaitingMessage = true;
                    WaitingMessage = isLocalUser
                        ? "대대담당자의 확정을 기다리고 있습니다."
                        : "지자체담당자의 확정을 기다리고 있습니다.";
                }
            }
            else
            {
                // 내가 아직 확정 안 했는데 상대방이 확정한 경우
                bool otherConfirmed = isLocalUser ? MilitaryConfirmed : LocalConfirmed;
                if (otherConfirmed)
                {
                    ShowConfirmRequestMessage = true;
                    ConfirmRequestMessage = isLocalUser
                        ? "대대담당자가 이미 확정했습니다.\n확정해주세요!"
                        : "지자체담당자가 이미 확정했습니다.\n확정해주세요!";
                }
            }
        }
    }

    /// <summary>
    /// 확정 버튼 클릭 - 모달 표시
    /// </summary>
    [RelayCommand]
    private void ShowConfirmDialog()
    {
        if (!CanConfirm) return;

        // 모달에 표시할 정보 설정
        ModalDateDisplay = ReservedDateDisplay;
        ModalTimeDisplay = ReservedTimeDisplay;
        ModalCompanyDisplay = CompanyName;
        ShowConfirmModal = true;
    }

    /// <summary>
    /// 모달 취소
    /// </summary>
    [RelayCommand]
    private void CancelConfirmDialog()
    {
        ShowConfirmModal = false;
    }

    /// <summary>
    /// 확정 (모달에서 최종 확정)
    /// </summary>
    [RelayCommand]
    private async Task ConfirmAsync()
    {
        System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] ConfirmAsync called - CanConfirm: {CanConfirm}, Role: {_currentUserRole}");

        // 모달 닫기
        ShowConfirmModal = false;

        if (!CanConfirm)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] ConfirmAsync - CanConfirm is false, returning");
            return;
        }

        IsLoading = true;
        ErrorMessage = "";
        SuccessMessage = "";

        try
        {
            var client = SupabaseService.Client;
            if (client == null || _schedule == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] ConfirmAsync - client or schedule is null");
                return;
            }

            bool isLocalUser = _currentUserRole == "user_local";
            bool isMilitaryUser = _currentUserRole == "user_military";
            var now = DateTime.UtcNow;

            System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] ConfirmAsync - isLocalUser: {isLocalUser}, isMilitaryUser: {isMilitaryUser}");
            System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] ConfirmAsync - Before: LocalConfirmed={_schedule.LocalConfirmed}, MilitaryConfirmed={_schedule.MilitaryConfirmed}");

            if (isLocalUser)
            {
                // 지자체담당자 확정
                System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] Updating LocalConfirmed to true");
#pragma warning disable CS8603 // Possible null reference return
                await client.From<Schedule>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
                    .Set(s => s.LocalConfirmed, true)
                    .Set(s => s.LocalConfirmedAt, now)
                    .Update();
#pragma warning restore CS8603

                _schedule.LocalConfirmed = true;
                _schedule.LocalConfirmedAt = now;
            }
            else if (isMilitaryUser)
            {
                // 대대담당자 확정
                System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] Updating MilitaryConfirmed to true");
#pragma warning disable CS8603 // Possible null reference return
                await client.From<Schedule>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
                    .Set(s => s.MilitaryConfirmed, true)
                    .Set(s => s.MilitaryConfirmedAt, now)
                    .Update();
#pragma warning restore CS8603

                _schedule.MilitaryConfirmed = true;
                _schedule.MilitaryConfirmedAt = now;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] Unknown role: {_currentUserRole}");
                ErrorMessage = "알 수 없는 사용자 역할입니다.";
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] ConfirmAsync - After: LocalConfirmed={_schedule.LocalConfirmed}, MilitaryConfirmed={_schedule.MilitaryConfirmed}");

            // 양측 모두 확정 시 상태 변경
            bool bothConfirmed = _schedule.LocalConfirmed && _schedule.MilitaryConfirmed;
            System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] bothConfirmed: {bothConfirmed}");

            if (bothConfirmed)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] Updating status to confirmed");
#pragma warning disable CS8603 // Possible null reference return
                await client.From<Schedule>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
                    .Set(s => s.Status, "confirmed")
                    .Set(s => s.StatusOrder, 4)
                    .Set(s => s.ConfirmedAt, now)
                    .Update();
#pragma warning restore CS8603

                _schedule.Status = "confirmed";
                _schedule.StatusOrder = 4;
                _schedule.ConfirmedAt = now;

                SuccessMessage = "일정이 확정되었습니다!";
            }
            else
            {
                SuccessMessage = "확정 완료! 상대방의 확정을 기다리고 있습니다.";
            }

            // UI 업데이트
            UpdateConfirmationStatus();

            System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] Confirmed by {_currentUserRole}, bothConfirmed: {bothConfirmed}");

            // 상태 변경 이벤트 발생 (양측 확정 시에만 상태가 변경됨)
            if (bothConfirmed)
            {
                ScheduleStatusChanged?.Invoke(this, new ScheduleStatusChangedEventArgs(_scheduleId, "confirmed", 4));
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] ConfirmAsync error: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ScheduleConfirmVM] ConfirmAsync stack: {ex.StackTrace}");
            ErrorMessage = "확정 처리 중 오류가 발생했습니다.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 닫기
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
