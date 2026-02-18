using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Config;
using MilOps.Models;
using MilOps.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace MilOps.ViewModels;

/// <summary>
/// 담당자 관리 탭 - users 테이블의 모든 활성 담당자를 표시
/// [v3] 직접 users 테이블 전체 조회 → 클라이언트에서 조직명 매핑
/// 기존(v2): RPC get_manager_users(p_role) → 역할별 필터링 (일관성 없음)
/// 개선(v3): users 테이블 전체 GET → 모든 유저가 동일 결과 조회
/// </summary>
public partial class ManagerViewModel : ViewModelBase
{
    // === 통계 ===
    [ObservableProperty]
    private int _totalCount;

    [ObservableProperty]
    private int _localCount;

    [ObservableProperty]
    private int _militaryCount;

    // === 필터 ===
    [ObservableProperty]
    private string _filterType = "all"; // all, local, military

    // === 유저 목록 ===
    [ObservableProperty]
    private ObservableCollection<UserListItem> _users = new();

    // === 상태 ===
    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _headerTitle = "담당자 목록";

    // === 채팅 시작 가능 여부 (역할 기반) ===
    [ObservableProperty]
    private bool _canStartChat = false;

    // 채팅 시작 요청 이벤트
    public event Action<ChatListItem>? ChatStartRequested;

    // === HTTP 클라이언트 (재사용) ===
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    // === 내부 캐시 ===
    private List<UserListItem> _allUserItems = new();

    // === 데이터 캐시 (탭 전환 시 네트워크 요청 방지) ===
    private static readonly TimeSpan UserCacheTtl = TimeSpan.FromMinutes(10);
    private DateTime? _userDataCachedAt;
    private bool _isLoadingData = false;

    /// <summary>
    /// 생성자 - 데이터 로드를 즉시 실행하지 않음 (지연 로드).
    /// Manager 탭은 기본 탭이 아니므로, 탭 진입 시에만 데이터 로드.
    /// </summary>
    public ManagerViewModel()
    {
        // 지연 로드: 데이터 로드 안 함
    }

    /// <summary>
    /// 탭 진입 시 호출 - 캐시가 유효하면 즉시 반환
    /// </summary>
    public async Task InitializeAsync()
    {
        await LoadUsersAsync(forceRefresh: false);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        // 수동 새로고침: 강제 갱신
        await LoadUsersAsync(forceRefresh: true);
    }

    /// <summary>
    /// [v3] users 테이블 직접 조회 → 클라이언트에서 조직명 매핑
    /// 모든 유저가 동일한 전체 담당자 목록을 조회함 (역할별 필터링 제거)
    /// </summary>
    private async Task LoadUsersAsync(bool forceRefresh = false)
    {
        // 이미 로딩 중이면 중복 실행 방지
        if (_isLoadingData)
        {
            System.Diagnostics.Debug.WriteLine($"[PERF][ManagerData] LoadUsersAsync 스킵 (이미 로딩 중)");
            return;
        }

        // 캐시가 유효하면 필터만 재적용하고 반환
        if (!forceRefresh && _userDataCachedAt != null &&
            (DateTime.UtcNow - _userDataCachedAt.Value) < UserCacheTtl &&
            _allUserItems.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"[PERF][ManagerData] 캐시 HIT → 필터만 재적용");
            ApplyFilter();
            return;
        }

        var totalSw = System.Diagnostics.Stopwatch.StartNew();
        _isLoadingData = true;
        IsLoading = true;
        ErrorMessage = "";

        try
        {
            var currentUser = AuthService.CurrentUser;
            if (currentUser == null)
            {
                ErrorMessage = "로그인 정보가 없습니다.";
                return;
            }

            // users 테이블 조회 + 조직 데이터 조회를 병렬로 실행
            var (userItems, totalCount, localCount, militaryCount, canStartChat) = await Task.Run(async () =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                System.Diagnostics.Debug.WriteLine($"[PERF][ManagerData] users 테이블 조회 시작 [BG Thread]");

                // 1. HttpClient 직접 REST 호출 (SDK 오버헤드 제거) + 조직 데이터 병렬
                var columns = "id,login_id,name,role,region_id,district_id,division_id,brigade_id,battalion_id,military_rank,deleted_at";
                var url = $"{SupabaseConfig.Url}/rest/v1/users?select={columns}&is_active=eq.true";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("apikey", SupabaseConfig.AnonKey);
                request.Headers.Add("Authorization", $"Bearer {SupabaseConfig.AnonKey}");

                var usersTask = _httpClient.SendAsync(request).ContinueWith(t => t.Result.Content.ReadAsStringAsync()).Unwrap();
                var orgTask = QueryHelper.GetOrgDataAsync();

                await Task.WhenAll(usersTask, orgTask);

                var json = usersTask.Result;
                System.Diagnostics.Debug.WriteLine($"[PERF][ManagerData] HTTP 응답: {sw.ElapsedMilliseconds}ms, {json.Length} bytes");

                // JSON 파싱 (경량 JArray)
                var jArr = JArray.Parse(json);
                var allUsers = new List<User>(jArr.Count);
                foreach (var jt in jArr)
                {
                    var u = new User
                    {
                        Id = jt["id"]!.ToObject<Guid>(),
                        LoginId = jt["login_id"]?.ToString() ?? "",
                        Name = jt["name"]?.ToString() ?? "",
                        Role = jt["role"]?.ToString() ?? "",
                        RegionId = jt["region_id"]?.Type == JTokenType.Null ? null : jt["region_id"]?.ToObject<Guid>(),
                        DistrictId = jt["district_id"]?.Type == JTokenType.Null ? null : jt["district_id"]?.ToObject<Guid>(),
                        DivisionId = jt["division_id"]?.Type == JTokenType.Null ? null : jt["division_id"]?.ToObject<Guid>(),
                        BrigadeId = jt["brigade_id"]?.Type == JTokenType.Null ? null : jt["brigade_id"]?.ToObject<Guid>(),
                        BattalionId = jt["battalion_id"]?.Type == JTokenType.Null ? null : jt["battalion_id"]?.ToObject<Guid>(),
                        MilitaryRank = jt["military_rank"]?.ToString(),
                        DeletedAt = jt["deleted_at"]?.Type == JTokenType.Null ? null : jt["deleted_at"]?.ToObject<DateTime?>()
                    };
                    allUsers.Add(u);
                }
                System.Diagnostics.Debug.WriteLine($"[PERF][ManagerData] users 조회+파싱: {sw.ElapsedMilliseconds}ms, {allUsers.Count}건");

                // 2. 조직 데이터로 Lookup 딕셔너리 생성
                var (regions, districts, divisions, brigades, battalions) = orgTask.Result;
                var regionMap = regions.ToDictionary(r => r.Id, r => r.Name);
                var districtMap = districts.ToDictionary(d => d.Id, d => d.Name);
                var divisionMap = divisions.ToDictionary(d => d.Id, d => d.Name);
                var brigadeMap = brigades.ToDictionary(b => b.Id, b => b.Name);
                var battalionMap = battalions.ToDictionary(b => b.Id, b => b.Name);

                // 3. User → UserListItem 변환 (자기 자신 제외, 삭제된 유저 제외)
                sw.Restart();
                var canChat = currentUser.Role is not ("super_admin_mois" or "super_admin_army" or "viewer_military");

                var items = allUsers
                    .Where(u => u.Id != currentUser.Id && !u.IsDeleted)
                    .Where(u => u.Role is not ("super_admin_mois" or "super_admin_army"))
                    .Select(u => new UserListItem
                    {
                        Id = u.Id,
                        Name = u.IsMilitarySide && !string.IsNullOrEmpty(u.MilitaryRank)
                            ? $"{u.Name} {u.MilitaryRank}"
                            : u.Name,
                        LoginId = u.LoginId,
                        RoleDisplayName = u.RoleDisplayName,
                        IsLocalSide = u.IsLocalSide,
                        Role = u.Role,
                        Affiliation = BuildAffiliation(u, regionMap, districtMap, divisionMap, brigadeMap, battalionMap)
                    })
                    .ToList();

                var total = items.Count;
                var local = items.Count(u => u.IsLocalSide);
                var military = items.Count(u => !u.IsLocalSide);
                System.Diagnostics.Debug.WriteLine($"[PERF][ManagerData] 데이터 변환 ({total}건): {sw.ElapsedMilliseconds}ms");

                return (items, total, local, military, canChat);
            });

            // UI 업데이트는 메인 스레드에서 실행 (ObservableProperty → UI 바인딩)
            _allUserItems = userItems;
            _userDataCachedAt = DateTime.UtcNow;
            CanStartChat = canStartChat;
            TotalCount = totalCount;
            LocalCount = localCount;
            MilitaryCount = militaryCount;
            HeaderTitle = "담당자 목록";
            ApplyFilter();

            System.Diagnostics.Debug.WriteLine($"[PERF][ManagerData] RPC 네트워크 호출: {totalSw.ElapsedMilliseconds}ms, {totalCount}건, 총: {totalSw.ElapsedMilliseconds}ms");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ManagerViewModel] LoadUsersAsync error: {ex.Message}");
            ErrorMessage = "담당자 목록을 불러오는 중 오류가 발생했습니다.";
        }
        finally
        {
            IsLoading = false;
            _isLoadingData = false;
        }
    }

    /// <summary>
    /// 소속 표시 문자열 생성 (조직 데이터 Lookup으로 매핑)
    /// </summary>
    private static string BuildAffiliation(
        User user,
        Dictionary<Guid, string> regionMap,
        Dictionary<Guid, string> districtMap,
        Dictionary<Guid, string> divisionMap,
        Dictionary<Guid, string> brigadeMap,
        Dictionary<Guid, string> battalionMap)
    {
        if (user.IsLocalSide)
        {
            // 행정 측: "강원도 춘천시" 형태
            var parts = new List<string>();
            if (user.RegionId.HasValue && regionMap.TryGetValue(user.RegionId.Value, out var rn))
                parts.Add(rn);
            if (user.DistrictId.HasValue && districtMap.TryGetValue(user.DistrictId.Value, out var dn))
                parts.Add(dn);
            return string.Join(" ", parts);
        }
        else
        {
            // 군 측: "31사단 93여단 작전대대" 형태
            var parts = new List<string>();
            if (user.DivisionId.HasValue && divisionMap.TryGetValue(user.DivisionId.Value, out var dvn))
                parts.Add(dvn);
            if (user.BrigadeId.HasValue && brigadeMap.TryGetValue(user.BrigadeId.Value, out var bn))
                parts.Add(bn);
            if (user.BattalionId.HasValue && battalionMap.TryGetValue(user.BattalionId.Value, out var btn))
                parts.Add(btn);
            return string.Join(" ", parts);
        }
    }

    // === 채팅 시작 ===

    [RelayCommand]
    private void SelectUser(UserListItem? item)
    {
        if (item == null || !CanStartChat) return;

        var chatItem = new ChatListItem
        {
            PartnerId = item.Id,
            PartnerName = item.Name,
            PartnerRole = item.Role,
            LastMessage = "",
            LastMessageAt = DateTimeOffset.UtcNow,
            UnreadCount = 0
        };
        ChatStartRequested?.Invoke(chatItem);
    }

    // === 필터 커맨드 ===

    [RelayCommand]
    private void SetFilterAll()
    {
        FilterType = "all";
        ApplyFilter();
    }

    [RelayCommand]
    private void SetFilterLocal()
    {
        FilterType = "local";
        ApplyFilter();
    }

    [RelayCommand]
    private void SetFilterMilitary()
    {
        FilterType = "military";
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var filtered = FilterType switch
        {
            "local" => _allUserItems.Where(u => u.IsLocalSide).ToList(),
            "military" => _allUserItems.Where(u => !u.IsLocalSide).ToList(),
            _ => _allUserItems
        };

        var sorted = filtered.OrderBy(u => u.RoleDisplayName).ThenBy(u => u.Name).ToList();

        // 디버그: 필터 적용 결과 로그
        System.Diagnostics.Debug.WriteLine($"[ManagerFilter] FilterType={FilterType}, all={_allUserItems.Count}, filtered={sorted.Count}");
        if (FilterType == "local")
        {
            var wrong = sorted.Where(u => !u.IsLocalSide).ToList();
            if (wrong.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ManagerFilter] *** BUG: 행정 필터에 군 {wrong.Count}명 포함 ***");
                foreach (var u in wrong)
                    System.Diagnostics.Debug.WriteLine($"  - {u.Name} role={u.Role} IsLocalSide={u.IsLocalSide}");
            }
        }

        // ObservableCollection을 통째로 교체하여 UI 이벤트 최소화
        Users = new ObservableCollection<UserListItem>(sorted);
    }

    /// <summary>
    /// ViewModel 정리
    /// </summary>
    public void Cleanup()
    {
        // 리소스 정리
    }

    /// <summary>
    /// 캐시 및 데이터 비우기 (로그아웃 시 호출)
    /// </summary>
    public void ClearCache()
    {
        _allUserItems.Clear();
        _userDataCachedAt = null;

        Users.Clear();
        TotalCount = 0;
        LocalCount = 0;
        MilitaryCount = 0;
    }
}

/// <summary>
/// 유저 목록 표시용 아이템
/// </summary>
public class UserListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string LoginId { get; set; } = "";
    public string RoleDisplayName { get; set; } = "";
    public string Affiliation { get; set; } = "";
    public bool IsLocalSide { get; set; }
    public string Role { get; set; } = "";

    /// <summary>
    /// 역할 아이콘 색상 (행정=파랑, 군=초록)
    /// </summary>
    public string RoleColor => IsLocalSide ? "#007AFF" : "#34C759";

    /// <summary>
    /// 역할 아이콘 텍스트
    /// </summary>
    public string RoleIcon => IsLocalSide ? "민" : "군";
}
