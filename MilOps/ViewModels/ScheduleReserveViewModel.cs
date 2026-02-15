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
/// 일정 예약 ViewModel (대대담당자/user_military 전용)
/// - 지자체담당자가 입력한 가능 일자/시간 중 선택하여 예약
/// </summary>
public partial class ScheduleReserveViewModel : ViewModelBase
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

    // 지자체담당자 정보
    [ObservableProperty]
    private string _localUserName = "";

    [ObservableProperty]
    private string _localUserPhone = "";

    // === 가능 일자/시간 선택 ===
    [ObservableProperty]
    private ObservableCollection<ReserveDateItem> _availableDates = new();

    [ObservableProperty]
    private ReserveDateItem? _selectedDate;

    [ObservableProperty]
    private ObservableCollection<ReserveTimeSlotItem> _availableTimeSlotsForDate = new();

    [ObservableProperty]
    private ReserveTimeSlotItem? _selectedTimeSlot;

    // 가능 일자 없음 여부
    [ObservableProperty]
    private bool _hasNoAvailableDates = false;

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

    // 예약 버튼 활성화
    [ObservableProperty]
    private bool _canReserve = false;

    // 이벤트
    public event EventHandler? CloseRequested;
    public event EventHandler<ScheduleStatusChangedEventArgs>? ScheduleStatusChanged;

    public ScheduleReserveViewModel()
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

            // 1단계: 일정 + 가능시간 동시 로드
            var scheduleTask = client.From<Schedule>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
                .Single();

            var availableTimesTask = client.From<ScheduleAvailableTime>()
                .Filter("schedule_id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
                .Order("available_date", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            await Task.WhenAll(scheduleTask, availableTimesTask);

            _schedule = scheduleTask.Result;
            if (_schedule == null)
            {
                ErrorMessage = "일정을 찾을 수 없습니다.";
                return;
            }

            // 가능 시간 처리 (UI 업데이트)
            var times = availableTimesTask.Result.Models;
            ProcessAvailableTimes(times);

            // 2단계: 관련 데이터 모두 병렬 로드
            var companyTask = client.From<Company>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _schedule.CompanyId.ToString())
                .Single();

            var localUserTask = client.From<User>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _schedule.LocalUserId.ToString())
                .Single();

            var militaryUserTask = client.From<User>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _schedule.MilitaryUserId.ToString())
                .Single();

            // 모든 지역/대대 정보를 한번에 로드
            var districtsTask = client.From<District>().Get();
            var regionsTask = client.From<Region>().Get();
            var battalionsTask = client.From<Battalion>().Get();
            var divisionsTask = client.From<Division>().Get();

            await Task.WhenAll(companyTask, localUserTask, militaryUserTask,
                              districtsTask, regionsTask, battalionsTask, divisionsTask);

            var company = companyTask.Result;
            var localUser = localUserTask.Result;
            var militaryUser = militaryUserTask.Result;
            var districts = districtsTask.Result.Models;
            var regions = regionsTask.Result.Models;
            var battalions = battalionsTask.Result.Models;
            var divisions = divisionsTask.Result.Models;

            // 업체 정보 설정
            if (company != null)
            {
                CompanyName = company.Name;
                CompanyAddress = company.Address ?? "";
                CompanyProducts = company.Products ?? "";

                var district = districts.FirstOrDefault(d => d.Id == company.DistrictId);
                if (district != null)
                {
                    DistrictName = district.Name;
                    var region = regions.FirstOrDefault(r => r.Id == district.RegionId);
                    if (region != null) RegionName = region.Name;
                }
            }

            // 지자체담당자 정보
            if (localUser != null)
            {
                LocalUserName = localUser.FullDisplayName;
                LocalUserPhone = localUser.Phone ?? "";
            }

            // 현재 사용자 표시 설정 (대대담당자)
            if (militaryUser != null)
            {
                string battalionDisplay = "";
                if (militaryUser.BattalionId.HasValue)
                {
                    var battalion = battalions.FirstOrDefault(b => b.Id == militaryUser.BattalionId.Value);
                    if (battalion != null)
                    {
                        var division = divisions.FirstOrDefault(d => d.Id == battalion.DivisionId);
                        battalionDisplay = division != null
                            ? $"{division.Name} {battalion.Name}"
                            : battalion.Name;
                    }
                }
                CurrentUserDisplay = $"{militaryUser.FullDisplayName} ({battalionDisplay} 대대담당자)";
            }

            ValidateCanReserve();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleReserveVM] LoadScheduleAsync error: {ex.Message}");
            ErrorMessage = "데이터 로드 중 오류가 발생했습니다.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 가능 시간 데이터 처리 (UI 업데이트)
    /// </summary>
    private void ProcessAvailableTimes(List<ScheduleAvailableTime> times)
    {
        if (times.Count == 0)
        {
            HasNoAvailableDates = true;
            return;
        }

        HasNoAvailableDates = false;

        var dateGroups = times
            .GroupBy(t => t.AvailableDate.Date)
            .OrderBy(g => g.Key)
            .ToList();

        Dispatcher.UIThread.Post(() =>
        {
            AvailableDates.Clear();
            foreach (var group in dateGroups)
            {
                AvailableDates.Add(new ReserveDateItem
                {
                    Date = group.Key,
                    DayDisplay = group.Key.Day.ToString() + "일",
                    TimeSlots = group.Select(t => new ReserveTimeSlotItem
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

    /// <summary>
    /// 날짜 선택
    /// </summary>
    [RelayCommand]
    private void SelectDate(ReserveDateItem dateItem)
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
        ValidateCanReserve();
    }

    /// <summary>
    /// 시간 슬롯 선택
    /// </summary>
    [RelayCommand]
    private void SelectTimeSlot(ReserveTimeSlotItem slot)
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
        ValidateCanReserve();
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
    /// 예약 가능 여부 검증
    /// </summary>
    private void ValidateCanReserve()
    {
        CanReserve = SelectedDate != null && SelectedTimeSlot != null;
    }

    /// <summary>
    /// 예약 저장
    /// </summary>
    [RelayCommand]
    private async Task ReserveAsync()
    {
        if (!CanReserve) return;

        IsLoading = true;
        ErrorMessage = "";
        SuccessMessage = "";

        try
        {
            var client = SupabaseService.Client;
            if (client == null || _schedule == null) return;

            if (SelectedDate == null || SelectedTimeSlot == null)
            {
                ErrorMessage = "예약할 날짜와 시간을 선택해주세요.";
                return;
            }

            // 1. 일정 업데이트
#pragma warning disable CS8603 // Possible null reference return
            await client.From<Schedule>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, _scheduleId.ToString())
                .Set(s => s.ReservedDate, SelectedDate.Date)
                .Set(s => s.ReservedStartTime, SelectedTimeSlot.StartTime)
                .Set(s => s.ReservedEndTime, SelectedTimeSlot.EndTime)
                .Set(s => s.Status, "reserved")
                .Set(s => s.StatusOrder, 3)
                .Update();
#pragma warning restore CS8603

            // 2. 선택된 시간 슬롯 표시
            if (SelectedTimeSlot.Id != Guid.Empty)
            {
                await client.From<ScheduleAvailableTime>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, SelectedTimeSlot.Id.ToString())
                    .Set(t => t.IsSelected, true)
                    .Update();
            }

            SuccessMessage = "예약이 완료되었습니다.";

            // 상태 변경 이벤트 발생 (reserved, statusOrder=3)
            ScheduleStatusChanged?.Invoke(this, new ScheduleStatusChangedEventArgs(_scheduleId, "reserved", 3));
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleReserveVM] ReserveAsync error: {ex.Message}");
            ErrorMessage = "예약 중 오류가 발생했습니다.";
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

/// <summary>
/// 예약용 시간 슬롯 아이템
/// </summary>
public partial class ReserveTimeSlotItem : ObservableObject
{
    public Guid Id { get; set; }

    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    public string TimeRangeDisplay => $"{StartTime:hh\\:mm}-{EndTime:hh\\:mm}";
}

/// <summary>
/// 예약용 날짜 아이템
/// </summary>
public partial class ReserveDateItem : ObservableObject
{
    public DateTime Date { get; set; }
    public string DayDisplay { get; set; } = "";
    public List<ReserveTimeSlotItem> TimeSlots { get; set; } = new();

    [ObservableProperty]
    private bool _isSelected;
}
