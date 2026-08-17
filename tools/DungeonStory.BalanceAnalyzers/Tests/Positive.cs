using System;
using System.IO;

sealed class BalanceImmutableRecordAttribute : Attribute { }
sealed class BalanceCaptureFactoryAttribute : Attribute { }
sealed class BalanceSerializationLayerAttribute : Attribute { }

[BalanceImmutableRecord]
sealed class FrozenRecord
{
    private FrozenRecord(int value) { Value = value; }
    public int Value { get; }

    [BalanceCaptureFactory]
    public static FrozenRecord Capture(int value) => new FrozenRecord(value);
}

[BalanceSerializationLayer]
static class PositiveWriter
{
    public static void Write(StreamWriter writer, FrozenRecord record)
    {
        ReadOnlySpan<char> text = "canonical".AsSpan();
        Span<char> bounded = stackalloc char[256];
        string domainText = DomainText.Replace("canonical");
        writer.Write(text);
        writer.Write(bounded.Slice(0, 0));
        writer.Write(domainText);
    }
}

static class DomainText
{
    public static string Replace(string value) => value;
}
