using Avalonia.Controls;
using MilOps.ViewModels;

namespace MilOps.Views;

public partial class ManagerView : UserControl
{
    public ManagerView()
    {
        InitializeComponent();
        DataContext = new ManagerViewModel();
    }
}
