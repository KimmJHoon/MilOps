using Avalonia.Data.Converters;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

/// <summary>
/// 캘린더 뷰용 컨버터들
/// </summary>
public static class CalendarConverters
{
    public static readonly DayBackgroundMultiConverter DayBackgroundConverter = new();
}

/// <summary>
/// 날짜 셀 배경색 컨버터 (IsSelected, IsToday)
/// </summary>
public class DayBackgroundMultiConverter : IMultiValueConverter
{
    private static readonly IBrush SelectedBrush = new SolidColorBrush(Color.Parse("#2A4A6A"));
    private static readonly IBrush TodayBrush = new SolidColorBrush(Color.Parse("#1A3A1A"));
    private static readonly IBrush DefaultBrush = Brushes.Transparent;

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Count < 2) return DefaultBrush;

        var isSelected = values[0] is true;
        var isToday = values[1] is true;

        if (isSelected) return SelectedBrush;
        if (isToday) return TodayBrush;
        return DefaultBrush;
    }
}

public partial class CalendarViewModel : ViewModelBase
{
    [ObservableProperty]
    private int _currentYear;

    [ObservableProperty]
    private int _currentMonth;

    [ObservableProperty]
    private string _currentMonthYear = "";

    [ObservableProperty]
    private ObservableCollection<CalendarDay> _days = new();

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private CalendarDay? _selectedDay;

    [ObservableProperty]
    private ObservableCollection<CalendarScheduleItem> _selectedDaySchedules = new();

    [ObservableProperty]
    private bool _hasSelectedDaySchedules = false;

    [ObservableProperty]
    private string _selectedDayTitle = "";

    // 월별 일정 캐시
    private Dictionary<DateTime, List<Schedule>> _scheduleCache = new();

    // 네비게이션 이벤트
    public event Action<Guid>? OnScheduleSelected;

    public CalendarViewModel()
    {
        var today = DateTime.Today;
        CurrentYear = today.Year;
        CurrentMonth = today.Month;
        UpdateCalendar();
    }

    [RelayCommand]
    private void PreviousMonth()
    {
        if (CurrentMonth == 1)
        {
            CurrentMonth = 12;
            CurrentYear--;
        }
        else
        {
            CurrentMonth--;
        }
        UpdateCalendar();
        _ = LoadSchedulesAsync();
    }

    [RelayCommand]
    private void NextMonth()
    {
        if (CurrentMonth == 12)
        {
            CurrentMonth = 1;
            CurrentYear++;
        }
        else
        {
            CurrentMonth++;
        }
        UpdateCalendar();
        _ = LoadSchedulesAsync();
    }

    [RelayCommand]
    private void SelectDay(CalendarDay? day)
    {
        if (day == null || day.Day == 0) return;

        // 이전 선택 해제
        if (SelectedDay != null)
        {
            SelectedDay.IsSelected = false;
        }

        // 새로운 선택
        day.IsSelected = true;
        SelectedDay = day;

        // 선택된 날짜의 일정 표시
        UpdateSelectedDaySchedules(day);
    }

    [RelayCommand]
    private void OpenScheduleDetail(CalendarScheduleItem? item)
    {
        if (item == null) return;
        OnScheduleSelected?.Invoke(item.ScheduleId);
    }

    [RelayCommand]
    private void CloseSelectedDaySchedules()
    {
        if (SelectedDay != null)
        {
            SelectedDay.IsSelected = false;
            SelectedDay = null;
        }
        HasSelectedDaySchedules = false;
        SelectedDaySchedules.Clear();
    }

    private void UpdateCalendar()
    {
        CurrentMonthYear = $"{CurrentYear}년 {CurrentMonth}월";

        Days.Clear();

        var firstDay = new DateTime(CurrentYear, CurrentMonth, 1);
        var daysInMonth = DateTime.DaysInMonth(CurrentYear, CurrentMonth);

        // 첫째 날의 요일 (일요일=0)
        int startDayOfWeek = (int)firstDay.DayOfWeek;

        // 이전 달의 빈 칸
        for (int i = 0; i < startDayOfWeek; i++)
        {
            Days.Add(new CalendarDay { Day = 0, IsCurrentMonth = false });
        }

        // 현재 달의 날짜
        var today = DateTime.Today;
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(CurrentYear, CurrentMonth, day);
            Days.Add(new CalendarDay
            {
                Day = day,
                Date = date,
                IsCurrentMonth = true,
                IsToday = date == today,
                IsSunday = date.DayOfWeek == DayOfWeek.Sunday,
                IsSaturday = date.DayOfWeek == DayOfWeek.Saturday
            });
        }

        // 다음 달의 빈 칸 (6주 = 42칸 채우기)
        while (Days.Count < 42)
        {
            Days.Add(new CalendarDay { Day = 0, IsCurrentMonth = false });
        }

        // 선택 상태 초기화
        SelectedDay = null;
        HasSelectedDaySchedules = false;
        SelectedDaySchedules.Clear();
    }

    /// <summary>
    /// 현재 월의 일정 로드
    /// </summary>
    public async Task LoadSchedulesAsync()
    {
        if (!AuthService.IsLoggedIn || AuthService.CurrentUser == null)
        {
            System.Diagnostics.Debug.WriteLine("[CalendarViewModel] Not logged in, skipping schedule load");
            return;
        }

        IsLoading = true;
        try
        {
            var currentUser = AuthService.CurrentUser;
            var role = AuthService.CurrentUserRole;

            // 현재 월의 시작과 끝
            var monthStart = new DateTime(CurrentYear, CurrentMonth, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] Loading schedules for {CurrentYear}-{CurrentMonth}, Role: {role}");

            List<Schedule> schedules = new();

            // 역할별 일정 조회
            if (role == UserRole.UserLocal)
            {
                // 지자체담당자: 본인이 담당하는 일정 (예약됨 + 확정됨)
                schedules = await LoadLocalUserSchedulesAsync(currentUser.Id, monthStart, monthEnd);
            }
            else if (role == UserRole.UserMilitary)
            {
                // 대대담당자: 본인이 담당하는 일정 (예약됨 + 확정됨)
                schedules = await LoadMilitaryUserSchedulesAsync(currentUser.Id, monthStart, monthEnd);
            }
            else if (role == UserRole.MiddleLocal)
            {
                // 지자체(도): 관할 전체 일정
                schedules = await LoadMiddleLocalSchedulesAsync(currentUser, monthStart, monthEnd);
            }
            else if (role == UserRole.MiddleMilitary)
            {
                // 사단담당자: 관할 전체 일정
                schedules = await LoadMiddleMilitarySchedulesAsync(currentUser, monthStart, monthEnd);
            }
            else if (role == UserRole.SuperAdminMois)
            {
                // SW0001: 전국 전체
                schedules = await LoadAllSchedulesAsync(monthStart, monthEnd);
            }
            else if (role == UserRole.SuperAdminArmy)
            {
                // SW0002: 전군 전체
                schedules = await LoadAllSchedulesAsync(monthStart, monthEnd);
            }

            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] Loaded {schedules.Count} schedules");

            // 날짜별로 그룹핑
            var schedulesByDate = schedules
                .Where(s => s.ReservedDate.HasValue)
                .GroupBy(s => s.ReservedDate!.Value.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 사용자 정보 로드 (대대/지자체 표시용)
            await LoadUserInfoAsync(schedules);

            // 캘린더에 일정 표시
            foreach (var day in Days)
            {
                if (day.Day == 0) continue;

                var date = new DateTime(CurrentYear, CurrentMonth, day.Day);
                if (schedulesByDate.TryGetValue(date, out var daySchedules))
                {
                    day.Schedules = daySchedules;
                    day.HasSchedules = true;
                    day.ScheduleCount = daySchedules.Count;

                    // 확정/미확정 상태 확인
                    day.HasConfirmedSchedule = daySchedules.Any(s => s.Status == "confirmed");
                    day.HasReservedSchedule = daySchedules.Any(s => s.Status == "reserved");

                    // 일정 표시 데이터 업데이트
                    day.UpdateScheduleDisplays(role);
                }
                else
                {
                    day.Schedules = new List<Schedule>();
                    day.HasSchedules = false;
                    day.ScheduleCount = 0;
                    day.HasConfirmedSchedule = false;
                    day.HasReservedSchedule = false;
                    day.ScheduleDisplays.Clear();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] Error loading schedules: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 지자체담당자용: 본인 담당 일정 조회
    /// </summary>
    private async Task<List<Schedule>> LoadLocalUserSchedulesAsync(Guid userId, DateTime monthStart, DateTime monthEnd)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadLocalUserSchedulesAsync: userId={userId}, range={monthStart:yyyy-MM-dd} ~ {monthEnd:yyyy-MM-dd}");

            var response = await SupabaseService.Client
                .From<Schedule>()
                .Filter("local_user_id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
                .Filter("reserved_date", Supabase.Postgrest.Constants.Operator.GreaterThanOrEqual, monthStart.ToString("yyyy-MM-dd"))
                .Filter("reserved_date", Supabase.Postgrest.Constants.Operator.LessThanOrEqual, monthEnd.ToString("yyyy-MM-dd"))
                .Filter("deleted_at", Supabase.Postgrest.Constants.Operator.Is, "null")
                .Order("reserved_date", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var schedules = response.Models.ToList();
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadLocalUserSchedulesAsync: Found {schedules.Count} schedules");

            // Company 정보 로드
            await LoadCompanyInfoAsync(schedules);

            return schedules;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadLocalUserSchedulesAsync error: {ex.Message}");
            return new List<Schedule>();
        }
    }

    /// <summary>
    /// 대대담당자용: 본인 담당 일정 조회
    /// </summary>
    private async Task<List<Schedule>> LoadMilitaryUserSchedulesAsync(Guid userId, DateTime monthStart, DateTime monthEnd)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMilitaryUserSchedulesAsync: userId={userId}, range={monthStart:yyyy-MM-dd} ~ {monthEnd:yyyy-MM-dd}");

            var response = await SupabaseService.Client
                .From<Schedule>()
                .Filter("military_user_id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
                .Filter("reserved_date", Supabase.Postgrest.Constants.Operator.GreaterThanOrEqual, monthStart.ToString("yyyy-MM-dd"))
                .Filter("reserved_date", Supabase.Postgrest.Constants.Operator.LessThanOrEqual, monthEnd.ToString("yyyy-MM-dd"))
                .Filter("deleted_at", Supabase.Postgrest.Constants.Operator.Is, "null")
                .Order("reserved_date", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var schedules = response.Models.ToList();
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMilitaryUserSchedulesAsync: Found {schedules.Count} schedules");

            return schedules;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMilitaryUserSchedulesAsync error: {ex.Message}");
            return new List<Schedule>();
        }
    }

    /// <summary>
    /// 지자체(도) 중간관리자용: 관할 전체 일정 조회
    /// </summary>
    private async Task<List<Schedule>> LoadMiddleLocalSchedulesAsync(User currentUser, DateTime monthStart, DateTime monthEnd)
    {
        try
        {
            // 먼저 관할 지자체담당자 ID 목록 조회
            var localUsersResponse = await SupabaseService.Client
                .From<User>()
                .Select("id")
                .Filter("parent_id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.Id.ToString())
                .Filter("role", Supabase.Postgrest.Constants.Operator.Equals, "user_local")
                .Get();

            var localUserIds = localUsersResponse.Models.Select(u => u.Id).ToList();

            if (!localUserIds.Any())
            {
                return new List<Schedule>();
            }

            // 관할 담당자들의 일정 조회
            var schedules = new List<Schedule>();
            foreach (var userId in localUserIds)
            {
                var userSchedules = await LoadLocalUserSchedulesAsync(userId, monthStart, monthEnd);
                schedules.AddRange(userSchedules);
            }

            return schedules.OrderBy(s => s.ReservedDate).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMiddleLocalSchedulesAsync error: {ex.Message}");
            return new List<Schedule>();
        }
    }

    /// <summary>
    /// 사단담당자용: 관할 전체 일정 조회
    /// </summary>
    private async Task<List<Schedule>> LoadMiddleMilitarySchedulesAsync(User currentUser, DateTime monthStart, DateTime monthEnd)
    {
        try
        {
            // 먼저 관할 대대담당자 ID 목록 조회
            var militaryUsersResponse = await SupabaseService.Client
                .From<User>()
                .Select("id")
                .Filter("parent_id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.Id.ToString())
                .Filter("role", Supabase.Postgrest.Constants.Operator.Equals, "user_military")
                .Get();

            var militaryUserIds = militaryUsersResponse.Models.Select(u => u.Id).ToList();

            if (!militaryUserIds.Any())
            {
                return new List<Schedule>();
            }

            // 관할 담당자들의 일정 조회
            var schedules = new List<Schedule>();
            foreach (var userId in militaryUserIds)
            {
                var userSchedules = await LoadMilitaryUserSchedulesAsync(userId, monthStart, monthEnd);
                schedules.AddRange(userSchedules);
            }

            return schedules.OrderBy(s => s.ReservedDate).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMiddleMilitarySchedulesAsync error: {ex.Message}");
            return new List<Schedule>();
        }
    }

    /// <summary>
    /// 최종관리자용: 전체 일정 조회
    /// </summary>
    private async Task<List<Schedule>> LoadAllSchedulesAsync(DateTime monthStart, DateTime monthEnd)
    {
        try
        {
            var response = await SupabaseService.Client
                .From<Schedule>()
                .Filter("reserved_date", Supabase.Postgrest.Constants.Operator.GreaterThanOrEqual, monthStart.ToString("yyyy-MM-dd"))
                .Filter("reserved_date", Supabase.Postgrest.Constants.Operator.LessThanOrEqual, monthEnd.ToString("yyyy-MM-dd"))
                .Filter("deleted_at", Supabase.Postgrest.Constants.Operator.Is, "null")
                .Order("reserved_date", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            var schedules = response.Models.ToList();

            // Company 정보 로드
            await LoadCompanyInfoAsync(schedules);

            return schedules;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadAllSchedulesAsync error: {ex.Message}");
            return new List<Schedule>();
        }
    }

    /// <summary>
    /// 일정에 업체 정보 로드
    /// </summary>
    private async Task LoadCompanyInfoAsync(List<Schedule> schedules)
    {
        if (!schedules.Any()) return;

        try
        {
            var companyIds = schedules.Select(s => s.CompanyId).Distinct().ToList();

            foreach (var companyId in companyIds)
            {
                var companyResponse = await SupabaseService.Client
                    .From<Company>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, companyId.ToString())
                    .Single();

                if (companyResponse != null)
                {
                    foreach (var schedule in schedules.Where(s => s.CompanyId == companyId))
                    {
                        schedule.Company = companyResponse;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadCompanyInfoAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 일정에 사용자 정보 로드 (대대/지자체 표시용)
    /// </summary>
    private async Task LoadUserInfoAsync(List<Schedule> schedules)
    {
        if (!schedules.Any()) return;

        try
        {
            // 대대담당자 정보 로드
            var militaryUserIds = schedules.Select(s => s.MilitaryUserId).Distinct().ToList();
            foreach (var userId in militaryUserIds)
            {
                var userResponse = await SupabaseService.Client
                    .From<User>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
                    .Single();

                if (userResponse != null)
                {
                    // 대대 정보 로드
                    if (userResponse.BattalionId.HasValue)
                    {
                        var battalionResponse = await SupabaseService.Client
                            .From<Battalion>()
                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userResponse.BattalionId.Value.ToString())
                            .Single();
                        userResponse.Battalion = battalionResponse;
                    }

                    foreach (var schedule in schedules.Where(s => s.MilitaryUserId == userId))
                    {
                        schedule.MilitaryUser = userResponse;
                    }
                }
            }

            // 지자체담당자 정보 로드
            var localUserIds = schedules.Select(s => s.LocalUserId).Distinct().ToList();
            foreach (var userId in localUserIds)
            {
                var userResponse = await SupabaseService.Client
                    .From<User>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userId.ToString())
                    .Single();

                if (userResponse != null)
                {
                    // 지자체(구) 정보 로드
                    if (userResponse.DistrictId.HasValue)
                    {
                        var districtResponse = await SupabaseService.Client
                            .From<District>()
                            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, userResponse.DistrictId.Value.ToString())
                            .Single();
                        userResponse.District = districtResponse;
                    }

                    foreach (var schedule in schedules.Where(s => s.LocalUserId == userId))
                    {
                        schedule.LocalUser = userResponse;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadUserInfoAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 선택된 날짜의 일정 목록 업데이트
    /// </summary>
    private void UpdateSelectedDaySchedules(CalendarDay day)
    {
        SelectedDaySchedules.Clear();
        SelectedDayTitle = $"{CurrentMonth}월 {day.Day}일 일정";

        if (day.Schedules == null || !day.Schedules.Any())
        {
            HasSelectedDaySchedules = false;
            return;
        }

        foreach (var schedule in day.Schedules.OrderBy(s => s.ReservedStartTime))
        {
            SelectedDaySchedules.Add(new CalendarScheduleItem
            {
                ScheduleId = schedule.Id,
                CompanyName = schedule.Company?.Name ?? "업체명 없음",
                TimeDisplay = schedule.ReservedStartTime.HasValue && schedule.ReservedEndTime.HasValue
                    ? $"{schedule.ReservedStartTime.Value:hh\\:mm} - {schedule.ReservedEndTime.Value:hh\\:mm}"
                    : "시간 미정",
                Status = schedule.Status,
                StatusDisplayName = schedule.StatusDisplayName,
                StatusColor = schedule.StatusColor,
                IsConfirmed = schedule.Status == "confirmed",
                LocalConfirmed = schedule.LocalConfirmed,
                MilitaryConfirmed = schedule.MilitaryConfirmed
            });
        }

        HasSelectedDaySchedules = SelectedDaySchedules.Any();
    }

    /// <summary>
    /// 외부에서 새로고침 호출용
    /// </summary>
    public async Task RefreshAsync()
    {
        UpdateCalendar();
        await LoadSchedulesAsync();
    }
}

/// <summary>
/// 캘린더 날짜 정보
/// </summary>
public partial class CalendarDay : ObservableObject
{
    public int Day { get; set; }
    public DateTime Date { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }
    public bool IsSunday { get; set; }
    public bool IsSaturday { get; set; }

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _hasSchedules;

    [ObservableProperty]
    private int _scheduleCount;

    [ObservableProperty]
    private bool _hasConfirmedSchedule;

    [ObservableProperty]
    private bool _hasReservedSchedule;

    [ObservableProperty]
    private ObservableCollection<CalendarDayScheduleDisplay> _scheduleDisplays = new();

    public List<Schedule> Schedules { get; set; } = new();

    public string DayText => Day > 0 ? Day.ToString() : "";

    public string DayColor
    {
        get
        {
            if (IsToday) return "#00FF00";
            if (IsSunday) return "#FF6B6B";
            if (IsSaturday) return "#6B9FFF";
            return "White";
        }
    }

    /// <summary>
    /// 일정 표시용 색상 (확정=초록, 예약=주황)
    /// </summary>
    public string ScheduleIndicatorColor
    {
        get
        {
            if (HasConfirmedSchedule) return "#4CAF50";  // 확정됨 - Green
            if (HasReservedSchedule) return "#FF9800";   // 예약됨 - Orange
            return "Transparent";
        }
    }

    /// <summary>
    /// 일정 개수 표시 텍스트
    /// </summary>
    public string ScheduleCountText => ScheduleCount > 0 ? ScheduleCount.ToString() : "";

    /// <summary>
    /// 2개 초과 일정이 있는지 여부 (모바일 화면 고려)
    /// </summary>
    public bool HasMoreSchedules => ScheduleCount > 2;

    /// <summary>
    /// 추가 일정 개수 (2개 초과분)
    /// </summary>
    public int MoreScheduleCount => Math.Max(0, ScheduleCount - 2);

    /// <summary>
    /// 추가 일정 표시 텍스트
    /// </summary>
    public string MoreSchedulesText => HasMoreSchedules ? $"+{MoreScheduleCount}" : "";

    /// <summary>
    /// 일정 표시 데이터 업데이트
    /// </summary>
    public void UpdateScheduleDisplays(UserRole currentRole)
    {
        ScheduleDisplays.Clear();

        if (Schedules == null || !Schedules.Any()) return;

        foreach (var schedule in Schedules.OrderBy(s => s.ReservedStartTime).Take(2)) // 모바일 화면 고려 최대 2개만 표시
        {
            var display = new CalendarDayScheduleDisplay
            {
                ScheduleId = schedule.Id,
                IsConfirmed = schedule.Status == "confirmed",
                TimeText = schedule.ReservedStartTime.HasValue
                    ? schedule.ReservedStartTime.Value.ToString(@"hh\:mm")
                    : "",
                CompanyName = schedule.Company?.Name ?? "",
                StatusColor = schedule.StatusColor
            };

            // 역할에 따라 상대방 정보 표시
            if (currentRole == UserRole.UserLocal)
            {
                // 지자체담당자: 대대 정보 표시
                display.CounterpartInfo = schedule.MilitaryUser?.Battalion?.Name ?? "대대";
            }
            else if (currentRole == UserRole.UserMilitary)
            {
                // 대대담당자: 지자체(구) 정보 표시
                display.CounterpartInfo = schedule.LocalUser?.District?.Name ?? "지자체";
            }
            else
            {
                // 중간관리자/최종관리자: 양쪽 모두 표시
                display.CounterpartInfo = schedule.MilitaryUser?.Battalion?.Name ?? "대대";
            }

            ScheduleDisplays.Add(display);
        }
    }
}

/// <summary>
/// 캘린더 셀 내 일정 표시용 클래스
/// </summary>
public class CalendarDayScheduleDisplay
{
    public Guid ScheduleId { get; set; }
    public bool IsConfirmed { get; set; }
    public string TimeText { get; set; } = "";
    public string CounterpartInfo { get; set; } = "";  // 대대 or 지자체
    public string CompanyName { get; set; } = "";
    public string StatusColor { get; set; } = "#FF9800";

    public string ConfirmMark => IsConfirmed ? "✓" : "";
    public string DisplayText => $"{ConfirmMark}{TimeText}";
}

/// <summary>
/// 캘린더 일정 아이템 (선택한 날짜의 일정 목록용)
/// </summary>
public class CalendarScheduleItem
{
    public Guid ScheduleId { get; set; }
    public string CompanyName { get; set; } = "";
    public string TimeDisplay { get; set; } = "";
    public string Status { get; set; } = "";
    public string StatusDisplayName { get; set; } = "";
    public string StatusColor { get; set; } = "";
    public bool IsConfirmed { get; set; }
    public bool LocalConfirmed { get; set; }
    public bool MilitaryConfirmed { get; set; }

    public string ConfirmStatusText
    {
        get
        {
            if (IsConfirmed) return "✓ 확정완료";
            if (LocalConfirmed && !MilitaryConfirmed) return "🏛️✓ 🎖️⏳";
            if (!LocalConfirmed && MilitaryConfirmed) return "🏛️⏳ 🎖️✓";
            return "⏳ 확정대기";
        }
    }
}
