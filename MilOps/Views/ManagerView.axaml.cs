using Avalonia.Controls;
using MilOps.ViewModels;

namespace MilOps.Views;

public partial class ManagerView : UserControl
{
    private ManagerViewModel? _viewModel;

    public ManagerView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(Avalonia.Interactivity.RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // View가 로드될 때 ViewModel이 없거나 역할이 변경되었으면 다시 초기화
        if (_viewModel == null)
        {
            _viewModel = new ManagerViewModel();
            DataContext = _viewModel;
        }
        else
        {
            // 이미 ViewModel이 있으면 역할만 다시 확인하고 데이터 새로고침
            _viewModel.RefreshCommand.Execute(null);
        }
    }
}
