using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MilOps.Models;
using MilOps.Services;
using MilOps.ViewModels;
using System;
using System.Linq;

namespace MilOps.Views;

public partial class ScheduleListView : UserControl
{
    private ScheduleListViewModel? _viewModel;
    private Guid? _lastUserId;
    private string? _lastUserRole;

    private bool _cleanupRegistered = false;

    public ScheduleListView()
    {
        InitializeComponent();
    }

    private void RegisterCleanupHandler()
    {
        if (!_cleanupRegistered)
        {
            AppRestartService.CleanupBeforeLogout += OnCleanupBeforeLogout;
            _cleanupRegistered = true;
        }
    }

    private void UnregisterCleanupHandler()
    {
        if (_cleanupRegistered)
        {
            AppRestartService.CleanupBeforeLogout -= OnCleanupBeforeLogout;
            _cleanupRegistered = false;
        }
    }

    private void OnCleanupBeforeLogout()
    {
        try
        {
            if (_viewModel != null)
            {
                _viewModel.NavigateToCompanyRegister -= OnNavigateToCompanyRegister;
                _viewModel.NavigateToScheduleCreate -= OnNavigateToScheduleCreate;
                _viewModel.NavigateToScheduleDetail -= OnNavigateToScheduleDetail;
            }
            _viewModel?.ClearCache();
            _viewModel = null;
            _lastUserId = null;
            _lastUserRole = null;
            DataContext = null;

            // ScheduleDataService 캐시 초기화 (공유 캐시)
            ScheduleDataService.ClearCache();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ScheduleListView] CleanupBeforeLogout error: {ex.Message}");
        }
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RegisterCleanupHandler();
        InitializeViewModel();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        InitializeViewModel();
    }

    /// <summary>
    /// 강제 초기화 (탭 전환 시 호출)
    /// </summary>
    public void ForceInitialize()
    {
        InitializeViewModel();
    }

    private void InitializeViewModel()
    {
        // 현재 사용자 정보가 없으면 무시
        if (AuthService.CurrentUser == null) return;

        var currentUserId = AuthService.CurrentUser.Id;
        var currentUserRole = AuthService.CurrentUser.Role;

        // 사용자가 변경되었거나 역할이 변경되었거나 처음 로드하는 경우 ViewModel 재생성
        bool needsNewViewModel = _viewModel == null ||
                                  _lastUserId != currentUserId ||
                                  _lastUserRole != currentUserRole;

        if (needsNewViewModel)
        {
            // 기존 ViewModel 정리
            if (_viewModel != null)
            {
                _viewModel.NavigateToCompanyRegister -= OnNavigateToCompanyRegister;
                _viewModel.NavigateToScheduleCreate -= OnNavigateToScheduleCreate;
                _viewModel.NavigateToScheduleDetail -= OnNavigateToScheduleDetail;
            }
            _viewModel?.ClearCache();

            // 새 ViewModel 생성
            _viewModel = new ScheduleListViewModel();
            _viewModel.NavigateToCompanyRegister += OnNavigateToCompanyRegister;
            _viewModel.NavigateToScheduleCreate += OnNavigateToScheduleCreate;
            _viewModel.NavigateToScheduleDetail += OnNavigateToScheduleDetail;
            DataContext = _viewModel;
            _lastUserId = currentUserId;
            _lastUserRole = currentUserRole;
        }
        else
        {
            // 동일 사용자면 데이터만 새로고침
            _viewModel?.RefreshCommand.Execute(null);
        }
    }

    private void OnNavigateToCompanyRegister()
    {
        // 부모 MainView 찾아서 업체 등록 화면 열기
        var mainView = this.GetVisualAncestors().OfType<MainView>().FirstOrDefault();
        mainView?.OpenCompanyRegister();
    }

    private void OnNavigateToScheduleCreate()
    {
        // 부모 MainView 찾아서 일정 생성 화면 열기
        var mainView = this.GetVisualAncestors().OfType<MainView>().FirstOrDefault();
        mainView?.OpenScheduleCreate();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // View가 제거될 때 정리
        UnregisterCleanupHandler();
        if (_viewModel != null)
        {
            _viewModel.NavigateToCompanyRegister -= OnNavigateToCompanyRegister;
            _viewModel.NavigateToScheduleCreate -= OnNavigateToScheduleCreate;
            _viewModel.NavigateToScheduleDetail -= OnNavigateToScheduleDetail;
        }
        _viewModel?.ClearCache();
    }

    private void OnNavigateToScheduleDetail(Schedule schedule, string mode)
    {
        var mainView = this.GetVisualAncestors().OfType<MainView>().FirstOrDefault();
        mainView?.OpenScheduleInput(schedule.Id, mode);
    }

    public ScheduleListViewModel? ViewModel => _viewModel;

    /// <summary>
    /// 삭제 모달 배경 오버레이 클릭 시 모달 닫기
    /// </summary>
    private void OnDeleteModalOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        _viewModel?.CancelDeleteCommand.Execute(null);
    }
}
