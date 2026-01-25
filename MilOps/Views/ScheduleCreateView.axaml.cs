using Avalonia.Controls;
using System;

namespace MilOps.Views;

public partial class ScheduleCreateView : UserControl
{
    public event EventHandler? CloseRequested;
    public event EventHandler? ScheduleCreated;

    public ScheduleCreateView()
    {
        InitializeComponent();
    }

    protected void OnCloseRequested()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    protected void OnScheduleCreated()
    {
        ScheduleCreated?.Invoke(this, EventArgs.Empty);
    }
}
