using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MilOps.Models;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace MilOps.ViewModels;

public partial class ManagerViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<Invitation> _invitations = new();

    [ObservableProperty]
    private int _totalCount = 0;

    [ObservableProperty]
    private int _activeCount = 0;

    [ObservableProperty]
    private int _pendingCount = 0;

    [ObservableProperty]
    private bool _isNewInviteDialogOpen = false;

    [ObservableProperty]
    private string _newInviteName = "";

    [ObservableProperty]
    private string _newInviteRegion = "";

    public ManagerViewModel()
    {
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        TotalCount = Invitations.Count;
        ActiveCount = Invitations.Count(i => i.Status == InvitationStatus.Active);
        PendingCount = Invitations.Count(i => i.Status == InvitationStatus.Pending);
    }

    [RelayCommand]
    private void OpenNewInviteDialog()
    {
        NewInviteName = "";
        NewInviteRegion = "";
        IsNewInviteDialogOpen = true;
    }

    [RelayCommand]
    private void CloseNewInviteDialog()
    {
        IsNewInviteDialogOpen = false;
    }

    [RelayCommand]
    private void SendInvite()
    {
        if (string.IsNullOrWhiteSpace(NewInviteName) || string.IsNullOrWhiteSpace(NewInviteRegion))
            return;

        var newInvite = new Invitation
        {
            Id = Guid.NewGuid().ToString(),
            Name = NewInviteName,
            Region = NewInviteRegion,
            RegisteredDate = DateTime.Today,
            ExpiryDate = DateTime.Today.AddDays(7),
            Status = InvitationStatus.Pending
        };

        Invitations.Insert(0, newInvite);
        UpdateCounts();
        IsNewInviteDialogOpen = false;
    }

    [RelayCommand]
    private void DeleteInvitation(Invitation invitation)
    {
        Invitations.Remove(invitation);
        UpdateCounts();
    }

    [RelayCommand]
    private void ResendInvitation(Invitation invitation)
    {
        invitation.ExpiryDate = DateTime.Today.AddDays(7);
        // 실제로는 초대 이메일/SMS 재발송 로직
    }

    [RelayCommand]
    private void CancelInvitation(Invitation invitation)
    {
        Invitations.Remove(invitation);
        UpdateCounts();
    }
}
