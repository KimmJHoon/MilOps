using Avalonia.Controls;
using MilOps.ViewModels;
using System;

namespace MilOps.Views;

public partial class MainView : UserControl
{
    private readonly MainViewModel _viewModel;

    public event Action? LogoutRequested;

    public MainView()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        _viewModel.LogoutRequested += () => LogoutRequested?.Invoke();
        DataContext = _viewModel;
    }
}