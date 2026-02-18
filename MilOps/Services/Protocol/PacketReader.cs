using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MilOps.Services.Protocol;

public static class PacketReader
{
    public static async Task<ChatPacket?> ReadAsync(Stream stream, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // 4-byte length prefix (big-endian)
        var lengthBytes = new byte[4];
        var bytesRead = await ReadExactAsync(stream, lengthBytes, 0, 4, ct);
        if (bytesRead < 4) return null;

        var length = (lengthBytes[0] << 24) | (lengthBytes[1] << 16) |
                     (lengthBytes[2] << 8) | lengthBytes[3];

        if (length <= 0 || length > 1_048_576) // Max 1MB
            throw new InvalidDataException($"Invalid packet length: {length}");

        // UTF-8 JSON payload
        var payloadBytes = new byte[length];
        bytesRead = await ReadExactAsync(stream, payloadBytes, 0, length, ct);
        if (bytesRead < length) return null;
        var networkMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var json = Encoding.UTF8.GetString(payloadBytes);
        var packet = JsonConvert.DeserializeObject<ChatPacket>(json);
        Debug.WriteLine($"[PERF][Packet] Read: 네트워크={networkMs}ms, 파싱={sw.ElapsedMilliseconds}ms, 크기={length}bytes, 타입={packet?.Type}");

        return packet;
    }

    private static async Task<int> ReadExactAsync(
        Stream stream, byte[] buffer, int offset, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            var read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead, ct);
            if (read == 0) return totalRead; // EOF
            totalRead += read;
        }
        return totalRead;
    }
}
