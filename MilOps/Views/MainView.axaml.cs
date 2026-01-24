using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MilOps.ViewModels;
using System;
using System.Threading.Tasks;

namespace MilOps.Views;

public partial class MainView : UserControl
{
    private readonly MainViewModel _viewModel;
    private TranslateTransform? _drawerTransform;
    private bool _isAnimating = false;

    public event Action? LogoutRequested;

    public MainView()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        _viewModel.LogoutRequested += () => LogoutRequested?.Invoke();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        DataContext = _viewModel;

        _drawerTransform = DrawerPanel.RenderTransform as TranslateTransform;
    }

    private void OnOverlayPressed(object? sender, PointerPressedEventArgs e)
    {
        _viewModel.CloseDrawerCommand.Execute(null);
    }

    private async void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsDrawerOpen) && !_isAnimating)
        {
            await AnimateDrawer(_viewModel.IsDrawerOpen);
        }
    }

    private async Task AnimateDrawer(bool open)
    {
        if (_drawerTransform == null) return;

        _isAnimating = true;
        var duration = TimeSpan.FromMilliseconds(200);

        if (open)
        {
            DrawerOverlay.IsVisible = true;
            DrawerPanel.IsVisible = true;

            // 동시에 애니메이션
            _ = AnimateOpacity(DrawerOverlay, 0, 0.5, duration);
            await AnimateTranslateX(_drawerTransform, -250, 0, duration);
        }
        else
        {
            // 동시에 애니메이션
            _ = AnimateOpacity(DrawerOverlay, 0.5, 0, duration);
            await AnimateTranslateX(_drawerTransform, 0, -250, duration);

            DrawerOverlay.IsVisible = false;
            DrawerPanel.IsVisible = false;
        }

        _isAnimating = false;
    }

    private async Task AnimateOpacity(Control control, double from, double to, TimeSpan duration)
    {
        var steps = 20;
        var stepDuration = duration.TotalMilliseconds / steps;
        var diff = to - from;

        for (int i = 0; i <= steps; i++)
        {
            var progress = (double)i / steps;
            var easedProgress = 1 - Math.Pow(1 - progress, 3);
            control.Opacity = from + (diff * easedProgress);
            await Task.Delay((int)stepDuration);
        }

        control.Opacity = to;
    }

    private async Task AnimateTranslateX(TranslateTransform transform, double from, double to, TimeSpan duration)
    {
        var steps = 20;
        var stepDuration = duration.TotalMilliseconds / steps;
        var diff = to - from;

        for (int i = 0; i <= steps; i++)
        {
            var progress = (double)i / steps;
            var easedProgress = 1 - Math.Pow(1 - progress, 3);
            transform.X = from + (diff * easedProgress);
            await Task.Delay((int)stepDuration);
        }

        transform.X = to;
    }
}