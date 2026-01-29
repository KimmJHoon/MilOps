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
    /// 현재 월의 일정 로드 (RPC 함수 사용 - 성능 최적화)
    /// </summary>
    public async Task LoadSchedulesAsync()
    {
        if (!AuthService.IsLoggedIn || AuthService.CurrentUser == null)
        {
            System.Diagnostics.Debug.WriteLine("[CalendarViewModel] Not logged in, skipping schedule load");
            return;
        }

        IsLoading = true;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var currentUser = AuthService.CurrentUser;
            var role = AuthService.CurrentUserRole;

            // 현재 월의 시작과 끝
            var monthStart = new DateTime(CurrentYear, CurrentMonth, 1);
            var monthEnd = monthStart.AddMonths(1).AddDays(-1);

            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] Loading schedules via RPC for {CurrentYear}-{CurrentMonth}, Role: {role}");

            // RPC 함수 호출 파라미터 설정
            var roleString = role switch
            {
                UserRole.UserLocal => "user_local",
                UserRole.UserMilitary => "user_military",
                UserRole.MiddleLocal => "middle_local",
                UserRole.MiddleMilitary => "middle_military",
                UserRole.SuperAdminMois => "super_admin_mois",
                UserRole.SuperAdminArmy => "super_admin_army",
                _ => "user_local"
            };

            var rpcParams = new Dictionary<string, object>
            {
                { "p_user_id", currentUser.Id.ToString() },
                { "p_role", roleString },
                { "p_start_date", monthStart.ToString("yyyy-MM-dd") },
                { "p_end_date", monthEnd.ToString("yyyy-MM-dd") }
            };

            // 중간관리자는 region_id 또는 division_id 추가
            if (role == UserRole.MiddleLocal && currentUser.RegionId.HasValue)
            {
                rpcParams["p_region_id"] = currentUser.RegionId.Value.ToString();
            }
            else if (role == UserRole.MiddleMilitary && currentUser.DivisionId.HasValue)
            {
                rpcParams["p_division_id"] = currentUser.DivisionId.Value.ToString();
            }

            // RPC 함수 호출
            var response = await SupabaseService.Client.Rpc("get_calendar_schedules", rpcParams);

            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] RPC response received in {stopwatch.ElapsedMilliseconds}ms");

            // JSON 결과를 DTO로 변환
            var dtos = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CalendarScheduleDto>>(response.Content ?? "[]")
                       ?? new List<CalendarScheduleDto>();

            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] Parsed {dtos.Count} schedules");

            // DTO를 Schedule로 변환 (Navigation Properties 포함)
            var schedules = dtos.Select(dto => dto.ToScheduleWithNavigation()).ToList();

            // 날짜별로 그룹핑
            var schedulesByDate = schedules
                .Where(s => s.ReservedDate.HasValue)
                .GroupBy(s => s.ReservedDate!.Value.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

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

            stopwatch.Stop();
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] Total load time: {stopwatch.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] Error loading schedules: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] Stack trace: {ex.StackTrace}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    #region Legacy Methods (RPC로 대체됨 - 롤백 시 사용)
    /*
    /// <summary>
    /// [DEPRECATED] 지자체담당자용: 본인 담당 일정 조회
    /// RPC 함수 get_calendar_schedules()로 대체됨
    /// </summary>
    private async Task<List<Schedule>> LoadLocalUserSchedulesAsync(Guid userId, DateTime monthStart, DateTime monthEnd)
    {
        // ... 기존 코드 생략 (RPC로 대체)
    }

    /// <summary>
    /// [DEPRECATED] 대대담당자용: 본인 담당 일정 조회
    /// RPC 함수 get_calendar_schedules()로 대체됨
    /// </summary>
    private async Task<List<Schedule>> LoadMilitaryUserSchedulesAsync(Guid userId, DateTime monthStart, DateTime monthEnd)
    {
        // ... 기존 코드 생략 (RPC로 대체)
    }

    /// <summary>
    /// [DEPRECATED] 지자체(도) 중간관리자용: 관할 전체 일정 조회
    /// RPC 함수 get_calendar_schedules()로 대체됨
    /// N+1 쿼리 문제가 있어 8초 이상 소요되던 메서드
    /// </summary>
    private async Task<List<Schedule>> LoadMiddleLocalSchedulesAsync(User currentUser, DateTime monthStart, DateTime monthEnd)
    {
        // ... 기존 코드 생략 (RPC로 대체)
    }

    /// <summary>
    /// [DEPRECATED] 사단담당자용: 관할 전체 일정 조회
    /// RPC 함수 get_calendar_schedules()로 대체됨
    /// N+1 쿼리 문제가 있어 8초 이상 소요되던 메서드
    /// </summary>
    private async Task<List<Schedule>> LoadMiddleMilitarySchedulesAsync(User currentUser, DateTime monthStart, DateTime monthEnd)
    {
        // ... 기존 코드 생략 (RPC로 대체)
    }

    /// <summary>
    /// [DEPRECATED] 최종관리자용: 전체 일정 조회
    /// RPC 함수 get_calendar_schedules()로 대체됨
    /// </summary>
    private async Task<List<Schedule>> LoadAllSchedulesAsync(DateTime monthStart, DateTime monthEnd)
    {
        // ... 기존 코드 생략 (RPC로 대체)
    }

    /// <summary>
    /// [DEPRECATED] 일정에 업체 정보 로드
    /// RPC 함수에서 JOIN으로 처리됨
    /// </summary>
    private async Task LoadCompanyInfoAsync(List<Schedule> schedules)
    {
        // ... 기존 코드 생략 (RPC로 대체)
    }

    /// <summary>
    /// [DEPRECATED] 일정에 사용자 정보 로드 (대대/지자체 표시용)
    /// RPC 함수에서 JOIN으로 처리됨
    /// N+1 쿼리 문제가 있어 성능 저하 원인이었던 메서드
    /// </summary>
    private async Task LoadUserInfoAsync(List<Schedule> schedules)
    {
        // ... 기존 코드 생략 (RPC로 대체)
    }
    */
    #endregion

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
    /// - 행정안전부(super_admin_mois): 시간 / 시도명 / 업체명 (시도별 색상)
    /// - 육군본부(super_admin_army): 시간 / 사단명 / 업체명 (사단별 색상)
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
                // 시/도명 (행정안전부용)
                RegionName = schedule.LocalUser?.District?.Region?.Name ?? "",
                RegionId = schedule.LocalUser?.District?.RegionId,
                // 사단명 (육군본부용)
                DivisionName = schedule.MilitaryUser?.Battalion?.Division?.Name ?? "",
                DivisionId = schedule.MilitaryUser?.Battalion?.DivisionId,
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
/// - 행정안전부(super_admin_mois): 시간 / 시도명 / 업체명 (시도별 색상)
/// - 육군본부(super_admin_army): 시간 / 사단명 / 업체명 (사단별 색상)
/// </summary>
public class CalendarDayScheduleDisplay
{
    public Guid ScheduleId { get; set; }
    public bool IsConfirmed { get; set; }
    public string TimeText { get; set; } = "";           // 예: "10:00"
    public string BattalionName { get; set; } = "";      // 예: "101대대"
    public string DistrictName { get; set; } = "";       // 예: "강남구"
    public string RegionName { get; set; } = "";         // 예: "경기도" (최종관리자용)
    public string DivisionName { get; set; } = "";       // 예: "제1사단" (최종관리자용)
    public string CompanyName { get; set; } = "";        // 예: "해태제과"
    public string StatusColor { get; set; } = "#FF9800";
    public UserRole CurrentRole { get; set; } = UserRole.None;

    // 시/도 및 사단 ID (색상 구분용)
    public Guid? RegionId { get; set; }
    public Guid? DivisionId { get; set; }

    public string ConfirmMark => IsConfirmed ? "✓" : "";

    // 첫 줄: ✓시간 (예: ✓10:00)
    public string Line1 => $"{ConfirmMark}{TimeText}";

    // 둘째 줄: 역할에 따라 다름
    public string Line2
    {
        get
        {
            return CurrentRole switch
            {
                UserRole.UserMilitary => DistrictName,       // 대대담당자: 시군구명
                UserRole.MiddleLocal => DistrictName,        // 지자체(도): 시군구명
                UserRole.MiddleMilitary => BattalionName,    // 사단담당자: 대대명
                UserRole.SuperAdminMois => RegionName,       // 행정안전부: 시도명
                UserRole.SuperAdminArmy => DivisionName,     // 육군본부: 사단명
                _ => BattalionName                           // 그 외(지자체담당자): 대대명
            };
        }
    }

    // 셋째 줄: 업체명 (예: 해태제과)
    public string Line3 => CompanyName;

    // 시/도별 색상 (행정안전부용)
    private static readonly Dictionary<int, string> RegionColors = new()
    {
        { 0, "#E57373" },  // 빨강
        { 1, "#81C784" },  // 초록
        { 2, "#64B5F6" },  // 파랑
        { 3, "#FFB74D" },  // 주황
        { 4, "#BA68C8" },  // 보라
        { 5, "#4DD0E1" },  // 청록
        { 6, "#F06292" },  // 분홍
        { 7, "#AED581" },  // 연두
        { 8, "#FFD54F" },  // 노랑
        { 9, "#90A4AE" },  // 회색
        { 10, "#7986CB" }, // 인디고
        { 11, "#4DB6AC" }, // 틸
        { 12, "#FF8A65" }, // 딥오렌지
        { 13, "#A1887F" }, // 브라운
        { 14, "#9575CD" }, // 딥퍼플
        { 15, "#4FC3F7" }, // 라이트블루
        { 16, "#FFF176" }, // 라이트옐로
    };

    // 사단별 색상 (육군본부용)
    private static readonly Dictionary<int, string> DivisionColors = new()
    {
        { 0, "#42A5F5" },  // 파랑
        { 1, "#66BB6A" },  // 초록
        { 2, "#FFA726" },  // 주황
        { 3, "#AB47BC" },  // 보라
        { 4, "#EF5350" },  // 빨강
        { 5, "#26C6DA" },  // 시안
        { 6, "#EC407A" },  // 핑크
        { 7, "#8D6E63" },  // 브라운
        { 8, "#78909C" },  // 블루그레이
        { 9, "#FFCA28" },  // 앰버
    };

    /// <summary>
    /// 최종관리자용 색상 (시/도별 또는 사단별)
    /// </summary>
    public string GroupColor
    {
        get
        {
            if (CurrentRole == UserRole.SuperAdminMois && RegionId.HasValue)
            {
                int index = Math.Abs(RegionId.Value.GetHashCode()) % RegionColors.Count;
                return RegionColors[index];
            }
            else if (CurrentRole == UserRole.SuperAdminArmy && DivisionId.HasValue)
            {
                int index = Math.Abs(DivisionId.Value.GetHashCode()) % DivisionColors.Count;
                return DivisionColors[index];
            }
            return StatusColor;
        }
    }
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
