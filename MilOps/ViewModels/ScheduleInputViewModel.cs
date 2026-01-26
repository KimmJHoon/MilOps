using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

/// <summary>
/// 일정 입력 ViewModel (지자체담당자/user_local 전용)
/// - 가능 일자/시간 입력
/// </summary>
public partial class ScheduleInputViewModel : ViewModelBase
{
    // 현재 일정
    private Schedule? _schedule;
    private Guid _scheduleId;

    // 로딩 상태
    [ObservableProperty]
    private bool _isLoading = false;

    // 현재 사용자 정보
    [ObservableProperty]
    private string _currentUserDisplay = "";

    [ObservableProperty]
    private string _currentUserRole = "";

    // 일정 정보
    [ObservableProperty]
    private string _regionName = "";

    [ObservableProperty]
    private string _districtName = "";

    [ObservableProperty]
    private string _companyName = "";

    [ObservableProperty]
    private string _companyProducts = "";

    [ObservableProperty]
    private string _battalionName = "";

    // 대대담당자 정보
    [ObservableProperty]
    private string _militaryUserName = "";

    [ObservableProperty]
    private string _militaryUserPhone = "";

    // === 가능 일자 입력 ===
    [ObservableProperty]
    private DateTime? _availableStartDate;

    [ObservableProperty]
    private DateTime? _availableEndDate;

    // 종료일 최소값 (시작일 기준)
    [ObservableProperty]
    private DateTime _minEndDate = DateTime.Now;

    // 날짜 유효성 검사
    [ObservableProperty]
    private bool _hasDateValidationError = false;

    [ObservableProperty]
    private string _dateValidationMessage = "";

    // 가능 시간 슬롯 (선택 가능한 시간대)
    [ObservableProperty]
    private ObservableCollection<TimeSlotItem> _timeSlots = new();

    // 선택된 시간 표시
    [ObservableProperty]
    private string _selectedTimesDisplay = "";

    // 메모
    [ObservableProperty]
    private string _memo = "";

    // 에러/성공 메시지
    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _successMessage = "";

    // 저장 버튼 활성화
    [ObservableProperty]
    private bool _canSave = false;

    // 권한 체크
    [ObservableProperty]
    private bool _canInput = false;

    // 이벤트
    public event EventHandler? CloseRequested;
    public event EventHandler<ScheduleStatusChangedEventArgs>? ScheduleStatusChanged;

    public ScheduleInputViewModel()
    {
        InitializeTimeSlots();
        // 기본 시작일을 내일로 설정
        AvailableStartDate = DateTime.Now.Date.AddDays(1);
        AvailableEndDate = DateTime.Now.Date.AddDays(7);
        MinEndDate = AvailableStartDate ?? DateTime.Now.Date;
    }

    /// <summary>
    /// 시간 슬롯 초기화 (09:00~17:00, 1시간 단위)
    /// </summary>
    private void InitializeTimeSlots()
    {
        TimeSlots.Clear();
        for (int hour = 9; hour < 17; hour++)
        {
            TimeSlots.Add(new TimeSlotItem
            {
                StartTime = new TimeSpan(hour, 0, 0),
                EndTime = new TimeSpan(hour + 1, 0, 0),
                IsSelected = false
            });
        }
    }

    /// <summary>
    /// 일정 ID로 초기화
    /// </summary>
    public async Task InitializeAsync(Guid scheduleId, string mode)
    {
        _scheduleId = scheduleId;

        var currentUser = AuthService.CurrentUser;
        if (currentUser == null) return;

        CurrentUserRole = currentUser.Role;

        // 권한 설정 (user_local만 입력 가능)
        CanInput = currentUser.Role == "user_local";

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

            // 대대담당자 정보
            if (militaryUser != null)
            {
                MilitaryUserName = militaryUser.FullDisplayName;
                MilitaryUserPhone = militaryUser.Phone ?? "";

                // Battalion 로드
                if (militaryUser.BattalionId.HasValue)
                {
                    var battalion = await client.From<Battalion>()
                        .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, militaryUser.BattalionId.Value.ToString())
                        .Single();

                    if (battalion != null)
                    {
                        // Division 로드
                        var division = await client.From<Division>()
                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, battalion.DivisionId.ToString())
                            .Single();

                        BattalionName = division != null
                            ? $"{division.Name} {battalion.Name}"
                            : battalion.Name;
                    }
                }
            }

            // 현재 사용자 표시 설정
            var currentUser = AuthService.CurrentUser;
            if (currentUser != null && localUser != null)
            {
                CurrentUserDisplay = $"{localUser.FullDisplayName} ({RegionName} {DistrictName} 지자체담당자)";
            }

            // 기존 메모 로드
            Memo = _schedule.Memo ?? "";

            // 기존 가능 일자 로드 (지자체담당자가 이미 입력한 경우)
            if (_schedule.AvailableStart.HasValue)
            {
                AvailableStartDate = _schedule.AvailableStart.Value;
            }
            if (_schedule.AvailableEnd.HasValue)
            {
                AvailableEndDate = _schedule.AvailableEnd.Value;
            }

            // 기존 선택된 시간 슬롯 로드
            await LoadExistingTimeSlotsAsync();

            ValidateCanSave();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleInputVM] LoadScheduleAsync error: {ex.Message}");
            ErrorMessage = "데이터 로드 중 오류가 발생했습니다.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 기존 선택된 시간 슬롯 로드
    /// </summary>
    private async Task LoadExistingTimeSlotsAsync()
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var response = await client.From<ScheduleAvailableTime>()
                .Filter("schedule_id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
                .Get();

            var existingTimes = response.Models;
            if (existingTimes.Count == 0) return;

            // 고유한 시간대 추출
            var uniqueTimeSlots = existingTimes
                .Select(t => new { t.StartTime, t.EndTime })
                .Distinct()
                .ToList();

            // 기존 선택된 시간 슬롯 표시
            foreach (var slot in TimeSlots)
            {
                slot.IsSelected = uniqueTimeSlots.Any(t =>
                    t.StartTime == slot.StartTime && t.EndTime == slot.EndTime);
            }

            UpdateSelectedTimesDisplay();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleInputVM] LoadExistingTimeSlotsAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 시간 슬롯 토글
    /// </summary>
    [RelayCommand]
    private void ToggleTimeSlot(TimeSlotItem slot)
    {
        if (slot == null) return;

        slot.IsSelected = !slot.IsSelected;
        UpdateSelectedTimesDisplay();
        ValidateCanSave();
    }

    /// <summary>
    /// 선택된 시간 표시 업데이트
    /// </summary>
    private void UpdateSelectedTimesDisplay()
    {
        var selected = TimeSlots.Where(t => t.IsSelected).ToList();
        if (selected.Count == 0)
        {
            SelectedTimesDisplay = "";
        }
        else
        {
            SelectedTimesDisplay = "선택된 시간: " + string.Join(", ", selected.Select(t => t.TimeRangeDisplay));
        }
    }

    /// <summary>
    /// 날짜 유효성 검사
    /// </summary>
    private void ValidateDates()
    {
        HasDateValidationError = false;
        DateValidationMessage = "";

        if (AvailableStartDate == null || AvailableEndDate == null)
        {
            return;
        }

        if (AvailableEndDate < AvailableStartDate)
        {
            HasDateValidationError = true;
            DateValidationMessage = "종료일은 시작일보다 이후여야 합니다.";
        }
    }

    /// <summary>
    /// 저장 가능 여부 검증
    /// </summary>
    private void ValidateCanSave()
    {
        ValidateDates();

        // 가능 일자와 시간이 선택되어야 함
        var hasSelectedTimes = TimeSlots.Any(t => t.IsSelected);
        var hasValidDates = AvailableStartDate != null &&
                            AvailableEndDate != null &&
                            !HasDateValidationError;

        CanSave = hasSelectedTimes && hasValidDates && CanInput;
    }

    /// <summary>
    /// 저장
    /// </summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!CanSave) return;

        IsLoading = true;
        ErrorMessage = "";
        SuccessMessage = "";

        try
        {
            var client = SupabaseService.Client;
            if (client == null || _schedule == null) return;

            await SaveInputAsync(client);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleInputVM] SaveAsync error: {ex.Message}");
            ErrorMessage = "저장 중 오류가 발생했습니다.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 가능 일정 입력 저장
    /// </summary>
    private async Task SaveInputAsync(Supabase.Client client)
    {
        var selectedTimes = TimeSlots.Where(t => t.IsSelected).ToList();
        if (selectedTimes.Count == 0)
        {
            ErrorMessage = "가능한 시간을 선택해주세요.";
            return;
        }

        if (AvailableStartDate == null || AvailableEndDate == null)
        {
            ErrorMessage = "가능한 일자를 선택해주세요.";
            return;
        }

        // 1. 일정 업데이트
#pragma warning disable CS8603 // Possible null reference return
        await client.From<Schedule>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
            .Set(s => s.AvailableStart, AvailableStartDate.Value)
            .Set(s => s.AvailableEnd, AvailableEndDate.Value)
            .Set(s => s.Memo, string.IsNullOrWhiteSpace(Memo) ? null : Memo)
            .Set(s => s.Status, "inputted")
            .Set(s => s.StatusOrder, 2)
            .Update();
#pragma warning restore CS8603

        // 2. 기존 가능 시간 삭제
        await client.From<ScheduleAvailableTime>()
            .Filter("schedule_id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
            .Delete();

        // 3. 새 가능 시간 생성 (선택된 날짜 범위 내 모든 날짜에 대해)
        var startDate = AvailableStartDate.Value.Date;
        var endDate = AvailableEndDate.Value.Date;

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            foreach (var timeSlot in selectedTimes)
            {
                var availableTime = new ScheduleAvailableTime
                {
                    Id = Guid.NewGuid(),
                    ScheduleId = _scheduleId,
                    AvailableDate = date,
                    StartTime = timeSlot.StartTime,
                    EndTime = timeSlot.EndTime,
                    IsSelected = false
                };

                await client.From<ScheduleAvailableTime>().Insert(availableTime);
            }
        }

        SuccessMessage = "가능 일정이 저장되었습니다.";
        System.Diagnostics.Debug.WriteLine($"[ScheduleInputVM] Input saved: {selectedTimes.Count} time slots for {(endDate - startDate).Days + 1} days");

        // 상태 변경 이벤트 발생 (inputted, statusOrder=2)
        ScheduleStatusChanged?.Invoke(this, new ScheduleStatusChangedEventArgs(_scheduleId, "inputted", 2));
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 닫기
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnAvailableStartDateChanged(DateTime? value)
    {
        // 시작일이 변경되면 종료일 최소값도 변경
        if (value.HasValue)
        {
            MinEndDate = value.Value;

            // 종료일이 시작일보다 이전이면 시작일과 같게 설정
            if (AvailableEndDate.HasValue && AvailableEndDate.Value < value.Value)
            {
                AvailableEndDate = value.Value;
            }
        }
        ValidateCanSave();
    }

    partial void OnAvailableEndDateChanged(DateTime? value)
    {
        ValidateCanSave();
    }
}

/// <summary>
/// 시간 슬롯 아이템
/// </summary>
public partial class TimeSlotItem : ObservableObject
{
    public Guid Id { get; set; }

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    public string TimeRangeDisplay => $"{StartTime:hh\\:mm}-{EndTime:hh\\:mm}";
}

/// <summary>
/// 날짜 아이템 (대대담당자 예약용 - 추후 ScheduleReserveViewModel에서 사용)
/// </summary>
public partial class DateItem : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayDisplay { get; set; } = "";
    public List<TimeSlotItem> TimeSlots { get; set; } = new();

    [ObservableProperty]
    private bool _isSelected;
}
