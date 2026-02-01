using MilOps.Models;
using MilOps.ViewModels;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MilOps.Services;

/// <summary>
/// 일정 데이터 서비스 (Service 레이어)
/// - RPC를 통한 단일 요청으로 모든 데이터 로드
/// - 백그라운드 스레드에서 데이터 로딩/필터링
/// - 데이터 변경 시 이벤트 발생 -> ViewModel이 구독
/// </summary>
public static class ScheduleDataService
{
    // 데이터 로드 완료 이벤트
    public static event Action<ScheduleDataLoadedEventArgs>? DataLoaded;

    // 로딩 상태 변경 이벤트
    public static event Action<bool>? LoadingStateChanged;

    // 캐시 로드 완료 이벤트 (RPC 사용 시 불필요하지만 호환성 유지)
    #pragma warning disable CS0067
    public static event Action<ScheduleCacheLoadedEventArgs>? CacheLoaded;
    #pragma warning restore CS0067

    // 현재 로딩 중인지 여부
    public static bool IsLoading { get; private set; } = false;

    // 마지막 로드 결과 캐시 (Optimistic UI용)
    private static ScheduleDataLoadedEventArgs? _lastResult = null;
    private static DateTime _lastLoadTime = DateTime.MinValue;

    // 캐시된 데이터 (여러 ViewModel에서 공유) - ConcurrentDictionary로 스레드 안전
    private static ConcurrentDictionary<Guid, string> _companyNames = new();
    private static ConcurrentDictionary<Guid, string> _battalionNames = new();
    private static ConcurrentDictionary<Guid, string> _districtNames = new();
    private static ConcurrentDictionary<Guid, string> _userNames = new();
    private static ConcurrentDictionary<Guid, User> _userCache = new();
    private static ConcurrentDictionary<Guid, Battalion> _battalionCache = new();
    private static ConcurrentDictionary<Guid, District> _districtCache = new();
    private static volatile bool _cacheLoaded = false;

    // 동시 로드 방지용 락
    private static readonly object _loadLock = new();
    private static bool _isLoadingInProgress = false;

    // 캐시 접근자 (읽기 전용)
    public static IReadOnlyDictionary<Guid, string> CompanyNames => _companyNames;
    public static IReadOnlyDictionary<Guid, string> BattalionNames => _battalionNames;
    public static IReadOnlyDictionary<Guid, string> DistrictNames => _districtNames;
    public static IReadOnlyDictionary<Guid, string> UserNames => _userNames;
    public static IReadOnlyDictionary<Guid, User> UserCache => _userCache;
    public static IReadOnlyDictionary<Guid, Battalion> BattalionCache => _battalionCache;
    public static IReadOnlyDictionary<Guid, District> DistrictCache => _districtCache;
    public static bool IsCacheLoaded => _cacheLoaded;

    /// <summary>
    /// 캐시 데이터 미리 로드 (로그인 직후 호출) - 더 이상 별도 호출 불필요
    /// RPC가 모든 데이터를 한 번에 가져오므로 빈 메서드로 유지
    /// </summary>
    public static void PreloadCache()
    {
        // RPC가 모든 데이터를 한 번에 가져오므로 별도 캐시 로드 불필요
        System.Diagnostics.Debug.WriteLine("[ScheduleDataService] PreloadCache called (RPC handles all data)");
    }

    /// <summary>
    /// 백그라운드에서 일정 목록 로드 (Optimistic UI 패턴)
    /// - 캐시가 있으면 즉시 보여주고 (0ms)
    /// - 백그라운드에서 새 데이터 로드 후 자동 업데이트
    /// </summary>
    public static void LoadSchedulesInBackground(User currentUser)
    {
        // 1단계: Optimistic UI - 캐시된 데이터가 있으면 즉시 반환 (0ms)
        if (_lastResult != null)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [Optimistic] Returning cached data immediately ({_lastResult.Schedules.Count} schedules)");
            DataLoaded?.Invoke(_lastResult);
            // 로딩 상태는 true로 설정하되, 이미 데이터가 있으므로 UI에서는 로딩 화면 안 보임
        }

        // 동시 로드 방지: 이미 로드 중이면 스킵
        lock (_loadLock)
        {
            if (_isLoadingInProgress)
            {
                System.Diagnostics.Debug.WriteLine("[ScheduleDataService] [BG] Load already in progress, skipping");
                return;
            }
            _isLoadingInProgress = true;
        }

        // 2단계: 백그라운드에서 최신 데이터 로드 (SWR - Stale While Revalidate)
        _ = Task.Run(async () =>
        {
            try
            {
                IsLoading = true;
                // 캐시가 없을 때만 로딩 상태 표시
                if (_lastResult == null)
                {
                    LoadingStateChanged?.Invoke(true);
                }

                System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [BG] Starting RPC load for user: {currentUser.LoginId}");

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // RPC를 통한 단일 요청으로 모든 데이터 로드
                var result = await LoadSchedulesViaRpcAsync(currentUser);

                stopwatch.Stop();
                System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [BG] RPC load complete in {stopwatch.ElapsedMilliseconds}ms. Schedules: {result.Schedules.Count}");

                // 결과 캐싱
                _lastResult = result;
                _lastLoadTime = DateTime.Now;

                // 데이터가 변경되었거나 첫 로드인 경우에만 이벤트 발생
                DataLoaded?.Invoke(result);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [BG] Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [BG] Stack: {ex.StackTrace}");
            }
            finally
            {
                lock (_loadLock)
                {
                    _isLoadingInProgress = false;
                }
                IsLoading = false;
                LoadingStateChanged?.Invoke(false);
            }
        });
    }

    /// <summary>
    /// RPC를 통한 일정 목록 로드 (단일 요청으로 모든 데이터)
    /// </summary>
    private static async Task<ScheduleDataLoadedEventArgs> LoadSchedulesViaRpcAsync(User currentUser)
    {
        if (!SupabaseService.IsInitialized)
        {
            return new ScheduleDataLoadedEventArgs { Schedules = new List<Schedule>() };
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // RPC 호출 파라미터
        var rpcParams = new Dictionary<string, object>
        {
            { "p_user_id", currentUser.Id.ToString() },
            { "p_role", currentUser.Role }
        };

        // RPC 호출 (단일 요청으로 모든 데이터)
        var response = await SupabaseService.Client.Rpc("get_schedule_list", rpcParams);

        System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [BG] RPC response in {stopwatch.ElapsedMilliseconds}ms");

        // 응답 내용 확인 (디버깅용)
        var rawContent = response.Content ?? "null";
        System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [BG] Raw response (first 500 chars): {rawContent.Substring(0, Math.Min(500, rawContent.Length))}");

        // JSON 파싱 - Supabase RPC 응답이 wrapper로 감싸져 있을 수 있음
        var jsonContent = rawContent;
        try
        {
            var tempToken = JToken.Parse(jsonContent);
            if (tempToken is JObject tempObj)
            {
                // 가능한 wrapper 속성들 확인
                if (tempObj.ContainsKey("body"))
                {
                    jsonContent = tempObj["body"]?.ToString() ?? "{}";
                    System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [BG] Extracted from 'body' wrapper");
                }
                else if (tempObj.ContainsKey("message"))
                {
                    jsonContent = tempObj["message"]?.ToString() ?? "{}";
                    System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [BG] Extracted from 'message' wrapper");
                }
                else if (tempObj.ContainsKey("data"))
                {
                    jsonContent = tempObj["data"]?.ToString() ?? "{}";
                    System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [BG] Extracted from 'data' wrapper");
                }
            }
        }
        catch (Exception parseEx)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [BG] JSON structure check failed: {parseEx.Message}");
        }

        var json = JObject.Parse(jsonContent ?? "{}");

        // 캐시 데이터 업데이트
        UpdateCacheFromRpcResponse(json);

        // 일정 파싱
        var schedules = ParseSchedulesFromJson(json["schedules"]);

        // 역할별 클라이언트 필터링 (middle_military, middle_local 등)
        schedules = FilterSchedulesByRole(schedules, currentUser);

        // 상태별 카운트 계산
        var statusCounts = new Dictionary<string, int>
        {
            ["all"] = schedules.Count,
            ["created"] = schedules.Count(s => s.Status == "created"),
            ["inputted"] = schedules.Count(s => s.Status == "inputted"),
            ["reserved"] = schedules.Count(s => s.Status == "reserved"),
            ["confirmed"] = schedules.Count(s => s.Status == "confirmed")
        };

        // ScheduleListItem 생성
        var items = schedules.Select(s => CreateScheduleListItem(s, currentUser)).ToList();

        stopwatch.Stop();
        System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] [BG] Total processing: {stopwatch.ElapsedMilliseconds}ms");

        return new ScheduleDataLoadedEventArgs
        {
            Schedules = schedules,
            Items = items,
            StatusCounts = statusCounts,
            CurrentUserRole = currentUser.Role
        };
    }

    /// <summary>
    /// RPC 응답에서 캐시 데이터 업데이트 (스레드 안전)
    /// </summary>
    private static void UpdateCacheFromRpcResponse(JObject json)
    {
        try
        {
            // Companies - 새 딕셔너리 생성 후 교체 (atomic)
            var companies = json["companies"] as JObject;
            if (companies != null)
            {
                var newCompanyNames = new ConcurrentDictionary<Guid, string>();
                foreach (var prop in companies.Properties())
                {
                    newCompanyNames[Guid.Parse(prop.Name)] = prop.Value?.ToString() ?? "";
                }
                _companyNames = newCompanyNames;
            }

            // Battalions
            var battalions = json["battalions"] as JObject;
            if (battalions != null)
            {
                var newBattalionNames = new ConcurrentDictionary<Guid, string>();
                var newBattalionCache = new ConcurrentDictionary<Guid, Battalion>();
                foreach (var prop in battalions.Properties())
                {
                    var id = Guid.Parse(prop.Name);
                    var obj = prop.Value as JObject;
                    if (obj != null)
                    {
                        newBattalionNames[id] = obj["name"]?.ToString() ?? "";
                        newBattalionCache[id] = new Battalion
                        {
                            Id = id,
                            Name = obj["name"]?.ToString() ?? "",
                            DivisionId = obj["division_id"]?.ToObject<Guid>() ?? Guid.Empty
                        };
                    }
                }
                _battalionNames = newBattalionNames;
                _battalionCache = newBattalionCache;
            }

            // Districts
            var districts = json["districts"] as JObject;
            if (districts != null)
            {
                var newDistrictNames = new ConcurrentDictionary<Guid, string>();
                var newDistrictCache = new ConcurrentDictionary<Guid, District>();
                foreach (var prop in districts.Properties())
                {
                    var id = Guid.Parse(prop.Name);
                    var obj = prop.Value as JObject;
                    if (obj != null)
                    {
                        newDistrictNames[id] = obj["name"]?.ToString() ?? "";
                        newDistrictCache[id] = new District
                        {
                            Id = id,
                            Name = obj["name"]?.ToString() ?? "",
                            RegionId = obj["region_id"]?.ToObject<Guid>() ?? Guid.Empty
                        };
                    }
                }
                _districtNames = newDistrictNames;
                _districtCache = newDistrictCache;
            }

            // Users
            var users = json["users"] as JObject;
            if (users != null)
            {
                var newUserNames = new ConcurrentDictionary<Guid, string>();
                var newUserCache = new ConcurrentDictionary<Guid, User>();
                foreach (var prop in users.Properties())
                {
                    var id = Guid.Parse(prop.Name);
                    var obj = prop.Value as JObject;
                    if (obj != null)
                    {
                        var name = obj["name"]?.ToString() ?? "";
                        var rank = obj["military_rank"]?.ToString() ?? "";
                        var fullName = string.IsNullOrEmpty(rank) ? name : $"{rank} {name}";

                        newUserNames[id] = fullName;
                        newUserCache[id] = new User
                        {
                            Id = id,
                            Name = name,
                            MilitaryRank = rank,
                            BattalionId = obj["battalion_id"]?.Type == JTokenType.Null ? null : obj["battalion_id"]?.ToObject<Guid>(),
                            DistrictId = obj["district_id"]?.Type == JTokenType.Null ? null : obj["district_id"]?.ToObject<Guid>(),
                            Role = obj["role"]?.ToString() ?? ""
                        };
                    }
                }
                _userNames = newUserNames;
                _userCache = newUserCache;
            }

            _cacheLoaded = true;
            System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] Cache updated: {_companyNames.Count} companies, {_battalionNames.Count} battalions, {_districtNames.Count} districts, {_userNames.Count} users");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] Cache update error: {ex.Message}");
        }
    }

    /// <summary>
    /// JSON에서 일정 목록 파싱
    /// </summary>
    private static List<Schedule> ParseSchedulesFromJson(JToken? schedulesToken)
    {
        var schedules = new List<Schedule>();
        if (schedulesToken == null || schedulesToken.Type == JTokenType.Null)
            return schedules;

        var schedulesArray = schedulesToken as JArray;
        if (schedulesArray == null)
            return schedules;

        foreach (var item in schedulesArray)
        {
            try
            {
                var schedule = new Schedule
                {
                    Id = item["id"]?.ToObject<Guid>() ?? Guid.Empty,
                    CompanyId = item["company_id"]?.ToObject<Guid>() ?? Guid.Empty,
                    LocalUserId = item["local_user_id"]?.ToObject<Guid>() ?? Guid.Empty,
                    MilitaryUserId = item["military_user_id"]?.ToObject<Guid>() ?? Guid.Empty,
                    Status = item["status"]?.ToString() ?? "",
                    LocalConfirmed = item["local_confirmed"]?.ToObject<bool>() ?? false,
                    MilitaryConfirmed = item["military_confirmed"]?.ToObject<bool>() ?? false,
                    CreatedBy = item["created_by"]?.ToObject<Guid>() ?? Guid.Empty,
                    CreatedAt = item["created_at"]?.ToObject<DateTime>() ?? DateTime.MinValue
                };

                // 날짜/시간 파싱
                var reservedDate = item["reserved_date"]?.ToString();
                if (!string.IsNullOrEmpty(reservedDate) && DateTime.TryParse(reservedDate, out var date))
                {
                    schedule.ReservedDate = date;
                }

                var startTime = item["reserved_start_time"]?.ToString();
                if (!string.IsNullOrEmpty(startTime) && TimeSpan.TryParse(startTime, out var start))
                {
                    schedule.ReservedStartTime = start;
                }

                var endTime = item["reserved_end_time"]?.ToString();
                if (!string.IsNullOrEmpty(endTime) && TimeSpan.TryParse(endTime, out var end))
                {
                    schedule.ReservedEndTime = end;
                }

                schedules.Add(schedule);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ScheduleDataService] Schedule parse error: {ex.Message}");
            }
        }

        return schedules;
    }

    /// <summary>
    /// 역할별 클라이언트 필터링 (서버에서 처리하기 어려운 복잡한 조건)
    /// </summary>
    private static List<Schedule> FilterSchedulesByRole(List<Schedule> schedules, User currentUser)
    {
        switch (currentUser.Role)
        {
            case "middle_military":
                // 사단담당자: 자신이 생성한 일정 또는 자신의 사단 소속 대대담당자가 배정된 일정
                if (!currentUser.DivisionId.HasValue)
                {
                    return schedules.Where(s => s.CreatedBy == currentUser.Id).ToList();
                }

                return schedules.Where(s =>
                    s.CreatedBy == currentUser.Id ||
                    IsMilitaryUserInDivision(s.MilitaryUserId, currentUser.DivisionId.Value)
                ).ToList();

            case "middle_local":
                // 지자체(도) 담당자: 예약됨/확정됨 상태 중 자신의 Region 소속
                if (!currentUser.RegionId.HasValue)
                {
                    return schedules.Where(s => s.Status == "reserved" || s.Status == "confirmed").ToList();
                }

                return schedules.Where(s =>
                    (s.Status == "reserved" || s.Status == "confirmed") &&
                    IsLocalUserInRegion(s.LocalUserId, currentUser.RegionId.Value)
                ).ToList();

            default:
                return schedules;
        }
    }

    private static bool IsMilitaryUserInDivision(Guid militaryUserId, Guid divisionId)
    {
        if (!_userCache.TryGetValue(militaryUserId, out var militaryUser))
            return false;

        if (!militaryUser.BattalionId.HasValue)
            return false;

        if (!_battalionCache.TryGetValue(militaryUser.BattalionId.Value, out var battalion))
            return false;

        return battalion.DivisionId == divisionId;
    }

    private static bool IsLocalUserInRegion(Guid localUserId, Guid regionId)
    {
        if (!_userCache.TryGetValue(localUserId, out var localUser))
            return false;

        if (!localUser.DistrictId.HasValue)
            return false;

        if (!_districtCache.TryGetValue(localUser.DistrictId.Value, out var district))
            return false;

        return district.RegionId == regionId;
    }

    /// <summary>
    /// ScheduleListItem 생성 (백그라운드에서 실행)
    /// </summary>
    private static ScheduleListItem CreateScheduleListItem(Schedule schedule, User currentUser)
    {
        var item = new ScheduleListItem
        {
            Schedule = schedule,
            CompanyName = _companyNames.GetValueOrDefault(schedule.CompanyId, "알 수 없는 업체"),
            BattalionName = GetBattalionNameFromUser(schedule.MilitaryUserId),
            DistrictName = GetDistrictNameFromUser(schedule.LocalUserId),
            LocalUserName = _userNames.GetValueOrDefault(schedule.LocalUserId, ""),
            MilitaryUserName = _userNames.GetValueOrDefault(schedule.MilitaryUserId, ""),
            StatusDisplay = schedule.StatusDisplayName,
            StatusColor = schedule.StatusColor,
        };

        // 액션 텍스트 설정
        item.ActionText = GetActionText(schedule, currentUser);
        item.ActionIcon = GetActionIcon(schedule, currentUser);
        item.ShowConfirmStatus = schedule.Status == "reserved";
        item.CanDelete = currentUser.Role == "middle_military" && schedule.Status == "created";

        // 미확정 정보 설정
        SetUnconfirmedInfo(item, schedule, currentUser);

        return item;
    }

    private static string GetBattalionNameFromUser(Guid userId)
    {
        if (_userCache.TryGetValue(userId, out var user) && user.BattalionId.HasValue)
        {
            return _battalionNames.GetValueOrDefault(user.BattalionId.Value, "");
        }
        return "";
    }

    private static string GetDistrictNameFromUser(Guid userId)
    {
        if (_userCache.TryGetValue(userId, out var user) && user.DistrictId.HasValue)
        {
            return _districtNames.GetValueOrDefault(user.DistrictId.Value, "");
        }
        return "";
    }

    private static string GetActionText(Schedule schedule, User currentUser)
    {
        return (schedule.Status, currentUser.Role) switch
        {
            ("created", "user_local") => "일정 입력하기",
            ("inputted", "user_military") => "일정 예약하기",
            ("reserved", "user_local") when !schedule.LocalConfirmed => "확정 필요",
            ("reserved", "user_military") when !schedule.MilitaryConfirmed => "확정 필요",
            ("reserved", _) when schedule.LocalConfirmed && schedule.MilitaryConfirmed => "확정 완료",
            ("reserved", "user_local") when schedule.LocalConfirmed => "상대방 대기",
            ("reserved", "user_military") when schedule.MilitaryConfirmed => "상대방 대기",
            ("confirmed", _) => "상세보기",
            ("created", "middle_military") => "삭제하기",
            _ => "상세보기"
        };
    }

    private static string GetActionIcon(Schedule schedule, User currentUser)
    {
        return "→";
    }

    private static void SetUnconfirmedInfo(ScheduleListItem item, Schedule schedule, User currentUser)
    {
        if (schedule.Status == "created")
        {
            item.UnconfirmedInfo = "양측 미확정";
        }
        else if (schedule.Status == "inputted")
        {
            if (currentUser.Role == "user_local" || currentUser.Role == "middle_local" || currentUser.Role == "super_admin_mois")
            {
                item.UnconfirmedInfo = "대대 미확정";
            }
            else if (currentUser.Role == "user_military" || currentUser.Role == "middle_military" || currentUser.Role == "super_admin_army")
            {
                item.UnconfirmedInfo = "지자체 미확정";
            }
        }
        else if (schedule.Status == "reserved")
        {
            if (currentUser.Role == "user_local")
            {
                item.NeedsMyConfirm = !schedule.LocalConfirmed;
            }
            else if (currentUser.Role == "user_military")
            {
                item.NeedsMyConfirm = !schedule.MilitaryConfirmed;
            }
        }
    }

    /// <summary>
    /// 캐시 초기화 (로그아웃 시 호출)
    /// </summary>
    public static void ClearCache()
    {
        // 새 빈 딕셔너리로 교체 (스레드 안전)
        _companyNames = new ConcurrentDictionary<Guid, string>();
        _battalionNames = new ConcurrentDictionary<Guid, string>();
        _battalionCache = new ConcurrentDictionary<Guid, Battalion>();
        _districtNames = new ConcurrentDictionary<Guid, string>();
        _districtCache = new ConcurrentDictionary<Guid, District>();
        _userNames = new ConcurrentDictionary<Guid, string>();
        _userCache = new ConcurrentDictionary<Guid, User>();
        _cacheLoaded = false;

        // Optimistic UI 캐시도 초기화
        _lastResult = null;
        _lastLoadTime = DateTime.MinValue;

        // 로드 상태 초기화
        lock (_loadLock)
        {
            _isLoadingInProgress = false;
        }

        System.Diagnostics.Debug.WriteLine("[ScheduleDataService] Cache cleared");
    }

    /// <summary>
    /// 캐시 강제 무효화 (데이터 변경 후 호출)
    /// 다음 로드 시 무조건 서버에서 가져옴
    /// </summary>
    public static void InvalidateCache()
    {
        _lastResult = null;
        System.Diagnostics.Debug.WriteLine("[ScheduleDataService] Cache invalidated");
    }

    /// <summary>
    /// 캐시된 결과가 있는지 확인
    /// </summary>
    public static bool HasCachedResult => _lastResult != null;

    /// <summary>
    /// 마지막 로드 시간 (캐시 신선도 확인용)
    /// </summary>
    public static DateTime LastLoadTime => _lastLoadTime;
}

/// <summary>
/// 일정 데이터 로드 완료 이벤트 인자
/// </summary>
public class ScheduleDataLoadedEventArgs
{
    public List<Schedule> Schedules { get; set; } = new();
    public List<ScheduleListItem> Items { get; set; } = new();
    public Dictionary<string, int> StatusCounts { get; set; } = new();
    public string CurrentUserRole { get; set; } = "";
}

/// <summary>
/// 캐시 로드 완료 이벤트 인자
/// </summary>
public class ScheduleCacheLoadedEventArgs
{
    public int CompanyCount { get; set; }
    public int BattalionCount { get; set; }
    public int DistrictCount { get; set; }
    public int UserCount { get; set; }
}
