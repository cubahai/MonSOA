using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

internal static class TotpSecurity
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
    private const int PeriodSeconds = 30;

    public static byte[] NewSecret() => RandomNumberGenerator.GetBytes(20);

    public static string Base32Encode(ReadOnlySpan<byte> bytes)
    {
        var output = new StringBuilder((bytes.Length * 8 + 4) / 5);
        var buffer = 0;
        var bits = 0;
        foreach (var value in bytes)
        {
            buffer = (buffer << 8) | value;
            bits += 8;
            while (bits >= 5)
            {
                bits -= 5;
                output.Append(Alphabet[(buffer >> bits) & 31]);
            }
            buffer &= (1 << bits) - 1;
        }
        if (bits > 0) output.Append(Alphabet[(buffer << (5 - bits)) & 31]);
        return output.ToString();
    }

    public static string ProvisioningUri(string username, byte[] secret)
    {
        const string issuer = "KTX Demo";
        return $"otpauth://totp/{Uri.EscapeDataString(issuer)}:{Uri.EscapeDataString(username)}" +
               $"?secret={Base32Encode(secret)}&issuer={Uri.EscapeDataString(issuer)}&algorithm=SHA1&digits=6&period=30";
    }

    public static long? Verify(byte[] secret, string? code, long? lastUsedStep = null)
    {
        if (code is null || code.Length != 6 || code.Any(ch => ch is < '0' or > '9')) return null;
        var currentStep = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / PeriodSeconds;
        for (var offset = -1; offset <= 1; offset++)
        {
            var step = currentStep + offset;
            if (lastUsedStep is not null && step <= lastUsedStep.Value) continue;
            var expected = GenerateCode(secret, step);
            if (CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(code)))
                return step;
        }
        return null;
    }

    private static string GenerateCode(byte[] secret, long step)
    {
        Span<byte> counter = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counter, step);
        var digest = HMACSHA1.HashData(secret, counter);
        var offset = digest[^1] & 0x0f;
        var binary = ((digest[offset] & 0x7f) << 24) |
                     (digest[offset + 1] << 16) |
                     (digest[offset + 2] << 8) |
                     digest[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    public static string NewRecoveryCode()
    {
        var code = Base32Encode(RandomNumberGenerator.GetBytes(10));
        return string.Join('-', Enumerable.Range(0, 4).Select(index => code.Substring(index * 4, 4)));
    }

    public static byte[] RecoveryCodeHash(string? code)
    {
        var normalized = new string((code ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return SHA256.HashData(Encoding.ASCII.GetBytes(normalized));
    }
}
