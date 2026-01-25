using Avalonia.Controls;
using System;

namespace MilOps.Views;

public partial class CompanyRegisterView : UserControl
{
    public event EventHandler? CloseRequested;

    public CompanyRegisterView()
    {
        InitializeComponent();
    }

    protected void OnCloseRequested()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
