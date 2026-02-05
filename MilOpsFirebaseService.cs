using Android.App;
using Android.Content;
using Android.OS;
using Android.Util;
using Firebase.Messaging;
using System;
using System.Threading.Tasks;

namespace MilOps.Android;

/// <summary>
/// FCM 메시지 수신 및 토큰 관리 서비스
/// </summary>
[Service(Exported = false, Name = "com.milops.schedule.MilOpsFirebaseService")]
[IntentFilter(new[] { "com.google.firebase.MESSAGING_EVENT" })]
public class MilOpsFirebaseService : FirebaseMessagingService
{
    private const string TAG = "MilOpsFCM";

    /// <summary>
    /// FCM 토큰이 갱신될 때 호출
    /// 앱 최초 설치, 토큰 만료, 앱 데이터 삭제 시 호출됨
    /// </summary>
    public override void OnNewToken(string token)
    {
        base.OnNewToken(token);
        Log.Info(TAG, $"FCM Token refreshed: {token}");

        // 토큰을 서버에 저장 (Supabase user_devices 테이블)
        Task.Run(async () => await SaveTokenToServerAsync(token));
    }

    /// <summary>
    /// FCM 메시지 수신 시 호출
    /// Data Message와 Notification Message 모두 여기서 처리
    ///
    /// 중요: notification + data 메시지가 올 경우 중복 알림 방지
    /// - notification이 있으면 시스템이 이미 알림을 표시하므로 data에서는 ShowNotification 스킵
    /// - data only 메시지일 경우에만 ShowNotification 호출
    /// </summary>
    public override void OnMessageReceived(RemoteMessage message)
    {
        base.OnMessageReceived(message);

        Log.Info(TAG, $"FCM Message received from: {message.From}");

        // Notification Message가 있는지 확인
        var hasNotification = message.GetNotification() != null;

        // Data Message 처리 (앱이 foreground/background 상관없이 항상 호출)
        // notification이 함께 왔으면 중복 알림 방지를 위해 showNotification=false
        if (message.Data.Count > 0)
        {
            Log.Info(TAG, $"Data Message received (hasNotification: {hasNotification})");
            HandleDataMessage(message.Data, showNotification: !hasNotification);
        }

        // Notification Message 처리 (앱이 foreground일 때만 호출)
        // 시스템이 background에서는 자동으로 표시하므로 foreground에서만 수동 표시
        if (hasNotification)
        {
            var notification = message.GetNotification();
            Log.Info(TAG, $"Notification: {notification.Title} - {notification.Body}");
            ShowNotification(notification.Title, notification.Body);
        }
    }

    /// <summary>
    /// Data Message 처리
    /// 초대 알림, 일정 변경 등 커스텀 데이터 처리
    /// </summary>
    /// <param name="data">FCM 데이터 페이로드</param>
    /// <param name="showNotification">알림을 표시할지 여부 (notification 메시지가 함께 왔으면 false)</param>
    private void HandleDataMessage(System.Collections.Generic.IDictionary<string, string> data, bool showNotification = true)
    {
        // showNotification이 false면 알림 표시 안 함 (notification 메시지가 함께 왔을 때 중복 방지)
        if (!showNotification)
        {
            Log.Info(TAG, "Skipping notification display (notification message already handled)");
            // 딥링크 등 다른 처리는 계속 진행
        }

        // 메시지 타입에 따라 분기 처리
        if (data.TryGetValue("type", out var messageType))
        {
            switch (messageType)
            {
                case "invitation_accepted":
                    // 초대 수락됨 알림
                    if (showNotification && data.TryGetValue("name", out var name) &&
                        data.TryGetValue("role", out var role))
                    {
                        ShowNotification(
                            "초대 수락",
                            $"{name}님이 {role}(으)로 가입했습니다."
                        );
                    }
                    break;

                case "schedule_created":
                    // 새 일정 생성됨
                    if (showNotification && data.TryGetValue("company_name", out var companyName))
                    {
                        ShowNotification(
                            "새 일정 배정",
                            $"{companyName} 방문 일정이 배정되었습니다."
                        );
                    }
                    break;

                case "schedule_updated":
                    // 일정 상태 변경
                    if (showNotification && data.TryGetValue("title", out var title) &&
                        data.TryGetValue("status", out var status))
                    {
                        ShowNotification(title, $"일정 상태: {status}");
                    }
                    break;

                case "confirm_needed":
                    // D-1 확정 리마인더
                    if (showNotification && data.TryGetValue("company_name", out var company) &&
                        data.TryGetValue("date", out var date))
                    {
                        ShowNotification(
                            "일정 확정 필요",
                            $"내일({date}) {company} 방문 일정을 확정해주세요."
                        );
                    }
                    break;

                case "schedule_inputted":
                    // 일정 입력됨
                    if (showNotification && data.TryGetValue("title", out var inputTitle) &&
                        data.TryGetValue("body", out var inputBody))
                    {
                        ShowNotification(inputTitle, inputBody);
                    }
                    break;

                case "schedule_reserved":
                    // 일정 예약됨
                    if (showNotification && data.TryGetValue("title", out var reservedTitle) &&
                        data.TryGetValue("body", out var reservedBody))
                    {
                        ShowNotification(reservedTitle, reservedBody);
                    }
                    break;

                case "schedule_confirmed":
                    // 일정 확정됨
                    if (showNotification && data.TryGetValue("title", out var confirmedTitle) &&
                        data.TryGetValue("body", out var confirmedBody))
                    {
                        ShowNotification(confirmedTitle, confirmedBody);
                    }
                    break;

                default:
                    // 기본 메시지 처리
                    if (showNotification && data.TryGetValue("title", out var defaultTitle) &&
                        data.TryGetValue("body", out var body))
                    {
                        ShowNotification(defaultTitle, body);
                    }
                    break;
            }
        }

        // 딥링크 처리
        if (data.TryGetValue("deep_link", out var deepLink))
        {
            Log.Info(TAG, $"Deep link received: {deepLink}");
            // TODO: 딥링크로 특정 화면 이동 처리
        }
    }

    /// <summary>
    /// 로컬 알림 표시
    /// </summary>
    private void ShowNotification(string? title, string? body)
    {
        if (string.IsNullOrEmpty(title) && string.IsNullOrEmpty(body))
            return;

        var channelId = "milops_default_channel";
        var notificationId = (int)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % int.MaxValue);

        // 알림 채널 생성 (Android 8.0+)
        CreateNotificationChannel(channelId);

        // 알림 클릭 시 앱 실행 인텐트
        var intent = new Intent(this, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.ClearTop);

        var pendingIntentFlags = PendingIntentFlags.UpdateCurrent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            pendingIntentFlags |= PendingIntentFlags.Immutable;
        }

        var pendingIntent = PendingIntent.GetActivity(
            this, 0, intent, pendingIntentFlags);

        // 알림 빌드
        var notificationBuilder = new Notification.Builder(this, channelId)
            .SetContentTitle(title)
            .SetContentText(body)
            .SetSmallIcon(Resource.Drawable.Icon)
            .SetAutoCancel(true)
            .SetContentIntent(pendingIntent);

        // 알림 표시
        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.Notify(notificationId, notificationBuilder.Build());
    }

    /// <summary>
    /// 알림 채널 생성 (Android 8.0+)
    /// </summary>
    private void CreateNotificationChannel(string channelId)
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            return;

        var channel = new NotificationChannel(
            channelId,
            "MilOps 알림",
            NotificationImportance.High)
        {
            Description = "군-지자체 일정관리 앱 알림"
        };

        channel.EnableVibration(true);
        channel.EnableLights(true);

        var notificationManager = (NotificationManager?)GetSystemService(NotificationService);
        notificationManager?.CreateNotificationChannel(channel);
    }

    /// <summary>
    /// FCM 토큰을 서버에 저장
    /// </summary>
    private async Task SaveTokenToServerAsync(string token)
    {
        try
        {
            // 현재 로그인된 사용자가 없으면 로컬에만 저장
            if (!Services.AuthService.IsLoggedIn || Services.AuthService.CurrentUser == null)
            {
                Log.Info(TAG, "User not logged in, storing token locally");
                FcmTokenHolder.Token = token;
                return;
            }

            // Supabase에 토큰 저장
            var supabase = Services.SupabaseService.Client;
            if (supabase == null)
            {
                Log.Warn(TAG, "Supabase client not initialized");
                FcmTokenHolder.Token = token;
                return;
            }

            var userId = Services.AuthService.CurrentUser.Id;

            // user_devices 테이블에 upsert
            await supabase
                .From<Models.UserDevice>()
                .Upsert(new Models.UserDevice
                {
                    UserId = userId,
                    FcmToken = token,
                    DeviceType = "android",
                    DeviceName = Build.Model ?? "Unknown",
                    IsActive = true
                });

            Log.Info(TAG, "FCM token saved to server");
        }
        catch (Exception ex)
        {
            Log.Error(TAG, $"Failed to save FCM token: {ex.Message}");
            // 실패 시 로컬에 저장하여 나중에 재시도
            FcmTokenHolder.Token = token;
        }
    }
}

/// <summary>
/// FCM 토큰 임시 저장소
/// 로그인 전에 토큰이 발급된 경우 사용
/// </summary>
public static class FcmTokenHolder
{
    public static string? Token { get; set; }

    /// <summary>
    /// 현재 FCM 토큰 가져오기
    /// </summary>
    public static Task<string?> GetTokenAsync()
    {
        var tcs = new TaskCompletionSource<string?>();

        if (!string.IsNullOrEmpty(Token))
        {
            tcs.SetResult(Token);
            return tcs.Task;
        }

        try
        {
            FirebaseMessaging.Instance.GetToken()
                .AddOnSuccessListener(new OnSuccessListener(token =>
                {
                    Token = token?.ToString();
                    tcs.SetResult(Token);
                }))
                .AddOnFailureListener(new OnFailureListener(ex =>
                {
                    Log.Error("MilOpsFCM", $"Failed to get FCM token: {ex.Message}");
                    tcs.SetResult(null);
                }));
        }
        catch (Exception ex)
        {
            Log.Error("MilOpsFCM", $"Failed to get FCM token: {ex.Message}");
            tcs.SetResult(null);
        }

        return tcs.Task;
    }
}

/// <summary>
/// Java OnSuccessListener 구현
/// </summary>
internal class OnSuccessListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnSuccessListener
{
    private readonly Action<Java.Lang.Object?> _action;

    public OnSuccessListener(Action<Java.Lang.Object?> action)
    {
        _action = action;
    }

    public void OnSuccess(Java.Lang.Object? result)
    {
        _action?.Invoke(result);
    }
}

/// <summary>
/// Java OnFailureListener 구현
/// </summary>
internal class OnFailureListener : Java.Lang.Object, global::Android.Gms.Tasks.IOnFailureListener
{
    private readonly Action<Java.Lang.Exception> _action;

    public OnFailureListener(Action<Java.Lang.Exception> action)
    {
        _action = action;
    }

    public void OnFailure(Java.Lang.Exception e)
    {
        _action?.Invoke(e);
    }
}
