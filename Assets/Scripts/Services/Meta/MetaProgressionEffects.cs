using System;
using UnityEngine;

public static class MetaUpgradeEffectIds
{
    public const string StartingFacilityCandidates = "meta:starting-facility-candidates";
    public const string StartingOwnerTraitCandidates = "meta:starting-owner-trait-candidates";
    public const string BasicPurchaseEntries = "meta:basic-purchase-entries";
    public const string PreservedRecipeSlots = "meta:preserved-recipe-slots";
    public const string OwnerMaxHealth = "meta:owner-max-health";
    public const string InvasionWarningThreshold = "meta:invasion-warning-threshold";
    public const string CommerceStockCost = "meta:commerce-stock-cost";
    public const string FortressDefenseFacilityCost = "meta:fortress-defense-facility-cost";
    public const string ArcaneResearchWork = "meta:arcane-research-work";
}

public interface IMetaUpgradeEffect
{
    string EffectId { get; }
}

public interface IMetaIntegerBonusEffect : IMetaUpgradeEffect
{
    int GetBonus(int level);
}

public interface IMetaMultiplierEffect : IMetaUpgradeEffect
{
    float GetMultiplierDelta(int level);
}

public sealed class MetaIntegerBonusEffect : IMetaIntegerBonusEffect
{
    private readonly int amountPerLevel;

    public MetaIntegerBonusEffect(string effectId, int amountPerLevel)
    {
        EffectId = RequireId(effectId);
        this.amountPerLevel = amountPerLevel;
    }

    public string EffectId { get; }

    public int GetBonus(int level)
    {
        return Mathf.Max(0, level) * amountPerLevel;
    }

    private static string RequireId(string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId))
        {
            throw new ArgumentException("Meta effect id is required.", nameof(effectId));
        }

        return effectId.Trim();
    }
}

public sealed class MetaMultiplierDeltaEffect : IMetaMultiplierEffect
{
    private readonly float deltaPerLevel;

    public MetaMultiplierDeltaEffect(string effectId, float deltaPerLevel)
    {
        EffectId = RequireId(effectId);
        this.deltaPerLevel = deltaPerLevel;
    }

    public string EffectId { get; }

    public float GetMultiplierDelta(int level)
    {
        return Mathf.Max(0, level) * deltaPerLevel;
    }

    private static string RequireId(string effectId)
    {
        if (string.IsNullOrWhiteSpace(effectId))
        {
            throw new ArgumentException("Meta effect id is required.", nameof(effectId));
        }

        return effectId.Trim();
    }
}

public static class MetaProgressionEffects
{
    public static int GetIntegerBonus(MetaProgressionState state, string effectId)
    {
        if (state == null || string.IsNullOrWhiteSpace(effectId))
        {
            return 0;
        }

        int total = 0;
        foreach (MetaUpgradeDefinition definition in MetaProgressionCatalog.All)
        {
            int level = state.GetUpgradeLevel(definition.id);
            if (level <= 0 || definition.effects == null)
            {
                continue;
            }

            for (int i = 0; i < definition.effects.Count; i++)
            {
                if (definition.effects[i] is IMetaIntegerBonusEffect effect
                    && string.Equals(effect.EffectId, effectId, StringComparison.Ordinal))
                {
                    total += effect.GetBonus(level);
                }
            }
        }

        return total;
    }

    public static float GetMultiplier(MetaProgressionState state, string effectId)
    {
        if (state == null || string.IsNullOrWhiteSpace(effectId))
        {
            return 1f;
        }

        float multiplier = 1f;
        foreach (MetaUpgradeDefinition definition in MetaProgressionCatalog.All)
        {
            int level = state.GetUpgradeLevel(definition.id);
            if (level <= 0 || definition.effects == null)
            {
                continue;
            }

            for (int i = 0; i < definition.effects.Count; i++)
            {
                if (definition.effects[i] is IMetaMultiplierEffect effect
                    && string.Equals(effect.EffectId, effectId, StringComparison.Ordinal))
                {
                    multiplier += effect.GetMultiplierDelta(level);
                }
            }
        }

        return Mathf.Max(0.05f, multiplier);
    }
}
