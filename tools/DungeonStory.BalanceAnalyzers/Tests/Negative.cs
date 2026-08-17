using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static System.String;

sealed class BalanceImmutableRecordAttribute : Attribute { }
sealed class BalanceCaptureFactoryAttribute : Attribute { }
sealed class BalanceSerializationLayerAttribute : Attribute { }
sealed class BalancePresentationLayerAttribute : Attribute { }

[BalanceImmutableRecord]
sealed class MutableRecord
{
    public MutableRecord() { }
    public List<string> Values { get; set; }
    public string[] Tokens;
}

static class DirectConstruction
{
    public static MutableRecord Build() => new MutableRecord();
}

[BalanceSerializationLayer]
static class NegativeWriter
{
    public static void Write(StreamWriter writer, List<string> rows, ReadOnlySpan<char> text)
    {
        string normalized = text.ToString().Replace("x", "y").Trim().ToLowerInvariant();
        IEnumerable<string> ordered = rows.OrderBy(value => value);
        Span<char> buffer = stackalloc char[text.Length * 2];
        string concatenated = Concat(normalized, text.ToString().Substring(0));
        StringBuilder builder = new StringBuilder().Append(concatenated);
        writer.Write($"{builder}:{ordered.Count()}:{buffer.Length}");
    }
}

[BalancePresentationLayer]
static class NegativePresentation
{
    public static string Format(string value) => value.ToUpperInvariant().Replace("A", "B");
}
