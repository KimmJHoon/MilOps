using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MilOps.Services.Protocol;

public static class PacketWriter
{
    public static async Task WriteAsync(Stream stream, ChatPacket packet, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var json = JsonConvert.SerializeObject(packet);
        var payloadBytes = Encoding.UTF8.GetBytes(json);
        var length = payloadBytes.Length;
        var serializeMs = sw.ElapsedMilliseconds;

        // 4-byte length prefix (big-endian) + JSON payload
        var combined = new byte[4 + length];
        combined[0] = (byte)(length >> 24);
        combined[1] = (byte)(length >> 16);
        combined[2] = (byte)(length >> 8);
        combined[3] = (byte)length;
        Buffer.BlockCopy(payloadBytes, 0, combined, 4, length);

        sw.Restart();
        await stream.WriteAsync(combined, 0, combined.Length, ct);
        await stream.FlushAsync(ct);
        Debug.WriteLine($"[PERF][Packet] Write: 직렬화={serializeMs}ms, 네트워크={sw.ElapsedMilliseconds}ms, 크기={length}bytes, 타입={packet.Type}");
    }
}
