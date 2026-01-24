using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using System;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

public partial class InviteCodeViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _inviteCode = "";

    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private bool _hasError = false;

    [ObservableProperty]
    private bool _isLoading = false;

    // 검증된 초대 정보
    public Invitation? ValidatedInvitation { get; private set; }

    public event Action? BackRequested;
    public event Action<Invitation>? CodeValidated;

    [RelayCommand]
    private void GoBack()
    {
        BackRequested?.Invoke();
    }

    [RelayCommand]
    private async Task VerifyCodeAsync()
    {
        HasError = false;
        ErrorMessage = "";

        var code = InviteCode?.Trim().ToUpper() ?? "";

        if (string.IsNullOrWhiteSpace(code))
        {
            ErrorMessage = "초대코드를 입력해주세요";
            HasError = true;
            return;
        }

        if (code.Length != 8)
        {
            ErrorMessage = "초대코드는 8자리입니다";
            HasError = true;
            return;
        }

        IsLoading = true;

        try
        {
            // Supabase 초기화
            if (!SupabaseService.IsInitialized)
            {
                await SupabaseService.InitializeAsync();
            }

            // 초대코드 조회
            var response = await SupabaseService.Client
                .From<Invitation>()
                .Filter("invite_code", Supabase.Postgrest.Constants.Operator.Equals, code)
                .Single();

            if (response == null)
            {
                ErrorMessage = "유효하지 않은 초대코드입니다";
                HasError = true;
                return;
            }

            // 상태 확인
            if (response.Status == "accepted")
            {
                ErrorMessage = "이미 사용된 초대코드입니다";
                HasError = true;
                return;
            }

            if (response.Status == "cancelled")
            {
                ErrorMessage = "취소된 초대코드입니다";
                HasError = true;
                return;
            }

            if (response.Status == "expired" || DateTime.UtcNow > response.ExpiresAt)
            {
                ErrorMessage = "만료된 초대코드입니다";
                HasError = true;
                return;
            }

            // 유효한 초대코드 - 소속 정보 조회
            await LoadAffiliationInfoAsync(response);
            ValidatedInvitation = response;
            CodeValidated?.Invoke(response);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InviteCodeVM] Error: {ex.Message}");
            ErrorMessage = "초대코드 확인 중 오류가 발생했습니다";
            HasError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 초대 정보에서 소속명을 조회하여 DisplayAffiliation에 설정
    /// </summary>
    private async Task LoadAffiliationInfoAsync(Invitation invitation)
    {
        try
        {
            string? affiliationName = null;

            // 우선순위: battalion > division > district > region
            if (invitation.BattalionId.HasValue)
            {
                var battalion = await SupabaseService.Client
                    .From<Battalion>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, invitation.BattalionId.Value.ToString())
                    .Single();
                affiliationName = battalion?.Name;
            }
            else if (invitation.DivisionId.HasValue)
            {
                var division = await SupabaseService.Client
                    .From<Division>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, invitation.DivisionId.Value.ToString())
                    .Single();
                affiliationName = division?.Name;
            }
            else if (invitation.DistrictId.HasValue)
            {
                var district = await SupabaseService.Client
                    .From<District>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, invitation.DistrictId.Value.ToString())
                    .Single();
                affiliationName = district?.Name;
            }
            else if (invitation.RegionId.HasValue)
            {
                var region = await SupabaseService.Client
                    .From<Region>()
                    .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, invitation.RegionId.Value.ToString())
                    .Single();
                affiliationName = region?.Name;
            }

            invitation.DisplayAffiliation = affiliationName ?? "";
            System.Diagnostics.Debug.WriteLine($"[InviteCodeVM] Loaded affiliation: {affiliationName}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InviteCodeVM] Failed to load affiliation: {ex.Message}");
            invitation.DisplayAffiliation = "";
        }
    }
}
