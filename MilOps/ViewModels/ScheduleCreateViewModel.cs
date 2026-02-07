using Avalonia.Threading;
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

/// <summary>
/// 일정 생성 ViewModel (사단담당자 전용)
/// </summary>
public partial class ScheduleCreateViewModel : ViewModelBase
{
    // 로딩 상태
    [ObservableProperty]
    private bool _isLoading = false;

    // 현재 사용자 정보 표시
    [ObservableProperty]
    private string _currentUserDisplay = "";

    // 지역 선택 (Region)
    [ObservableProperty]
    private ObservableCollection<Region> _regions = new();

    [ObservableProperty]
    private Region? _selectedRegion;

    // 시군구 선택 (District)
    [ObservableProperty]
    private ObservableCollection<District> _districts = new();

    [ObservableProperty]
    private District? _selectedDistrict;

    // 담당대대 선택 (Battalion)
    [ObservableProperty]
    private ObservableCollection<Battalion> _battalions = new();

    [ObservableProperty]
    private Battalion? _selectedBattalion;

    // 업체 검색 (Company)
    [ObservableProperty]
    private string _companySearchText = "";

    [ObservableProperty]
    private ObservableCollection<Company> _searchedCompanies = new();

    [ObservableProperty]
    private Company? _selectedCompany;

    [ObservableProperty]
    private bool _showCompanySearchResults = false;

    [ObservableProperty]
    private string _companySearchPlaceholder = "구/군을 먼저 선택하세요";

    // 자동 연결 정보
    [ObservableProperty]
    private bool _showAutoInfo = false;

    // 업체 정보
    [ObservableProperty]
    private string _companyAddress = "";

    [ObservableProperty]
    private string _companyProducts = "";

    // 지자체담당자 정보
    [ObservableProperty]
    private string _localUserName = "";

    [ObservableProperty]
    private string _localUserPhone = "";

    [ObservableProperty]
    private bool _hasLocalUser = false;

    // 대대담당자 정보
    [ObservableProperty]
    private string _militaryUserName = "";

    [ObservableProperty]
    private string _militaryUserPhone = "";

    [ObservableProperty]
    private bool _hasMilitaryUser = false;

    // 에러/성공 메시지
    [ObservableProperty]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _successMessage = "";

    // 권한 체크
    [ObservableProperty]
    private bool _hasPermission = false;

    // 생성 버튼 활성화
    [ObservableProperty]
    private bool _canCreate = false;

    // 캐시된 데이터
    private List<Region> _allRegions = new();
    private List<District> _allDistricts = new();
    private List<Battalion> _allBattalions = new();
    private List<Company> _allCompanies = new();
    private List<User> _allUsers = new();
    private List<DistrictBattalionMapping> _districtBattalionMappings = new();

    // 선택된 담당자
    private User? _selectedLocalUser;
    private User? _selectedMilitaryUser;

    // 이벤트: 화면 닫기 요청
    public event EventHandler? CloseRequested;

    // 이벤트: 일정 생성 완료
    public event EventHandler? ScheduleCreated;

    public ScheduleCreateViewModel()
    {
        CheckPermission();
    }

    private void CheckPermission()
    {
        // 사단담당자만 일정 생성 가능
        HasPermission = AuthService.CurrentUserRole == UserRole.MiddleMilitary;

        // 현재 사용자 정보 표시
        var currentUser = AuthService.CurrentUser;
        if (currentUser != null)
        {
            CurrentUserDisplay = $"👤 {currentUser.FullDisplayName}";
            if (currentUser.DivisionId.HasValue)
            {
                // Division 이름은 나중에 로드
            }
        }
    }

    public async Task InitializeAsync()
    {
        if (!HasPermission)
        {
            ErrorMessage = "일정 생성 권한이 없습니다.";
            return;
        }

        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = "";

        try
        {
            var client = SupabaseService.Client;
            if (client == null)
            {
                return;
            }

            var currentUser = AuthService.CurrentUser;
            if (currentUser == null)
            {
                return;
            }

            // 모든 데이터 병렬 로드
            var regionsTask = client.From<Region>().Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true").Get();
            var districtsTask = client.From<District>().Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true").Get();
            var battalionsTask = client.From<Battalion>().Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true").Get();
            var companiesTask = client.From<Company>().Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true").Get();
            var usersTask = client.From<User>().Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true").Get();
            var divisionsTask = client.From<Division>().Get();

            await Task.WhenAll(regionsTask, districtsTask, battalionsTask, companiesTask, usersTask, divisionsTask);

            _allRegions = regionsTask.Result.Models;
            _allDistricts = districtsTask.Result.Models;
            _allBattalions = battalionsTask.Result.Models;
            _allCompanies = companiesTask.Result.Models;
            _allUsers = usersTask.Result.Models;

            // 현재 사용자의 Division 표시
            if (currentUser.DivisionId.HasValue)
            {
                var division = divisionsTask.Result.Models.FirstOrDefault(d => d.Id == currentUser.DivisionId.Value);
                if (division != null)
                {
                    CurrentUserDisplay = $"👤 {currentUser.FullDisplayName} ({division.Name} 사단담당자)";
                }
            }

            // 사단담당자가 관리하는 대대만 필터링
            var managedBattalions = _allBattalions
                .Where(b => b.DivisionId == currentUser.DivisionId)
                .ToList();

            // UI 업데이트
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Region 목록 설정
                Regions.Clear();
                foreach (var region in _allRegions.OrderBy(r => r.Name))
                {
                    Regions.Add(region);
                }

                // Battalion 목록 설정 (사단담당자가 관리하는 대대만)
                Battalions.Clear();
                foreach (var battalion in managedBattalions.OrderBy(b => b.Name))
                {
                    Battalions.Add(battalion);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleCreateViewModel] LoadDataAsync error: {ex.Message}\n{ex.StackTrace}");
            ErrorMessage = "데이터 로드 중 오류가 발생했습니다.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedRegionChanged(Region? value)
    {
        if (value == null)
        {
            Districts.Clear();
            SelectedDistrict = null;
            return;
        }

        // 해당 Region의 District만 필터링
        Districts.Clear();
        var filtered = _allDistricts.Where(d => d.RegionId == value.Id).OrderBy(d => d.Name).ToList();
        foreach (var district in filtered)
        {
            Districts.Add(district);
        }

        SelectedDistrict = null;
        // 업체 검색 초기화
        CompanySearchText = "";
        SelectedCompany = null;
        SearchedCompanies.Clear();
        ShowCompanySearchResults = false;
        CompanySearchPlaceholder = "구/군을 먼저 선택하세요";
        UpdateAutoInfo();
    }

    partial void OnSelectedDistrictChanged(District? value)
    {
        // 업체 검색 초기화
        CompanySearchText = "";
        SelectedCompany = null;
        SearchedCompanies.Clear();
        ShowCompanySearchResults = false;

        if (value != null)
        {
            CompanySearchPlaceholder = "업체명 검색 (2글자 이상)";
        }
        else
        {
            CompanySearchPlaceholder = "구/군을 먼저 선택하세요";
        }

        UpdateLocalUser();
        UpdateAutoInfo();
    }

    partial void OnSelectedBattalionChanged(Battalion? value)
    {
        UpdateMilitaryUser();
        UpdateAutoInfo();
    }

    partial void OnSelectedCompanyChanged(Company? value)
    {
        if (value != null)
        {
            // 업체 선택 시 검색 결과 숨기기
            ShowCompanySearchResults = false;
            CompanySearchText = value.Name;
        }
        UpdateAutoInfo();
    }

    partial void OnCompanySearchTextChanged(string value)
    {
        // 업체가 선택된 상태에서 검색어가 선택된 업체명과 같으면 무시
        if (SelectedCompany != null && value == SelectedCompany.Name)
        {
            return;
        }

        // 검색어 변경 시 선택 해제
        SelectedCompany = null;
        SearchCompanies();
    }

    private void SearchCompanies()
    {
        SearchedCompanies.Clear();

        if (SelectedDistrict == null)
        {
            ShowCompanySearchResults = false;
            return;
        }

        var searchText = CompanySearchText?.Trim() ?? "";

        // 2글자 이상 입력 시 검색
        if (searchText.Length < 2)
        {
            ShowCompanySearchResults = false;
            return;
        }

        // 선택된 구/군의 업체 중 검색어가 포함된 업체 검색
        var filtered = _allCompanies
            .Where(c => c.DistrictId == SelectedDistrict.Id &&
                       c.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Name)
            .Take(10) // 최대 10개까지만 표시
            .ToList();

        foreach (var company in filtered)
        {
            SearchedCompanies.Add(company);
        }

        ShowCompanySearchResults = SearchedCompanies.Count > 0;
    }

    [RelayCommand]
    private void SelectCompany(Company company)
    {
        SelectedCompany = company;
        ShowCompanySearchResults = false;
    }

    [RelayCommand]
    private void ClearCompanySelection()
    {
        SelectedCompany = null;
        CompanySearchText = "";
        SearchedCompanies.Clear();
        ShowCompanySearchResults = false;
        UpdateAutoInfo();
    }

    private void UpdateLocalUser()
    {
        _selectedLocalUser = null;
        HasLocalUser = false;
        LocalUserName = "";
        LocalUserPhone = "";

        if (SelectedDistrict == null) return;

        // 해당 구/군의 지자체담당자 찾기 (user_local 역할)
        _selectedLocalUser = _allUsers
            .FirstOrDefault(u => u.Role == "user_local" && u.DistrictId == SelectedDistrict.Id);

        if (_selectedLocalUser != null)
        {
            HasLocalUser = true;
            LocalUserName = $"[민] {_selectedLocalUser.FullDisplayName}";
            LocalUserPhone = _selectedLocalUser.Phone;
        }
        else
        {
            LocalUserName = "! 담당자 미지정";
        }
    }

    private void UpdateMilitaryUser()
    {
        _selectedMilitaryUser = null;
        HasMilitaryUser = false;
        MilitaryUserName = "";
        MilitaryUserPhone = "";

        if (SelectedBattalion == null) return;

        // 해당 대대의 대대담당자 찾기 (user_military 역할)
        _selectedMilitaryUser = _allUsers
            .FirstOrDefault(u => u.Role == "user_military" && u.BattalionId == SelectedBattalion.Id);

        if (_selectedMilitaryUser != null)
        {
            HasMilitaryUser = true;
            MilitaryUserName = $"[군] {_selectedMilitaryUser.FullDisplayName}";
            MilitaryUserPhone = _selectedMilitaryUser.Phone;
        }
        else
        {
            MilitaryUserName = "! 담당자 미지정";
        }
    }

    private void UpdateAutoInfo()
    {
        // 업체 정보 표시
        if (SelectedCompany != null)
        {
            CompanyAddress = $"📍 {SelectedCompany.Address}";
            CompanyProducts = !string.IsNullOrEmpty(SelectedCompany.Products)
                ? $"📦 {SelectedCompany.Products}"
                : "";
        }
        else
        {
            CompanyAddress = "";
            CompanyProducts = "";
        }

        // 자동 연결 정보 표시 여부
        ShowAutoInfo = SelectedCompany != null ||
                      SelectedDistrict != null ||
                      SelectedBattalion != null;

        // 생성 가능 여부 체크
        ValidateCanCreate();
    }

    private void ValidateCanCreate()
    {
        CanCreate = SelectedRegion != null &&
                   SelectedDistrict != null &&
                   SelectedBattalion != null &&
                   SelectedCompany != null &&
                   _selectedLocalUser != null &&
                   _selectedMilitaryUser != null;

        if (!CanCreate && ShowAutoInfo)
        {
            // 에러 메시지 설정
            if (_selectedLocalUser == null && SelectedDistrict != null)
            {
                ErrorMessage = "선택한 지역에 지자체담당자가 없습니다.";
            }
            else if (_selectedMilitaryUser == null && SelectedBattalion != null)
            {
                ErrorMessage = "선택한 대대에 대대담당자가 없습니다.";
            }
            else
            {
                ErrorMessage = "";
            }
        }
        else
        {
            ErrorMessage = "";
        }
    }

    [RelayCommand]
    private async Task CreateScheduleAsync()
    {
        if (!CanCreate)
        {
            ErrorMessage = "모든 항목을 선택해주세요.";
            return;
        }

        IsLoading = true;
        ErrorMessage = "";
        SuccessMessage = "";

        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var currentUser = AuthService.CurrentUser;
            if (currentUser == null) return;

            // 새 일정 생성
            var newSchedule = new Schedule
            {
                Id = Guid.NewGuid(),
                CompanyId = SelectedCompany!.Id,
                LocalUserId = _selectedLocalUser!.Id,
                MilitaryUserId = _selectedMilitaryUser!.Id,
                CreatedBy = currentUser.Id,
                Status = "created",
                StatusOrder = 1
            };

            await client.From<Schedule>().Insert(newSchedule);

            SuccessMessage = "일정이 생성되었습니다.";

            // TODO: 알림 기능 구현 시 지자체담당자에게 알림 발송

            // 이벤트 발생
            ScheduleCreated?.Invoke(this, EventArgs.Empty);

            // 폼 초기화
            ClearForm();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleCreateViewModel] CreateScheduleAsync error: {ex.Message}\n{ex.StackTrace}");
            ErrorMessage = "일정 생성에 실패했습니다.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private void ClearForm()
    {
        SelectedRegion = null;
        SelectedDistrict = null;
        SelectedBattalion = null;
        SelectedCompany = null;
        CompanySearchText = "";
        Districts.Clear();
        SearchedCompanies.Clear();
        ShowCompanySearchResults = false;
        CompanySearchPlaceholder = "구/군을 먼저 선택하세요";
        _selectedLocalUser = null;
        _selectedMilitaryUser = null;
        ShowAutoInfo = false;
        CompanyAddress = "";
        CompanyProducts = "";
        LocalUserName = "";
        LocalUserPhone = "";
        MilitaryUserName = "";
        MilitaryUserPhone = "";
        HasLocalUser = false;
        HasMilitaryUser = false;
        CanCreate = false;
        ErrorMessage = "";
    }
}
