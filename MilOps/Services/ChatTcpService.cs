using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MilOps.Config;
using MilOps.Services.Protocol;

namespace MilOps.Services;

public static class ChatTcpService
{
    private static TcpClient? _tcp;
    private static NetworkStream? _stream;
    private static CancellationTokenSource? _cts;
    private static bool _authenticated;
    private static Guid _userId;

    // 연결 상태
    public static bool IsConnected => _tcp?.Connected == true && _authenticated;

    // ViewModel에서 구독하는 이벤트들
    public static event Action<ChatPacket>? MessageReceived;   // MSG_RECV
    public static event Action<ChatPacket>? MessageAcked;      // MSG_ACK (전송 확인)
    public static event Action<ChatPacket>? ReadAcked;         // MSG_READ_ACK (상대 읽음)
    public static event Action<bool>? ConnectionStateChanged;  // 연결/끊김 알림

    /// <summary>
    /// TCP 서버에 연결 + AUTH 인증
    /// 앱 로그인 성공 후 1회 호출
    /// </summary>
    public static async Task ConnectAsync(Guid userId)
    {
        if (IsConnected) return;

        _userId = userId;

        try
        {
            _cts = new CancellationTokenSource();
            _tcp = new TcpClient();

            // 3초 timeout으로 연결 시도 (기본 TCP timeout이 너무 길어 UI 블로킹 방지)
            using var connectCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(connectCts.Token, _cts.Token);

            try
            {
                await _tcp.ConnectAsync(ChatServerConfig.Host, ChatServerConfig.Port, linkedCts.Token);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"서버 연결 시간 초과 ({ChatServerConfig.Host}:{ChatServerConfig.Port})");
            }

            _stream = _tcp.GetStream();

            // AUTH 패킷 전송
            var authPacket = new ChatPacket
            {
                Type = PacketType.AUTH,
                SenderId = userId
            };
            await PacketWriter.WriteAsync(_stream, authPacket, _cts.Token);

            // AUTH_ACK 대기
            var ack = await PacketReader.ReadAsync(_stream, _cts.Token);
            if (ack?.Type != PacketType.AUTH_ACK || ack.Success != true)
            {
                throw new Exception(ack?.Error ?? "AUTH failed");
            }

            _authenticated = true;
            ConnectionStateChanged?.Invoke(true);

            // 수신 루프 시작 (상시 대기)
            _ = Task.Run(() => ReceiveLoopAsync(_cts.Token));

            // 로컬 큐 플러시
            _ = Task.Run(() => FlushPendingQueueAsync(userId));

            System.Diagnostics.Debug.WriteLine("[ChatTcpService] Connected & authenticated");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatTcpService] Connect failed: {ex.Message}");
            Cleanup();
            throw;
        }
    }

    /// <summary>
    /// 메시지 전송 — 온라인이면 TCP, 오프라인이면 로컬 큐
    /// </summary>
    public static async Task SendMessageAsync(Guid senderId, Guid receiverId, string content)
    {
        // 연결이 끊겨 있으면 재연결 시도
        if (!IsConnected)
        {
            try
            {
                await ConnectAsync(senderId);
            }
            catch
            {
                // 재연결 실패 → 로컬 큐
                await LocalChatDatabase.EnqueuePendingAsync(receiverId.ToString(), content);
                return;
            }
        }

        if (_stream != null && _cts != null)
        {
            try
            {
                var packet = new ChatPacket
                {
                    Type = PacketType.MSG_SEND,
                    SenderId = senderId,
                    ReceiverId = receiverId,
                    Content = content
                };
                await PacketWriter.WriteAsync(_stream, packet, _cts.Token);
                return;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ChatTcpService] Send failed, queueing: {ex.Message}");
                Cleanup();
            }
        }

        // 전송 실패 → 로컬 큐에 저장
        await LocalChatDatabase.EnqueuePendingAsync(receiverId.ToString(), content);
    }

    /// <summary>
    /// 읽음 처리 전송
    /// </summary>
    public static async Task SendReadAsync(Guid myId, Guid partnerId)
    {
        if (!IsConnected || _stream == null || _cts == null) return;

        try
        {
            var packet = new ChatPacket
            {
                Type = PacketType.MSG_READ,
                SenderId = myId,
                ReceiverId = partnerId
            };
            await PacketWriter.WriteAsync(_stream, packet, _cts.Token);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatTcpService] SendRead failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 연결 종료 — 로그아웃 시 호출
    /// </summary>
    public static void Disconnect()
    {
        Cleanup();
        System.Diagnostics.Debug.WriteLine("[ChatTcpService] Disconnected");
    }

    // ========== 내부 루프 ==========

    private static async Task ReceiveLoopAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && _stream != null)
            {
                var packet = await PacketReader.ReadAsync(_stream, ct);
                if (packet == null) break; // EOF — 서버 끊김

                switch (packet.Type)
                {
                    case PacketType.MSG_RECV:
                        MessageReceived?.Invoke(packet);
                        break;
                    case PacketType.MSG_ACK:
                        MessageAcked?.Invoke(packet);
                        break;
                    case PacketType.MSG_READ_ACK:
                        ReadAcked?.Invoke(packet);
                        break;
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatTcpService] ReceiveLoop error: {ex.Message}");
        }
        finally
        {
            _authenticated = false;
            ConnectionStateChanged?.Invoke(false);
        }
    }

    /// <summary>
    /// 연결 복구 후 로컬 큐의 미전송 메시지를 순서대로 전송
    /// </summary>
    private static async Task FlushPendingQueueAsync(Guid senderId)
    {
        try
        {
            var pending = await LocalChatDatabase.DequeuePendingAsync();
            foreach (var msg in pending)
            {
                if (!IsConnected) break;

                await SendMessageAsync(senderId, Guid.Parse(msg.ReceiverId), msg.Content);
                await LocalChatDatabase.RemovePendingAsync(msg.QueueOrder);
            }

            if (pending.Count > 0)
                System.Diagnostics.Debug.WriteLine($"[ChatTcpService] Flushed {pending.Count} pending messages");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChatTcpService] FlushPending error: {ex.Message}");
        }
    }

    private static void Cleanup()
    {
        _authenticated = false;
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;

        try { _stream?.Dispose(); } catch { }
        try { _tcp?.Dispose(); } catch { }
        _stream = null;
        _tcp = null;

        ConnectionStateChanged?.Invoke(false);
    }
}
