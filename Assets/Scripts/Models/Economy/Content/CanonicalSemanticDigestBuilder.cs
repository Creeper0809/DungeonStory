using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Single canonical token framing authority for authored semantic digests.
/// Tokens are UTF-8 byte-length framed, floats use normalized IEEE-754 bits,
/// and the result is lowercase SHA-256.
/// </summary>
public sealed class CanonicalSemanticDigestBuilder
{
    private readonly StringBuilder canonical = new();

    public void Append(string value)
    {
        string token = value ?? string.Empty;
        canonical.Append(Encoding.UTF8.GetByteCount(token).ToString(
            CultureInfo.InvariantCulture));
        canonical.Append(':').Append(token).Append('|');
    }

    public void Append(bool value) => Append(value ? "1" : "0");

    public void Append(int value) =>
        Append(value.ToString(CultureInfo.InvariantCulture));

    public void Append(long value) =>
        Append(value.ToString(CultureInfo.InvariantCulture));

    public void AppendEnum<T>(T value) where T : struct, Enum => Append(
        Convert.ToInt64(value, CultureInfo.InvariantCulture));

    public void AppendFloat(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            throw new InvalidOperationException(
                "Semantic digest float must be finite.");
        }

        float normalized = value == 0f ? 0f : value;
        Append(BitConverter.SingleToInt32Bits(normalized)
            .ToString("x8", CultureInfo.InvariantCulture));
    }

    public void AppendDouble(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            throw new InvalidOperationException(
                "Semantic digest double must be finite.");
        }

        double normalized = value == 0d ? 0d : value;
        Append(BitConverter.DoubleToInt64Bits(normalized)
            .ToString("x16", CultureInfo.InvariantCulture));
    }

    public string ComputeSha256()
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(canonical.ToString()));
        StringBuilder result = new(digest.Length * 2);
        foreach (byte part in digest)
            result.Append(part.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }
}
