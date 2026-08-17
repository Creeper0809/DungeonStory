using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

sealed class BalanceImmutableRecordAttribute : Attribute { }
sealed class BalanceCaptureFactoryAttribute : Attribute { }
sealed class BalanceSerializationLayerAttribute : Attribute { }

[BalanceImmutableRecord]
sealed class MutableRecord
{
    public MutableRecord() { }
    public List<string> Values { get; set; }
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
        string normalized = text.ToString().Replace("x", "y");
        IEnumerable<string> ordered = rows.OrderBy(value => value);
        Span<char> buffer = stackalloc char[text.Length * 2];
        writer.Write($"{normalized}:{ordered.Count()}:{buffer.Length}");
    }
}
