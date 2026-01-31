using System.Collections.Generic;
using System.Threading.Tasks;
using MilOps.Models;
using Supabase;

namespace MilOps.Services.Abstractions;

/// <summary>
/// Supabase 서비스 인터페이스 - 테스트 가능하게 하기 위한 추상화
/// </summary>
public interface ISupabaseService
{
    bool IsInitialized { get; }
    Client? Client { get; }

    Task InitializeAsync();
    Task<User?> GetUserByLoginIdAsync(string loginId);

    // Schedule 관련
    Task<List<Schedule>> GetSchedulesAsync();
    Task UpdateScheduleAsync(Schedule schedule);
    Task SoftDeleteScheduleAsync(System.Guid scheduleId, System.Guid deletedBy);

    // 캐시 데이터 로드
    Task<List<Company>> GetActiveCompaniesAsync();
    Task<List<Battalion>> GetBattalionsAsync();
    Task<List<District>> GetDistrictsAsync();
    Task<List<User>> GetActiveUsersAsync();
}

/// <summary>
/// 기존 static SupabaseService를 래핑하는 어댑터
/// 프로덕션에서 사용, 테스트에서는 Mock으로 대체
/// </summary>
public class SupabaseServiceAdapter : ISupabaseService
{
    public bool IsInitialized => SupabaseService.IsInitialized;
    public Client? Client => SupabaseService.IsInitialized ? SupabaseService.Client : null;

    public Task InitializeAsync() => SupabaseService.InitializeAsync();

    public Task<User?> GetUserByLoginIdAsync(string loginId)
        => SupabaseService.GetUserByLoginIdAsync(loginId);

    public async Task<List<Schedule>> GetSchedulesAsync()
    {
        var response = await SupabaseService.Client.From<Schedule>()
            .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
            .Get();
        return response.Models;
    }

    public async Task UpdateScheduleAsync(Schedule schedule)
    {
        await SupabaseService.Client.From<Schedule>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, schedule.Id.ToString())
            .Update(schedule);
    }

    public async Task SoftDeleteScheduleAsync(System.Guid scheduleId, System.Guid deletedBy)
    {
        await SupabaseService.Client.From<Schedule>()
            .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, scheduleId.ToString())
            .Set(s => s.DeletedAt, System.DateTime.UtcNow)
            .Set(s => s.DeletedBy, deletedBy)
            .Update();
    }

    public async Task<List<Company>> GetActiveCompaniesAsync()
    {
        var response = await SupabaseService.Client.From<Company>()
            .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
            .Get();
        return response.Models;
    }

    public async Task<List<Battalion>> GetBattalionsAsync()
    {
        var response = await SupabaseService.Client.From<Battalion>().Get();
        return response.Models;
    }

    public async Task<List<District>> GetDistrictsAsync()
    {
        var response = await SupabaseService.Client.From<District>().Get();
        return response.Models;
    }

    public async Task<List<User>> GetActiveUsersAsync()
    {
        var response = await SupabaseService.Client.From<User>()
            .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
            .Get();
        return response.Models;
    }
}
