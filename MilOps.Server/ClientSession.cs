using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MilOps.Server.Protocol;

namespace MilOps.Server;

public class ClientSession : IDisposable
{
    public Guid UserId { get; private set; }
    public bool IsAuthenticated => UserId != Guid.Empty;

    private readonly TcpClient _tcpClient;
    private readonly NetworkStream _stream;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public event Action<ClientSession>? Disconnected;

    public ClientSession(TcpClient tcpClient)
    {
        _tcpClient = tcpClient;
        _stream = tcpClient.GetStream();
    }

    public void Authenticate(Guid userId)
    {
        UserId = userId;
    }

    public async Task<ChatPacket?> ReadPacketAsync()
    {
        return await PacketReader.ReadAsync(_stream, _cts.Token);
    }

    public async Task SendPacketAsync(ChatPacket packet)
    {
        await _writeLock.WaitAsync(_cts.Token);
        try
        {
            await PacketWriter.WriteAsync(_stream, packet, _cts.Token);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Disconnect(string reason = "")
    {
        try
        {
            if (!string.IsNullOrEmpty(reason))
                Console.WriteLine($"[Session] Disconnecting {UserId}: {reason}");

            _cts.Cancel();
            _stream.Close();
            _tcpClient.Close();
        }
        catch { }
        finally
        {
            Disconnected?.Invoke(this);
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        _writeLock.Dispose();
        _stream.Dispose();
        _tcpClient.Dispose();
    }
}
