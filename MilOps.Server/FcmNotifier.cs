using System;
using System.Threading.Tasks;

namespace MilOps.Server;

/// <summary>
/// FCM push notification for offline users (stub — Phase 12-7에서 구현 예정)
/// </summary>
public class FcmNotifier
{
    public Task NotifyAsync(Guid userId, string senderName, string messagePreview)
    {
        Console.WriteLine($"[FCM Stub] Would notify user {userId}: {senderName}: {messagePreview}");
        return Task.CompletedTask;
    }
}
