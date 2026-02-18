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
/// 홈 대시보드 ViewModel
/// - 일정 공지(최근 일정), 일정 현황, 오늘의 일정 표시
/// </summary>
public partial class HomeViewModel : ViewModelBase
{
    // === 사용자 정보 ===
    [ObservableProperty]
    private string _userName = "";

    [ObservableProperty]
    private string _userRole = "";

    [ObservableProperty]
    private string _userRegion = "";

    // === 일정 상태별 카운트 ===
    [ObservableProperty]
    private int _allCount = 0;

    [ObservableProperty]
    private int _createdCount = 0;

    [ObservableProperty]
    private int _inputtedCount = 0;

    [ObservableProperty]
    private int _reservedCount = 0;

    [ObservableProperty]
    private int _confirmedCount = 0;

    // === 오늘의 일정 ===
    [ObservableProperty]
    private ObservableCollection<ScheduleListItem> _todaySchedules = new();

    [ObservableProperty]
    private bool _hasTodaySchedules = false;

    // === 최근 일정 ===
    [ObservableProperty]
    private ObservableCollection<ScheduleListItem> _recentSchedules = new();

    [ObservableProperty]
    private bool _hasRecentSchedules = false;

    // === 로딩 상태 ===
    [ObservableProperty]
    private bool _isLoading = false;

    // === 이벤트 (MainView에서 구독) ===
    public event Action<Guid>? OnScheduleSelected;
    public event Action? OnViewAllSchedules;
    public event Action<string>? OnStatusFilterSelected;

    // ScheduleDataService 이벤트 구독 상태
    private bool _isSubscribed = false;

    public HomeViewModel()
    {
        SubscribeToScheduleData();
    }

    /// <summary>
    /// ScheduleDataService의 DataLoaded 이벤트 구독
    /// </summary>
    private void SubscribeToScheduleData()
    {
        if (_isSubscribed) return;
        ScheduleDataService.DataLoaded += OnScheduleDataLoaded;
        _isSubscribed = true;
    }

    /// <summary>
    /// ScheduleDataService에서 데이터 로드 완료 시 호출
    /// </summary>
    private void OnScheduleDataLoaded(ScheduleDataLoadedEventArgs args)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // 상태별 카운트 업데이트
            AllCount = args.StatusCounts.GetValueOrDefault("all", 0);
            CreatedCount = args.StatusCounts.GetValueOrDefault("created", 0);
            InputtedCount = args.StatusCounts.GetValueOrDefault("inputted", 0);
            ReservedCount = args.StatusCounts.GetValueOrDefault("reserved", 0);
            ConfirmedCount = args.StatusCounts.GetValueOrDefault("confirmed", 0);

            // 오늘의 일정 필터링 (예약일이 오늘인 일정)
            var today = DateTime.Today;
            var todayItems = args.Items
                .Where(item => item.Schedule?.ReservedDate?.Date == today)
                .Take(5)
                .ToList();

            TodaySchedules = new ObservableCollection<ScheduleListItem>(todayItems);
            HasTodaySchedules = TodaySchedules.Count > 0;

            // 최근 일정 3개 (역할별 필터링 후 상태 변경일 오름차순)
            var currentUser = AuthService.CurrentUser;
            IEnumerable<ScheduleListItem> filtered = args.Items;

            // user_military / user_local은 자기 관련 일정만 표시
            if (currentUser != null)
            {
                switch (currentUser.Role)
                {
                    case "user_military":
                        filtered = args.Items.Where(item =>
                            item.Schedule?.MilitaryUserId == currentUser.Id ||
                            item.Schedule?.CreatedBy == currentUser.Id);
                        break;
                    case "user_local":
                        filtered = args.Items.Where(item =>
                            item.Schedule?.LocalUserId == currentUser.Id ||
                            item.Schedule?.CreatedBy == currentUser.Id);
                        break;
                    // 그 외 역할(middle_military, viewer_military, middle_local,
                    // super_admin_army, super_admin_mois)은 전체 일정에서 최신 3개
                }
            }

            var recentItems = filtered
                .OrderByDescending(item => item.Schedule?.UpdatedAt)
                .Take(3)
                .ToList();

            RecentSchedules = new ObservableCollection<ScheduleListItem>(recentItems);
            HasRecentSchedules = RecentSchedules.Count > 0;
        });
    }

    /// <summary>
    /// 대시보드 데이터 전체 로드 (탭 진입 시 호출)
    /// </summary>
    public async Task LoadDashboardAsync()
    {
        IsLoading = true;

        try
        {
            // 사용자 정보 (동기)
            var user = AuthService.CurrentUser;
            if (user != null)
            {
                UserName = user.FullDisplayName ?? "";
                UserRole = user.RoleDisplayName ?? "";
            }

            // 병렬 로드: 일정 데이터 + 사용자 지역
            var scheduleTask = LoadScheduleDataAsync();
            var regionTask = LoadUserRegionAsync();

            await Task.WhenAll(scheduleTask, regionTask);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] LoadDashboardAsync error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 일정 데이터 로드 (ScheduleDataService 활용)
    /// </summary>
    private Task LoadScheduleDataAsync()
    {
        var user = AuthService.CurrentUser;
        if (user != null)
        {
            // SWR 패턴: 캐시 있으면 즉시 보여주고 백그라운드에서 새로 로드
            ScheduleDataService.LoadSchedulesInBackground(user);
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// 사용자 소속 지역 로드
    /// </summary>
    private async Task LoadUserRegionAsync()
    {
        try
        {
            var user = AuthService.CurrentUser;
            if (user == null) return;

            var regionString = await Task.Run(async () =>
            {
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

            UserRegion = regionString;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomeViewModel] LoadUserRegionAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// 캐시 초기화 (로그아웃 시)
    /// </summary>
    public void ClearCache()
    {
        UserName = "";
        UserRole = "";
        UserRegion = "";
        AllCount = 0;
        CreatedCount = 0;
        InputtedCount = 0;
        ReservedCount = 0;
        ConfirmedCount = 0;
        TodaySchedules.Clear();
        HasTodaySchedules = false;
        RecentSchedules.Clear();
        HasRecentSchedules = false;
    }

    // === 커맨드 ===

    [RelayCommand]
    private void SelectSchedule(ScheduleListItem? item)
    {
        if (item?.Schedule?.Id != null)
        {
            OnScheduleSelected?.Invoke(item.Schedule.Id);
        }
    }

    [RelayCommand]
    private void ViewAllSchedules()
    {
        OnViewAllSchedules?.Invoke();
    }

    [RelayCommand]
    private void SelectRecentSchedule(ScheduleListItem? item)
    {
        if (item?.Schedule?.Id != null)
        {
            OnScheduleSelected?.Invoke(item.Schedule.Id);
        }
    }

    [RelayCommand]
    private void SelectStatusFilter(string? status)
    {
        if (!string.IsNullOrEmpty(status))
        {
            OnStatusFilterSelected?.Invoke(status);
        }
    }
}
