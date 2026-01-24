using System;

namespace MilOps.Models;

public enum InvitationStatus
{
    Active,     // 활성
    Pending,    // 초대중
    Expired     // 만료
}

public class Invitation
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Region { get; set; } = "";
    public DateTime RegisteredDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public InvitationStatus Status { get; set; }

    public string StatusText
    {
        get
        {
            return Status switch
            {
                InvitationStatus.Active => "활성",
                InvitationStatus.Pending => GetPendingText(),
                InvitationStatus.Expired => "만료",
                _ => ""
            };
        }
    }

    public string StatusColor
    {
        get
        {
            return Status switch
            {
                InvitationStatus.Active => "#4CAF50",
                InvitationStatus.Pending => "#FF9800",
                InvitationStatus.Expired => "#F44336",
                _ => "White"
            };
        }
    }

    public string RegisteredDateText => $"등록일: {RegisteredDate:yyyy-MM-dd}";

    private string GetPendingText()
    {
        if (ExpiryDate.HasValue)
        {
            var daysLeft = (ExpiryDate.Value - DateTime.Today).Days;
            return $"초대중 (D-{daysLeft})";
        }
        return "초대중";
    }
}
