using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Threading;
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

    // 필터링된 일정 목록 (모달에서 표시용)
    [ObservableProperty]
    private ObservableCollection<CalendarScheduleItem> _filteredSelectedDaySchedules = new();

    [ObservableProperty]
    private bool _hasSelectedDaySchedules = false;

    [ObservableProperty]
    private string _selectedDayTitle = "";

    // 월별 일정 캐시
    private Dictionary<DateTime, List<Schedule>> _scheduleCache = new();

    // === 최종관리자용 필터 ===
    // 현재 사용자 역할
    [ObservableProperty]
    private UserRole _currentRole = UserRole.None;

    // 필터 표시 여부 (최종관리자만)
    [ObservableProperty]
    private bool _showFilter = false;

    // 범례 표시 여부 (최종관리자가 아닐 때만)
    [ObservableProperty]
    private bool _showLegend = true;

    // 시도 필터 목록 (행정안전부용)
    [ObservableProperty]
    private ObservableCollection<RegionFilterItem> _regionFilters = new();

    // 사단 필터 목록 (육군본부용)
    [ObservableProperty]
    private ObservableCollection<DivisionFilterItem> _divisionFilters = new();

    // 선택된 시도 필터
    [ObservableProperty]
    private RegionFilterItem? _selectedRegionFilter;

    // 선택된 사단 필터
    [ObservableProperty]
    private DivisionFilterItem? _selectedDivisionFilter;

    // === 중간관리자용 필터 ===
    // 군/구 필터 목록 (지자체 도 중간관리자용)
    [ObservableProperty]
    private ObservableCollection<DistrictFilterItem> _districtFilters = new();

    // 대대 필터 목록 (사단 중간관리자용)
    [ObservableProperty]
    private ObservableCollection<BattalionFilterItem> _battalionFilters = new();

    // 선택된 군/구 필터
    [ObservableProperty]
    private DistrictFilterItem? _selectedDistrictFilter;

    // 선택된 대대 필터
    [ObservableProperty]
    private BattalionFilterItem? _selectedBattalionFilter;

    // 필터 타입 (region, division, district, battalion)
    [ObservableProperty]
    private string _filterType = "";

    // 필터 타입별 표시 여부
    public bool IsRegionFilter => FilterType == "region";
    public bool IsDivisionFilter => FilterType == "division";
    public bool IsDistrictFilter => FilterType == "district";
    public bool IsBattalionFilter => FilterType == "battalion";

    partial void OnFilterTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsRegionFilter));
        OnPropertyChanged(nameof(IsDivisionFilter));
        OnPropertyChanged(nameof(IsDistrictFilter));
        OnPropertyChanged(nameof(IsBattalionFilter));
    }

    // 네비게이션 이벤트
    public event Action<Guid>? OnScheduleSelected;

    public CalendarViewModel()
    {
        // 42개의 CalendarDay 객체를 미리 생성 (6주 x 7일)
        for (int i = 0; i < 42; i++)
        {
            Days.Add(new CalendarDay { Day = 0, IsCurrentMonth = false });
        }

        var today = DateTime.Today;
        CurrentYear = today.Year;
        CurrentMonth = today.Month;
        UpdateCalendar();

        // === DataService 이벤트 구독 (MVVM 패턴) ===
        CalendarDataService.DataLoaded += OnDataLoaded;
        CalendarDataService.LoadingStateChanged += OnLoadingStateChanged;

        // 현재 로딩 중이면 즉시 로딩 상태 반영 (Preload된 경우)
        if (CalendarDataService.IsLoading)
        {
            IsLoading = true;
        }
    }

    /// <summary>
    /// DataService에서 로딩 상태 변경 시 호출 (백그라운드 → UI 스레드)
    /// </summary>
    private void OnLoadingStateChanged(bool isLoading)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsLoading = isLoading;
        });
    }

    /// <summary>
    /// DataService에서 데이터 로드 완료 시 호출 (백그라운드 → UI 스레드)
    /// </summary>
    private void OnDataLoaded(CalendarDataLoadedEventArgs args)
    {
        // 현재 표시 중인 월과 다르면 무시 (이전 요청 응답)
        if (args.Year != CurrentYear || args.Month != CurrentMonth)
        {
            return;
        }

        // UI 스레드에서 컬렉션 업데이트
        Dispatcher.UIThread.Post(() =>
        {
            // CalendarDayData → CalendarDay 변환 및 컬렉션 교체
            var newDays = new ObservableCollection<CalendarDay>();
            foreach (var data in args.Days)
            {
                var day = new CalendarDay
                {
                    Day = data.Day,
                    Date = data.Date,
                    IsCurrentMonth = data.IsCurrentMonth,
                    IsToday = data.IsToday,
                    IsSunday = data.IsSunday,
                    IsSaturday = data.IsSaturday,
                    HasSchedules = data.HasSchedules,
                    ScheduleCount = data.ScheduleCount,
                    HasConfirmedSchedule = data.HasConfirmedSchedule,
                    HasReservedSchedule = data.HasReservedSchedule,
                    IsSuperAdmin = data.IsSuperAdmin,
                    Schedules = data.Schedules
                };

                // 그룹 뱃지 변환
                foreach (var badge in data.GroupBadges)
                {
                    day.GroupBadges.Add(new GroupBadgeItem
                    {
                        GroupId = badge.GroupId,
                        Count = badge.Count,
                        Color = badge.Color,
                        IsOverflow = badge.IsOverflow
                    });
                }

                newDays.Add(day);
            }

            Days = newDays;
        });
    }

    /// <summary>
    /// 일정 로드 요청 (DataService에 위임)
    /// </summary>
    public void RequestLoadSchedules()
    {
        var selectedRegionId = SelectedRegionFilter?.Id;
        var selectedDivisionId = SelectedDivisionFilter?.Id;
        var selectedDistrictId = SelectedDistrictFilter?.Id;
        var selectedBattalionId = SelectedBattalionFilter?.Id;

        // 백그라운드에서 데이터 로드 (fire-and-forget, 완료되면 이벤트로 통지)
        CalendarDataService.LoadSchedulesInBackground(
            CurrentYear, CurrentMonth, selectedRegionId, selectedDivisionId,
            selectedDistrictId, selectedBattalionId);
    }

    /// <summary>
    /// 필터 초기화 (최종관리자 및 중간관리자용)
    /// </summary>
    public async Task InitializeFiltersAsync()
    {
        CurrentRole = AuthService.CurrentUserRole;

        // 최종관리자인 경우: 필터 표시, 범례 숨김
        if (CurrentRole == UserRole.SuperAdminMois)
        {
            ShowFilter = true;
            ShowLegend = false;
            FilterType = "region";
            await LoadRegionFiltersAsync();
        }
        else if (CurrentRole == UserRole.SuperAdminArmy)
        {
            ShowFilter = true;
            ShowLegend = false;
            FilterType = "division";
            await LoadDivisionFiltersAsync();
        }
        // 중간관리자인 경우: 필터 표시, 범례 숨김
        else if (CurrentRole == UserRole.MiddleLocal)
        {
            ShowFilter = true;
            ShowLegend = false;
            FilterType = "district";
            await LoadDistrictFiltersAsync();
        }
        else if (CurrentRole == UserRole.MiddleMilitary)
        {
            ShowFilter = true;
            ShowLegend = false;
            FilterType = "battalion";
            await LoadBattalionFiltersAsync();
        }
        else
        {
            ShowFilter = false;
            ShowLegend = true;  // 일반 사용자는 범례 표시
        }
    }

    /// <summary>
    /// 시도 필터 목록 로드 (행정안전부용)
    /// </summary>
    private async Task LoadRegionFiltersAsync()
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var response = await client.From<Region>()
                .Order("name", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            RegionFilters.Clear();

            // 전체 옵션 추가
            RegionFilters.Add(new RegionFilterItem
            {
                Id = Guid.Empty,
                Name = "전체",
                Color = "#808080",
                IsSelected = true
            });

            foreach (var region in response.Models)
            {
                RegionFilters.Add(new RegionFilterItem
                {
                    Id = region.Id,
                    Name = region.Name,
                    Color = CalendarColorHelper.GetColorForGroup(region.Id, UserRole.SuperAdminMois),
                    IsSelected = false
                });
            }

            SelectedRegionFilter = RegionFilters.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadRegionFiltersAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 사단 필터 목록 로드 (육군본부용)
    /// </summary>
    private async Task LoadDivisionFiltersAsync()
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var response = await client.From<Division>()
                .Order("name", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            DivisionFilters.Clear();

            // 전체 옵션 추가
            DivisionFilters.Add(new DivisionFilterItem
            {
                Id = Guid.Empty,
                Name = "전체",
                Color = "#808080",
                IsSelected = true
            });

            foreach (var division in response.Models)
            {
                DivisionFilters.Add(new DivisionFilterItem
                {
                    Id = division.Id,
                    Name = division.Name,
                    Color = CalendarColorHelper.GetColorForGroup(division.Id, UserRole.SuperAdminArmy),
                    IsSelected = false
                });
            }

            SelectedDivisionFilter = DivisionFilters.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadDivisionFiltersAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 군/구 필터 목록 로드 (지자체 도 중간관리자용)
    /// 본인이 속한 시/도의 군/구만 표시
    /// </summary>
    private async Task LoadDistrictFiltersAsync()
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var currentUser = AuthService.CurrentUser;
            if (currentUser?.RegionId == null)
            {
                return;
            }

            // 본인 관할 시/도의 군/구만 조회
            var response = await client.From<District>()
                .Filter("region_id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.RegionId.Value.ToString())
                .Order("name", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            DistrictFilters.Clear();

            // 전체 옵션 추가
            DistrictFilters.Add(new DistrictFilterItem
            {
                Id = Guid.Empty,
                Name = "전체",
                Color = "#808080",
                IsSelected = true
            });

            foreach (var district in response.Models)
            {
                DistrictFilters.Add(new DistrictFilterItem
                {
                    Id = district.Id,
                    Name = district.Name,
                    Color = CalendarColorHelper.GetColorForGroup(district.Id, UserRole.MiddleLocal),
                    IsSelected = false
                });
            }

            SelectedDistrictFilter = DistrictFilters.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadDistrictFiltersAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 대대 필터 목록 로드 (사단 중간관리자용)
    /// 본인이 속한 사단의 대대만 표시
    /// </summary>
    private async Task LoadBattalionFiltersAsync()
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var currentUser = AuthService.CurrentUser;
            if (currentUser?.DivisionId == null)
            {
                return;
            }

            // 본인 관할 사단의 대대만 조회
            var response = await client.From<Battalion>()
                .Filter("division_id", Supabase.Postgrest.Constants.Operator.Equals, currentUser.DivisionId.Value.ToString())
                .Order("name", Supabase.Postgrest.Constants.Ordering.Ascending)
                .Get();

            BattalionFilters.Clear();

            // 전체 옵션 추가
            BattalionFilters.Add(new BattalionFilterItem
            {
                Id = Guid.Empty,
                Name = "전체",
                Color = "#808080",
                IsSelected = true
            });

            foreach (var battalion in response.Models)
            {
                BattalionFilters.Add(new BattalionFilterItem
                {
                    Id = battalion.Id,
                    Name = battalion.Name,
                    Color = CalendarColorHelper.GetColorForGroup(battalion.Id, UserRole.MiddleMilitary),
                    IsSelected = false
                });
            }

            SelectedBattalionFilter = BattalionFilters.FirstOrDefault();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarViewModel] LoadBattalionFiltersAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 시도 필터 선택 (모달 내 필터링)
    /// </summary>
    [RelayCommand]
    private void SelectRegionFilter(RegionFilterItem? filter)
    {
        if (filter == null) return;

        // 이전 선택 해제
        foreach (var f in RegionFilters)
            f.IsSelected = false;

        filter.IsSelected = true;
        SelectedRegionFilter = filter;

        // 모달 내 일정 필터링 적용
        ApplyModalFilter();
    }

    /// <summary>
    /// 사단 필터 선택 (모달 내 필터링)
    /// </summary>
    [RelayCommand]
    private void SelectDivisionFilter(DivisionFilterItem? filter)
    {
        if (filter == null) return;

        // 이전 선택 해제
        foreach (var f in DivisionFilters)
            f.IsSelected = false;

        filter.IsSelected = true;
        SelectedDivisionFilter = filter;

        // 모달 내 일정 필터링 적용
        ApplyModalFilter();
    }

    /// <summary>
    /// 군/구 필터 선택 (중간관리자 지자체도용)
    /// </summary>
    [RelayCommand]
    private void SelectDistrictFilter(DistrictFilterItem? filter)
    {
        if (filter == null) return;

        // 이전 선택 해제
        foreach (var f in DistrictFilters)
            f.IsSelected = false;

        filter.IsSelected = true;
        SelectedDistrictFilter = filter;

        // 모달 내 일정 필터링 적용
        ApplyModalFilter();
    }

    /// <summary>
    /// 대대 필터 선택 (중간관리자 사단용)
    /// </summary>
    [RelayCommand]
    private void SelectBattalionFilter(BattalionFilterItem? filter)
    {
        if (filter == null) return;

        // 이전 선택 해제
        foreach (var f in BattalionFilters)
            f.IsSelected = false;

        filter.IsSelected = true;
        SelectedBattalionFilter = filter;

        // 모달 내 일정 필터링 적용
        ApplyModalFilter();
    }

    /// <summary>
    /// 모달 내 필터 적용
    /// </summary>
    private void ApplyModalFilter()
    {
        FilteredSelectedDaySchedules.Clear();

        foreach (var item in SelectedDaySchedules)
        {
            // 최종관리자 필터 적용
            if (CurrentRole == UserRole.SuperAdminMois)
            {
                // 전체 선택 또는 해당 시도와 일치하는 경우
                if (SelectedRegionFilter == null || SelectedRegionFilter.Id == Guid.Empty ||
                    item.RegionId == SelectedRegionFilter.Id)
                {
                    FilteredSelectedDaySchedules.Add(item);
                }
            }
            else if (CurrentRole == UserRole.SuperAdminArmy)
            {
                // 전체 선택 또는 해당 사단과 일치하는 경우
                if (SelectedDivisionFilter == null || SelectedDivisionFilter.Id == Guid.Empty ||
                    item.DivisionId == SelectedDivisionFilter.Id)
                {
                    FilteredSelectedDaySchedules.Add(item);
                }
            }
            // 중간관리자 필터 적용
            else if (CurrentRole == UserRole.MiddleLocal)
            {
                // 전체 선택 또는 해당 군/구와 일치하는 경우
                if (SelectedDistrictFilter == null || SelectedDistrictFilter.Id == Guid.Empty ||
                    item.DistrictId == SelectedDistrictFilter.Id)
                {
                    FilteredSelectedDaySchedules.Add(item);
                }
            }
            else if (CurrentRole == UserRole.MiddleMilitary)
            {
                // 전체 선택 또는 해당 대대와 일치하는 경우
                if (SelectedBattalionFilter == null || SelectedBattalionFilter.Id == Guid.Empty ||
                    item.BattalionId == SelectedBattalionFilter.Id)
                {
                    FilteredSelectedDaySchedules.Add(item);
                }
            }
            else
            {
                // 일반 사용자는 필터 없이 모두 표시
                FilteredSelectedDaySchedules.Add(item);
            }
        }
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

        // 일정이 없는 날짜 (Schedules가 null이거나 비어있으면 HasSchedules로 판단)
        if (!day.HasSchedules || day.Schedules == null || day.Schedules.Count == 0)
        {
            HasSelectedDaySchedules = false;
            SelectedDaySchedules.Clear();
            SelectedDay = null;
            return;
        }

        // 새 날짜 선택
        day.IsSelected = true;
        SelectedDay = day;

        // 일정 목록 패널 업데이트
        UpdateSelectedDaySchedules(day);
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
        FilteredSelectedDaySchedules.Clear();

        // 필터를 "전체"로 초기화
        ResetFilterToAll();
    }

    /// <summary>
    /// 필터를 "전체"로 초기화 (모달 닫을 때 호출)
    /// </summary>
    private void ResetFilterToAll()
    {
        if (CurrentRole == UserRole.SuperAdminMois)
        {
            foreach (var f in RegionFilters)
                f.IsSelected = f.Id == Guid.Empty;
            SelectedRegionFilter = RegionFilters.FirstOrDefault(f => f.Id == Guid.Empty);
        }
        else if (CurrentRole == UserRole.SuperAdminArmy)
        {
            foreach (var f in DivisionFilters)
                f.IsSelected = f.Id == Guid.Empty;
            SelectedDivisionFilter = DivisionFilters.FirstOrDefault(f => f.Id == Guid.Empty);
        }
        else if (CurrentRole == UserRole.MiddleLocal)
        {
            foreach (var f in DistrictFilters)
                f.IsSelected = f.Id == Guid.Empty;
            SelectedDistrictFilter = DistrictFilters.FirstOrDefault(f => f.Id == Guid.Empty);
        }
        else if (CurrentRole == UserRole.MiddleMilitary)
        {
            foreach (var f in BattalionFilters)
                f.IsSelected = f.Id == Guid.Empty;
            SelectedBattalionFilter = BattalionFilters.FirstOrDefault(f => f.Id == Guid.Empty);
        }
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
    /// 캐시 및 상태 초기화 (로그인 전환 시 호출)
    /// </summary>
    public void ClearCache()
    {
        _scheduleCache.Clear();
        SelectedDaySchedules.Clear();
        FilteredSelectedDaySchedules.Clear();
        HasSelectedDaySchedules = false;
        SelectedDay = null;
        // 최종관리자 필터 초기화
        RegionFilters.Clear();
        DivisionFilters.Clear();
        SelectedRegionFilter = null;
        SelectedDivisionFilter = null;
        // 중간관리자 필터 초기화
        DistrictFilters.Clear();
        BattalionFilters.Clear();
        SelectedDistrictFilter = null;
        SelectedBattalionFilter = null;

        // 캘린더 날짜 일정 표시 초기화
        foreach (var day in Days)
        {
            day.Reset();
        }
    }

    /// <summary>
    /// 현재 월의 일정 로드 (DataService에 위임 - MVVM 패턴)
    /// 기존 호출부와의 호환성을 위해 유지, 내부적으로 RequestLoadSchedules 호출
    /// </summary>
    public Task LoadSchedulesAsync()
    {
        if (!AuthService.IsLoggedIn || AuthService.CurrentUser == null)
        {
            return Task.CompletedTask;
        }

        // DataService에 위임 (백그라운드에서 실행, 완료되면 이벤트로 통지)
        RequestLoadSchedules();
        return Task.CompletedTask;
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
        FilteredSelectedDaySchedules.Clear();
        SelectedDayTitle = $"{CurrentMonth}월 {day.Day}일 일정";

        if (day.Schedules == null || !day.Schedules.Any())
        {
            HasSelectedDaySchedules = false;
            return;
        }

        foreach (var schedule in day.Schedules.OrderBy(s => s.ReservedStartTime))
        {
            // 시도/사단 정보 추출
            var regionId = schedule.LocalUser?.District?.RegionId;
            var regionName = schedule.LocalUser?.District?.Region?.Name ?? "";
            var divisionId = schedule.MilitaryUser?.Battalion?.DivisionId;
            var divisionName = schedule.MilitaryUser?.Battalion?.Division?.Name ?? "";
            // 군/구/대대 정보 추출 (중간관리자용)
            var districtId = schedule.LocalUser?.DistrictId;
            var districtName = schedule.LocalUser?.District?.Name ?? "";
            var battalionId = schedule.MilitaryUser?.BattalionId;
            var battalionName = schedule.MilitaryUser?.Battalion?.Name ?? "";

            // 그룹명 (역할에 따라 다름)
            string groupName = "";
            string groupColor = "#808080";
            if (CurrentRole == UserRole.SuperAdminMois && regionId.HasValue)
            {
                groupName = regionName;
                groupColor = CalendarColorHelper.GetColorForGroup(regionId.Value, CurrentRole);
            }
            else if (CurrentRole == UserRole.SuperAdminArmy && divisionId.HasValue)
            {
                groupName = divisionName;
                groupColor = CalendarColorHelper.GetColorForGroup(divisionId.Value, CurrentRole);
            }
            else if (CurrentRole == UserRole.MiddleLocal && districtId.HasValue)
            {
                groupName = districtName;
                groupColor = CalendarColorHelper.GetColorForGroup(districtId.Value, CurrentRole);
            }
            else if (CurrentRole == UserRole.MiddleMilitary && battalionId.HasValue)
            {
                groupName = battalionName;
                groupColor = CalendarColorHelper.GetColorForGroup(battalionId.Value, CurrentRole);
            }

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
                MilitaryConfirmed = schedule.MilitaryConfirmed,
                // 최종관리자용 그룹 정보
                RegionId = regionId,
                RegionName = regionName,
                DivisionId = divisionId,
                DivisionName = divisionName,
                // 중간관리자용 그룹 정보
                DistrictId = districtId,
                DistrictName = districtName,
                BattalionId = battalionId,
                BattalionName = battalionName,
                GroupName = groupName,
                GroupColor = groupColor
            });
        }

        HasSelectedDaySchedules = SelectedDaySchedules.Any();

        // 필터 적용
        ApplyModalFilter();
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

    // 최종관리자용 색상 뱃지 (최대 4개) - deprecated, GroupBadges 사용
    [ObservableProperty]
    private ObservableCollection<string> _uniqueGroupColors = new();

    // 최종관리자용 그룹별 카운트 뱃지
    [ObservableProperty]
    private ObservableCollection<GroupBadgeItem> _groupBadges = new();

    // 최종관리자 여부 (색상 뱃지 표시용)
    [ObservableProperty]
    private bool _isSuperAdmin = false;

    [ObservableProperty]
    private List<Schedule> _schedules = new();

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
        UniqueGroupColors.Clear();
        GroupBadges.Clear();
        IsSuperAdmin = false;
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

        // 최종관리자용: 그룹별 카운트 뱃지 생성 (최대 4개)
        IsSuperAdmin = currentRole == UserRole.SuperAdminMois || currentRole == UserRole.SuperAdminArmy;
        if (IsSuperAdmin)
        {
            UniqueGroupColors.Clear();
            GroupBadges.Clear();

            // 시/도별 또는 사단별로 그룹핑하여 카운트 계산
            // 최대 3개까지 표시하고, 4개 이상이면 "+N" 오버플로우 뱃지 추가
            const int maxBadges = 3;

            if (currentRole == UserRole.SuperAdminMois)
            {
                var regionGroups = Schedules
                    .Where(s => s.LocalUser?.District != null)
                    .GroupBy(s => s.LocalUser!.District!.RegionId)
                    .Select(g => new
                    {
                        Id = g.Key,
                        ScheduleCount = g.Count(),
                        GroupColor = CalendarColorHelper.GetColorForGroup(g.Key, currentRole)
                    })
                    .OrderByDescending(g => g.ScheduleCount)
                    .ToList();

                var displayGroups = regionGroups.Take(maxBadges).ToList();
                foreach (var grp in displayGroups)
                {
                    GroupBadges.Add(new GroupBadgeItem
                    {
                        GroupId = grp.Id,
                        Count = grp.ScheduleCount,
                        Color = grp.GroupColor
                    });
                }

                // 오버플로우 뱃지 (+N)
                if (regionGroups.Count > maxBadges)
                {
                    var overflowCount = regionGroups.Count - maxBadges;
                    GroupBadges.Add(new GroupBadgeItem
                    {
                        GroupId = Guid.Empty,
                        Count = overflowCount,
                        Color = "#6a6a6a",
                        IsOverflow = true
                    });
                }
            }
            else
            {
                var divisionGroups = Schedules
                    .Where(s => s.MilitaryUser?.Battalion != null)
                    .GroupBy(s => s.MilitaryUser!.Battalion!.DivisionId)
                    .Select(g => new
                    {
                        Id = g.Key,
                        ScheduleCount = g.Count(),
                        GroupColor = CalendarColorHelper.GetColorForGroup(g.Key, currentRole)
                    })
                    .OrderByDescending(g => g.ScheduleCount)
                    .ToList();

                var displayGroups = divisionGroups.Take(maxBadges).ToList();
                foreach (var grp in displayGroups)
                {
                    GroupBadges.Add(new GroupBadgeItem
                    {
                        GroupId = grp.Id,
                        Count = grp.ScheduleCount,
                        Color = grp.GroupColor
                    });
                }

                // 오버플로우 뱃지 (+N)
                if (divisionGroups.Count > maxBadges)
                {
                    var overflowCount = divisionGroups.Count - maxBadges;
                    GroupBadges.Add(new GroupBadgeItem
                    {
                        GroupId = Guid.Empty,
                        Count = overflowCount,
                        Color = "#6a6a6a",
                        IsOverflow = true
                    });
                }
            }
        }

        // UI 바인딩 갱신을 위해 핵심 속성 변경 알림 강제 발생
        OnPropertyChanged(nameof(HasSchedules));
        OnPropertyChanged(nameof(GroupBadges));
        OnPropertyChanged(nameof(IsSuperAdmin));
        OnPropertyChanged(nameof(ScheduleDisplays));
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

    /// <summary>
    /// 최종관리자용 색상 (시/도별 또는 사단별 - CalendarColorHelper 사용)
    /// </summary>
    public string GroupColor
    {
        get
        {
            if (CurrentRole == UserRole.SuperAdminMois && RegionId.HasValue)
            {
                return CalendarColorHelper.GetColorForGroup(RegionId.Value, CurrentRole);
            }
            else if (CurrentRole == UserRole.SuperAdminArmy && DivisionId.HasValue)
            {
                return CalendarColorHelper.GetColorForGroup(DivisionId.Value, CurrentRole);
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

    // 최종관리자용 그룹 정보
    public Guid? RegionId { get; set; }
    public string RegionName { get; set; } = "";
    public Guid? DivisionId { get; set; }
    public string DivisionName { get; set; } = "";
    // 중간관리자용 그룹 정보
    public Guid? DistrictId { get; set; }
    public string DistrictName { get; set; } = "";
    public Guid? BattalionId { get; set; }
    public string BattalionName { get; set; } = "";
    public string GroupName { get; set; } = "";
    public string GroupColor { get; set; } = "#808080";

    // 그룹명이 있는지 여부 (XAML 바인딩용)
    public bool HasGroupName => !string.IsNullOrEmpty(GroupName);

    public string ConfirmStatusText
    {
        get
        {
            if (IsConfirmed) return "● 확정완료";
            if (LocalConfirmed && !MilitaryConfirmed) return "[민]● [군]○";
            if (!LocalConfirmed && MilitaryConfirmed) return "[민]○ [군]●";
            return "○ 확정대기";
        }
    }
}

/// <summary>
/// 시도 필터 아이템 (행정안전부용)
/// </summary>
public partial class RegionFilterItem : ObservableObject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#808080";

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// 사단 필터 아이템 (육군본부용)
/// </summary>
public partial class DivisionFilterItem : ObservableObject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#808080";

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// 군/구 필터 아이템 (지자체 도 중간관리자용)
/// </summary>
public partial class DistrictFilterItem : ObservableObject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#808080";

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// 대대 필터 아이템 (사단 중간관리자용)
/// </summary>
public partial class BattalionFilterItem : ObservableObject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#808080";

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// 최종관리자용 그룹별 카운트 뱃지 (캘린더 셀에 표시)
/// </summary>
public class GroupBadgeItem
{
    public Guid GroupId { get; set; }
    public int Count { get; set; }
    public string Color { get; set; } = "#808080";
    public bool IsOverflow { get; set; } = false; // "+N" 오버플로우 뱃지 여부
    public string CountText => IsOverflow ? $"+{Count}" : Count.ToString();
}

/// <summary>
/// 색상 헬퍼 클래스 (시/도 및 사단별 색상 통합 관리)
/// </summary>
public static class CalendarColorHelper
{
    private static readonly string[] RegionColors =
    {
        "#E57373", "#81C784", "#64B5F6", "#FFB74D", "#BA68C8",
        "#4DD0E1", "#F06292", "#AED581", "#FFD54F", "#90A4AE",
        "#7986CB", "#4DB6AC", "#FF8A65", "#A1887F", "#9575CD",
        "#4FC3F7", "#FFF176"
    };

    private static readonly string[] DivisionColors =
    {
        "#42A5F5", "#66BB6A", "#FFA726", "#AB47BC", "#EF5350",
        "#26C6DA", "#EC407A", "#8D6E63", "#78909C", "#FFCA28"
    };

    /// <summary>
    /// 그룹 ID에 해당하는 색상 반환 (필터와 뱃지에서 동일하게 사용)
    /// </summary>
    public static string GetColorForGroup(Guid groupId, UserRole role)
    {
        int index = Math.Abs(groupId.GetHashCode()) % 17;
        if (role == UserRole.SuperAdminMois)
        {
            return RegionColors[index % RegionColors.Length];
        }
        else
        {
            return DivisionColors[index % DivisionColors.Length];
        }
    }
}
