using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Factions;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public static class FactionRouteBalanceRules
{
    public const float ReferenceDailyProduction = 504.9f;
    public const float MaximumTradeDailyShare = 0.05f;
    public const float MaximumSupplyDailyShare = 0.10f;
    public const int MinimumTradeCooldownDays = 7;
    public const int MinimumSupplyCooldownDays = 20;
    public const int MinimumReinforcementCooldownDays = 10;

    public static int CalculateCargoCooldownDays(
        float cargoEwu,
        bool supply)
    {
        float share = supply
            ? MaximumSupplyDailyShare
            : MaximumTradeDailyShare;
        int minimum = supply
            ? MinimumSupplyCooldownDays
            : MinimumTradeCooldownDays;
        return Mathf.Max(
            minimum,
            Mathf.CeilToInt(Mathf.Max(0f, cargoEwu)
                / (ReferenceDailyProduction * share)));
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[CreateAssetMenu(
    menuName = "DungeonStory/Factions/Dungeon Faction",
    order = 0)]
public sealed class DungeonFactionDefinitionSO : ScriptableObject
{
    public const string ResourcePath = "SO/Factions/Dungeons";

    public string factionId = string.Empty;
    public string displayName = string.Empty;
    public string speciesTag = string.Empty;
    [TextArea] public string description = string.Empty;
    public string[] relationTags = Array.Empty<string>();
    public string[] tradeTags = Array.Empty<string>();
    public string reinforcementRole = string.Empty;
    public Sprite crest;
    public List<FactionCargoLine> tradeCargo = new List<FactionCargoLine>();
    public List<FactionCargoLine> supplyCargo = new List<FactionCargoLine>();
    public FactionRouteEconomicPolicyDescriptor tradeEconomicPolicy;
    public FactionRouteEconomicPolicyDescriptor supplyEconomicPolicy;
    [Min(1)] public int tradeCooldownDays = 7;
    [Min(1)] public int supplyCooldownDays = 20;
    [Min(1)] public int reinforcementCooldownDays = 10;

    public string StableId => factionId?.Trim() ?? string.Empty;

    public FactionDefinitionSnapshot ToSnapshot()
    {
        return new FactionDefinitionSnapshot(
            StableId,
            displayName,
            speciesTag,
            description,
            (relationTags ?? Array.Empty<string>()).ToArray(),
            (tradeTags ?? Array.Empty<string>()).ToArray(),
            reinforcementRole,
            (tradeCargo ?? new List<FactionCargoLine>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToArray(),
            (supplyCargo ?? new List<FactionCargoLine>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToArray(),
            tradeEconomicPolicy,
            supplyEconomicPolicy,
            tradeCooldownDays,
            supplyCooldownDays,
            reinforcementCooldownDays);
    }
}
