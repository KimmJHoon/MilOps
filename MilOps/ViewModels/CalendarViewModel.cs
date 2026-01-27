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
        // 42개의 CalendarDay 객체를 미리 생성 (6주 x 7일)
        // XAML에서 Days[0]~Days[41] 인덱스로 바인딩하므로 항상 42개 유지해야 함
        for (int i = 0; i < 42; i++)
        {
            Days.Add(new CalendarDay { Day = 0, IsCurrentMonth = false });
        }

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

        // 일정이 하나만 있으면 바로 상세 화면으로 이동
        if (day.Schedules != null && day.Schedules.Count == 1)
        {
            OnScheduleSelected?.Invoke(day.Schedules[0].Id);
            return;
        }

        // 일정이 여러 개면 첫 번째 일정으로 이동 (또는 선택 UI 표시 가능)
        if (day.Schedules != null && day.Schedules.Count > 1)
        {
            // 일정 여러 개일 때 첫 번째 일정 상세로 이동
            OnScheduleSelected?.Invoke(day.Schedules[0].Id);
            return;
        }

        // 일정이 없는 날짜는 무시
    }

    /// <summary>
    /// 캘린더 셀 내의 일정 아이템 클릭 시 바로 상세 화면으로 이동
    /// </summary>
    [RelayCommand]
    private void OpenScheduleFromCell(CalendarDayScheduleDisplay? item)
    {
        if (item == null) return;

        OnScheduleSelected?.Invoke(item.ScheduleId);
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

        var firstDay = new DateTime(CurrentYear, CurrentMonth, 1);
        var daysInMonth = DateTime.DaysInMonth(CurrentYear, CurrentMonth);

        // 첫째 날의 요일 (일요일=0)
        int startDayOfWeek = (int)firstDay.DayOfWeek;
        var today = DateTime.Today;

        int index = 0;

        // 이전 달의 빈 칸
        for (int i = 0; i < startDayOfWeek; i++)
        {
            Days[index].Reset();
            Days[index].Day = 0;
            Days[index].IsCurrentMonth = false;
            index++;
        }

        // 현재 달의 날짜
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(CurrentYear, CurrentMonth, day);
            Days[index].Reset();
            Days[index].Day = day;
            Days[index].Date = date;
            Days[index].IsCurrentMonth = true;
            Days[index].IsToday = date == today;
            Days[index].IsSunday = date.DayOfWeek == DayOfWeek.Sunday;
            Days[index].IsSaturday = date.DayOfWeek == DayOfWeek.Saturday;
            index++;
        }

        // 다음 달의 빈 칸 (6주 = 42칸 채우기)
        while (index < 42)
        {
            Days[index].Reset();
            Days[index].Day = 0;
            Days[index].IsCurrentMonth = false;
            index++;
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
    /// - 본인이 속한 도(region) 하위의 모든 시/군/구 일정을 조회
    /// </summary>
    private async Task<List<Schedule>> LoadMiddleLocalSchedulesAsync(User currentUser, DateTime monthStart, DateTime monthEnd)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMiddleLocalSchedulesAsync: regionId={currentUser.RegionId}");

            if (!currentUser.RegionId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("[CalendarViewModel] LoadMiddleLocalSchedulesAsync: No regionId");
                return new List<Schedule>();
            }

            // 1. 해당 도(region)에 속한 모든 시/군/구(district) 조회
            var districtsResponse = await SupabaseService.Client
                .From<District>()
                .Select("id")
                .Filter("region_id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.RegionId.Value.ToString())
                .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                .Get();

            var districtIds = districtsResponse.Models.Select(d => d.Id).ToList();
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMiddleLocalSchedulesAsync: Found {districtIds.Count} districts");

            if (!districtIds.Any())
            {
                return new List<Schedule>();
            }

            // 2. 해당 시/군/구에 소속된 지자체담당자(user_local) 조회
            var localUsers = new List<User>();
            foreach (var districtId in districtIds)
            {
                var usersResponse = await SupabaseService.Client
                    .From<User>()
                    .Select("id")
                    .Filter("district_id", Supabase.Postgrest.Constants.Operator.Equals, districtId.ToString())
                    .Filter("role", Supabase.Postgrest.Constants.Operator.Equals, "user_local")
                    .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                    .Get();

                localUsers.AddRange(usersResponse.Models);
            }

            var localUserIds = localUsers.Select(u => u.Id).Distinct().ToList();
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMiddleLocalSchedulesAsync: Found {localUserIds.Count} local users");

            if (!localUserIds.Any())
            {
                return new List<Schedule>();
            }

            // 3. 해당 담당자들의 일정 조회 (예약됨 + 확정됨)
            var schedules = new List<Schedule>();
            foreach (var userId in localUserIds)
            {
                var userSchedules = await LoadLocalUserSchedulesAsync(userId, monthStart, monthEnd);
                schedules.AddRange(userSchedules);
            }

            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMiddleLocalSchedulesAsync: Total {schedules.Count} schedules");
            return schedules.OrderBy(s => s.ReservedDate).ThenBy(s => s.ReservedStartTime).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMiddleLocalSchedulesAsync error: {ex.Message}");
            return new List<Schedule>();
        }
    }

    /// <summary>
    /// 사단담당자용: 관할 전체 일정 조회
    /// - 본인이 속한 사단(division) 하위의 모든 대대 일정을 조회
    /// </summary>
    private async Task<List<Schedule>> LoadMiddleMilitarySchedulesAsync(User currentUser, DateTime monthStart, DateTime monthEnd)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMiddleMilitarySchedulesAsync: divisionId={currentUser.DivisionId}");

            if (!currentUser.DivisionId.HasValue)
            {
                System.Diagnostics.Debug.WriteLine("[CalendarViewModel] LoadMiddleMilitarySchedulesAsync: No divisionId");
                return new List<Schedule>();
            }

            // 1. 해당 사단(division)에 속한 모든 대대(battalion) 조회
            var battalionsResponse = await SupabaseService.Client
                .From<Battalion>()
                .Select("id")
                .Filter("division_id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.DivisionId.Value.ToString())
                .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                .Get();

            var battalionIds = battalionsResponse.Models.Select(b => b.Id).ToList();
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMiddleMilitarySchedulesAsync: Found {battalionIds.Count} battalions");

            if (!battalionIds.Any())
            {
                return new List<Schedule>();
            }

            // 2. 해당 대대에 소속된 대대담당자(user_military) 조회
            var militaryUsers = new List<User>();
            foreach (var battalionId in battalionIds)
            {
                var usersResponse = await SupabaseService.Client
                    .From<User>()
                    .Select("id")
                    .Filter("battalion_id", Supabase.Postgrest.Constants.Operator.Equals, battalionId.ToString())
                    .Filter("role", Supabase.Postgrest.Constants.Operator.Equals, "user_military")
                    .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                    .Get();

                militaryUsers.AddRange(usersResponse.Models);
            }

            var militaryUserIds = militaryUsers.Select(u => u.Id).Distinct().ToList();
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMiddleMilitarySchedulesAsync: Found {militaryUserIds.Count} military users");

            if (!militaryUserIds.Any())
            {
                return new List<Schedule>();
            }

            // 3. 해당 담당자들의 일정 조회 (예약됨 + 확정됨)
            var schedules = new List<Schedule>();
            foreach (var userId in militaryUserIds)
            {
                var userSchedules = await LoadMilitaryUserSchedulesAsync(userId, monthStart, monthEnd);
                schedules.AddRange(userSchedules);
            }

            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadMiddleMilitarySchedulesAsync: Total {schedules.Count} schedules");
            return schedules.OrderBy(s => s.ReservedDate).ThenBy(s => s.ReservedStartTime).ToList();
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
            // 업체 정보 로드
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
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DayText))]
    private int _day;

    [ObservableProperty]
    private DateTime _date;

    [ObservableProperty]
    private bool _isCurrentMonth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DayColor))]
    private bool _isToday;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DayColor))]
    private bool _isSunday;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DayColor))]
    private bool _isSaturday;

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

    /// <summary>
    /// 날짜 데이터 초기화 (재사용을 위해)
    /// </summary>
    public void Reset()
    {
        Day = 0;
        Date = default;
        IsCurrentMonth = false;
        IsToday = false;
        IsSunday = false;
        IsSaturday = false;
        IsSelected = false;
        HasSchedules = false;
        ScheduleCount = 0;
        HasConfirmedSchedule = false;
        HasReservedSchedule = false;
        ScheduleDisplays.Clear();
        Schedules.Clear();
    }

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
    /// - 지자체담당자(user_local): 시간 / 대대명 / 업체명
    /// - 대대담당자(user_military): 시간 / 군,구명 / 업체명
    /// - 지자체(도) 중간관리자(middle_local): 시간 / 시군구명 / 업체명
    /// - 사단담당자(middle_military): 시간 / 대대명 / 업체명
    /// </summary>
    public void UpdateScheduleDisplays(UserRole currentRole)
    {
        ScheduleDisplays.Clear();

        if (Schedules == null || !Schedules.Any()) return;

        foreach (var schedule in Schedules.OrderBy(s => s.ReservedStartTime).Take(3)) // 최대 3개 표시
        {
            var display = new CalendarDayScheduleDisplay
            {
                ScheduleId = schedule.Id,
                IsConfirmed = schedule.Status == "confirmed",
                CurrentRole = currentRole,
                // 시간 (예: 10:00)
                TimeText = schedule.ReservedStartTime.HasValue
                    ? schedule.ReservedStartTime.Value.ToString(@"hh\:mm")
                    : "",
                // 업체명
                CompanyName = schedule.Company?.Name ?? "",
                // 대대명 (지자체담당자, 사단담당자용)
                BattalionName = schedule.MilitaryUser?.Battalion?.Name ?? "",
                // 군,구명 (대대담당자, 지자체(도) 중간관리자용)
                DistrictName = schedule.LocalUser?.District?.Name ?? "",
                StatusColor = schedule.StatusColor
            };

            ScheduleDisplays.Add(display);
        }
    }
}

/// <summary>
/// 캘린더 셀 내 일정 표시용 클래스
/// - 지자체담당자(user_local): 시간 / 대대명 / 업체명
/// - 대대담당자(user_military): 시간 / 군,구명 / 업체명
/// - 지자체(도) 중간관리자(middle_local): 시간 / 시군구명 / 업체명
/// - 사단담당자(middle_military): 시간 / 대대명 / 업체명
/// </summary>
public class CalendarDayScheduleDisplay
{
    public Guid ScheduleId { get; set; }
    public bool IsConfirmed { get; set; }
    public string TimeText { get; set; } = "";           // 예: "10:00"
    public string BattalionName { get; set; } = "";      // 예: "101대대"
    public string DistrictName { get; set; } = "";       // 예: "강남구"
    public string CompanyName { get; set; } = "";        // 예: "해태제과"
    public string StatusColor { get; set; } = "#FF9800";
    public UserRole CurrentRole { get; set; } = UserRole.None;

    public string ConfirmMark => IsConfirmed ? "✓" : "";

    // 첫 줄: ✓시간 (예: ✓10:00)
    public string Line1 => $"{ConfirmMark}{TimeText}";

    // 둘째 줄: 역할에 따라 다름
    // - 지자체담당자, 사단담당자, 최종관리자: 대대명
    // - 대대담당자, 지자체(도) 중간관리자: 시군구명
    // - 지자체담당자, 사단담당자, 최종관리자: 대대명
    public string Line2
    {
        get
        {
            return CurrentRole switch
            {
                UserRole.UserMilitary => DistrictName,      // 대대담당자: 시군구명
                UserRole.MiddleLocal => DistrictName,       // 지자체(도): 시군구명
                UserRole.MiddleMilitary => BattalionName,   // 사단담당자: 대대명
                _ => BattalionName                          // 그 외(지자체담당자, 최종관리자): 대대명
            };
        }
    }

    // 셋째 줄: 업체명 (예: 해태제과)
    public string Line3 => CompanyName;
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
