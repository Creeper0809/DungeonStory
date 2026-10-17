using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Read-only living-state projection for an already prepared starting
/// candidate. This deliberately consumes only the candidate's prepared growth
/// state and authored definitions: opening a preparation view cannot create a
/// body/need state or consume a random roll.
/// </summary>
public readonly struct StartPartyCandidateLivingSummary
{
    public StartPartyCandidateLivingSummary(
        IReadOnlyList<StartPartyInitialHealthCondition> initialHealthConditions,
        CharacterDietPolicyKind dietPolicy,
        float initialSleep,
        float sleepRateMultiplier,
        SpeciesThermalProfile thermalProfile)
    {
        InitialHealthConditions = initialHealthConditions
            ?? Array.Empty<StartPartyInitialHealthCondition>();
        DietPolicy = dietPolicy;
        InitialSleep = UnityEngine.Mathf.Clamp(initialSleep, 0f, 100f);
        SleepRateMultiplier = UnityEngine.Mathf.Max(0f, sleepRateMultiplier);
        ThermalProfile = thermalProfile;
    }

    public IReadOnlyList<StartPartyInitialHealthCondition> InitialHealthConditions { get; }
    public CharacterDietPolicyKind DietPolicy { get; }
    public float InitialSleep { get; }
    public float SleepRateMultiplier { get; }
    public SpeciesThermalProfile ThermalProfile { get; }
}

public readonly struct StartPartyInitialHealthCondition
{
    public StartPartyInitialHealthCondition(
        string conditionId,
        string displayName,
        AgeConditionSeverity severity)
    {
        ConditionId = conditionId?.Trim() ?? string.Empty;
        DisplayName = displayName?.Trim() ?? string.Empty;
        Severity = severity;
    }

    public string ConditionId { get; }
    public string DisplayName { get; }
    public AgeConditionSeverity Severity { get; }
}

public static class StartPartyCandidateLivingSummaryProjector
{
    public static StartPartyCandidateLivingSummary Create(
        StartPartyMemberPreparation member,
        IEnumerable<AgeConditionDefinitionSO> ageConditions,
        ICharacterRuntimeProfileFactory runtimeProfileFactory,
        ICharacterNeedDefinitionCatalog needDefinitionCatalog)
    {
        if (member?.CharacterData == null || member.Progression == null)
        {
            throw new InvalidOperationException(
                "Prepared candidate requires character data and progression for its living summary.");
        }

        if (runtimeProfileFactory == null || needDefinitionCatalog == null)
        {
            throw new InvalidOperationException(
                "Prepared candidate living summary requires runtime-profile and need-definition services.");
        }

        CharacterRuntimeProfile profile = runtimeProfileFactory.Create(
            CharacterSpawnRequest.FromAuthoring(
                member.CharacterData,
                member.Progression.ResolveSelectedTraits()));
        if (!needDefinitionCatalog.TryGet(
                CharacterCondition.SLEEP,
                out CharacterNeedDefinition sleepDefinition))
        {
            throw new InvalidOperationException(
                "Prepared candidate living summary requires the authored sleep need definition.");
        }

        return new StartPartyCandidateLivingSummary(
            ResolveInitialHealthConditions(member, ageConditions),
            CharacterConsumablesPolicyRules.DefaultDietPolicy,
            sleepDefinition.DefaultValue,
            profile.GetNeedProfile().sleepRateMultiplier,
            profile.GetEnvironmentProfile().ToThermalProfile());
    }

    private static IReadOnlyList<StartPartyInitialHealthCondition> ResolveInitialHealthConditions(
        StartPartyMemberPreparation member,
        IEnumerable<AgeConditionDefinitionSO> ageConditions)
    {
        string[] conditionIds = (member.Progression.GrowthState.startingProfile?.initialAgeConditionIds
                ?? new List<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (conditionIds.Length == 0)
        {
            return Array.Empty<StartPartyInitialHealthCondition>();
        }

        Dictionary<string, AgeConditionDefinitionSO> definitions = (ageConditions
                ?? Array.Empty<AgeConditionDefinitionSO>())
            .Where(value => value != null)
            .ToDictionary(value => value.conditionId, StringComparer.Ordinal);
        return conditionIds.Select(conditionId =>
        {
            if (!definitions.TryGetValue(conditionId, out AgeConditionDefinitionSO definition))
            {
                throw new InvalidOperationException(
                    $"Prepared candidate references unknown initial age condition '{conditionId}'.");
            }

            // CharacterLifeRecord.AddInitialAgeConditions initializes every
            // carried condition at Mild before the body-health adapter applies it.
            return new StartPartyInitialHealthCondition(
                definition.conditionId,
                definition.displayName,
                AgeConditionSeverity.Mild);
        }).ToArray();
    }
}
