using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using MilOps.Services;
using Supabase.Realtime;
using Supabase.Realtime.PostgresChanges;
using EventType = Supabase.Realtime.Constants.EventType;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
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

    // 전화번호 포맷팅 플래그 (무한 루프 방지)
    private bool _isFormattingPhone = false;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string _copiedMessage = "";

    // 소속 옵션 (DB에서 로드)
    [ObservableProperty]
    private ObservableCollection<AffiliationOption> _affiliationOptions = new();

    [ObservableProperty]
    private AffiliationOption? _selectedAffiliation;

    // 계급 옵션 (군부대용 - 카테고리 포함)
    [ObservableProperty]
    private ObservableCollection<RankOption> _rankOptions = new();

    // 선택된 계급
    private RankOption? _selectedRankOption;
    public RankOption? SelectedRankOption
    {
        get => _selectedRankOption;
        set
        {
            // 카테고리가 선택되면 다음 실제 계급으로 자동 선택
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
                return; // 카테고리는 선택 불가
            }
            SetProperty(ref _selectedRankOption, value);
        }
    }

    // UI 표시 조건
    [ObservableProperty]
    private bool _showRankField = false;

    // 텍스트 입력 모드 (사단/대대 직접 입력용)
    [ObservableProperty]
    private bool _showAffiliationTextInput = false;

    [ObservableProperty]
    private string _newAffiliationText = "";

    // 텍스트 입력 시 접미사 ("사단" 또는 "대대")
    [ObservableProperty]
    private string _affiliationSuffix = "";

    // 소속 선택 드롭다운 표시 여부
    [ObservableProperty]
    private bool _showAffiliationComboBox = true;

    // 캐시된 조직 데이터
    private List<Region> _regions = new();
    private List<District> _districts = new();
    private List<Division> _divisions = new();
    private List<Battalion> _battalions = new();

    // 빠른 조회를 위한 딕셔너리 캐시
    private Dictionary<Guid, string> _regionNames = new();
    private Dictionary<Guid, string> _districtNames = new();
    private Dictionary<Guid, string> _divisionNames = new();
    private Dictionary<Guid, string> _battalionNames = new();

    // 초기화 완료 플래그
    private bool _isOrganizationDataLoaded = false;

    // Realtime 채널
    private RealtimeChannel? _invitationsChannel;

    // 주기적 새로고침 타이머 (30초)
    private Timer? _refreshTimer;
    private const int RefreshIntervalMs = 30000; // 30초

    public ManagerViewModel()
    {
        InitializeRankOptions();
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (!_isOrganizationDataLoaded)
        {
            await LoadOrganizationDataAsync();
        }

        DetermineUserTypeAndRole();

        await LoadInvitationsAsync();

        // UI 렌더링이 먼저 완료되도록 실시간 구독은 백그라운드에서 처리
        _ = Task.Run(async () =>
        {
            try
            {
                await SubscribeToRealtimeAsync();
            }
            catch (Exception)
            {
                // Realtime 연결 실패 시 무시 (주기적 새로고침이 백업)
            }
        });

        StartPeriodicRefresh();
    }

    /// <summary>
    /// 주기적 새로고침 타이머 시작 (30초마다)
    /// Realtime이 실패할 경우를 대비한 백업 메커니즘
    /// </summary>
    private void StartPeriodicRefresh()
    {
        _refreshTimer = new Timer(
            async _ =>
            {
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(async () =>
                    {
                        // 백그라운드 새로고침 - 로딩 화면 없이 조용히 갱신
                        await LoadInvitationsSilentlyAsync();
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ManagerVM] Periodic refresh error: {ex.Message}");
                }
            },
            null,
            RefreshIntervalMs,  // 최초 실행까지 대기 시간
            RefreshIntervalMs   // 반복 간격
        );
    }

    /// <summary>
    /// 백그라운드에서 조용히 데이터 새로고침 (로딩 화면 없음)
    /// </summary>
    private async Task LoadInvitationsSilentlyAsync()
    {
        if (!SupabaseService.IsInitialized) return;
        if (AuthService.CurrentUser == null) return;

        try
        {
            var currentUserId = AuthService.CurrentUser.Id;
            var visibleRoles = GetVisibleInvitationRoles();

            if (visibleRoles.Count == 0)
            {
                if (Invitations.Count > 0)
                {
                    Invitations.Clear();
                    UpdateCounts();
                }
                return;
            }

            // 자신이 초대한 것만 볼 수 있음
            var response = await SupabaseService.Client
                .From<Invitation>()
                .Filter("invited_by", Supabase.Postgrest.Constants.Operator.Equals, currentUserId.ToString())
                .Filter("role", Supabase.Postgrest.Constants.Operator.In, visibleRoles)
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            var newInvitations = response.Models;
            var hasChanges = MergeInvitations(newInvitations);

            if (hasChanges)
            {
                UpdateCounts();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Background refresh failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 주기적 새로고침 타이머 중지
    /// </summary>
    private void StopPeriodicRefresh()
    {
        _refreshTimer?.Dispose();
        _refreshTimer = null;
    }

    /// <summary>
    /// Realtime 구독 설정
    /// </summary>
    private async Task SubscribeToRealtimeAsync()
    {
        try
        {
            if (!SupabaseService.IsInitialized) return;
            if (AuthService.CurrentUser == null) return;

            var currentUserId = AuthService.CurrentUser.Id.ToString();

            // Realtime 연결
            await SupabaseService.ConnectRealtimeAsync();

            // invitations 테이블 변경 구독
            _invitationsChannel = SupabaseService.Client.Realtime.Channel("invitations-changes");

            _invitationsChannel.Register(new PostgresChangesOptions("public", "invitations"));

            _invitationsChannel.AddPostgresChangeHandler(PostgresChangesOptions.ListenType.All, (sender, change) =>
            {
                // UI 스레드에서 처리
                Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    await HandleRealtimeChangeAsync(change, currentUserId);
                });
            });

            await _invitationsChannel.Subscribe();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Failed to subscribe to realtime: {ex.Message}");
        }
    }

    /// <summary>
    /// Realtime 변경 처리
    /// </summary>
    private async Task HandleRealtimeChangeAsync(PostgresChangesResponse change, string currentUserId)
    {
        try
        {
            // Model/OldModel로 데이터 가져오기
            Invitation? model = null;
            Invitation? oldModel = null;

            try { model = change.Model<Invitation>(); } catch { }
            try { oldModel = change.OldModel<Invitation>(); } catch { }

            // invited_by 확인
            var invitedBy = model?.InvitedBy.ToString() ?? oldModel?.InvitedBy.ToString();

            // 현재 사용자가 초대한 것만 처리
            if (invitedBy != currentUserId)
            {
                return;
            }

            switch (change.Event)
            {
                case EventType.Update:
                    // 상태 업데이트 (예: pending → accepted)
                    await LoadInvitationsAsync();
                    break;

                case EventType.Delete:
                    // 삭제됨
                    if (oldModel != null)
                    {
                        var toRemove = Invitations.FirstOrDefault(i => i.Id == oldModel.Id);
                        if (toRemove != null)
                        {
                            Invitations.Remove(toRemove);
                            UpdateCounts();
                        }
                    }
                    break;

                case EventType.Insert:
                    // 새 초대 (다른 기기에서 추가된 경우)
                    await LoadInvitationsAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Error handling realtime change: {ex.Message}");
        }
    }

    /// <summary>
    /// ViewModel 정리 (Realtime 구독 해제 및 타이머 중지)
    /// </summary>
    public void Cleanup()
    {
        try
        {
            // 타이머 중지
            StopPeriodicRefresh();

            // Realtime 구독 해제
            if (_invitationsChannel != null)
            {
                _invitationsChannel.Unsubscribe();
                _invitationsChannel = null;
            }

            // 캐시 및 데이터 비우기
            ClearCache();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Error during cleanup: {ex.Message}");
        }
    }

    /// <summary>
    /// 캐시 및 데이터 비우기 (로그아웃 시 호출)
    /// </summary>
    public void ClearCache()
    {
        Invitations.Clear();
        AffiliationOptions.Clear();

        _regions.Clear();
        _districts.Clear();
        _divisions.Clear();
        _battalions.Clear();

        _regionNames.Clear();
        _districtNames.Clear();
        _divisionNames.Clear();
        _battalionNames.Clear();

        _isOrganizationDataLoaded = false;

        TotalCount = 0;
        ActiveCount = 0;
        PendingCount = 0;

        // UI 상태 초기화
        CurrentUserType = "local";
        TargetRole = "";
        InviteFormTitle = "";
        AffiliationLabel = "";
        ShowRankField = false;
        ShowAffiliationTextInput = false;
        ShowAffiliationComboBox = true;
        AffiliationSuffix = "";
        NewAffiliationText = "";
        NewInviteName = "";
        NewInvitePhone = "";
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

            // 빠른 조회를 위한 딕셔너리 캐시 생성
            _regionNames = _regions.ToDictionary(r => r.Id, r => r.Name);
            _districtNames = _districts.ToDictionary(d => d.Id, d => d.Name);
            _divisionNames = _divisions.ToDictionary(d => d.Id, d => d.Name);
            _battalionNames = _battalions.ToDictionary(b => b.Id, b => b.Name);

            _isOrganizationDataLoaded = true;
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

        // DB의 role 문자열을 직접 사용하여 판단
        // 기본값 설정
        ShowAffiliationComboBox = true;
        ShowAffiliationTextInput = false;
        ShowRankField = false;
        AffiliationSuffix = "";

        if (dbRole == "super_admin_mois")
        {
            // 행정안전부 → 지자체(도) 중간관리자 초대
            CurrentUserType = "local";
            TargetRole = "middle_local";
            InviteFormTitle = "지자체(도) 중간관리자 초대";
            AffiliationLabel = "소속 (시/도)";
            ShowRankField = false;
            ShowAffiliationComboBox = true;
            ShowAffiliationTextInput = false;
            InitializeRegionOptions();
        }
        else if (dbRole == "super_admin_army")
        {
            // 육군본부 → 사단담당자 중간관리자 초대
            // 사단은 텍스트 입력 모드 (수기 입력)
            CurrentUserType = "military";
            TargetRole = "middle_military";
            InviteFormTitle = "사단담당자 중간관리자 초대";
            AffiliationLabel = "소속 (사단)";
            ShowRankField = true;
            ShowAffiliationComboBox = false;
            ShowAffiliationTextInput = true;
            AffiliationSuffix = "사단";
        }
        else if (dbRole == "middle_local")
        {
            // 지자체(도) 중간관리자 → 지자체담당자 초대
            CurrentUserType = "local";
            TargetRole = "user_local";
            InviteFormTitle = "지자체담당자 초대";
            AffiliationLabel = "소속 (구)";
            ShowRankField = false;
            ShowAffiliationComboBox = true;
            ShowAffiliationTextInput = false;
            InitializeDistrictOptionsForMiddleLocal();
        }
        else if (dbRole == "middle_military")
        {
            // 사단담당자 중간관리자 → 대대담당자 초대
            // 대대는 텍스트 입력 모드 (수기 입력)
            CurrentUserType = "military";
            TargetRole = "user_military";
            InviteFormTitle = "대대담당자 초대";
            AffiliationLabel = "소속 (대대)";
            ShowRankField = true;
            ShowAffiliationComboBox = false;
            ShowAffiliationTextInput = true;
            AffiliationSuffix = "대대";
        }
        else
        {
            // 일반 사용자는 초대 권한 없음
            CurrentUserType = "none";
            TargetRole = "";
            InviteFormTitle = "";
            ShowAffiliationComboBox = true;
            ShowAffiliationTextInput = false;
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

        var filteredList = filteredDistricts.OrderBy(d => d.Name).ToList();

        foreach (var district in filteredList)
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
    /// 계급 목록 (부사관-준사관-장교) 카테고리별 분류
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

        // 첫 번째 실제 계급 선택 (카테고리가 아닌 것)
        SelectedRankOption = RankOptions.FirstOrDefault(r => !r.IsCategory);
    }

    /// <summary>
    /// 역할별로 볼 수 있는 초대 대상 역할 목록 반환
    /// </summary>
    private List<string> GetVisibleInvitationRoles()
    {
        var currentUser = AuthService.CurrentUser;
        var dbRole = currentUser?.Role ?? "";

        return dbRole switch
        {
            // SW0001 (행정안전부): 지자체 중간관리자, 지자체 담당자만 조회
            "super_admin_mois" => new List<string> { "middle_local", "user_local" },

            // SW0002 (육군본부): 사단 담당자, 대대 담당자만 조회
            "super_admin_army" => new List<string> { "middle_military", "user_military" },

            // 지자체 중간관리자: 지자체 담당자만 조회 (자신이 초대한 것만)
            "middle_local" => new List<string> { "user_local" },

            // 사단 담당자: 대대 담당자만 조회 (자신이 초대한 것만)
            "middle_military" => new List<string> { "user_military" },

            _ => new List<string>()
        };
    }

    private async Task LoadInvitationsAsync()
    {
        if (!SupabaseService.IsInitialized) return;
        if (AuthService.CurrentUser == null) return;

        IsLoading = true;
        try
        {
            var currentUserId = AuthService.CurrentUser.Id;
            var currentRole = AuthService.CurrentUser.Role;
            var visibleRoles = GetVisibleInvitationRoles();

            if (visibleRoles.Count == 0)
            {
                if (Invitations.Count > 0)
                {
                    Invitations.Clear();
                    UpdateCounts();
                }
                return;
            }

            // 자신이 초대한 것만 볼 수 있음
            var response = await SupabaseService.Client
                .From<Invitation>()
                .Filter("invited_by", Supabase.Postgrest.Constants.Operator.Equals, currentUserId.ToString())
                .Filter("role", Supabase.Postgrest.Constants.Operator.In, visibleRoles)
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            // 데이터 비교 후 갱신 방식
            var newInvitations = response.Models;
            var hasChanges = MergeInvitations(newInvitations);

            if (hasChanges)
            {
                UpdateCounts();
            }
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

    /// <summary>
    /// 기존 데이터와 새 데이터를 비교하여 변경된 항목만 업데이트
    /// </summary>
    /// <param name="newInvitations">서버에서 받아온 새 데이터</param>
    /// <returns>변경 사항이 있으면 true</returns>
    private bool MergeInvitations(List<Invitation> newInvitations)
    {
        var hasChanges = false;
        var existingIds = Invitations.ToDictionary(i => i.Id, i => i);
        var newIds = newInvitations.Select(i => i.Id).ToHashSet();

        // 1. 삭제된 항목 제거 (새 데이터에 없는 기존 항목)
        var toRemove = Invitations.Where(i => !newIds.Contains(i.Id)).ToList();
        foreach (var item in toRemove)
        {
            Invitations.Remove(item);
            hasChanges = true;
        }

        // 2. 새 항목 추가 및 기존 항목 업데이트
        for (int i = 0; i < newInvitations.Count; i++)
        {
            var newItem = newInvitations[i];
            newItem.DisplayAffiliation = GetDisplayAffiliation(newItem);

            if (existingIds.TryGetValue(newItem.Id, out var existingItem))
            {
                // 기존 항목 - 변경 사항 확인 후 업데이트
                if (HasInvitationChanged(existingItem, newItem))
                {
                    // 기존 항목의 위치 찾기
                    var existingIndex = Invitations.IndexOf(existingItem);

                    // 값 업데이트
                    existingItem.Status = newItem.Status;
                    existingItem.ExpiresAt = newItem.ExpiresAt;
                    existingItem.AcceptedAt = newItem.AcceptedAt;
                    existingItem.AcceptedUserId = newItem.AcceptedUserId;
                    existingItem.DisplayAffiliation = newItem.DisplayAffiliation;

                    // 순서가 변경된 경우 재정렬
                    if (existingIndex != i && i < Invitations.Count)
                    {
                        Invitations.Move(existingIndex, i);
                    }

                    hasChanges = true;
                }
            }
            else
            {
                // 새 항목 - 올바른 위치에 삽입
                if (i < Invitations.Count)
                {
                    Invitations.Insert(i, newItem);
                }
                else
                {
                    Invitations.Add(newItem);
                }
                hasChanges = true;
            }
        }

        return hasChanges;
    }

    /// <summary>
    /// 초대 데이터가 변경되었는지 확인
    /// </summary>
    private static bool HasInvitationChanged(Invitation existing, Invitation newItem)
    {
        return existing.Status != newItem.Status ||
               existing.ExpiresAt != newItem.ExpiresAt ||
               existing.AcceptedAt != newItem.AcceptedAt ||
               existing.AcceptedUserId != newItem.AcceptedUserId;
    }

    private string? GetDisplayAffiliation(Invitation invitation)
    {
        // 딕셔너리를 사용한 O(1) 조회
        if (invitation.BattalionId.HasValue && _battalionNames.TryGetValue(invitation.BattalionId.Value, out var battalionName))
            return battalionName;

        if (invitation.DivisionId.HasValue && _divisionNames.TryGetValue(invitation.DivisionId.Value, out var divisionName))
            return divisionName;

        if (invitation.DistrictId.HasValue && _districtNames.TryGetValue(invitation.DistrictId.Value, out var districtName))
            return districtName;

        if (invitation.RegionId.HasValue && _regionNames.TryGetValue(invitation.RegionId.Value, out var regionName))
            return regionName;

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
        NewAffiliationText = "";
        CopiedMessage = "";

        if (AffiliationOptions.Count > 0)
            SelectedAffiliation = AffiliationOptions[0];

        // 첫 번째 실제 계급 선택 (카테고리가 아닌 것)
        SelectedRankOption = RankOptions.FirstOrDefault(r => !r.IsCategory);

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

        // 전화번호 11자리 검증
        var phoneDigits = new string(NewInvitePhone.Where(char.IsDigit).ToArray());
        if (phoneDigits.Length > 11)
        {
            CopiedMessage = "연락처는 11자리를 초과할 수 없습니다";
            return;
        }

        // 텍스트 입력 모드일 때 검증 (사단/대대)
        if (ShowAffiliationTextInput)
        {
            if (string.IsNullOrWhiteSpace(NewAffiliationText))
            {
                CopiedMessage = $"{AffiliationSuffix}명을 입력해주세요";
                return;
            }

            // 숫자만 추출하여 5자리 검증
            var affiliationDigits = new string(NewAffiliationText.Where(char.IsDigit).ToArray());
            if (affiliationDigits.Length > 5)
            {
                CopiedMessage = "소속 번호는 최대 5자리입니다";
                return;
            }
        }
        else if (SelectedAffiliation == null)
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
                Name = NewInviteName.Trim(),
                Phone = NewInvitePhone.Trim(),
                MilitaryRank = ShowRankField ? SelectedRankOption?.Value : null,
                InvitedBy = AuthService.CurrentUser.Id,
                CreatedBy = AuthService.CurrentUser.Id,
                Status = "pending",
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            string displayAffiliation = "";

            // 텍스트 입력 모드 (사단 또는 대대)
            if (ShowAffiliationTextInput)
            {
                var currentUser = AuthService.CurrentUser;
                var affiliationName = $"{NewAffiliationText.Trim()}{AffiliationSuffix}";

                if (AffiliationSuffix == "사단")
                {
                    // SW0002 (육군본부) → 사단담당자 초대: divisions 테이블에 생성
                    var newDivision = new Division
                    {
                        Id = Guid.NewGuid(),
                        Name = affiliationName,
                        CreatedAt = DateTime.UtcNow
                    };

                    await SupabaseService.Client
                        .From<Division>()
                        .Insert(newDivision);

                    newInvite.DivisionId = newDivision.Id;
                    displayAffiliation = affiliationName;

                    // 캐시 업데이트
                    _divisionNames[newDivision.Id] = affiliationName;
                }
                else if (AffiliationSuffix == "대대")
                {
                    // middle_military (사단담당자) → 대대담당자 초대: battalions 테이블에 생성
                    var userDivisionId = currentUser.DivisionId;
                    if (!userDivisionId.HasValue)
                    {
                        CopiedMessage = "사용자의 사단 정보가 없습니다";
                        return;
                    }

                    var newBattalion = new Battalion
                    {
                        Id = Guid.NewGuid(),
                        DivisionId = userDivisionId.Value,
                        Name = affiliationName,
                        CreatedAt = DateTime.UtcNow
                    };

                    await SupabaseService.Client
                        .From<Battalion>()
                        .Insert(newBattalion);

                    newInvite.BattalionId = newBattalion.Id;
                    newInvite.DivisionId = userDivisionId.Value;
                    displayAffiliation = affiliationName;

                    // 캐시 업데이트
                    _battalionNames[newBattalion.Id] = affiliationName;
                }
            }
            else
            {
                // ComboBox 선택 모드
                switch (SelectedAffiliation!.Type)
                {
                    case AffiliationType.Region:
                        newInvite.RegionId = SelectedAffiliation.Id;
                        break;
                    case AffiliationType.District:
                        newInvite.DistrictId = SelectedAffiliation.Id;
                        newInvite.RegionId = SelectedAffiliation.ParentId;
                        break;
                    case AffiliationType.Division:
                        newInvite.DivisionId = SelectedAffiliation.Id;
                        break;
                    case AffiliationType.Battalion:
                        newInvite.BattalionId = SelectedAffiliation.Id;
                        newInvite.DivisionId = SelectedAffiliation.ParentId;
                        break;
                }
                displayAffiliation = SelectedAffiliation.Name;
            }

            await SupabaseService.Client
                .From<Invitation>()
                .Insert(newInvite);

            // UI 표시용 소속 설정
            newInvite.DisplayAffiliation = displayAffiliation;
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
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Failed to delete invitation: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ManagerVM] Exception: {ex}");
            CopiedMessage = "삭제 실패: 권한이 없거나 오류가 발생했습니다";
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
        // 조직 데이터는 이미 로드되어 있으면 다시 로드하지 않음
        // 초대 목록만 새로고침
        await LoadInvitationsAsync();
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }

    /// <summary>
    /// 전화번호 변경 시 자동 포맷팅 (CommunityToolkit.Mvvm partial method)
    /// </summary>
    partial void OnNewInvitePhoneChanged(string value)
    {
        if (_isFormattingPhone) return;

        _isFormattingPhone = true;
        try
        {
            var formatted = FormatPhoneNumber(value);
            if (formatted != value)
            {
                NewInvitePhone = formatted;
            }
        }
        finally
        {
            _isFormattingPhone = false;
        }
    }

    /// <summary>
    /// 전화번호 포맷팅: 숫자만 추출하여 010-0000-0000 형식으로 변환
    /// </summary>
    private static string FormatPhoneNumber(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";

        // 숫자만 추출
        var digits = new string(input.Where(char.IsDigit).ToArray());

        // 최대 11자리로 제한
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

/// <summary>
/// 계급 옵션 (카테고리 헤더 또는 실제 계급)
/// </summary>
public class RankOption
{
    public string DisplayName { get; }
    public string? Value { get; }
    public bool IsCategory { get; }

    public RankOption(string displayName, string? value = null, bool isCategory = false)
    {
        DisplayName = displayName;
        Value = value;
        IsCategory = isCategory;
    }

    public override string ToString() => DisplayName;
}
