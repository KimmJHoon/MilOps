using Avalonia.Controls;
using Avalonia.Interactivity;
using MilOps.Models;
using MilOps.Services;
using MilOps.ViewModels;
using System;

namespace MilOps.Views;

public partial class ManagerView : UserControl
{
    private ManagerViewModel? _viewModel;
    private Guid? _lastUserId;
    private string? _lastUserRole;

    // 채팅 시작 요청 이벤트 (MainView에서 구독)
    public event Action<ChatListItem>? OnChatStartRequested;

    public ManagerView()
    {
        InitializeComponent();
        // 부모(MainView)의 MainViewModel DataContext 상속을 차단
        // x:DataType=ManagerViewModel 컴파일 바인딩이 MainViewModel로 캐스트 시도 → InvalidCastException 방지
        DataContext = null;

        // 로그아웃 전 정리 작업 등록
        AppRestartService.CleanupBeforeLogout += OnCleanupBeforeLogout;
    }

    private void OnCleanupBeforeLogout()
    {
        _viewModel?.Cleanup();
        _viewModel = null;
        _lastUserId = null;
        _lastUserRole = null;
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // ViewModel만 생성 (데이터 로드 없음) - 바인딩 오류 방지
        EnsureViewModelCreated();
    }

    // OnLoaded 제거: OnAttachedToVisualTree에서 이미 ViewModel 생성됨.
    // 데이터 로드는 ForceInitialize()(탭 진입 시)에서만 실행.
    // Manager 탭은 기본 탭이 아니므로, 로그인 직후 7개 쿼리 fire-and-forget이
    // CalendarView 초기화와 Supabase 연결 포화를 유발했던 문제 해결.

    /// <summary>
    /// 강제 초기화 (탭 전환 시 호출) - 데이터 로드 포함
    /// </summary>
    public void ForceInitialize()
    {
        InitializeAndLoadData();
    }

    /// <summary>
    /// ViewModel 생성만 (데이터 로드 없음) - OnAttachedToVisualTree에서 호출
    /// </summary>
    private void EnsureViewModelCreated()
    {
        if (AuthService.CurrentUser == null) return;

        var currentUserId = AuthService.CurrentUser.Id;
        var currentUserRole = AuthService.CurrentUser.Role;

        bool needsNewViewModel = _viewModel == null ||
                                  _lastUserId != currentUserId ||
                                  _lastUserRole != currentUserRole;

        if (needsNewViewModel)
        {
            System.Diagnostics.Debug.WriteLine($"[PERF][ManagerInit] ViewModel 생성만 (데이터 로드 없음)");
            if (_viewModel != null)
            {
                _viewModel.ChatStartRequested -= OnViewModelChatStartRequested;
                _viewModel.Cleanup();
            }

            _viewModel = new ManagerViewModel();
            _viewModel.ChatStartRequested += OnViewModelChatStartRequested;
            DataContext = _viewModel;
            _lastUserId = currentUserId;
            _lastUserRole = currentUserRole;
        }
    }

    /// <summary>
    /// ViewModel 생성 + 데이터 로드 (탭 진입 시 호출)
    /// </summary>
    private void InitializeAndLoadData()
    {
        if (AuthService.CurrentUser == null) return;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var currentUserId = AuthService.CurrentUser.Id;
        var currentUserRole = AuthService.CurrentUser.Role;

        bool needsNewViewModel = _viewModel == null ||
                                  _lastUserId != currentUserId ||
                                  _lastUserRole != currentUserRole;

        if (needsNewViewModel)
        {
            System.Diagnostics.Debug.WriteLine($"[PERF][ManagerInit] 새 ViewModel 생성+로드 시작");
            if (_viewModel != null)
            {
                _viewModel.ChatStartRequested -= OnViewModelChatStartRequested;
                _viewModel.Cleanup();
            }

            _viewModel = new ManagerViewModel();
            _viewModel.ChatStartRequested += OnViewModelChatStartRequested;
            DataContext = _viewModel;
            _lastUserId = currentUserId;
            _lastUserRole = currentUserRole;

            // 탭 진입 시에만 데이터 로드
            _ = _viewModel.InitializeAsync();
            System.Diagnostics.Debug.WriteLine($"[PERF][ManagerInit] 새 ViewModel 생성+로드 완료: {sw.ElapsedMilliseconds}ms");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine($"[PERF][ManagerInit] 탭 재진입 → InitializeAsync (캐시 활용)");
            // 캐시가 유효하면 InitializeAsync 내부에서 즉시 반환됨
            _ = _viewModel!.InitializeAsync();
            System.Diagnostics.Debug.WriteLine($"[PERF][ManagerInit] InitializeAsync 호출: {sw.ElapsedMilliseconds}ms");
        }
    }

    private void OnViewModelChatStartRequested(ChatListItem partner)
    {
        OnChatStartRequested?.Invoke(partner);
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // View가 제거될 때 이벤트 해제 및 리소스 정리
        if (_viewModel != null)
        {
            _viewModel.ChatStartRequested -= OnViewModelChatStartRequested;
            _viewModel.Cleanup();
        }
    }
}
