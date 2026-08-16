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
        writer.Write(text);
    }
}
