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
/// 일정 입력/예약 ViewModel
/// - 지자체담당자(user_local): 가능 일자/시간 입력
/// - 대대담당자(user_military): 예약 일자/시간 선택
/// </summary>
public partial class ScheduleInputViewModel : ViewModelBase
{
    // 현재 일정
    private Schedule? _schedule;
    private Guid _scheduleId;

    // 현재 모드 (input: 지자체담당자 입력, reserve: 대대담당자 예약)
    [ObservableProperty]
    private string _currentMode = "input";

    // 탭 선택 상태
    [ObservableProperty]
    private bool _isInputTabSelected = true;

    [ObservableProperty]
    private bool _isReserveTabSelected = false;

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
    private string _companyAddress = "";

    [ObservableProperty]
    private string _companyProducts = "";

    [ObservableProperty]
    private string _battalionName = "";

    // 지자체담당자 정보
    [ObservableProperty]
    private string _localUserName = "";

    [ObservableProperty]
    private string _localUserPhone = "";

    // 대대담당자 정보
    [ObservableProperty]
    private string _militaryUserName = "";

    [ObservableProperty]
    private string _militaryUserPhone = "";

    // === 지자체담당자용: 가능 일자 입력 ===
    [ObservableProperty]
    private DateTimeOffset _availableStartDate = DateTimeOffset.Now.AddDays(1);

    [ObservableProperty]
    private DateTimeOffset _availableEndDate = DateTimeOffset.Now.AddDays(7);

    // 가능 시간 슬롯 (선택 가능한 시간대)
    [ObservableProperty]
    private ObservableCollection<TimeSlotItem> _timeSlots = new();

    // 선택된 시간 표시
    [ObservableProperty]
    private string _selectedTimesDisplay = "";

    // 메모
    [ObservableProperty]
    private string _memo = "";

    // === 대대담당자용: 예약 일자/시간 선택 ===
    [ObservableProperty]
    private ObservableCollection<DateItem> _availableDates = new();

    [ObservableProperty]
    private DateItem? _selectedDate;

    [ObservableProperty]
    private ObservableCollection<TimeSlotItem> _availableTimeSlotsForDate = new();

    [ObservableProperty]
    private TimeSlotItem? _selectedTimeSlot;

    // 예약 정보 표시
    [ObservableProperty]
    private string _reservationDisplay = "";

    [ObservableProperty]
    private bool _showReservationDisplay = false;

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

    [ObservableProperty]
    private bool _canReserve = false;

    // 이벤트
    public event EventHandler? CloseRequested;
    public event EventHandler? ScheduleUpdated;

    public ScheduleInputViewModel()
    {
        InitializeTimeSlots();
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
        CurrentMode = mode;
        IsInputTabSelected = mode == "input";
        IsReserveTabSelected = mode == "reserve";

        var currentUser = AuthService.CurrentUser;
        if (currentUser == null) return;

        CurrentUserRole = currentUser.Role;

        // 권한 설정
        CanInput = currentUser.Role == "user_local";
        CanReserve = currentUser.Role == "user_military";

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
                CompanyAddress = company.Address;
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
                LocalUserName = localUser.FullDisplayName;
                LocalUserPhone = localUser.Phone ?? "";
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
            if (currentUser != null)
            {
                if (currentUser.Role == "user_local")
                {
                    CurrentUserDisplay = $"{localUser?.FullDisplayName ?? ""} ({RegionName} {DistrictName} 지자체담당자)";
                }
                else if (currentUser.Role == "user_military")
                {
                    CurrentUserDisplay = $"{militaryUser?.FullDisplayName ?? ""} ({BattalionName} 대대담당자)";
                }
            }

            // 기존 입력된 가능 시간 로드 (대대담당자 예약 모드에서)
            if (CurrentMode == "reserve" && _schedule.Status == "inputted")
            {
                await LoadAvailableTimesAsync();
            }

            // 기존 메모 로드
            Memo = _schedule.Memo ?? "";

            // 기존 가능 일자 로드 (지자체담당자가 이미 입력한 경우)
            if (_schedule.AvailableStart.HasValue)
            {
                AvailableStartDate = new DateTimeOffset(_schedule.AvailableStart.Value);
            }
            if (_schedule.AvailableEnd.HasValue)
            {
                AvailableEndDate = new DateTimeOffset(_schedule.AvailableEnd.Value);
            }

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
    /// 가능 시간 로드 (대대담당자 예약 모드)
    /// </summary>
    private async Task LoadAvailableTimesAsync()
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var response = await client.From<ScheduleAvailableTime>()
                .Filter("schedule_id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
                .Order("available_date", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var times = response.Models;

            // 날짜별로 그룹화
            var dateGroups = times
                .GroupBy(t => t.AvailableDate.Date)
                .OrderBy(g => g.Key)
                .ToList();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AvailableDates.Clear();
                foreach (var group in dateGroups)
                {
                    AvailableDates.Add(new DateItem
                    {
                        Date = group.Key,
                        DayDisplay = group.Key.Day.ToString() + "일",
                        TimeSlots = group.Select(t => new TimeSlotItem
                        {
                            Id = t.Id,
                            StartTime = t.StartTime,
                            EndTime = t.EndTime,
                            IsSelected = false
                        }).ToList()
                    });
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleInputVM] LoadAvailableTimesAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 시간 슬롯 토글 (지자체담당자)
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
    /// 날짜 선택 (대대담당자)
    /// </summary>
    [RelayCommand]
    private void SelectDate(DateItem dateItem)
    {
        if (dateItem == null) return;

        // 이전 선택 해제
        foreach (var d in AvailableDates)
        {
            d.IsSelected = false;
        }

        dateItem.IsSelected = true;
        SelectedDate = dateItem;

        // 해당 날짜의 시간 슬롯 표시
        AvailableTimeSlotsForDate.Clear();
        foreach (var slot in dateItem.TimeSlots)
        {
            slot.IsSelected = false;
            AvailableTimeSlotsForDate.Add(slot);
        }

        SelectedTimeSlot = null;
        UpdateReservationDisplay();
        ValidateCanSave();
    }

    /// <summary>
    /// 시간 슬롯 선택 (대대담당자)
    /// </summary>
    [RelayCommand]
    private void SelectTimeSlot(TimeSlotItem slot)
    {
        if (slot == null) return;

        // 이전 선택 해제
        foreach (var s in AvailableTimeSlotsForDate)
        {
            s.IsSelected = false;
        }

        slot.IsSelected = true;
        SelectedTimeSlot = slot;

        UpdateReservationDisplay();
        ValidateCanSave();
    }

    /// <summary>
    /// 예약 정보 표시 업데이트
    /// </summary>
    private void UpdateReservationDisplay()
    {
        if (SelectedDate != null && SelectedTimeSlot != null)
        {
            ReservationDisplay = $"{SelectedDate.Date:yyyy년 M월 d일} | {SelectedTimeSlot.TimeRangeDisplay}";
            ShowReservationDisplay = true;
        }
        else
        {
            ReservationDisplay = "";
            ShowReservationDisplay = false;
        }
    }

    /// <summary>
    /// 저장 가능 여부 검증
    /// </summary>
    private void ValidateCanSave()
    {
        if (CurrentMode == "input")
        {
            // 지자체담당자: 가능 일자와 시간이 선택되어야 함
            var hasSelectedTimes = TimeSlots.Any(t => t.IsSelected);
            var validDateRange = AvailableStartDate <= AvailableEndDate;
            CanSave = hasSelectedTimes && validDateRange && CanInput;
        }
        else if (CurrentMode == "reserve")
        {
            // 대대담당자: 날짜와 시간이 선택되어야 함
            CanSave = SelectedDate != null && SelectedTimeSlot != null && CanReserve;
        }
        else
        {
            CanSave = false;
        }
    }

    /// <summary>
    /// 저장 (지자체담당자: 입력, 대대담당자: 예약)
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

            if (CurrentMode == "input")
            {
                await SaveInputAsync(client);
            }
            else if (CurrentMode == "reserve")
            {
                await SaveReserveAsync(client);
            }
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
    /// 지자체담당자: 가능 일정 입력 저장
    /// </summary>
    private async Task SaveInputAsync(Supabase.Client client)
    {
        var selectedTimes = TimeSlots.Where(t => t.IsSelected).ToList();
        if (selectedTimes.Count == 0)
        {
            ErrorMessage = "가능한 시간을 선택해주세요.";
            return;
        }

        // 1. 일정 업데이트
        await client.From<Schedule>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
            .Set(s => s.AvailableStart, AvailableStartDate.DateTime)
            .Set(s => s.AvailableEnd, AvailableEndDate.DateTime)
            .Set(s => s.Memo, string.IsNullOrWhiteSpace(Memo) ? null : Memo)
            .Set(s => s.Status, "inputted")
            .Set(s => s.StatusOrder, 2)
            .Update();

        // 2. 기존 가능 시간 삭제
        await client.From<ScheduleAvailableTime>()
            .Filter("schedule_id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
            .Delete();

        // 3. 새 가능 시간 생성 (선택된 날짜 범위 내 모든 날짜에 대해)
        var startDate = AvailableStartDate.Date;
        var endDate = AvailableEndDate.Date;

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

        // 이벤트 발생
        ScheduleUpdated?.Invoke(this, EventArgs.Empty);

        // 잠시 후 화면 닫기
        await Task.Delay(1000);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 대대담당자: 예약 저장
    /// </summary>
    private async Task SaveReserveAsync(Supabase.Client client)
    {
        if (SelectedDate == null || SelectedTimeSlot == null)
        {
            ErrorMessage = "예약할 날짜와 시간을 선택해주세요.";
            return;
        }

        // 1. 일정 업데이트
        await client.From<Schedule>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
            .Set(s => s.ReservedDate, SelectedDate.Date)
            .Set(s => s.ReservedStartTime, SelectedTimeSlot.StartTime)
            .Set(s => s.ReservedEndTime, SelectedTimeSlot.EndTime)
            .Set(s => s.Status, "reserved")
            .Set(s => s.StatusOrder, 3)
            .Update();

        // 2. 선택된 시간 슬롯 표시
        if (SelectedTimeSlot.Id != Guid.Empty)
        {
            await client.From<ScheduleAvailableTime>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, SelectedTimeSlot.Id.ToString())
                .Set(t => t.IsSelected, true)
                .Update();
        }

        SuccessMessage = "예약이 완료되었습니다.";
        System.Diagnostics.Debug.WriteLine($"[ScheduleInputVM] Reserved: {SelectedDate.Date:yyyy-MM-dd} {SelectedTimeSlot.TimeRangeDisplay}");

        // 이벤트 발생
        ScheduleUpdated?.Invoke(this, EventArgs.Empty);

        // 잠시 후 화면 닫기
        await Task.Delay(1000);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 탭 전환
    /// </summary>
    [RelayCommand]
    private void SwitchTab(string tab)
    {
        if (tab == "input")
        {
            IsInputTabSelected = true;
            IsReserveTabSelected = false;
            CurrentMode = "input";
        }
        else if (tab == "reserve")
        {
            IsInputTabSelected = false;
            IsReserveTabSelected = true;
            CurrentMode = "reserve";
        }

        ValidateCanSave();
    }

    /// <summary>
    /// 닫기
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    partial void OnAvailableStartDateChanged(DateTimeOffset value)
    {
        ValidateCanSave();
    }

    partial void OnAvailableEndDateChanged(DateTimeOffset value)
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
/// 날짜 아이템 (대대담당자 예약용)
/// </summary>
public partial class DateItem : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayDisplay { get; set; } = "";
    public List<TimeSlotItem> TimeSlots { get; set; } = new();

    [ObservableProperty]
    private bool _isSelected;
}
