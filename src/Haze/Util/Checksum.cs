using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Haze.Util;

public class Checksum
{
    public static async Task<string> Sha256Sum(string data, CancellationToken ct = default)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
        return await Sha256Sum(stream, ct);
    }

    public static async Task<string> Sha256Sum(Stream stream, CancellationToken ct = default)
    {
        var bytes = await SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexStringLower(bytes);
    }

    public static async Task<string> Sha256SumObject<T>(T value, CancellationToken ct = default)
    {
        await using var stream = new MemoryStream(0);
        await JsonSerializer.SerializeAsync(stream, value, cancellationToken: ct);
        stream.Seek(0, SeekOrigin.Begin);
        return await Sha256Sum(stream, ct);
    }
}
