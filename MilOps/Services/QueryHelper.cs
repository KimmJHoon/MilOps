using MilOps.Config;
using MilOps.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace MilOps.Services;

/// <summary>
/// Supabase 쿼리 헬퍼 — 반복되는 From<T>().Filter().Get() 패턴 통합
/// </summary>
public static class QueryHelper
{
    // ========== 경량 REST/RPC 직접 호출 (Supabase SDK JIT 오버헤드 회피) ==========
    // 콜드 스타트에서 From<T>().Get()은 리플렉션+직렬화 JIT로 15~19초 소요.
    // RPC 1회 호출 + JObject 직접 파싱은 동일 데이터를 ~200ms에 로드 가능.
    private static readonly HttpClient _restClient = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// <summary>
    /// Supabase RPC 호출 (POST /rest/v1/rpc/{functionName})
    /// 8개 개별 REST 요청 → RPC 1회로 통합 (네트워크 왕복 대폭 절감)
    /// </summary>
    private static async Task<string> RpcCallAsync(string functionName)
    {
        var url = $"{SupabaseConfig.Url}/rest/v1/rpc/{functionName}";

        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("apikey", SupabaseConfig.AnonKey);
        request.Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");

        // RLS 대응: 로그인 세션의 access token 우선 사용
        string? accessToken = null;
        try
        {
            if (SupabaseService.IsInitialized)
                accessToken = SupabaseService.Client.Auth.CurrentSession?.AccessToken;
        }
        catch { /* 초기화 전이면 무시 */ }

        request.Headers.Add("Authorization",
            $"Bearer {(string.IsNullOrEmpty(accessToken) ? SupabaseConfig.AnonKey : accessToken)}");

        var response = await _restClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            System.Diagnostics.Debug.WriteLine($"[RPC] {functionName} 호출 실패: HTTP {(int)response.StatusCode} - {body}");
            throw new HttpRequestException($"RPC {functionName} failed: {(int)response.StatusCode} - {body}");
        }

        return body;
    }

    // --- RPC 일괄 호출 + 파싱 메서드들 ---

    /// <summary>
    /// get_org_cache_data() RPC 1회 호출로 5개 조직 테이블 일괄 조회
    /// (네트워크 왕복 5회 → 1회)
    /// </summary>
    private static async Task<(List<Region>, List<District>, List<Division>, List<Brigade>, List<Battalion>)> FetchOrgDataViaRpc()
    {
        var json = await RpcCallAsync("get_org_cache_data");
        var obj = JObject.Parse(json);

        var regions = ParseRegions(obj["regions"] as JArray ?? new JArray());
        var districts = ParseDistricts(obj["districts"] as JArray ?? new JArray());
        var divisions = ParseDivisions(obj["divisions"] as JArray ?? new JArray());
        var brigades = ParseBrigades(obj["brigades"] as JArray ?? new JArray());
        var battalions = ParseBattalions(obj["battalions"] as JArray ?? new JArray());

        return (regions, districts, divisions, brigades, battalions);
    }

    /// <summary>
    /// get_biz_cache_data() RPC 1회 호출로 3개 업무 테이블 일괄 조회
    /// (네트워크 왕복 3회 → 1회)
    /// </summary>
    private static async Task<(List<Company>, List<User>, HashSet<Guid>)> FetchBizDataViaRpc()
    {
        var json = await RpcCallAsync("get_biz_cache_data");
        var obj = JObject.Parse(json);

        var companies = ParseCompanies(obj["companies"] as JArray ?? new JArray());
        var users = ParseUsers(obj["users"] as JArray ?? new JArray());

        var idsArr = obj["schedule_company_ids"] as JArray ?? new JArray();
        var matchedIds = new HashSet<Guid>(idsArr.Count);
        foreach (var jt in idsArr)
        {
            if (jt.Type != JTokenType.Null)
                matchedIds.Add(jt.ToObject<Guid>());
        }

        return (companies, users, matchedIds);
    }

    // --- JSON 파싱 헬퍼 (row_to_json 결과 → Model 변환) ---

    private static List<Region> ParseRegions(JArray arr)
    {
        var list = new List<Region>(arr.Count);
        foreach (var jt in arr)
        {
            list.Add(new Region
            {
                Id = jt["id"]!.ToObject<Guid>(),
                Name = jt["name"]?.ToString() ?? "",
                IsActive = jt["is_active"]?.ToObject<bool>() ?? true,
                CreatedAt = jt["created_at"]?.ToObject<DateTime>() ?? default
            });
        }
        return list;
    }

    private static List<District> ParseDistricts(JArray arr)
    {
        var list = new List<District>(arr.Count);
        foreach (var jt in arr)
        {
            list.Add(new District
            {
                Id = jt["id"]!.ToObject<Guid>(),
                RegionId = jt["region_id"]!.ToObject<Guid>(),
                Name = jt["name"]?.ToString() ?? "",
                IsActive = jt["is_active"]?.ToObject<bool>() ?? true,
                CreatedAt = jt["created_at"]?.ToObject<DateTime>() ?? default
            });
        }
        return list;
    }

    private static List<Division> ParseDivisions(JArray arr)
    {
        var list = new List<Division>(arr.Count);
        foreach (var jt in arr)
        {
            list.Add(new Division
            {
                Id = jt["id"]!.ToObject<Guid>(),
                Name = jt["name"]?.ToString() ?? "",
                IsActive = jt["is_active"]?.ToObject<bool>() ?? true,
                CreatedAt = jt["created_at"]?.ToObject<DateTime>() ?? default
            });
        }
        return list;
    }

    private static List<Brigade> ParseBrigades(JArray arr)
    {
        var list = new List<Brigade>(arr.Count);
        foreach (var jt in arr)
        {
            list.Add(new Brigade
            {
                Id = jt["id"]!.ToObject<Guid>(),
                DivisionId = jt["division_id"]!.ToObject<Guid>(),
                Name = jt["name"]?.ToString() ?? "",
                IsActive = jt["is_active"]?.ToObject<bool>() ?? true,
                CreatedAt = jt["created_at"]?.ToObject<DateTime>() ?? default
            });
        }
        return list;
    }

    private static List<Battalion> ParseBattalions(JArray arr)
    {
        var list = new List<Battalion>(arr.Count);
        foreach (var jt in arr)
        {
            list.Add(new Battalion
            {
                Id = jt["id"]!.ToObject<Guid>(),
                DivisionId = jt["division_id"]!.ToObject<Guid>(),
                BrigadeId = jt["brigade_id"]?.Type == JTokenType.Null ? null : jt["brigade_id"]?.ToObject<Guid>(),
                Name = jt["name"]?.ToString() ?? "",
                IsActive = jt["is_active"]?.ToObject<bool>() ?? true,
                CreatedAt = jt["created_at"]?.ToObject<DateTime>() ?? default
            });
        }
        return list;
    }

    private static List<Company> ParseCompanies(JArray arr)
    {
        var list = new List<Company>(arr.Count);
        foreach (var jt in arr)
        {
            list.Add(new Company
            {
                Id = jt["id"]!.ToObject<Guid>(),
                Name = jt["name"]?.ToString() ?? "",
                Address = jt["address"]?.ToString() ?? "",
                Products = jt["products"]?.Type == JTokenType.Null ? null : jt["products"]?.ToString(),
                DistrictId = jt["district_id"]!.ToObject<Guid>(),
                CreatedBy = jt["created_by"]!.ToObject<Guid>(),
                IsActive = jt["is_active"]?.ToObject<bool>() ?? true,
                DeletedAt = jt["deleted_at"]?.Type == JTokenType.Null ? null : jt["deleted_at"]?.ToObject<DateTime?>(),
                DeletedBy = jt["deleted_by"]?.Type == JTokenType.Null ? null : jt["deleted_by"]?.ToObject<Guid?>(),
                CreatedAt = jt["created_at"]?.ToObject<DateTime>() ?? default,
                UpdatedAt = jt["updated_at"]?.ToObject<DateTime>() ?? default
            });
        }
        return list;
    }

    private static List<User> ParseUsers(JArray arr)
    {
        var list = new List<User>(arr.Count);
        foreach (var jt in arr)
        {
            list.Add(new User
            {
                Id = jt["id"]!.ToObject<Guid>(),
                LoginId = jt["login_id"]?.ToString() ?? "",
                Name = jt["name"]?.ToString() ?? "",
                Email = jt["email"]?.Type == JTokenType.Null ? null : jt["email"]?.ToString(),
                Role = jt["role"]?.ToString() ?? "",
                RegionId = jt["region_id"]?.Type == JTokenType.Null ? null : jt["region_id"]?.ToObject<Guid>(),
                DistrictId = jt["district_id"]?.Type == JTokenType.Null ? null : jt["district_id"]?.ToObject<Guid>(),
                DivisionId = jt["division_id"]?.Type == JTokenType.Null ? null : jt["division_id"]?.ToObject<Guid>(),
                BrigadeId = jt["brigade_id"]?.Type == JTokenType.Null ? null : jt["brigade_id"]?.ToObject<Guid>(),
                BattalionId = jt["battalion_id"]?.Type == JTokenType.Null ? null : jt["battalion_id"]?.ToObject<Guid>(),
                MilitaryRank = jt["military_rank"]?.Type == JTokenType.Null ? null : jt["military_rank"]?.ToString(),
                IsActive = jt["is_active"]?.ToObject<bool>() ?? true,
                DeletedAt = jt["deleted_at"]?.Type == JTokenType.Null ? null : jt["deleted_at"]?.ToObject<DateTime?>(),
                CreatedAt = jt["created_at"]?.ToObject<DateTime>() ?? default,
                UpdatedAt = jt["updated_at"]?.ToObject<DateTime>() ?? default
            });
        }
        return list;
    }

    // ========== Supabase SDK 래퍼 (캐시 외 개별 쿼리용, 기존 호환) ==========

    /// <summary>
    /// is_active = true 필터로 엔티티 목록 조회
    /// </summary>
    public static async Task<List<T>> GetActiveAsync<T>() where T : Supabase.Postgrest.Models.BaseModel, new()
    {
        var response = await SupabaseService.Client
            .From<T>()
            .Filter("is_active", Operator.Equals, "true")
            .Get();
        return response.Models;
    }

    /// <summary>
    /// 전체 엔티티 목록 조회 (필터 없음)
    /// </summary>
    public static async Task<List<T>> GetAllAsync<T>() where T : Supabase.Postgrest.Models.BaseModel, new()
    {
        var response = await SupabaseService.Client
            .From<T>()
            .Get();
        return response.Models;
    }

    /// <summary>
    /// 특정 필드로 필터링하여 조회
    /// </summary>
    public static async Task<List<T>> GetByFilterAsync<T>(string column, string value) where T : Supabase.Postgrest.Models.BaseModel, new()
    {
        var response = await SupabaseService.Client
            .From<T>()
            .Filter(column, Operator.Equals, value)
            .Get();
        return response.Models;
    }

    /// <summary>
    /// 특정 필드가 NULL인 레코드만 조회
    /// </summary>
    public static async Task<List<T>> GetWhereNullAsync<T>(string column) where T : Supabase.Postgrest.Models.BaseModel, new()
    {
        var response = await SupabaseService.Client
            .From<T>()
            .Filter(column, Operator.Is, "null")
            .Get();
        return response.Models;
    }

    /// <summary>
    /// is_active 필터로 엔티티 조회 Task 반환 (Task.WhenAll 병렬화용)
    /// </summary>
    public static Task<List<T>> GetActiveTask<T>() where T : Supabase.Postgrest.Models.BaseModel, new()
    {
        return GetActiveAsync<T>();
    }

    /// <summary>
    /// 전체 엔티티 조회 Task 반환 (Task.WhenAll 병렬화용)
    /// </summary>
    public static Task<List<T>> GetAllTask<T>() where T : Supabase.Postgrest.Models.BaseModel, new()
    {
        return GetAllAsync<T>();
    }

    // ========== 조직 데이터 공유 캐시 (ManagerData + MainViewModel 공용) ==========

    private static List<Region>? _cachedRegions;
    private static List<District>? _cachedDistricts;
    private static List<Division>? _cachedDivisions;
    private static List<Brigade>? _cachedBrigades;
    private static List<Battalion>? _cachedBattalions;
    private static readonly object _orgCacheLock = new();
    private static Task? _orgPreloadTask;

    public static bool IsOrgDataCached
    {
        get { lock (_orgCacheLock) return _cachedRegions != null; }
    }

    /// <summary>
    /// 파일 캐시에서만 조직 데이터 로드 (Supabase Client 불필요).
    /// SplashActivity에서 호출 — 이 시점에서는 SupabaseService가 초기화 전이므로
    /// 서버 호출 없이 파일 캐시만 메모리에 올림.
    /// </summary>
    public static void PreloadOrgDataFromFileOnly()
    {
        lock (_orgCacheLock)
        {
            if (_cachedRegions != null) return;

            var (fileRegions, fileDistricts, fileDivisions, fileBrigades, fileBattalions) = OrgCacheFileService.LoadFromFile();
            if (fileRegions != null)
            {
                _cachedRegions = fileRegions;
                _cachedDistricts = fileDistricts;
                _cachedDivisions = fileDivisions;
                _cachedBrigades = fileBrigades;
                _cachedBattalions = fileBattalions;
                System.Diagnostics.Debug.WriteLine("[PERF][OrgCache] 파일 캐시로 즉시 메모리 세팅 완료 (Splash)");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[OrgCache] 파일 캐시 없음 (첫 실행)");
            }
        }
    }

    /// <summary>
    /// 조직 데이터 Preload (로그인 직후 호출, fire-and-forget)
    /// 1단계: 파일 캐시에서 즉시 로드 (~10ms) → 메모리 캐시 설정
    /// 2단계: 백그라운드에서 서버 RPC 1회로 갱신 (stale-while-revalidate)
    /// </summary>
    public static void PreloadOrgData()
    {
        lock (_orgCacheLock)
        {
            if (_cachedRegions != null || _orgPreloadTask != null) return;

            // 1단계: 파일 캐시에서 즉시 로드 (동기, ~10ms)
            var (fileRegions, fileDistricts, fileDivisions, fileBrigades, fileBattalions) = OrgCacheFileService.LoadFromFile();
            if (fileRegions != null)
            {
                _cachedRegions = fileRegions;
                _cachedDistricts = fileDistricts;
                _cachedDivisions = fileDivisions;
                _cachedBrigades = fileBrigades;
                _cachedBattalions = fileBattalions;
                System.Diagnostics.Debug.WriteLine("[PERF][OrgCache] 파일 캐시로 즉시 메모리 세팅 완료");
            }

            // 2단계: 백그라운드에서 RPC 1회 호출로 서버 최신 데이터 갱신
            _orgPreloadTask = Task.Run(async () =>
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    System.Diagnostics.Debug.WriteLine("[PERF][OrgCache] Preload 시작 (RPC 서버 갱신)");

                    var (regions, districts, divisions, brigades, battalions) = await FetchOrgDataViaRpc();

                    lock (_orgCacheLock)
                    {
                        _cachedRegions = regions;
                        _cachedDistricts = districts;
                        _cachedDivisions = divisions;
                        _cachedBrigades = brigades;
                        _cachedBattalions = battalions;
                    }

                    System.Diagnostics.Debug.WriteLine($"[PERF][OrgCache] Preload 완료 (RPC 1회): {sw.ElapsedMilliseconds}ms " +
                        $"(Region={regions.Count}, District={districts.Count}, " +
                        $"Division={divisions.Count}, Brigade={brigades.Count}, " +
                        $"Battalion={battalions.Count})");

                    OrgCacheFileService.SaveToFile(regions, districts, divisions, brigades, battalions);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OrgCache] Preload error: {ex.Message}");
                    lock (_orgCacheLock)
                    {
                        if (_cachedRegions == null) _orgPreloadTask = null;
                    }
                }
            });
        }
    }

    /// <summary>
    /// 캐시된 조직 데이터 반환 (캐시 없으면 서버에서 로드)
    /// </summary>
    public static async Task<(List<Region> Regions, List<District> Districts, List<Division> Divisions, List<Brigade> Brigades, List<Battalion> Battalions)> GetOrgDataAsync()
    {
        // 1. 메모리 캐시 우선 확인
        lock (_orgCacheLock)
        {
            if (_cachedRegions != null)
            {
                System.Diagnostics.Debug.WriteLine("[PERF][OrgCache] 캐시 HIT (즉시 반환)");
                return (_cachedRegions!, _cachedDistricts!, _cachedDivisions!, _cachedBrigades!, _cachedBattalions!);
            }
        }

        // 2. Preload 진행 중이면 완료 대기
        Task? pending;
        lock (_orgCacheLock) { pending = _orgPreloadTask; }
        if (pending != null)
        {
            try { await pending; }
            catch { /* Preload 에러는 이미 로깅됨 */ }
        }

        // 3. Preload 완료 후 캐시 재확인
        lock (_orgCacheLock)
        {
            if (_cachedRegions != null)
            {
                System.Diagnostics.Debug.WriteLine("[PERF][OrgCache] 캐시 HIT (Preload 후)");
                return (_cachedRegions!, _cachedDistricts!, _cachedDivisions!, _cachedBrigades!, _cachedBattalions!);
            }
        }

        // 4. 캐시 없으면 RPC 1회로 직접 로드
        Task loadTask;
        lock (_orgCacheLock)
        {
            if (_cachedRegions != null)
                return (_cachedRegions!, _cachedDistricts!, _cachedDivisions!, _cachedBrigades!, _cachedBattalions!);
            if (_orgPreloadTask != null)
            {
                loadTask = _orgPreloadTask;
            }
            else
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                System.Diagnostics.Debug.WriteLine("[PERF][OrgCache] 캐시 MISS → RPC 서버 로드");
                loadTask = Task.Run(async () =>
                {
                    var (regions, districts, divisions, brigades, battalions) = await FetchOrgDataViaRpc();

                    lock (_orgCacheLock)
                    {
                        _cachedRegions = regions;
                        _cachedDistricts = districts;
                        _cachedDivisions = divisions;
                        _cachedBrigades = brigades;
                        _cachedBattalions = battalions;
                    }
                    System.Diagnostics.Debug.WriteLine($"[PERF][OrgCache] RPC 서버 로드 완료: {sw.ElapsedMilliseconds}ms");

                    OrgCacheFileService.SaveToFile(regions, districts, divisions, brigades, battalions);
                });
                _orgPreloadTask = loadTask;
            }
        }

        await loadTask;

        lock (_orgCacheLock)
        {
            return (_cachedRegions!, _cachedDistricts!, _cachedDivisions!, _cachedBrigades!, _cachedBattalions!);
        }
    }

    /// <summary>
    /// 조직 데이터 캐시 초기화 (로그아웃 시 호출)
    /// </summary>
    public static void ClearOrgCache()
    {
        lock (_orgCacheLock)
        {
            _cachedRegions = null;
            _cachedDistricts = null;
            _cachedDivisions = null;
            _cachedBrigades = null;
            _cachedBattalions = null;
            _orgPreloadTask = null;
        }
        OrgCacheFileService.ClearCache();
    }

    // ========== 업무 데이터 공유 캐시 (Company + User + MatchedCompanyIds) ==========

    private static List<Company>? _cachedCompanies;
    private static List<User>? _cachedUsers;
    private static HashSet<Guid>? _cachedMatchedCompanyIds;
    private static readonly object _bizCacheLock = new();
    private static Task? _bizPreloadTask;

    public static bool IsBizDataCached
    {
        get { lock (_bizCacheLock) return _cachedCompanies != null; }
    }

    /// <summary>
    /// 업무 데이터 Preload (로그인 직후 호출, fire-and-forget)
    /// RPC 1회 호출로 Company, User, MatchedCompanyIds 일괄 로드.
    /// </summary>
    public static void PreloadBizData()
    {
        lock (_bizCacheLock)
        {
            if (_cachedCompanies != null || _bizPreloadTask != null) return;

            _bizPreloadTask = Task.Run(async () =>
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    System.Diagnostics.Debug.WriteLine("[PERF][BizCache] Preload 시작 (RPC 1회)");

                    var (companies, users, matchedIds) = await FetchBizDataViaRpc();

                    lock (_bizCacheLock)
                    {
                        _cachedCompanies = companies;
                        _cachedUsers = users;
                        _cachedMatchedCompanyIds = matchedIds;
                    }

                    System.Diagnostics.Debug.WriteLine($"[PERF][BizCache] Preload 완료 (RPC 1회): {sw.ElapsedMilliseconds}ms " +
                        $"(Company={companies.Count}, User={users.Count}, " +
                        $"MatchedIds={matchedIds.Count})");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[BizCache] Preload error: {ex.Message}");
                    lock (_bizCacheLock)
                    {
                        if (_cachedCompanies == null) _bizPreloadTask = null;
                    }
                }
            });
        }
    }

    /// <summary>
    /// 캐시된 업무 데이터 반환 (캐시 없으면 서버에서 로드)
    /// </summary>
    public static async Task<(List<Company> Companies, List<User> Users, HashSet<Guid> MatchedCompanyIds)> GetBizDataAsync()
    {
        // 1. 메모리 캐시 우선 확인
        lock (_bizCacheLock)
        {
            if (_cachedCompanies != null)
            {
                System.Diagnostics.Debug.WriteLine("[PERF][BizCache] 캐시 HIT (즉시 반환)");
                return (_cachedCompanies!, _cachedUsers!, _cachedMatchedCompanyIds!);
            }
        }

        // 2. Preload 진행 중이면 완료 대기
        Task? pending;
        lock (_bizCacheLock) { pending = _bizPreloadTask; }
        if (pending != null)
        {
            try { await pending; }
            catch { /* Preload 에러는 이미 로깅됨 */ }
        }

        // 3. Preload 완료 후 캐시 재확인
        lock (_bizCacheLock)
        {
            if (_cachedCompanies != null)
            {
                System.Diagnostics.Debug.WriteLine("[PERF][BizCache] 캐시 HIT (Preload 후)");
                return (_cachedCompanies!, _cachedUsers!, _cachedMatchedCompanyIds!);
            }
        }

        // 4. 캐시 없으면 RPC 1회로 직접 로드
        Task loadTask;
        lock (_bizCacheLock)
        {
            if (_cachedCompanies != null)
                return (_cachedCompanies!, _cachedUsers!, _cachedMatchedCompanyIds!);
            if (_bizPreloadTask != null)
            {
                loadTask = _bizPreloadTask;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[PERF][BizCache] 캐시 MISS → RPC 서버 로드");
                loadTask = Task.Run(async () =>
                {
                    var (companies, users, matchedIds) = await FetchBizDataViaRpc();

                    lock (_bizCacheLock)
                    {
                        _cachedCompanies = companies;
                        _cachedUsers = users;
                        _cachedMatchedCompanyIds = matchedIds;
                    }
                });
                _bizPreloadTask = loadTask;
            }
        }

        await loadTask;

        lock (_bizCacheLock)
        {
            return (_cachedCompanies!, _cachedUsers!, _cachedMatchedCompanyIds!);
        }
    }

    /// <summary>
    /// 업무 데이터 캐시 갱신 (업체 등록/삭제, 일정 생성 후 호출)
    /// </summary>
    public static void InvalidateBizCache()
    {
        lock (_bizCacheLock)
        {
            _cachedCompanies = null;
            _cachedUsers = null;
            _cachedMatchedCompanyIds = null;
            _bizPreloadTask = null;
        }
        System.Diagnostics.Debug.WriteLine("[BizCache] 캐시 무효화됨");
    }

    /// <summary>
    /// 업무 데이터 캐시 초기화 (로그아웃 시 호출)
    /// </summary>
    public static void ClearBizCache()
    {
        lock (_bizCacheLock)
        {
            _cachedCompanies = null;
            _cachedUsers = null;
            _cachedMatchedCompanyIds = null;
            _bizPreloadTask = null;
        }
    }
}
