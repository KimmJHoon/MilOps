using MilOps.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace MilOps.Services;

/// <summary>
/// 조직 데이터 파일 캐시 (stale-while-revalidate 패턴)
/// - 앱 시작 시 로컬 파일에서 즉시 로드 (~10ms)
/// - 백그라운드에서 서버 데이터로 갱신 (stale-while-revalidate)
/// - 조직 데이터는 거의 변경되지 않으므로 파일 캐시 적중률 99%+
/// </summary>
public static class OrgCacheFileService
{
    private static readonly string CacheFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MilOps",
        "org_cache.json"
    );

    /// <summary>
    /// 직렬화용 DTO (Supabase BaseModel 의존성 제거)
    /// </summary>
    private class OrgCacheDto
    {
        public List<OrgItemDto> Regions { get; set; } = new();
        public List<DistrictDto> Districts { get; set; } = new();
        public List<OrgItemDto> Divisions { get; set; } = new();
        public List<BrigadeDto> Brigades { get; set; } = new();
        public List<BattalionDto> Battalions { get; set; } = new();
        public long SavedAtTicks { get; set; }
    }

    private class OrgItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }

    private class DistrictDto
    {
        public Guid Id { get; set; }
        public Guid RegionId { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }

    private class BrigadeDto
    {
        public Guid Id { get; set; }
        public Guid DivisionId { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }

    private class BattalionDto
    {
        public Guid Id { get; set; }
        public Guid DivisionId { get; set; }
        public Guid? BrigadeId { get; set; }
        public string Name { get; set; } = "";
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// 로컬 파일에서 캐시된 조직 데이터 로드 (동기적으로 빠르게 읽기)
    /// 캐시가 없거나 파싱 실패 시 null 반환
    /// </summary>
    public static (List<Region>? Regions, List<District>? Districts, List<Division>? Divisions,
                    List<Brigade>? Brigades, List<Battalion>? Battalions) LoadFromFile()
    {
        try
        {
            if (!File.Exists(CacheFilePath))
            {
                System.Diagnostics.Debug.WriteLine("[OrgCacheFile] 캐시 파일 없음");
                return (null, null, null, null, null);
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var json = File.ReadAllText(CacheFilePath);
            var dto = JsonSerializer.Deserialize<OrgCacheDto>(json);

            if (dto == null || dto.Regions.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[OrgCacheFile] 캐시 파일 비어있음");
                return (null, null, null, null, null);
            }

            var regions = dto.Regions.Select(r => new Region
            {
                Id = r.Id,
                Name = r.Name,
                IsActive = r.IsActive
            }).ToList();

            var districts = dto.Districts.Select(d => new District
            {
                Id = d.Id,
                RegionId = d.RegionId,
                Name = d.Name,
                IsActive = d.IsActive
            }).ToList();

            var divisions = dto.Divisions.Select(d => new Division
            {
                Id = d.Id,
                Name = d.Name,
                IsActive = d.IsActive
            }).ToList();

            var brigades = dto.Brigades.Select(b => new Brigade
            {
                Id = b.Id,
                DivisionId = b.DivisionId,
                Name = b.Name,
                IsActive = b.IsActive
            }).ToList();

            var battalions = dto.Battalions.Select(b => new Battalion
            {
                Id = b.Id,
                DivisionId = b.DivisionId,
                BrigadeId = b.BrigadeId,
                Name = b.Name,
                IsActive = b.IsActive
            }).ToList();

            System.Diagnostics.Debug.WriteLine($"[PERF][OrgCacheFile] 파일 캐시 로드: {sw.ElapsedMilliseconds}ms " +
                $"(Region={regions.Count}, District={districts.Count}, Division={divisions.Count}, " +
                $"Brigade={brigades.Count}, Battalion={battalions.Count})");

            return (regions, districts, divisions, brigades, battalions);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OrgCacheFile] 파일 로드 실패: {ex.Message}");
            return (null, null, null, null, null);
        }
    }

    /// <summary>
    /// 조직 데이터를 로컬 파일에 저장 (백그라운드에서 호출)
    /// </summary>
    public static void SaveToFile(List<Region> regions, List<District> districts,
                                   List<Division> divisions, List<Brigade> brigades,
                                   List<Battalion> battalions)
    {
        try
        {
            var directory = Path.GetDirectoryName(CacheFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var dto = new OrgCacheDto
            {
                Regions = regions.Select(r => new OrgItemDto { Id = r.Id, Name = r.Name, IsActive = r.IsActive }).ToList(),
                Districts = districts.Select(d => new DistrictDto { Id = d.Id, RegionId = d.RegionId, Name = d.Name, IsActive = d.IsActive }).ToList(),
                Divisions = divisions.Select(d => new OrgItemDto { Id = d.Id, Name = d.Name, IsActive = d.IsActive }).ToList(),
                Brigades = brigades.Select(b => new BrigadeDto { Id = b.Id, DivisionId = b.DivisionId, Name = b.Name, IsActive = b.IsActive }).ToList(),
                Battalions = battalions.Select(b => new BattalionDto { Id = b.Id, DivisionId = b.DivisionId, BrigadeId = b.BrigadeId, Name = b.Name, IsActive = b.IsActive }).ToList(),
                SavedAtTicks = DateTime.UtcNow.Ticks
            };

            var json = JsonSerializer.Serialize(dto);
            File.WriteAllText(CacheFilePath, json);

            System.Diagnostics.Debug.WriteLine($"[OrgCacheFile] 파일 캐시 저장 완료 ({json.Length} bytes)");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OrgCacheFile] 파일 저장 실패: {ex.Message}");
        }
    }

    /// <summary>
    /// 캐시 파일 삭제 (로그아웃 시)
    /// </summary>
    public static void ClearCache()
    {
        try
        {
            if (File.Exists(CacheFilePath))
            {
                File.Delete(CacheFilePath);
                System.Diagnostics.Debug.WriteLine("[OrgCacheFile] 캐시 파일 삭제");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OrgCacheFile] 캐시 삭제 실패: {ex.Message}");
        }
    }
}
