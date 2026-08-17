using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum V20FactionContractKind { Supply, CrisisResponse, Strategic }

public static class AuthoredFactionContractBalanceRules
{
    public const int ReferenceAdultWorkers = 12;
    public const float WorkUnitsPerAdultDay =
        SettlementLaborAuthority.EffectiveOutputWuPerAdultDay;
    public const float ProductiveLaborShare = 0.425f;

    public static float CalculateReferenceProduction(int deadlineDays) =>
        ReferenceAdultWorkers
        * WorkUnitsPerAdultDay
        * Mathf.Max(1, deadlineDays)
        * ProductiveLaborShare;

    public static Vector2 BurdenBand(V20FactionContractKind kind) => kind switch
    {
        V20FactionContractKind.Supply => new Vector2(0.01f, 0.03f),
        V20FactionContractKind.CrisisResponse => new Vector2(0.03f, 0.08f),
        V20FactionContractKind.Strategic => new Vector2(0.05f, 0.15f),
        _ => Vector2.zero
    };
}

[CreateAssetMenu(fileName = "FactionContract", menuName = "DungeonStory/V20/Faction Contract")]
public sealed class FactionContractDefinitionSO : V20AuthoredContentSO
{
    public string factionId = string.Empty;
    public V20FactionContractKind kind;
    [Min(1)] public int deadlineDays = 10;
    public V20ContentRequirementSet completionRequirements = new();
    public List<V20ContentEffect> successEffects = new();
    public List<V20ContentEffect> failureEffects = new();

    public override IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = base.ValidateDefinition().ToList();
        if (string.IsNullOrWhiteSpace(factionId)) errors.Add($"'{StableId}' requires a faction id.");
        errors.AddRange((completionRequirements ?? new()).Validate(StableId));
        if (successEffects == null || successEffects.Count == 0 || successEffects.Any(value => value == null || !value.IsValid))
            errors.Add($"'{StableId}' requires success effects.");
        return errors;
    }
}
