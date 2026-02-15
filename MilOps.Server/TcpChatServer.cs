using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace MilOps.Server;

public class TcpChatServer
{
    private readonly int _port;
    private readonly ConnectionManager _connectionManager;
    private readonly MessageRouter _messageRouter;

    public TcpChatServer(int port, ConnectionManager connectionManager, MessageRouter messageRouter)
    {
        _port = port;
        _connectionManager = connectionManager;
        _messageRouter = messageRouter;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var listener = new TcpListener(IPAddress.Any, _port);
        listener.Start();
        Console.WriteLine($"[Server] Listening on port {_port}");

        try
        {
            while (!ct.IsCancellationRequested)
            {
                var tcpClient = await listener.AcceptTcpClientAsync(ct);
                Console.WriteLine($"[Server] Client connected: {tcpClient.Client.RemoteEndPoint}");
                _ = HandleClientAsync(tcpClient, ct);
            }
        }
        finally
        {
            listener.Stop();
            Console.WriteLine("[Server] Stopped");
        }
    }

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken ct)
    {
        var session = new ClientSession(tcpClient);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var packet = await session.ReadPacketAsync();
                if (packet == null) break; // Connection closed

                await _messageRouter.RouteAsync(session, packet);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"[Server] Client error: {ex.Message}");
        }
        finally
        {
            if (session.IsAuthenticated)
            {
                _connectionManager.Remove(session.UserId);
                Console.WriteLine($"[Server] User {session.UserId} disconnected. Online: {_connectionManager.OnlineCount}");
            }
            session.Dispose();
        }
    }
}
