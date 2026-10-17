using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class CharacterAcquiredTraitManifestationGateDefinition
{
    [SerializeField, Min(1)] private int meaningfulRecordMilestone;
    [SerializeField] private CharacterSkillRarity rarity;
    [SerializeField, Min(1)] private int budget;

    public int MeaningfulRecordMilestone => meaningfulRecordMilestone;
    public CharacterSkillRarity Rarity => rarity;
    public int Budget => budget;
}

public static class CharacterAcquiredTraitCombinationIdentity
{
    public const int MinimumModuleCount = 1;
    public const int MaximumModuleCount = 3;
    public const string Prefix = "acquired-trait-combination:";

    public static string Build(IEnumerable<string> moduleIds)
    {
        string[] canonical = Canonicalize(moduleIds);
        if (canonical.Length is < MinimumModuleCount or > MaximumModuleCount)
        {
            throw new InvalidOperationException(
                $"An acquired-trait combination requires {MinimumModuleCount} to "
                + $"{MaximumModuleCount} modules.");
        }
        return Prefix + NarrativeInferenceHash.ComputeSha256Utf8(
            string.Join("|", canonical));
    }

    public static string[] Canonicalize(IEnumerable<string> moduleIds)
    {
        string[] raw = (moduleIds ?? Array.Empty<string>()).ToArray();
        string[] canonical = raw
            .Select(value => value?.Trim() ?? string.Empty)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (canonical.Any(value => value.Length == 0)
            || canonical.Distinct(StringComparer.Ordinal).Count()
                != canonical.Length)
        {
            throw new InvalidOperationException(
                "Acquired-trait module IDs must be non-empty and unique.");
        }
        return canonical;
    }
}
