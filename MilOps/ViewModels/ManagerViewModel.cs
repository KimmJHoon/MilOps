using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
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

public partial class ManagerViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<Invitation> _invitations = new();

    [ObservableProperty]
    private int _totalCount = 0;

    [ObservableProperty]
    private int _activeCount = 0;

    [ObservableProperty]
    private int _pendingCount = 0;

    [ObservableProperty]
    private bool _isNewInviteDialogOpen = false;

    // 현재 사용자의 역할 타입 (local/military)
    [ObservableProperty]
    private string _currentUserType = "local";

    // 초대 대상 역할 (자동 결정됨)
    [ObservableProperty]
    private string _targetRole = "";

    // 초대 폼 타이틀
    [ObservableProperty]
    private string _inviteFormTitle = "";

    // 소속 라벨
    [ObservableProperty]
    private string _affiliationLabel = "";

    // 공통 입력 필드
    [ObservableProperty]
    private string _newInviteName = "";

    [ObservableProperty]
    private string _newInvitePhone = "";

    // 계급 (군부대용)
    [ObservableProperty]
    private string _newInviteRank = "";

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _copiedMessage = "";

    // 소속 옵션 (DB에서 로드)
    [ObservableProperty]
    private ObservableCollection<AffiliationOption> _affiliationOptions = new();

    [ObservableProperty]
    private AffiliationOption? _selectedAffiliation;

    // 계급 옵션 (군부대용)
    [ObservableProperty]
    private ObservableCollection<string> _rankOptions = new();

    // UI 표시 조건
    [ObservableProperty]
    private bool _showRankField = false;

    // 캐시된 조직 데이터
    private List<Region> _regions = new();
    private List<District> _districts = new();
    private List<Division> _divisions = new();
    private List<Battalion> _battalions = new();

    public ManagerViewModel()
    {
        InitializeRankOptions();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await LoadOrganizationDataAsync();
        DetermineUserTypeAndRole();
        await LoadInvitationsAsync();
    }

    /// <summary>
    /// DB에서 조직 데이터 로드
    /// </summary>
    private async Task LoadOrganizationDataAsync()
    {
        if (!SupabaseService.IsInitialized)
        {
            await SupabaseService.InitializeAsync();
        }

        try
        {
            // 모든 조직 데이터 병렬로 로드
            var regionsTask = SupabaseService.Client.From<Region>().Get();
            var districtsTask = SupabaseService.Client.From<District>().Get();
            var divisionsTask = SupabaseService.Client.From<Division>().Get();
            var battalionsTask = SupabaseService.Client.From<Battalion>().Get();

            await Task.WhenAll(regionsTask, districtsTask, divisionsTask, battalionsTask);

            _regions = regionsTask.Result.Models;
            _districts = districtsTask.Result.Models;
            _divisions = divisionsTask.Result.Models;
            _battalions = battalionsTask.Result.Models;

            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Loaded: {_regions.Count} regions, {_districts.Count} districts, {_divisions.Count} divisions, {_battalions.Count} battalions");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Failed to load organization data: {ex.Message}");
        }
    }

    /// <summary>
    /// 현재 사용자의 역할에 따라 초대 대상 역할과 폼 설정 결정
    /// </summary>
    private void DetermineUserTypeAndRole()
    {
        var currentUser = AuthService.CurrentUser;
        var dbRole = currentUser?.Role ?? "";

        System.Diagnostics.Debug.WriteLine($"[ManagerVM] CurrentUser: {currentUser?.LoginId ?? "null"}, Role from DB: {dbRole}");

        // DB의 role 문자열을 직접 사용하여 판단
        if (dbRole == "super_admin_army")
        {
            // 육군본부 → 사단담당자 중간관리자 초대
            CurrentUserType = "military";
            TargetRole = "middle_military";
            InviteFormTitle = "사단담당자 중간관리자 초대";
            AffiliationLabel = "소속 (사단)";
            ShowRankField = true;
            InitializeDivisionOptions();
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Set as SuperAdminArmy, TargetRole={TargetRole}");
        }
        else if (dbRole == "super_admin_mois")
        {
            // 행정안전부 → 지자체(도) 중간관리자 초대
            CurrentUserType = "local";
            TargetRole = "middle_local";
            InviteFormTitle = "지자체(도) 중간관리자 초대";
            AffiliationLabel = "소속 (시/도)";
            ShowRankField = false;
            InitializeRegionOptions();
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Set as SuperAdminMois, TargetRole={TargetRole}");
        }
        // 중간관리자
        else if (dbRole == "middle_local")
        {
            // 지자체(도) 중간관리자 → 지자체담당자 초대
            CurrentUserType = "local";
            TargetRole = "user_local";
            InviteFormTitle = "지자체담당자 초대";
            AffiliationLabel = "소속 (구)";
            ShowRankField = false;
            InitializeDistrictOptionsForMiddleLocal();
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Set as MiddleLocal, TargetRole={TargetRole}");
        }
        else if (dbRole == "middle_military")
        {
            // 사단담당자 중간관리자 → 대대담당자 초대
            CurrentUserType = "military";
            TargetRole = "user_military";
            InviteFormTitle = "대대담당자 초대";
            AffiliationLabel = "소속 (대대)";
            ShowRankField = true;
            InitializeBattalionOptionsForMiddleMilitary();
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Set as MiddleMilitary, TargetRole={TargetRole}");
        }
        else
        {
            // 일반 사용자는 초대 권한 없음
            CurrentUserType = "none";
            TargetRole = "";
            InviteFormTitle = "";
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] No invite permission, dbRole={dbRole}");
        }
    }

    /// <summary>
    /// 시/도 목록 (행정안전부 → 지자체 중간관리자용)
    /// </summary>
    private void InitializeRegionOptions()
    {
        AffiliationOptions.Clear();
        foreach (var region in _regions.OrderBy(r => r.Name))
        {
            AffiliationOptions.Add(new AffiliationOption(
                region.Id,
                region.Name,
                AffiliationType.Region
            ));
        }

        if (AffiliationOptions.Count > 0)
            SelectedAffiliation = AffiliationOptions[0];
    }

    /// <summary>
    /// 사단 목록 (육군본부 → 사단담당자용)
    /// </summary>
    private void InitializeDivisionOptions()
    {
        AffiliationOptions.Clear();
        foreach (var division in _divisions.OrderBy(d => d.Name))
        {
            AffiliationOptions.Add(new AffiliationOption(
                division.Id,
                division.Name,
                AffiliationType.Division
            ));
        }

        if (AffiliationOptions.Count > 0)
            SelectedAffiliation = AffiliationOptions[0];
    }

    /// <summary>
    /// 구 목록 (지자체 중간관리자 → 지자체담당자용)
    /// 현재 사용자의 region_id에 속한 district만 표시
    /// </summary>
    private void InitializeDistrictOptionsForMiddleLocal()
    {
        AffiliationOptions.Clear();
        var currentUser = AuthService.CurrentUser;

        // 현재 사용자의 region_id로 필터링
        var userRegionId = currentUser?.RegionId;
        var filteredDistricts = userRegionId.HasValue
            ? _districts.Where(d => d.RegionId == userRegionId.Value)
            : _districts;

        foreach (var district in filteredDistricts.OrderBy(d => d.Name))
        {
            AffiliationOptions.Add(new AffiliationOption(
                district.Id,
                district.Name,
                AffiliationType.District,
                district.RegionId
            ));
        }

        if (AffiliationOptions.Count > 0)
            SelectedAffiliation = AffiliationOptions[0];
    }

    /// <summary>
    /// 대대 목록 (사단담당자 → 대대담당자용)
    /// 현재 사용자의 division_id에 속한 battalion만 표시
    /// </summary>
    private void InitializeBattalionOptionsForMiddleMilitary()
    {
        AffiliationOptions.Clear();
        var currentUser = AuthService.CurrentUser;

        // 현재 사용자의 division_id로 필터링
        var userDivisionId = currentUser?.DivisionId;
        var filteredBattalions = userDivisionId.HasValue
            ? _battalions.Where(b => b.DivisionId == userDivisionId.Value)
            : _battalions;

        foreach (var battalion in filteredBattalions.OrderBy(b => b.Name))
        {
            AffiliationOptions.Add(new AffiliationOption(
                battalion.Id,
                battalion.Name,
                AffiliationType.Battalion,
                battalion.DivisionId
            ));
        }

        if (AffiliationOptions.Count > 0)
            SelectedAffiliation = AffiliationOptions[0];
    }

    /// <summary>
    /// 계급 목록 (부사관-준사관-장교)
    /// </summary>
    private void InitializeRankOptions()
    {
        RankOptions.Clear();
        // 부사관
        RankOptions.Add("하사");
        RankOptions.Add("중사");
        RankOptions.Add("상사");
        RankOptions.Add("원사");
        // 준사관
        RankOptions.Add("준위");
        // 장교
        RankOptions.Add("소위");
        RankOptions.Add("중위");
        RankOptions.Add("대위");
        RankOptions.Add("소령");
        RankOptions.Add("중령");
        RankOptions.Add("대령");

        if (RankOptions.Count > 0)
            NewInviteRank = RankOptions[0];
    }

    private async Task LoadInvitationsAsync()
    {
        if (!SupabaseService.IsInitialized) return;
        if (AuthService.CurrentUser == null) return;

        IsLoading = true;
        try
        {
            var currentUserId = AuthService.CurrentUser.Id;
            var response = await SupabaseService.Client
                .From<Invitation>()
                .Filter("invited_by", Supabase.Postgrest.Constants.Operator.Equals, currentUserId.ToString())
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            Invitations.Clear();
            foreach (var invitation in response.Models)
            {
                // UI 표시용 소속 정보 설정
                invitation.DisplayAffiliation = GetDisplayAffiliation(invitation);
                Invitations.Add(invitation);
            }
            UpdateCounts();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load invitations: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private string? GetDisplayAffiliation(Invitation invitation)
    {
        // region_id, district_id, division_id, battalion_id로 실제 이름 조회
        if (invitation.BattalionId.HasValue)
        {
            var battalion = _battalions.FirstOrDefault(b => b.Id == invitation.BattalionId.Value);
            return battalion?.Name;
        }
        if (invitation.DivisionId.HasValue)
        {
            var division = _divisions.FirstOrDefault(d => d.Id == invitation.DivisionId.Value);
            return division?.Name;
        }
        if (invitation.DistrictId.HasValue)
        {
            var district = _districts.FirstOrDefault(d => d.Id == invitation.DistrictId.Value);
            return district?.Name;
        }
        if (invitation.RegionId.HasValue)
        {
            var region = _regions.FirstOrDefault(r => r.Id == invitation.RegionId.Value);
            return region?.Name;
        }
        return null;
    }

    private void UpdateCounts()
    {
        TotalCount = Invitations.Count;
        ActiveCount = Invitations.Count(i => i.IsUsed);
        PendingCount = Invitations.Count(i => i.IsValid);
    }

    [RelayCommand]
    private void OpenNewInviteDialog()
    {
        if (string.IsNullOrEmpty(TargetRole))
        {
            CopiedMessage = "초대 권한이 없습니다";
            return;
        }

        // 입력 필드 초기화
        NewInviteName = "";
        NewInvitePhone = "";
        CopiedMessage = "";

        if (AffiliationOptions.Count > 0)
            SelectedAffiliation = AffiliationOptions[0];

        if (RankOptions.Count > 0)
            NewInviteRank = RankOptions[0];

        IsNewInviteDialogOpen = true;
    }

    [RelayCommand]
    private void CloseNewInviteDialog()
    {
        IsNewInviteDialogOpen = false;
        CopiedMessage = "";
    }

    [RelayCommand]
    private async Task SendInviteAsync()
    {
        if (string.IsNullOrWhiteSpace(TargetRole))
            return;

        if (string.IsNullOrWhiteSpace(NewInviteName))
        {
            CopiedMessage = "이름을 입력해주세요";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewInvitePhone))
        {
            CopiedMessage = "연락처를 입력해주세요";
            return;
        }

        if (SelectedAffiliation == null)
        {
            CopiedMessage = "소속을 선택해주세요";
            return;
        }

        if (AuthService.CurrentUser == null) return;

        IsLoading = true;
        try
        {
            var inviteCode = GenerateInviteCode();
            var newInvite = new Invitation
            {
                Id = Guid.NewGuid(),
                InviteCode = inviteCode,
                Role = TargetRole,
                Name = NewInviteName,
                Phone = NewInvitePhone,
                MilitaryRank = ShowRankField ? NewInviteRank : null,
                InvitedBy = AuthService.CurrentUser.Id,
                Status = "pending",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            // 선택된 소속 타입에 따라 적절한 ID 설정
            switch (SelectedAffiliation.Type)
            {
                case AffiliationType.Region:
                    newInvite.RegionId = SelectedAffiliation.Id;
                    break;
                case AffiliationType.District:
                    newInvite.DistrictId = SelectedAffiliation.Id;
                    // district의 region_id도 설정
                    newInvite.RegionId = SelectedAffiliation.ParentId;
                    break;
                case AffiliationType.Division:
                    newInvite.DivisionId = SelectedAffiliation.Id;
                    break;
                case AffiliationType.Battalion:
                    newInvite.BattalionId = SelectedAffiliation.Id;
                    // battalion의 division_id도 설정
                    newInvite.DivisionId = SelectedAffiliation.ParentId;
                    break;
            }

            await SupabaseService.Client
                .From<Invitation>()
                .Insert(newInvite);

            // UI 표시용 소속 설정
            newInvite.DisplayAffiliation = SelectedAffiliation.Name;
            Invitations.Insert(0, newInvite);
            UpdateCounts();

            // 초대 코드를 클립보드에 복사
            await CopyToClipboardAsync(inviteCode);
            CopiedMessage = $"초대코드 {inviteCode} 가 복사되었습니다!";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create invitation: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Exception details: {ex}");
            CopiedMessage = $"초대 생성 실패: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task CopyToClipboardAsync(string text)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                }
            }
            else if (Application.Current?.ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            {
                var topLevel = TopLevel.GetTopLevel(singleView.MainView);
                if (topLevel?.Clipboard != null)
                {
                    await topLevel.Clipboard.SetTextAsync(text);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to copy to clipboard: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CopyInviteCodeAsync(Invitation invitation)
    {
        await CopyToClipboardAsync(invitation.InviteCode);
        CopiedMessage = $"초대코드 {invitation.InviteCode} 가 복사되었습니다!";
    }

    [RelayCommand]
    private async Task DeleteInvitationAsync(Invitation invitation)
    {
        try
        {
            await SupabaseService.Client
                .From<Invitation>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, invitation.Id.ToString())
                .Delete();

            Invitations.Remove(invitation);
            UpdateCounts();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete invitation: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ResendInvitationAsync(Invitation invitation)
    {
        try
        {
            invitation.ExpiresAt = DateTime.UtcNow.AddDays(7);
            invitation.Status = "pending";

            await SupabaseService.Client
                .From<Invitation>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, invitation.Id.ToString())
                .Set(i => i.ExpiresAt, invitation.ExpiresAt)
                .Set(i => i.Status, "pending")
                .Update();

            // UI 갱신을 위해 목록 다시 로드
            await LoadInvitationsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to resend invitation: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CancelInvitationAsync(Invitation invitation)
    {
        try
        {
            invitation.Status = "cancelled";

            await SupabaseService.Client
                .From<Invitation>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, invitation.Id.ToString())
                .Set(i => i.Status, "cancelled")
                .Update();

            // UI 갱신을 위해 목록 다시 로드
            await LoadInvitationsAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to cancel invitation: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadOrganizationDataAsync();
        DetermineUserTypeAndRole();
        await LoadInvitationsAsync();
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

public enum AffiliationType
{
    Region,     // 시/도
    District,   // 구/군
    Division,   // 사단
    Battalion   // 대대
}

public class AffiliationOption
{
    public Guid Id { get; }
    public string Name { get; }
    public AffiliationType Type { get; }
    public Guid? ParentId { get; }

    public AffiliationOption(Guid id, string name, AffiliationType type, Guid? parentId = null)
    {
        Id = id;
        Name = name;
        Type = type;
        ParentId = parentId;
    }

    public override string ToString() => Name;
}
