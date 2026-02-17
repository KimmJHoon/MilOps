using MilOps.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Supabase.Postgrest;
using static Supabase.Postgrest.Constants;

namespace MilOps.Services;

/// <summary>
/// Supabase 쿼리 헬퍼 — 반복되는 From<T>().Filter().Get() 패턴 통합
/// </summary>
public static class QueryHelper
{
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
    // ManagerViewModel과 MainViewModel.LoadUserRegionAsync()가 같은 조직 데이터를 중복 쿼리하는 문제 해결.
    // 한 번 로드하면 로그아웃까지 캐시 유지.

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
    /// 조직 데이터 Preload (로그인 직후 호출, fire-and-forget)
    /// 1단계: 파일 캐시에서 즉시 로드 (~10ms) → 메모리 캐시 설정
    /// 2단계: 백그라운드에서 서버 데이터로 갱신 (stale-while-revalidate)
    /// _orgPreloadTask는 완료 후에도 유지하여 GetOrgDataAsync()에서 await 가능.
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

            // 2단계: 백그라운드에서 서버 최신 데이터로 갱신 (stale-while-revalidate)
            _orgPreloadTask = Task.Run(async () =>
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    System.Diagnostics.Debug.WriteLine("[PERF][OrgCache] Preload 시작 (서버 갱신)");

                    var regionsTask = GetActiveTask<Region>();
                    var districtsTask = GetActiveTask<District>();
                    var divisionsTask = GetAllTask<Division>();
                    var brigadesTask = GetActiveTask<Brigade>();
                    var battalionsTask = GetActiveTask<Battalion>();

                    await Task.WhenAll(regionsTask, districtsTask, divisionsTask, brigadesTask, battalionsTask);

                    lock (_orgCacheLock)
                    {
                        _cachedRegions = regionsTask.Result;
                        _cachedDistricts = districtsTask.Result;
                        _cachedDivisions = divisionsTask.Result;
                        _cachedBrigades = brigadesTask.Result;
                        _cachedBattalions = battalionsTask.Result;
                    }

                    System.Diagnostics.Debug.WriteLine($"[PERF][OrgCache] Preload 완료 (서버): {sw.ElapsedMilliseconds}ms " +
                        $"(Region={regionsTask.Result.Count}, District={districtsTask.Result.Count}, " +
                        $"Division={divisionsTask.Result.Count}, Brigade={brigadesTask.Result.Count}, " +
                        $"Battalion={battalionsTask.Result.Count})");

                    // 서버 데이터를 파일 캐시에 저장 (다음 앱 시작 시 즉시 사용)
                    OrgCacheFileService.SaveToFile(
                        regionsTask.Result, districtsTask.Result, divisionsTask.Result,
                        brigadesTask.Result, battalionsTask.Result);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OrgCache] Preload error: {ex.Message}");
                    // 에러 시 파일 캐시가 이미 로드되었으면 _orgPreloadTask를 null로 초기화하지 않음
                    lock (_orgCacheLock)
                    {
                        if (_cachedRegions == null) _orgPreloadTask = null;
                    }
                }
                // NOTE: finally에서 _orgPreloadTask = null 제거!
                // 완료된 Task를 유지해야 GetOrgDataAsync()에서 await 시 즉시 반환됨.
                // ClearOrgCache()에서만 null로 초기화.
            });
        }
    }

    /// <summary>
    /// 캐시된 조직 데이터 반환 (캐시 없으면 서버에서 로드)
    /// 메모리 캐시가 있으면 즉시 반환 (파일 캐시 or 이전 서버 로드 결과)
    /// 메모리 캐시가 없으면 Preload 완료 대기 → 이중 로드 방지
    /// </summary>
    public static async Task<(List<Region> Regions, List<District> Districts, List<Division> Divisions, List<Brigade> Brigades, List<Battalion> Battalions)> GetOrgDataAsync()
    {
        // 1. 메모리 캐시 우선 확인 — 파일 캐시로 이미 세팅된 경우 서버 갱신 대기 없이 즉시 반환
        lock (_orgCacheLock)
        {
            if (_cachedRegions != null)
            {
                System.Diagnostics.Debug.WriteLine("[PERF][OrgCache] 캐시 HIT (즉시 반환)");
                return (_cachedRegions!, _cachedDistricts!, _cachedDivisions!, _cachedBrigades!, _cachedBattalions!);
            }
        }

        // 2. 메모리 캐시 없음 → Preload 진행 중이면 완료 대기
        Task? pending;
        lock (_orgCacheLock)
        {
            pending = _orgPreloadTask;
        }
        if (pending != null)
        {
            try { await pending; }
            catch { /* Preload 에러는 이미 로깅됨, 아래에서 fallback 로드 */ }
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

        // 캐시 없으면 직접 로드 (동시에 여러 caller가 여기 도달하는 것을 방지)
        // 새로운 _orgPreloadTask를 생성하여 다른 caller가 대기할 수 있게 함
        Task loadTask;
        lock (_orgCacheLock)
        {
            // 다른 스레드가 이미 로드를 시작했을 수 있으므로 다시 확인
            if (_cachedRegions != null)
            {
                return (_cachedRegions!, _cachedDistricts!, _cachedDivisions!, _cachedBrigades!, _cachedBattalions!);
            }
            if (_orgPreloadTask != null)
            {
                loadTask = _orgPreloadTask;
            }
            else
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                System.Diagnostics.Debug.WriteLine("[PERF][OrgCache] 캐시 MISS → 서버 로드");
                loadTask = Task.Run(async () =>
                {
                    var regionsTask = GetActiveTask<Region>();
                    var districtsTask = GetActiveTask<District>();
                    var divisionsTask = GetAllTask<Division>();
                    var brigadesTask = GetActiveTask<Brigade>();
                    var battalionsTask = GetActiveTask<Battalion>();

                    await Task.WhenAll(regionsTask, districtsTask, divisionsTask, brigadesTask, battalionsTask);

                    lock (_orgCacheLock)
                    {
                        _cachedRegions = regionsTask.Result;
                        _cachedDistricts = districtsTask.Result;
                        _cachedDivisions = divisionsTask.Result;
                        _cachedBrigades = brigadesTask.Result;
                        _cachedBattalions = battalionsTask.Result;
                    }
                    System.Diagnostics.Debug.WriteLine($"[PERF][OrgCache] 서버 로드 완료: {sw.ElapsedMilliseconds}ms");

                    // 파일 캐시에도 저장
                    OrgCacheFileService.SaveToFile(
                        regionsTask.Result, districtsTask.Result, divisionsTask.Result,
                        brigadesTask.Result, battalionsTask.Result);
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
        // 파일 캐시도 삭제
        OrgCacheFileService.ClearCache();
    }
}
