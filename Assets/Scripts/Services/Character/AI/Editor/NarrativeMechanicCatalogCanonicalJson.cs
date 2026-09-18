#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Immutable JSON DOM used by the V25 handoff.  It deliberately has no floating
/// point number node: every JSON number accepted by this writer is a signed Int64.
/// </summary>
public abstract class NarrativeMechanicCatalogCanonicalJsonValue
{
    internal abstract void WriteCanonical(StringBuilder builder);

    internal abstract NarrativeMechanicCatalogCanonicalJsonValue Copy();

    public byte[] ToCanonicalUtf8()
    {
        StringBuilder builder = new StringBuilder();
        WriteCanonical(builder);
        return NarrativeMechanicCatalogCanonicalJson.Utf8NoBom.GetBytes(builder.ToString());
    }

    public string ToCanonicalString()
    {
        return NarrativeMechanicCatalogCanonicalJson.Utf8NoBom.GetString(ToCanonicalUtf8());
    }
}

public sealed class NarrativeMechanicCatalogCanonicalJsonObject
    : NarrativeMechanicCatalogCanonicalJsonValue
{
    private readonly KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>[] properties;

    public NarrativeMechanicCatalogCanonicalJsonObject(
        IEnumerable<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        List<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> copied = new();
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue> pair in values)
        {
            if (pair.Key == null)
            {
                throw new ArgumentException("Canonical JSON object keys cannot be null.", nameof(values));
            }

            NarrativeMechanicCatalogCanonicalJson.RequireWellFormedUtf16(pair.Key, nameof(values));

            if (pair.Value == null)
            {
                throw new ArgumentException(
                    $"Canonical JSON object value for '{pair.Key}' cannot be null.",
                    nameof(values));
            }

            if (!keys.Add(pair.Key))
            {
                throw new ArgumentException(
                    $"Canonical JSON object has duplicate ordinal key '{pair.Key}'.",
                    nameof(values));
            }

            copied.Add(new KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>(
                pair.Key,
                pair.Value.Copy()));
        }

        properties = copied.ToArray();
    }

    public IReadOnlyList<KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>> Properties =>
        Array.AsReadOnly(properties);

    public bool ContainsKey(string key)
    {
        return properties.Any(pair => string.Equals(pair.Key, key, StringComparison.Ordinal));
    }

    public NarrativeMechanicCatalogCanonicalJsonObject With(
        string key,
        NarrativeMechanicCatalogCanonicalJsonValue value)
    {
        if (string.IsNullOrEmpty(key))
        {
            throw new ArgumentException("Canonical JSON object key is required.", nameof(key));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (ContainsKey(key))
        {
            throw new InvalidOperationException(
                $"Canonical JSON object already contains key '{key}'.");
        }

        return new NarrativeMechanicCatalogCanonicalJsonObject(
            properties.Concat(new[]
            {
                new KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>(key, value)
            }));
    }

    internal override void WriteCanonical(StringBuilder builder)
    {
        builder.Append('{');
        bool first = true;
        foreach (KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue> pair in properties
                     .OrderBy(value => value.Key, NarrativeMechanicCatalogCanonicalJson.UnicodeScalarOrdinalComparer))
        {
            if (!first)
            {
                builder.Append(',');
            }

            first = false;
            NarrativeMechanicCatalogCanonicalJson.WriteString(builder, pair.Key);
            builder.Append(':');
            pair.Value.WriteCanonical(builder);
        }

        builder.Append('}');
    }

    internal override NarrativeMechanicCatalogCanonicalJsonValue Copy()
    {
        return new NarrativeMechanicCatalogCanonicalJsonObject(properties);
    }
}

public sealed class NarrativeMechanicCatalogCanonicalJsonArray
    : NarrativeMechanicCatalogCanonicalJsonValue
{
    private readonly NarrativeMechanicCatalogCanonicalJsonValue[] values;

    public NarrativeMechanicCatalogCanonicalJsonArray(
        IEnumerable<NarrativeMechanicCatalogCanonicalJsonValue> values)
    {
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        this.values = values.Select(value => value?.Copy()
            ?? throw new ArgumentException("Canonical JSON arrays cannot contain null.", nameof(values)))
            .ToArray();
    }

    public IReadOnlyList<NarrativeMechanicCatalogCanonicalJsonValue> Values =>
        Array.AsReadOnly(values);

    internal override void WriteCanonical(StringBuilder builder)
    {
        builder.Append('[');
        for (int index = 0; index < values.Length; index++)
        {
            if (index > 0)
            {
                builder.Append(',');
            }

            values[index].WriteCanonical(builder);
        }

        builder.Append(']');
    }

    internal override NarrativeMechanicCatalogCanonicalJsonValue Copy()
    {
        return new NarrativeMechanicCatalogCanonicalJsonArray(values);
    }
}

public sealed class NarrativeMechanicCatalogCanonicalJsonString
    : NarrativeMechanicCatalogCanonicalJsonValue
{
    public NarrativeMechanicCatalogCanonicalJsonString(string value)
    {
        Value = value ?? throw new ArgumentNullException(nameof(value));
        NarrativeMechanicCatalogCanonicalJson.RequireWellFormedUtf16(Value, nameof(value));
    }

    public string Value { get; }

    internal override void WriteCanonical(StringBuilder builder)
    {
        NarrativeMechanicCatalogCanonicalJson.WriteString(builder, Value);
    }

    internal override NarrativeMechanicCatalogCanonicalJsonValue Copy()
    {
        return new NarrativeMechanicCatalogCanonicalJsonString(Value);
    }
}

public sealed class NarrativeMechanicCatalogCanonicalJsonInt64
    : NarrativeMechanicCatalogCanonicalJsonValue
{
    public NarrativeMechanicCatalogCanonicalJsonInt64(long value)
    {
        Value = value;
    }

    public long Value { get; }

    internal override void WriteCanonical(StringBuilder builder)
    {
        builder.Append(Value.ToString(CultureInfo.InvariantCulture));
    }

    internal override NarrativeMechanicCatalogCanonicalJsonValue Copy()
    {
        return new NarrativeMechanicCatalogCanonicalJsonInt64(Value);
    }
}

public sealed class NarrativeMechanicCatalogCanonicalJsonBoolean
    : NarrativeMechanicCatalogCanonicalJsonValue
{
    public NarrativeMechanicCatalogCanonicalJsonBoolean(bool value)
    {
        Value = value;
    }

    public bool Value { get; }

    internal override void WriteCanonical(StringBuilder builder)
    {
        builder.Append(Value ? "true" : "false");
    }

    internal override NarrativeMechanicCatalogCanonicalJsonValue Copy()
    {
        return new NarrativeMechanicCatalogCanonicalJsonBoolean(Value);
    }
}

public static class NarrativeMechanicCatalogCanonicalJson
{
    // throwOnInvalidBytes rejects an unpaired surrogate rather than silently
    // replacing it, matching a strict UTF-8 Python handoff boundary.
    internal static readonly UTF8Encoding Utf8NoBom = new(false, true);
    internal static readonly IComparer<string> UnicodeScalarOrdinalComparer =
        new UnicodeScalarComparer();

    public static NarrativeMechanicCatalogCanonicalJsonObject Object(
        params KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>[] properties)
    {
        return new NarrativeMechanicCatalogCanonicalJsonObject(properties);
    }

    public static NarrativeMechanicCatalogCanonicalJsonArray Array(
        params NarrativeMechanicCatalogCanonicalJsonValue[] values)
    {
        return new NarrativeMechanicCatalogCanonicalJsonArray(values);
    }

    public static NarrativeMechanicCatalogCanonicalJsonString String(string value)
    {
        return new NarrativeMechanicCatalogCanonicalJsonString(value);
    }

    public static NarrativeMechanicCatalogCanonicalJsonInt64 Integer(long value)
    {
        return new NarrativeMechanicCatalogCanonicalJsonInt64(value);
    }

    public static NarrativeMechanicCatalogCanonicalJsonBoolean Boolean(bool value)
    {
        return new NarrativeMechanicCatalogCanonicalJsonBoolean(value);
    }

    public static KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue> Property(
        string key,
        NarrativeMechanicCatalogCanonicalJsonValue value)
    {
        return new KeyValuePair<string, NarrativeMechanicCatalogCanonicalJsonValue>(
            key ?? throw new ArgumentNullException(nameof(key)),
            value ?? throw new ArgumentNullException(nameof(value)));
    }

    public static string Sha256Prefixed(byte[] bytes)
    {
        if (bytes == null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        using SHA256 sha256 = SHA256.Create();
        return "sha256:" + string.Concat(sha256.ComputeHash(bytes)
            .Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
    }

    internal static void WriteString(StringBuilder builder, string value)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        RequireWellFormedUtf16(value, nameof(value));
        builder.Append('"');
        foreach (char character in value)
        {
            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                default:
                    if (character < 0x20)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }

        builder.Append('"');
    }

    internal static void RequireWellFormedUtf16(string value, string parameterName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                {
                    throw new ArgumentException(
                        "Canonical JSON strings must contain well-formed UTF-16.",
                        parameterName);
                }

                index++;
            }
            else if (char.IsLowSurrogate(character))
            {
                throw new ArgumentException(
                    "Canonical JSON strings must contain well-formed UTF-16.",
                    parameterName);
            }
        }
    }

    /// <summary>
    /// Python's sort_keys compares Unicode scalar values, while .NET's ordinary
    /// ordinal comparer compares UTF-16 code units.  They differ for a supplementary
    /// scalar adjacent to a BMP private-use scalar, so the writer must not use
    /// StringComparer.Ordinal here.
    /// </summary>
    private sealed class UnicodeScalarComparer : IComparer<string>
    {
        public int Compare(string left, string right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            RequireWellFormedUtf16(left, nameof(left));
            RequireWellFormedUtf16(right, nameof(right));
            int leftIndex = 0;
            int rightIndex = 0;
            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                int leftScalar = ReadScalar(left, ref leftIndex);
                int rightScalar = ReadScalar(right, ref rightIndex);
                if (leftScalar != rightScalar)
                {
                    return leftScalar < rightScalar ? -1 : 1;
                }
            }

            return leftIndex == left.Length
                ? rightIndex == right.Length ? 0 : -1
                : 1;
        }

        private static int ReadScalar(string value, ref int index)
        {
            char first = value[index++];
            if (!char.IsHighSurrogate(first))
            {
                return first;
            }

            char second = value[index++];
            return char.ConvertToUtf32(first, second);
        }
    }
}
#endif
