using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace MilOps.ViewModels;

/// <summary>
/// 역할별 초대 폼을 담당하는 ViewModel
/// ManagerViewModel에서 분리하여 단일 책임 원칙 적용
/// </summary>
public partial class InviteFormViewModel : ViewModelBase
{
    // 현재 사용자 역할에 따른 초대 타입
    public enum InviteType
    {
        None,                    // 초대 권한 없음
        SuperAdminMois,          // SW0001: 지자체(도) 중간관리자 초대
        SuperAdminArmy,          // SW0002: 사단담당자 초대
        MiddleLocal,             // 지자체 중간관리자: 지자체담당자 초대
        MiddleMilitary           // 사단담당자: 대대담당자 초대
    }

    [ObservableProperty]
    private InviteType _currentInviteType = InviteType.None;

    [ObservableProperty]
    private string _formTitle = "";

    [ObservableProperty]
    private string _affiliationLabel = "";

    [ObservableProperty]
    private string _targetRole = "";

    // 공통 입력 필드
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _phone = "";

    // 전화번호 포맷팅 플래그
    private bool _isFormattingPhone = false;

    // 소속 선택 (ComboBox용)
    [ObservableProperty]
    private ObservableCollection<AffiliationOption> _affiliationOptions = new();

    [ObservableProperty]
    private AffiliationOption? _selectedAffiliation;

    // 소속 텍스트 입력 (사단/대대 직접 입력용)
    [ObservableProperty]
    private string _affiliationTextInput = "";

    // 계급 선택 (군부대용)
    [ObservableProperty]
    private ObservableCollection<RankOption> _rankOptions = new();

    private RankOption? _selectedRankOption;
    public RankOption? SelectedRankOption
    {
        get => _selectedRankOption;
        set
        {
            // 카테고리 선택 시 다음 실제 계급으로 자동 이동
            if (value != null && value.IsCategory)
            {
                var index = RankOptions.IndexOf(value);
                if (index >= 0 && index < RankOptions.Count - 1)
                {
                    var nextItem = RankOptions[index + 1];
                    if (!nextItem.IsCategory)
                    {
                        SetProperty(ref _selectedRankOption, nextItem);
                        return;
                    }
                }
                return;
            }
            SetProperty(ref _selectedRankOption, value);
        }
    }

    // UI 표시 조건
    [ObservableProperty]
    private bool _showAffiliationComboBox = true;

    [ObservableProperty]
    private bool _showAffiliationTextInput = false;

    [ObservableProperty]
    private bool _showRankField = false;

    [ObservableProperty]
    private string _affiliationSuffix = ""; // "사단" 또는 "대대"

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _message = "";

    [ObservableProperty]
    private bool _isError = false;

    // 캐시된 조직 데이터
    private List<Region> _regions = new();
    private List<District> _districts = new();
    private List<Division> _divisions = new();

    // 이벤트
    public event Action<Invitation>? InvitationCreated;
    public event Action? CloseRequested;

    public InviteFormViewModel()
    {
        InitializeRankOptions();
    }

    /// <summary>
    /// 현재 로그인한 사용자의 역할에 따라 초대 폼 초기화
    /// </summary>
    public async Task InitializeAsync()
    {
        var currentUser = AuthService.CurrentUser;
        var dbRole = currentUser?.Role ?? "";

        System.Diagnostics.Debug.WriteLine($"[InviteForm] Initializing for role: {dbRole}");

        // 조직 데이터 로드
        await LoadOrganizationDataAsync();

        // 역할에 따라 초대 타입 결정
        switch (dbRole)
        {
            case "super_admin_mois":
                await SetupSuperAdminMoisFormAsync();
                break;

            case "super_admin_army":
                await SetupSuperAdminArmyFormAsync();
                break;

            case "middle_local":
                await SetupMiddleLocalFormAsync();
                break;

            case "middle_military":
                await SetupMiddleMilitaryFormAsync();
                break;

            default:
                CurrentInviteType = InviteType.None;
                FormTitle = "초대 권한 없음";
                System.Diagnostics.Debug.WriteLine($"[InviteForm] No invite permission for role: {dbRole}");
                break;
        }

        // 입력 필드 초기화
        ClearInputFields();
    }

    /// <summary>
    /// SW0001 (행정안전부) - 지자체(도) 중간관리자 초대
    /// </summary>
    private async Task SetupSuperAdminMoisFormAsync()
    {
        CurrentInviteType = InviteType.SuperAdminMois;
        FormTitle = "지자체(도) 중간관리자 초대";
        AffiliationLabel = "소속 (시/도)";
        TargetRole = "middle_local";

        ShowAffiliationComboBox = true;
        ShowAffiliationTextInput = false;
        ShowRankField = false;

        // 전국 시/도 목록 로드
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

        System.Diagnostics.Debug.WriteLine($"[InviteForm] SuperAdminMois form setup complete. Regions: {AffiliationOptions.Count}");
    }

    /// <summary>
    /// SW0002 (육군본부) - 사단담당자 중간관리자 초대
    /// </summary>
    private async Task SetupSuperAdminArmyFormAsync()
    {
        CurrentInviteType = InviteType.SuperAdminArmy;
        FormTitle = "사단담당자 중간관리자 초대";
        AffiliationLabel = "소속 (사단)";
        TargetRole = "middle_military";

        ShowAffiliationComboBox = false;
        ShowAffiliationTextInput = true;
        ShowRankField = true;
        AffiliationSuffix = "사단";

        System.Diagnostics.Debug.WriteLine($"[InviteForm] SuperAdminArmy form setup complete");
    }

    /// <summary>
    /// 지자체 중간관리자 - 지자체담당자 초대
    /// </summary>
    private async Task SetupMiddleLocalFormAsync()
    {
        CurrentInviteType = InviteType.MiddleLocal;
        FormTitle = "지자체담당자 초대";
        AffiliationLabel = "소속 (구)";
        TargetRole = "user_local";

        ShowAffiliationComboBox = true;
        ShowAffiliationTextInput = false;
        ShowRankField = false;

        // 현재 사용자의 시/도에 해당하는 구/군만 표시
        var currentUser = AuthService.CurrentUser;
        var userRegionId = currentUser?.RegionId;

        AffiliationOptions.Clear();

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

        System.Diagnostics.Debug.WriteLine($"[InviteForm] MiddleLocal form setup complete. Districts: {AffiliationOptions.Count} (RegionId: {userRegionId})");
    }

    /// <summary>
    /// 사단담당자 - 대대담당자 초대
    /// </summary>
    private async Task SetupMiddleMilitaryFormAsync()
    {
        CurrentInviteType = InviteType.MiddleMilitary;
        FormTitle = "대대담당자 초대";
        AffiliationLabel = "소속 (대대)";
        TargetRole = "user_military";

        ShowAffiliationComboBox = false;
        ShowAffiliationTextInput = true;
        ShowRankField = true;
        AffiliationSuffix = "대대";

        System.Diagnostics.Debug.WriteLine($"[InviteForm] MiddleMilitary form setup complete");
    }

    /// <summary>
    /// 조직 데이터 로드
    /// </summary>
    private async Task LoadOrganizationDataAsync()
    {
        if (!SupabaseService.IsInitialized)
        {
            await SupabaseService.InitializeAsync();
        }

        try
        {
            var regionsTask = SupabaseService.Client.From<Region>().Get();
            var districtsTask = SupabaseService.Client.From<District>().Get();
            var divisionsTask = SupabaseService.Client.From<Division>().Get();

            await Task.WhenAll(regionsTask, districtsTask, divisionsTask);

            _regions = regionsTask.Result.Models;
            _districts = districtsTask.Result.Models;
            _divisions = divisionsTask.Result.Models;

            System.Diagnostics.Debug.WriteLine($"[InviteForm] Loaded: {_regions.Count} regions, {_districts.Count} districts, {_divisions.Count} divisions");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InviteForm] Failed to load organization data: {ex.Message}");
        }
    }

    /// <summary>
    /// 계급 옵션 초기화
    /// </summary>
    private void InitializeRankOptions()
    {
        RankOptions.Clear();

        // 부사관 카테고리
        RankOptions.Add(new RankOption("부사관", null, true));
        RankOptions.Add(new RankOption("    하사", "하사"));
        RankOptions.Add(new RankOption("    중사", "중사"));
        RankOptions.Add(new RankOption("    상사", "상사"));
        RankOptions.Add(new RankOption("    원사", "원사"));

        // 준사관 카테고리
        RankOptions.Add(new RankOption("준사관", null, true));
        RankOptions.Add(new RankOption("    준위", "준위"));

        // 장교 카테고리
        RankOptions.Add(new RankOption("장교", null, true));
        RankOptions.Add(new RankOption("    소위", "소위"));
        RankOptions.Add(new RankOption("    중위", "중위"));
        RankOptions.Add(new RankOption("    대위", "대위"));
        RankOptions.Add(new RankOption("    소령", "소령"));
        RankOptions.Add(new RankOption("    중령", "중령"));
        RankOptions.Add(new RankOption("    대령", "대령"));

        SelectedRankOption = RankOptions.FirstOrDefault(r => !r.IsCategory);
    }

    /// <summary>
    /// 입력 필드 초기화
    /// </summary>
    private void ClearInputFields()
    {
        Name = "";
        Phone = "";
        AffiliationTextInput = "";
        Message = "";
        IsError = false;

        if (AffiliationOptions.Count > 0)
            SelectedAffiliation = AffiliationOptions[0];

        SelectedRankOption = RankOptions.FirstOrDefault(r => !r.IsCategory);
    }

    /// <summary>
    /// 초대 생성
    /// </summary>
    [RelayCommand]
    private async Task CreateInviteAsync()
    {
        Message = "";
        IsError = false;

        // 유효성 검사
        if (!ValidateInput())
            return;

        IsLoading = true;

        try
        {
            var inviteCode = GenerateSecureInviteCode();
            var currentUser = AuthService.CurrentUser;

            var invitation = new Invitation
            {
                Id = Guid.NewGuid(),
                InviteCode = inviteCode,
                Role = TargetRole,
                Name = Name.Trim(),
                Phone = Phone.Trim(),
                MilitaryRank = ShowRankField ? SelectedRankOption?.Value : null,
                InvitedBy = currentUser!.Id,
                Status = "pending",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            // 소속 정보 설정
            await SetAffiliationAsync(invitation);

            // DB에 저장
            await SupabaseService.Client
                .From<Invitation>()
                .Insert(invitation);

            // 클립보드에 복사
            await CopyToClipboardAsync(inviteCode);

            Message = $"초대코드 {inviteCode}가 복사되었습니다!";
            IsError = false;

            // 이벤트 발생
            InvitationCreated?.Invoke(invitation);

            System.Diagnostics.Debug.WriteLine($"[InviteForm] Invitation created: {inviteCode} for role: {TargetRole}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InviteForm] Failed to create invitation: {ex.Message}");
            Message = $"초대 생성 실패: {ex.Message}";
            IsError = true;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 소속 정보 설정
    /// </summary>
    private async Task SetAffiliationAsync(Invitation invitation)
    {
        switch (CurrentInviteType)
        {
            case InviteType.SuperAdminMois:
                // 시/도 선택
                if (SelectedAffiliation != null)
                {
                    invitation.RegionId = SelectedAffiliation.Id;
                    invitation.DisplayAffiliation = SelectedAffiliation.Name;
                }
                break;

            case InviteType.SuperAdminArmy:
                // 사단 직접 입력 → divisions 테이블에 생성
                var divisionName = $"{AffiliationTextInput.Trim()}사단";
                var newDivision = new Division
                {
                    Id = Guid.NewGuid(),
                    Name = divisionName,
                    CreatedAt = DateTime.UtcNow
                };

                await SupabaseService.Client
                    .From<Division>()
                    .Insert(newDivision);

                invitation.DivisionId = newDivision.Id;
                invitation.DisplayAffiliation = divisionName;

                System.Diagnostics.Debug.WriteLine($"[InviteForm] Created division: {divisionName}");
                break;

            case InviteType.MiddleLocal:
                // 구/군 선택
                if (SelectedAffiliation != null)
                {
                    invitation.DistrictId = SelectedAffiliation.Id;
                    invitation.RegionId = SelectedAffiliation.ParentId;
                    invitation.DisplayAffiliation = SelectedAffiliation.Name;
                }
                break;

            case InviteType.MiddleMilitary:
                // 대대 직접 입력 → battalions 테이블에 생성
                var currentUser = AuthService.CurrentUser;
                var userDivisionId = currentUser?.DivisionId;

                if (!userDivisionId.HasValue)
                {
                    throw new InvalidOperationException("사용자의 사단 정보가 없습니다");
                }

                var battalionName = $"{AffiliationTextInput.Trim()}대대";
                var newBattalion = new Battalion
                {
                    Id = Guid.NewGuid(),
                    DivisionId = userDivisionId.Value,
                    Name = battalionName,
                    CreatedAt = DateTime.UtcNow
                };

                await SupabaseService.Client
                    .From<Battalion>()
                    .Insert(newBattalion);

                invitation.BattalionId = newBattalion.Id;
                invitation.DivisionId = userDivisionId.Value;
                invitation.DisplayAffiliation = battalionName;

                System.Diagnostics.Debug.WriteLine($"[InviteForm] Created battalion: {battalionName}");
                break;
        }
    }

    /// <summary>
    /// 입력 유효성 검사
    /// </summary>
    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            Message = "이름을 입력해주세요";
            IsError = true;
            return false;
        }

        if (string.IsNullOrWhiteSpace(Phone))
        {
            Message = "연락처를 입력해주세요";
            IsError = true;
            return false;
        }

        // 전화번호 11자리 검증
        var phoneDigits = new string(Phone.Where(char.IsDigit).ToArray());
        if (phoneDigits.Length > 11)
        {
            Message = "연락처는 11자리를 초과할 수 없습니다";
            IsError = true;
            return false;
        }

        // 소속 검증
        if (ShowAffiliationComboBox && SelectedAffiliation == null)
        {
            Message = "소속을 선택해주세요";
            IsError = true;
            return false;
        }

        if (ShowAffiliationTextInput)
        {
            if (string.IsNullOrWhiteSpace(AffiliationTextInput))
            {
                Message = "소속을 입력해주세요";
                IsError = true;
                return false;
            }

            // 숫자만 추출하여 5자리 검증
            var affiliationDigits = new string(AffiliationTextInput.Where(char.IsDigit).ToArray());
            if (affiliationDigits.Length > 5)
            {
                Message = "소속 번호는 최대 5자리입니다";
                IsError = true;
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 암호학적으로 안전한 초대 코드 생성
    /// </summary>
    private static string GenerateSecureInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var bytes = new byte[8];
        using (var rng = RandomNumberGenerator.Create())
        {
            rng.GetBytes(bytes);
        }

        var result = new char[8];
        for (int i = 0; i < 8; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }
        return new string(result);
    }

    /// <summary>
    /// 전화번호 자동 포맷팅
    /// </summary>
    partial void OnPhoneChanged(string value)
    {
        if (_isFormattingPhone) return;

        _isFormattingPhone = true;
        try
        {
            var formatted = FormatPhoneNumber(value);
            if (formatted != value)
            {
                Phone = formatted;
            }
        }
        finally
        {
            _isFormattingPhone = false;
        }
    }

    /// <summary>
    /// 전화번호 포맷팅 (11자리 제한 + 010-0000-0000 형식)
    /// </summary>
    private static string FormatPhoneNumber(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        // 숫자만 추출
        var digits = new string(input.Where(char.IsDigit).ToArray());

        // 11자리로 제한
        if (digits.Length > 11)
            digits = digits.Substring(0, 11);

        // 포맷팅
        if (digits.Length <= 3)
        {
            return digits;
        }
        else if (digits.Length <= 7)
        {
            return $"{digits.Substring(0, 3)}-{digits.Substring(3)}";
        }
        else
        {
            return $"{digits.Substring(0, 3)}-{digits.Substring(3, 4)}-{digits.Substring(7)}";
        }
    }

    /// <summary>
    /// 클립보드에 복사
    /// </summary>
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
            System.Diagnostics.Debug.WriteLine($"[InviteForm] Failed to copy to clipboard: {ex.Message}");
        }
    }

    /// <summary>
    /// 닫기 명령
    /// </summary>
    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke();
    }
}
