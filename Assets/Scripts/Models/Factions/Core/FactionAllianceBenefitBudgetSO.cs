using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class FactionAllianceBenefitRouteCostRecord
{
    public string factionId = string.Empty;
    public int cooldownDays;
    public string supplyQuoteSourceDigest = string.Empty;
    public long debitMilliEwu;
}

[CreateAssetMenu(
    fileName = "FactionAllianceBenefitBudget",
    menuName = "DungeonStory/Factions/Alliance Benefit Budget",
    order = 1)]
public sealed class FactionAllianceBenefitBudgetSO : ScriptableObject
{
    public const string ResourcePath =
        "SO/Factions/FactionAllianceBenefitBudget";

    public int schemaVersion = 1;
    public string approvedBalanceSourceDigest = string.Empty;
    public long capacityMilliEwu;
    public long refillNumeratorMilliEwu;
    public long refillDenominatorDays = 1;
    public List<FactionAllianceBenefitRouteCostRecord> routeCosts = new();

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (schemaVersion != 1
            || !IsSha256(approvedBalanceSourceDigest)
            || capacityMilliEwu <= 0
            || refillNumeratorMilliEwu <= 0
            || refillDenominatorDays <= 0)
        {
            errors.Add("Alliance-benefit budget scalar authority is invalid.");
        }

        HashSet<string> factions = new(StringComparer.Ordinal);
        long debitSum = 0;
        string previousFaction = string.Empty;
        foreach (FactionAllianceBenefitRouteCostRecord route in
                 routeCosts ?? new List<FactionAllianceBenefitRouteCostRecord>())
        {
            if (route == null
                || !IsCanonical(route.factionId)
                || !factions.Add(route.factionId)
                || (previousFaction.Length > 0 && string.Compare(
                    previousFaction,
                    route.factionId,
                    StringComparison.Ordinal) >= 0)
                || route.cooldownDays <= 0
                || !IsSha256(route.supplyQuoteSourceDigest)
                || route.debitMilliEwu <= 0)
            {
                errors.Add("Alliance-benefit route cost rows are not canonical.");
                continue;
            }
            try
            {
                debitSum = checked(debitSum + route.debitMilliEwu);
            }
            catch (OverflowException)
            {
                errors.Add("Alliance-benefit route cost sum overflowed.");
            }
            previousFaction = route.factionId;
        }
        if ((routeCosts?.Count ?? 0) == 0 || debitSum != capacityMilliEwu)
            errors.Add("Alliance-benefit capacity does not equal one full route bundle.");
        return errors;
    }

    private static bool IsCanonical(string value) =>
        !string.IsNullOrEmpty(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}
