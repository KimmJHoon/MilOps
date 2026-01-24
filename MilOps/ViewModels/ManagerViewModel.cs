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
    private string _newInviteRole = "middle_local";

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private ObservableCollection<RoleOption> _availableRoles = new();

    public ManagerViewModel()
    {
        InitializeAvailableRoles();
        _ = LoadInvitationsAsync();
    }

    private void InitializeAvailableRoles()
    {
        AvailableRoles.Clear();

        if (AuthService.IsSuperAdmin)
        {
            // 최상위 관리자는 중간 담당자만 초대 가능
            AvailableRoles.Add(new RoleOption("middle_local", "지자체 담당자"));
            AvailableRoles.Add(new RoleOption("middle_military", "군부대 담당자"));
        }
        else
        {
            // 중간 담당자는 실무자만 초대 가능
            AvailableRoles.Add(new RoleOption("user_local", "지자체 실무자"));
            AvailableRoles.Add(new RoleOption("user_military", "군부대 실무자"));
        }

        // 기본 선택값 설정
        if (AvailableRoles.Count > 0)
        {
            NewInviteRole = AvailableRoles[0].Value;
        }
    }

    private async Task LoadInvitationsAsync()
    {
        if (!SupabaseService.IsInitialized) return;
        if (AuthService.CurrentUser == null) return;  // 로그인 전이면 스킵

        IsLoading = true;
        try
        {
            var currentUserId = AuthService.CurrentUser.Id;
            var response = await SupabaseService.Client
                .From<Invitation>()
                .Filter("created_by", Supabase.Postgrest.Constants.Operator.Equals, currentUserId.ToString())
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            Invitations.Clear();
            foreach (var invitation in response.Models)
            {
                Invitations.Add(invitation);
            }
            UpdateCounts();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load invitations: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void UpdateCounts()
    {
        TotalCount = Invitations.Count;
        ActiveCount = Invitations.Count(i => i.IsUsed);
        PendingCount = Invitations.Count(i => i.IsValid);
    }

    [RelayCommand]
    private void OpenNewInviteDialog()
    {
        // 첫 번째 가능한 역할로 초기화
        if (AvailableRoles.Count > 0)
        {
            NewInviteRole = AvailableRoles[0].Value;
        }
        IsNewInviteDialogOpen = true;
    }

    [RelayCommand]
    private void CloseNewInviteDialog()
    {
        IsNewInviteDialogOpen = false;
    }

    [RelayCommand]
    private async Task SendInviteAsync()
    {
        if (string.IsNullOrWhiteSpace(NewInviteRole))
            return;

        if (AuthService.CurrentUser == null) return;

        IsLoading = true;
        try
        {
            var newInvite = new Invitation
            {
                Id = Guid.NewGuid(),
                InviteCode = GenerateInviteCode(),
                Role = NewInviteRole,
                CreatedBy = AuthService.CurrentUser.Id,
                ExpiresAt = DateTime.UtcNow.AddDays(7),
                CreatedAt = DateTime.UtcNow
            };

            await SupabaseService.Client
                .From<Invitation>()
                .Insert(newInvite);

            Invitations.Insert(0, newInvite);
            UpdateCounts();
            IsNewInviteDialogOpen = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to create invitation: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task DeleteInvitationAsync(Invitation invitation)
    {
        try
        {
            await SupabaseService.Client
                .From<Invitation>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, invitation.Id.ToString())
                .Delete();

            Invitations.Remove(invitation);
            UpdateCounts();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to delete invitation: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task ResendInvitationAsync(Invitation invitation)
    {
        try
        {
            invitation.ExpiresAt = DateTime.UtcNow.AddDays(7);

            await SupabaseService.Client
                .From<Invitation>()
                .Filter("id", Supabase.Postgrest.Constants.Operator.Equals, invitation.Id.ToString())
                .Set(i => i.ExpiresAt, invitation.ExpiresAt)
                .Update();

            UpdateCounts();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to resend invitation: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task CancelInvitationAsync(Invitation invitation)
    {
        await DeleteInvitationAsync(invitation);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadInvitationsAsync();
    }

    private static string GenerateInviteCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());
    }
}

public class RoleOption
{
    public string Value { get; }
    public string DisplayName { get; }

    public RoleOption(string value, string displayName)
    {
        Value = value;
        DisplayName = displayName;
    }

    public override string ToString() => DisplayName;
}
