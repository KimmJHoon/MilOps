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
/// 업체 등록 ViewModel (지자체(도) 중간관리자 전용)
/// </summary>
public partial class CompanyRegisterViewModel : ViewModelBase
{
    // 업체 목록
    [ObservableProperty]
    private ObservableCollection<CompanyListItem> _companies = new();

    // 로딩 상태
    [ObservableProperty]
    private bool _isLoading = false;

    // 다이얼로그 표시 여부
    [ObservableProperty]
    private bool _isRegisterDialogOpen = false;

    // 폼 모드 (create/edit)
    [ObservableProperty]
    private string _formMode = "create";

    // 폼 타이틀
    [ObservableProperty]
    private string _formTitle = "업체 등록";

    // 빈 목록 메시지
    [ObservableProperty]
    private bool _showEmptyMessage = false;

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

    // 입력 필드
    [ObservableProperty]
    private string _address = "";

    // 입력 필드
    [ObservableProperty]
    private string _companyName = "";

    [ObservableProperty]
    private string _products = "";

    // 에러 메시지
    [ObservableProperty]
    private string _errorMessage = "";

    // 성공 메시지
    [ObservableProperty]
    private string _successMessage = "";

    // 권한 체크
    [ObservableProperty]
    private bool _hasPermission = false;

    // 삭제 확인 모달
    [ObservableProperty]
    private bool _isDeleteConfirmVisible = false;

    [ObservableProperty]
    private string _deleteConfirmCompanyName = "";

    // 삭제 불가 에러 모달
    [ObservableProperty]
    private bool _isDeleteErrorVisible = false;

    [ObservableProperty]
    private string _deleteErrorMessage = "";

    // 삭제 대기 중인 아이템
    private CompanyListItem? _pendingDeleteItem;

    // 수정 중인 업체 ID
    private Guid? _editingCompanyId;

    // 캐시된 데이터
    private List<Region> _allRegions = new();
    private List<District> _allDistricts = new();
    private Dictionary<Guid, string> _regionNames = new();
    private Dictionary<Guid, string> _districtNames = new();

    // 이벤트: 화면 닫기 요청
    public event EventHandler? CloseRequested;

    public CompanyRegisterViewModel()
    {
        CheckPermission();
    }

    private void CheckPermission()
    {
        // 지자체(도) 중간관리자만 업체 등록 가능
        HasPermission = AuthService.CurrentUserRole == UserRole.MiddleLocal;
    }

    public async Task InitializeAsync()
    {
        if (!HasPermission)
        {
            ErrorMessage = "업체 등록 권한이 없습니다.";
            return;
        }

        await LoadOrganizationDataAsync();
        await LoadCompaniesAsync();
    }

    private async Task LoadOrganizationDataAsync()
    {
        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            // 현재 사용자의 region_id 가져오기
            var currentUser = AuthService.CurrentUser;

            if (currentUser?.RegionId == null)
            {
                ErrorMessage = "사용자의 지역 정보가 없습니다.";
                return;
            }

            // Region 로드 (현재 사용자의 지역만)
            _allRegions = await QueryHelper.GetByFilterAsync<Region>("id", currentUser.RegionId.ToString()!);
            _regionNames = _allRegions.ToDictionary(r => r.Id, r => r.Name);

            // District 로드 (해당 Region의 시군구만)
            _allDistricts = await QueryHelper.GetByFilterAsync<District>("region_id", currentUser.RegionId.ToString()!);
            _districtNames = _allDistricts.ToDictionary(d => d.Id, d => d.Name);

            // UI 업데이트
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // 일괄 교체 — N번 CollectionChanged → 1번
                Regions = new ObservableCollection<Region>(_allRegions);

                if (Regions.Count > 0)
                {
                    SelectedRegion = Regions[0];
                }

                // Districts도 바로 표시 — 일괄 교체
                Districts = new ObservableCollection<District>(_allDistricts);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"조직 데이터 로드 실패: {ex.Message}\n{ex.StackTrace}");
            ErrorMessage = "데이터 로드 중 오류가 발생했습니다.";
        }
    }

    partial void OnSelectedRegionChanged(Region? value)
    {
        if (value == null) return;

        // 해당 Region의 District만 필터링 — 일괄 교체
        Districts = new ObservableCollection<District>(
            _allDistricts.Where(d => d.RegionId == value.Id));

        SelectedDistrict = null;
    }

    private async Task LoadCompaniesAsync()
    {
        if (IsLoading) return;

        IsLoading = true;
        ErrorMessage = "";

        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var currentUser = AuthService.CurrentUser;
            if (currentUser?.RegionId == null) return;

            // 현재 사용자가 관리하는 지역의 업체만 로드
            var districtIds = _allDistricts.Select(d => d.Id.ToString()).ToList();

            // 모든 활성 업체 로드
            var companiesResponse = await client.From<Company>()
                .Filter("is_active", Supabase.Postgrest.Constants.Operator.Equals, "true")
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            // 필터링: 해당 지역 업체만
            List<Company> companies;
            if (districtIds.Count > 0)
            {
                companies = companiesResponse.Models
                    .Where(c => !c.IsDeleted && districtIds.Contains(c.DistrictId.ToString()))
                    .ToList();
            }
            else
            {
                // districtIds가 비어있으면 현재 사용자가 생성한 업체만 표시
                companies = companiesResponse.Models
                    .Where(c => !c.IsDeleted && c.CreatedBy == currentUser.Id)
                    .ToList();
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // 일괄 교체 — N번 CollectionChanged → 1번
                Companies = new ObservableCollection<CompanyListItem>(
                    companies.Select(c => CreateCompanyListItem(c)));
                ShowEmptyMessage = Companies.Count == 0;
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"업체 목록 로드 실패: {ex.Message}\n{ex.StackTrace}");
            ErrorMessage = "업체 목록을 불러오는데 실패했습니다.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private CompanyListItem CreateCompanyListItem(Company company)
    {
        var districtName = _districtNames.GetValueOrDefault(company.DistrictId, "알 수 없음");
        var regionName = "";

        var district = _allDistricts.FirstOrDefault(d => d.Id == company.DistrictId);
        if (district != null)
        {
            regionName = _regionNames.GetValueOrDefault(district.RegionId, "");
        }

        return new CompanyListItem
        {
            Id = company.Id,
            Name = company.Name,
            Address = company.Address,
            Products = company.Products ?? "",
            DistrictId = company.DistrictId,
            RegionName = regionName,
            DistrictName = districtName,
            CreatedAt = company.CreatedAt
        };
    }

    [RelayCommand]
    private void OpenRegisterDialog()
    {
        FormMode = "create";
        FormTitle = "업체 등록";
        ClearForm();
        IsRegisterDialogOpen = true;
    }

    [RelayCommand]
    private void CloseRegisterDialog()
    {
        IsRegisterDialogOpen = false;
        ClearForm();
    }

    [RelayCommand]
    private void EditCompany(CompanyListItem? item)
    {
        if (item == null) return;

        FormMode = "edit";
        FormTitle = "업체 수정";
        _editingCompanyId = item.Id;

        CompanyName = item.Name;
        Address = item.Address;
        Products = item.Products;

        // 지역 선택
        var district = _allDistricts.FirstOrDefault(d => d.Id == item.DistrictId);
        if (district != null)
        {
            SelectedRegion = Regions.FirstOrDefault(r => r.Id == district.RegionId);
            // OnSelectedRegionChanged가 Districts를 업데이트함
            SelectedDistrict = Districts.FirstOrDefault(d => d.Id == item.DistrictId);
        }

        IsRegisterDialogOpen = true;
    }

    [RelayCommand]
    private async Task SaveCompanyAsync()
    {
        if (!ValidateForm()) return;

        IsLoading = true;
        ErrorMessage = "";
        SuccessMessage = "";

        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var currentUser = AuthService.CurrentUser;
            if (currentUser == null) return;

            if (FormMode == "create")
            {
                // 새 업체 생성
                var newCompany = new Company
                {
                    Id = Guid.NewGuid(),  // UUID 생성
                    Name = CompanyName.Trim(),
                    Address = Address.Trim(),
                    Products = string.IsNullOrWhiteSpace(Products) ? null : Products.Trim(),
                    DistrictId = SelectedDistrict!.Id,
                    CreatedBy = currentUser.Id,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await client.From<Company>().Insert(newCompany);
                SuccessMessage = "업체가 등록되었습니다.";
            }
            else if (FormMode == "edit" && _editingCompanyId.HasValue)
            {
                // 업체 수정
#pragma warning disable CS8603 // Possible null reference return
                await client.From<Company>()
                    .Where(c => c.Id == _editingCompanyId.Value)
                    .Set(c => c.Name, CompanyName.Trim())
                    .Set(c => c.Address, Address.Trim())
                    .Set(c => c.Products, string.IsNullOrWhiteSpace(Products) ? null : Products.Trim())
                    .Set(c => c.DistrictId, SelectedDistrict!.Id)
                    .Update();
#pragma warning restore CS8603

                SuccessMessage = "업체가 수정되었습니다.";
            }

            // 다이얼로그 닫기
            IsRegisterDialogOpen = false;
            ClearForm();

            // 로딩 상태 해제 후 목록 새로고침
            IsLoading = false;
            await LoadCompaniesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"업체 저장 실패: {ex.Message}");
            ErrorMessage = "업체 저장에 실패했습니다.";
            IsLoading = false;
        }
    }

    /// <summary>
    /// 삭제 버튼 클릭 → 확인 모달 표시
    /// </summary>
    [RelayCommand]
    private void DeleteCompany(CompanyListItem? item)
    {
        if (item == null) return;

        _pendingDeleteItem = item;
        DeleteConfirmCompanyName = item.Name;
        IsDeleteConfirmVisible = true;
    }

    /// <summary>
    /// 삭제 확인 모달에서 "삭제" 클릭 → 일정 존재 여부 확인 후 삭제 실행
    /// </summary>
    [RelayCommand]
    private async Task ConfirmDeleteAsync()
    {
        IsDeleteConfirmVisible = false;

        if (_pendingDeleteItem == null) return;

        ErrorMessage = "";

        try
        {
            var client = SupabaseService.Client;
            if (client == null) return;

            var currentUser = AuthService.CurrentUser;
            if (currentUser == null) return;

            // 해당 업체에 활성 일정이 있는지 확인 (삭제되지 않은 일정)
            var schedulesResponse = await client.From<Schedule>()
                .Filter("company_id", Supabase.Postgrest.Constants.Operator.Equals, _pendingDeleteItem.Id.ToString())
                .Get();

            var activeSchedules = schedulesResponse.Models
                .Where(s => !s.IsDeleted)
                .ToList();

            if (activeSchedules.Count > 0)
            {
                // 일정이 존재하면 삭제 불가 에러 모달 표시
                DeleteErrorMessage = "이미 일정이 등록된 업체는 삭제가 불가능합니다.";
                IsDeleteErrorVisible = true;
                _pendingDeleteItem = null;
                return;
            }

            // 일정이 없으면 Soft delete 실행
#pragma warning disable CS8603 // Possible null reference return
            await client.From<Company>()
                .Where(c => c.Id == _pendingDeleteItem.Id)
                .Set(c => c.DeletedAt, DateTime.UtcNow)
                .Set(c => c.DeletedBy, currentUser.Id)
                .Set(c => c.IsActive, false)
                .Update();
#pragma warning restore CS8603

            SuccessMessage = "업체가 삭제되었습니다.";
            _pendingDeleteItem = null;

            // 목록 새로고침
            await LoadCompaniesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"업체 삭제 실패: {ex.Message}\n{ex.StackTrace}");
            ErrorMessage = "업체 삭제에 실패했습니다.";
            _pendingDeleteItem = null;
        }
    }

    /// <summary>
    /// 삭제 확인 모달에서 "취소" 클릭
    /// </summary>
    [RelayCommand]
    private void CancelDelete()
    {
        IsDeleteConfirmVisible = false;
        _pendingDeleteItem = null;
    }

    /// <summary>
    /// 삭제 에러 모달 닫기
    /// </summary>
    [RelayCommand]
    private void CloseDeleteError()
    {
        IsDeleteErrorVisible = false;
        DeleteErrorMessage = "";
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool ValidateForm()
    {
        if (string.IsNullOrWhiteSpace(CompanyName))
        {
            ErrorMessage = "업체명을 입력해주세요.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Address))
        {
            ErrorMessage = "주소를 입력해주세요.";
            return false;
        }

        if (SelectedDistrict == null)
        {
            ErrorMessage = "시/군/구를 선택해주세요.";
            return false;
        }

        ErrorMessage = "";
        return true;
    }

    private void ClearForm()
    {
        _editingCompanyId = null;
        CompanyName = "";
        Address = "";
        Products = "";
        SelectedDistrict = null;
        ErrorMessage = "";
        SuccessMessage = "";
    }
}

/// <summary>
/// 업체 목록 아이템
/// </summary>
public class CompanyListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Products { get; set; } = "";
    public Guid DistrictId { get; set; }
    public string RegionName { get; set; } = "";
    public string DistrictName { get; set; } = "";
    public DateTime CreatedAt { get; set; }

    public string LocationDisplay => $"{RegionName} {DistrictName}";
    public string CreatedAtDisplay => CreatedAt.Year > 2000 ? CreatedAt.ToLocalTime().ToString("yyyy-MM-dd") : "";
}
