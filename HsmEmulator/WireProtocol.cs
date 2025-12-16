using System.Buffers.Binary;

namespace HsmEmulator;

public static class WireProtocol
{
    public static async Task WriteInt32Async(Stream s, int value, CancellationToken ct)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        await s.WriteAsync(buf, ct);
    }

    public static async Task<int> ReadInt32Async(Stream s, CancellationToken ct)
    {
        var buf = new byte[4];
        await ReadExactlyAsync(s, buf, ct);
        return BinaryPrimitives.ReadInt32LittleEndian(buf);
    }

    public static async Task WriteInt64Async(Stream s, long value, CancellationToken ct)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf, value);
        await s.WriteAsync(buf, ct);
    }

    public static async Task<long> ReadInt64Async(Stream s, CancellationToken ct)
    {
        var buf = new byte[8];
        await ReadExactlyAsync(s, buf, ct);
        return BinaryPrimitives.ReadInt64LittleEndian(buf);
    }

    public static Task WriteBoolAsync(Stream s, bool value, CancellationToken ct)
        => s.WriteAsync(new[] { (byte)(value ? 1 : 0) }, ct);

    public static async Task<bool> ReadBoolAsync(Stream s, CancellationToken ct)
    {
        var b = new byte[1];
        await ReadExactlyAsync(s, b, ct);
        return b[0] != 0;
    }

    public static async Task WriteStringAsync(Stream s, string value, CancellationToken ct)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        await WriteInt32Async(s, bytes.Length, ct);
        await s.WriteAsync(bytes, ct);
    }

    public static async Task<string> ReadStringAsync(Stream s, CancellationToken ct)
    {
        var len = await ReadInt32Async(s, ct);
        if (len < 0) throw new InvalidDataException("Negative string length.");
        var buf = new byte[len];
        await ReadExactlyAsync(s, buf, ct);
        return System.Text.Encoding.UTF8.GetString(buf);
    }

    public static async Task WriteBytesAsync(Stream s, byte[]? value, CancellationToken ct)
    {
        if (value is null)
        {
            await WriteInt32Async(s, -1, ct);
            return;
        }
        await WriteInt32Async(s, value.Length, ct);
        await s.WriteAsync(value, ct);
    }

    public static async Task<byte[]?> ReadBytesAsync(Stream s, CancellationToken ct)
    {
        var len = await ReadInt32Async(s, ct);
        if (len == -1) return null;
        if (len < -1) throw new InvalidDataException("Invalid byte[] length.");
        var buf = new byte[len];
        await ReadExactlyAsync(s, buf, ct);
        return buf;
    }

    private static async Task ReadExactlyAsync(Stream s, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var n = await s.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), ct);
            if (n == 0) throw new EndOfStreamException();
            offset += n;
        }
    }
}
