using MilOps.Models;
using MilOps.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MilOps.Services;

/// <summary>
/// 캘린더 데이터 서비스 (Model 레이어)
/// - 백그라운드 스레드에서 데이터 로딩/갱신
/// - 데이터 변경 시 이벤트 발생 → ViewModel이 구독
/// - 월별 캐싱으로 이전/다음 월 이동 시 즉시 표시
/// </summary>
public static class CalendarDataService
{
    // 데이터 로드 완료 이벤트
    public static event Action<CalendarDataLoadedEventArgs>? DataLoaded;

    // 로딩 상태 변경 이벤트
    public static event Action<bool>? LoadingStateChanged;

    // 현재 로딩 중인지 여부 (ViewModel 생성 시 초기 상태 설정용)
    public static bool IsLoading { get; private set; } = false;

    // 월별 캐시 (최대 3개월 유지: 이전달, 현재달, 다음달)
    // 캐시 키: (Year, Month, RegionId, DivisionId, DistrictId, BattalionId)
    private static readonly Dictionary<(int Year, int Month, Guid? RegionId, Guid? DivisionId, Guid? DistrictId, Guid? BattalionId), CalendarDataLoadedEventArgs> _monthlyCache = new();
    private static readonly object _cacheLock = new();
    private const int MaxCacheMonths = 5;  // 최대 캐시 유지 개월 수

    /// <summary>
    /// 로그인 직후 캘린더 데이터 미리 로드 시작 (Preload)
    /// 화면 전환 전에 호출하여 빈 화면 시간 최소화
    /// </summary>
    public static void PreloadCurrentMonth()
    {
        var today = DateTime.Today;
        LoadSchedulesInBackground(today.Year, today.Month);
    }

    /// <summary>
    /// 백그라운드에서 캘린더 일정 로드
    /// UI 스레드와 완전히 독립적으로 실행
    /// 캐시가 있으면 즉시 반환, 없으면 서버에서 로드
    /// </summary>
    /// <param name="year">연도</param>
    /// <param name="month">월</param>
    /// <param name="selectedRegionId">최종관리자(행안부) 필터용 시/도 ID</param>
    /// <param name="selectedDivisionId">최종관리자(육본) 필터용 사단 ID</param>
    /// <param name="selectedDistrictId">중간관리자(지자체도) 필터용 군/구 ID</param>
    /// <param name="selectedBattalionId">중간관리자(사단) 필터용 대대 ID</param>
    /// <param name="forceRefresh">캐시 무시하고 강제 새로고침</param>
    public static void LoadSchedulesInBackground(int year, int month,
        Guid? selectedRegionId = null, Guid? selectedDivisionId = null,
        Guid? selectedDistrictId = null, Guid? selectedBattalionId = null,
        bool forceRefresh = false)
    {
        var cacheKey = (year, month, selectedRegionId, selectedDivisionId, selectedDistrictId, selectedBattalionId);

        // 캐시 확인 (forceRefresh가 아니고 캐시가 있으면 즉시 반환)
        if (!forceRefresh)
        {
            lock (_cacheLock)
            {
                if (_monthlyCache.TryGetValue(cacheKey, out var cachedResult))
                {
                    DataLoaded?.Invoke(cachedResult);
                    return;
                }
            }
        }

        // 백그라운드 스레드에서 실행 (fire-and-forget)
        _ = Task.Run(async () =>
        {
            try
            {
                IsLoading = true;
                // Optimistic UI: 캐시가 없을 때만 로딩 상태 표시
                LoadingStateChanged?.Invoke(true);

                var result = await LoadSchedulesInternalAsync(year, month, selectedRegionId, selectedDivisionId, selectedDistrictId, selectedBattalionId);

                // 캐시에 저장
                lock (_cacheLock)
                {
                    _monthlyCache[cacheKey] = result;
                    TrimCache();  // 캐시 크기 제한
                }

                // 이벤트 발생 → ViewModel이 UI 스레드에서 처리
                DataLoaded?.Invoke(result);

                // 인접 월 미리 로드 (백그라운드에서)
                _ = PreloadAdjacentMonthsAsync(year, month, selectedRegionId, selectedDivisionId, selectedDistrictId, selectedBattalionId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CalendarDataService] [BG] Error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                LoadingStateChanged?.Invoke(false);
            }
        });
    }

    /// <summary>
    /// 인접 월 미리 로드 (백그라운드에서 조용히 실행)
    /// </summary>
    private static async Task PreloadAdjacentMonthsAsync(int year, int month,
        Guid? selectedRegionId, Guid? selectedDivisionId,
        Guid? selectedDistrictId, Guid? selectedBattalionId)
    {
        try
        {
            // 이전 달
            var prevYear = month == 1 ? year - 1 : year;
            var prevMonth = month == 1 ? 12 : month - 1;
            var prevKey = (prevYear, prevMonth, selectedRegionId, selectedDivisionId, selectedDistrictId, selectedBattalionId);

            // 다음 달
            var nextYear = month == 12 ? year + 1 : year;
            var nextMonth = month == 12 ? 1 : month + 1;
            var nextKey = (nextYear, nextMonth, selectedRegionId, selectedDivisionId, selectedDistrictId, selectedBattalionId);

            // 캐시에 없으면 미리 로드
            bool needsPrev, needsNext;
            lock (_cacheLock)
            {
                needsPrev = !_monthlyCache.ContainsKey(prevKey);
                needsNext = !_monthlyCache.ContainsKey(nextKey);
            }

            var tasks = new List<Task>();

            if (needsPrev)
            {
                tasks.Add(LoadAndCacheAsync(prevYear, prevMonth, selectedRegionId, selectedDivisionId, selectedDistrictId, selectedBattalionId));
            }

            if (needsNext)
            {
                tasks.Add(LoadAndCacheAsync(nextYear, nextMonth, selectedRegionId, selectedDivisionId, selectedDistrictId, selectedBattalionId));
            }

            if (tasks.Any())
            {
                await Task.WhenAll(tasks);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarDataService] [PRELOAD] Error: {ex.Message}");
        }
    }

    /// <summary>
    /// 데이터 로드 후 캐시에 저장 (이벤트 발생 없음)
    /// </summary>
    private static async Task LoadAndCacheAsync(int year, int month,
        Guid? selectedRegionId, Guid? selectedDivisionId,
        Guid? selectedDistrictId, Guid? selectedBattalionId)
    {
        var cacheKey = (year, month, selectedRegionId, selectedDivisionId, selectedDistrictId, selectedBattalionId);
        var result = await LoadSchedulesInternalAsync(year, month, selectedRegionId, selectedDivisionId, selectedDistrictId, selectedBattalionId);

        lock (_cacheLock)
        {
            _monthlyCache[cacheKey] = result;
            TrimCache();
        }
    }

    /// <summary>
    /// 캐시 크기 제한 (오래된 항목 삭제)
    /// </summary>
    private static void TrimCache()
    {
        if (_monthlyCache.Count > MaxCacheMonths)
        {
            // 가장 오래된 항목 삭제 (단순히 처음 항목 삭제)
            var oldestKey = _monthlyCache.Keys.First();
            _monthlyCache.Remove(oldestKey);
        }
    }

    /// <summary>
    /// 캐시 초기화 (로그아웃 시 호출)
    /// </summary>
    public static void ClearCache()
    {
        lock (_cacheLock)
        {
            _monthlyCache.Clear();
        }
    }

    /// <summary>
    /// 특정 월의 캐시 무효화 (일정 변경 시 호출)
    /// </summary>
    public static void InvalidateMonth(int year, int month)
    {
        lock (_cacheLock)
        {
            var keysToRemove = _monthlyCache.Keys
                .Where(k => k.Year == year && k.Month == month)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _monthlyCache.Remove(key);
            }
        }
    }

    /// <summary>
    /// 내부 데이터 로딩 로직 (백그라운드 스레드에서 실행)
    /// </summary>
    private static async Task<CalendarDataLoadedEventArgs> LoadSchedulesInternalAsync(
        int year, int month, Guid? selectedRegionId, Guid? selectedDivisionId,
        Guid? selectedDistrictId, Guid? selectedBattalionId)
    {
        if (!AuthService.IsLoggedIn || AuthService.CurrentUser == null)
        {
            return new CalendarDataLoadedEventArgs
            {
                Year = year,
                Month = month,
                Schedules = new List<Schedule>(),
                Days = new List<CalendarDayData>()
            };
        }

        var currentUser = AuthService.CurrentUser;
        var role = AuthService.CurrentUserRole;

        // 현재 월의 시작과 끝
        var monthStart = new DateTime(year, month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        // RPC 파라미터 설정
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

        // 기본 파라미터 (8개 모두 명시적으로 전달 - SQL 함수 시그니처와 일치)
        var rpcParams = new Dictionary<string, object?>
        {
            { "p_user_id", currentUser.Id.ToString() },
            { "p_role", roleString },
            { "p_start_date", monthStart.ToString("yyyy-MM-dd") },
            { "p_end_date", monthEnd.ToString("yyyy-MM-dd") },
            { "p_region_id", null },
            { "p_division_id", null },
            { "p_district_id", null },
            { "p_battalion_id", null }
        };

        // 중간관리자 필터 (관할 구역 + 선택 필터)
        if (role == UserRole.MiddleLocal && currentUser.RegionId.HasValue)
        {
            // 관할 시/도 필터 (필수)
            rpcParams["p_region_id"] = currentUser.RegionId.Value.ToString();
            // 선택한 군/구 필터 (옵션)
            if (selectedDistrictId.HasValue && selectedDistrictId.Value != Guid.Empty)
            {
                rpcParams["p_district_id"] = selectedDistrictId.Value.ToString();
            }
        }
        else if (role == UserRole.MiddleMilitary && currentUser.DivisionId.HasValue)
        {
            // 관할 사단 필터 (필수)
            rpcParams["p_division_id"] = currentUser.DivisionId.Value.ToString();
            // 선택한 대대 필터 (옵션)
            if (selectedBattalionId.HasValue && selectedBattalionId.Value != Guid.Empty)
            {
                rpcParams["p_battalion_id"] = selectedBattalionId.Value.ToString();
            }
        }
        // 최종관리자 필터
        else if (role == UserRole.SuperAdminMois && selectedRegionId.HasValue && selectedRegionId.Value != Guid.Empty)
        {
            rpcParams["p_region_id"] = selectedRegionId.Value.ToString();
        }
        else if (role == UserRole.SuperAdminArmy && selectedDivisionId.HasValue && selectedDivisionId.Value != Guid.Empty)
        {
            rpcParams["p_division_id"] = selectedDivisionId.Value.ToString();
        }

        // RPC 호출 (네트워크 I/O)
        var response = await SupabaseService.Client.Rpc("get_calendar_schedules", rpcParams);

        var rawContent = response.Content ?? "null";

        // JSON 파싱 (CPU 작업)
        // Supabase RPC 응답이 배열이 아닌 객체로 감싸져 있을 수 있음
        var jsonContent = rawContent;
        try
        {
            // 응답이 객체인지 확인 (body, message, data 등의 wrapper가 있을 수 있음)
            var jsonObj = Newtonsoft.Json.Linq.JToken.Parse(jsonContent);
            if (jsonObj is Newtonsoft.Json.Linq.JObject jObj)
            {
                // 가능한 wrapper 속성들 확인
                if (jObj.ContainsKey("body"))
                {
                    jsonContent = jObj["body"]?.ToString() ?? "[]";
                }
                else if (jObj.ContainsKey("message"))
                {
                    jsonContent = jObj["message"]?.ToString() ?? "[]";
                }
                else if (jObj.ContainsKey("data"))
                {
                    jsonContent = jObj["data"]?.ToString() ?? "[]";
                }
            }
        }
        catch (Exception parseEx)
        {
            System.Diagnostics.Debug.WriteLine($"[CalendarDataService] [BG] JSON structure check failed: {parseEx.Message}");
        }

        var dtos = Newtonsoft.Json.JsonConvert.DeserializeObject<List<CalendarScheduleDto>>(jsonContent ?? "[]")
                   ?? new List<CalendarScheduleDto>();

        var schedules = dtos.Select(dto => dto.ToScheduleWithNavigation()).ToList();

        // 날짜별로 그룹핑
        var schedulesByDate = schedules
            .Where(s => s.ReservedDate.HasValue)
            .GroupBy(s => s.ReservedDate!.Value.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        // CalendarDayData 생성 (42개)
        var days = new List<CalendarDayData>();
        var firstDay = new DateTime(year, month, 1);
        var daysInMonth = DateTime.DaysInMonth(year, month);
        int startDayOfWeek = (int)firstDay.DayOfWeek;
        var today = DateTime.Today;

        // 이전 달 빈 칸
        for (int i = 0; i < startDayOfWeek; i++)
        {
            days.Add(new CalendarDayData { Day = 0, IsCurrentMonth = false });
        }

        // 현재 달
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(year, month, day);
            var dayData = new CalendarDayData
            {
                Day = day,
                Date = date,
                IsCurrentMonth = true,
                IsToday = date == today,
                IsSunday = date.DayOfWeek == DayOfWeek.Sunday,
                IsSaturday = date.DayOfWeek == DayOfWeek.Saturday
            };

            if (schedulesByDate.TryGetValue(date, out var daySchedules))
            {
                dayData.Schedules = daySchedules;
                dayData.HasSchedules = true;
                dayData.ScheduleCount = daySchedules.Count;
                dayData.HasConfirmedSchedule = daySchedules.Any(s => s.Status == "confirmed");
                dayData.HasReservedSchedule = daySchedules.Any(s => s.Status == "reserved");

                // 그룹 뱃지 생성 (최종관리자 + 중간관리자용)
                // 일반 사용자(user_local, user_military)는 뱃지 대신 확정/예약 점 표시
                if (role == UserRole.SuperAdminMois || role == UserRole.SuperAdminArmy ||
                    role == UserRole.MiddleLocal || role == UserRole.MiddleMilitary)
                {
                    dayData.IsSuperAdmin = true;  // 뱃지 표시용 플래그 (최종관리자 + 중간관리자)
                    dayData.GroupBadges = CreateGroupBadges(daySchedules, role);
                }
                // 일반 사용자는 IsSuperAdmin = false (기본값)로 유지 → 확정/예약 점 표시
            }

            days.Add(dayData);
        }

        // 다음 달 빈 칸 (42칸 채우기)
        while (days.Count < 42)
        {
            days.Add(new CalendarDayData { Day = 0, IsCurrentMonth = false });
        }

        return new CalendarDataLoadedEventArgs
        {
            Year = year,
            Month = month,
            Role = role,
            Schedules = schedules,
            Days = days
        };
    }

    /// <summary>
    /// 그룹 뱃지 생성 (최종관리자 + 중간관리자용)
    /// - 최종관리자(행안부): 시/도별 그룹핑
    /// - 최종관리자(육본): 사단별 그룹핑
    /// - 중간관리자(지자체도): 군/구별 그룹핑
    /// - 중간관리자(사단): 대대별 그룹핑
    /// </summary>
    private static List<GroupBadgeData> CreateGroupBadges(List<Schedule> schedules, UserRole role)
    {
        var badges = new List<GroupBadgeData>();
        const int maxBadges = 3; // 최대 3개 표시, 초과 시 +N 뱃지 추가

        if (role == UserRole.SuperAdminMois)
        {
            // 행안부: 시/도별 그룹핑
            var allGroups = schedules
                .Where(s => s.LocalUser?.District?.RegionId != null)
                .GroupBy(s => s.LocalUser!.District!.RegionId)
                .Select(g => new GroupBadgeData
                {
                    GroupId = g.Key,
                    Count = g.Count(),
                    Color = CalendarColorHelper.GetColorForGroup(g.Key, role)
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            badges.AddRange(allGroups.Take(maxBadges));
            if (allGroups.Count > maxBadges)
            {
                badges.Add(new GroupBadgeData
                {
                    GroupId = Guid.Empty,
                    Count = allGroups.Count - maxBadges,
                    Color = "#6a6a6a",
                    IsOverflow = true
                });
            }
        }
        else if (role == UserRole.SuperAdminArmy)
        {
            // 육본: 사단별 그룹핑
            var allGroups = schedules
                .Where(s => s.MilitaryUser?.Battalion?.DivisionId != null)
                .GroupBy(s => s.MilitaryUser!.Battalion!.DivisionId)
                .Select(g => new GroupBadgeData
                {
                    GroupId = g.Key,
                    Count = g.Count(),
                    Color = CalendarColorHelper.GetColorForGroup(g.Key, role)
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            badges.AddRange(allGroups.Take(maxBadges));
            if (allGroups.Count > maxBadges)
            {
                badges.Add(new GroupBadgeData
                {
                    GroupId = Guid.Empty,
                    Count = allGroups.Count - maxBadges,
                    Color = "#6a6a6a",
                    IsOverflow = true
                });
            }
        }
        else if (role == UserRole.MiddleLocal)
        {
            // 지자체(도) 중간관리자: 군/구별 그룹핑
            var allGroups = schedules
                .Where(s => s.LocalUser?.DistrictId != null)
                .GroupBy(s => s.LocalUser!.DistrictId!.Value)
                .Select(g => new GroupBadgeData
                {
                    GroupId = g.Key,
                    Count = g.Count(),
                    Color = CalendarColorHelper.GetColorForGroup(g.Key, role)
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            badges.AddRange(allGroups.Take(maxBadges));
            if (allGroups.Count > maxBadges)
            {
                badges.Add(new GroupBadgeData
                {
                    GroupId = Guid.Empty,
                    Count = allGroups.Count - maxBadges,
                    Color = "#6a6a6a",
                    IsOverflow = true
                });
            }
        }
        else if (role == UserRole.MiddleMilitary)
        {
            // 사단 중간관리자: 대대별 그룹핑
            var allGroups = schedules
                .Where(s => s.MilitaryUser?.BattalionId != null)
                .GroupBy(s => s.MilitaryUser!.BattalionId!.Value)
                .Select(g => new GroupBadgeData
                {
                    GroupId = g.Key,
                    Count = g.Count(),
                    Color = CalendarColorHelper.GetColorForGroup(g.Key, role)
                })
                .OrderByDescending(g => g.Count)
                .ToList();

            badges.AddRange(allGroups.Take(maxBadges));
            if (allGroups.Count > maxBadges)
            {
                badges.Add(new GroupBadgeData
                {
                    GroupId = Guid.Empty,
                    Count = allGroups.Count - maxBadges,
                    Color = "#6a6a6a",
                    IsOverflow = true
                });
            }
        }

        return badges;
    }
}

/// <summary>
/// 캘린더 데이터 로드 완료 이벤트 인자
/// </summary>
public class CalendarDataLoadedEventArgs
{
    public int Year { get; set; }
    public int Month { get; set; }
    public UserRole Role { get; set; }
    public List<Schedule> Schedules { get; set; } = new();
    public List<CalendarDayData> Days { get; set; } = new();
}

/// <summary>
/// 캘린더 날짜 데이터 (순수 데이터, UI 독립적)
/// </summary>
public class CalendarDayData
{
    public int Day { get; set; }
    public DateTime Date { get; set; }
    public bool IsCurrentMonth { get; set; }
    public bool IsToday { get; set; }
    public bool IsSunday { get; set; }
    public bool IsSaturday { get; set; }
    public bool HasSchedules { get; set; }
    public int ScheduleCount { get; set; }
    public bool HasConfirmedSchedule { get; set; }
    public bool HasReservedSchedule { get; set; }
    public bool IsSuperAdmin { get; set; }
    public List<Schedule> Schedules { get; set; } = new();
    public List<GroupBadgeData> GroupBadges { get; set; } = new();
}

/// <summary>
/// 그룹 뱃지 데이터 (순수 데이터)
/// </summary>
public class GroupBadgeData
{
    public Guid GroupId { get; set; }
    public int Count { get; set; }
    public string Color { get; set; } = "#808080";
    public bool IsOverflow { get; set; } = false; // "+N" 오버플로우 뱃지 여부
}
