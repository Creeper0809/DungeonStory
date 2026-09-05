using System;
using System.Globalization;

public static class GoldenHarvestDeterministicOutcomeAuthority
{
    public const string OutcomeDomain = "harvest";
    public const string TraitSalt = "304";
    public const ulong HashOffsetBasis = 14695981039346656037UL;
    public const ulong HashPrime = 1099511628211UL;
    public const ulong PartSeparator = 0x1FUL;
    public const ulong RollResolution = 1_000_000UL;

    public static ulong CaptureRollHash(
        ulong runSeed,
        string fieldId,
        int attemptIndex,
        string characterId)
    {
        string field = RequireCanonical(fieldId, nameof(fieldId));
        string character = RequireCanonical(characterId, nameof(characterId));
        if (attemptIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(attemptIndex));

        ulong hash = HashOffsetBasis;
        Append(ref hash, runSeed.ToString(CultureInfo.InvariantCulture));
        Append(ref hash, OutcomeDomain);
        Append(ref hash, field);
        Append(ref hash, attemptIndex.ToString(CultureInfo.InvariantCulture));
        Append(ref hash, character);
        Append(ref hash, TraitSalt);
        return hash;
    }

    public static float CaptureRoll01(
        ulong runSeed,
        string fieldId,
        int attemptIndex,
        string characterId) =>
        CaptureRoll01(CaptureRollHash(
            runSeed,
            fieldId,
            attemptIndex,
            characterId));

    public static float CaptureRoll01(ulong rollHash) =>
        (rollHash % RollResolution) / (float)RollResolution;

    private static string RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.IndexOf('\r') >= 0
            || value.IndexOf('\n') >= 0)
        {
            throw new ArgumentException(
                "Golden Harvest deterministic outcome requires a canonical ID.",
                parameterName);
        }
        return value;
    }

    private static void Append(ref ulong hash, string value)
    {
        foreach (char character in value)
        {
            unchecked
            {
                hash ^= character;
                hash *= HashPrime;
            }
        }
        unchecked
        {
            hash ^= PartSeparator;
            hash *= HashPrime;
        }
    }
}
