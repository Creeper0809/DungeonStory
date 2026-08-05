using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FacilityEvolutionDomain = DungeonStory.FacilityEvolution;

public enum FacilityEvolutionRecordTokenConsumePolicy
{
    ConsumeRequiredAmount = 0,
    Preserve = 1,
    ConsumeAll = 2
}

[CreateAssetMenu(menuName = "DungeonStory/Facility Evolution/Record Token Definition", order = 1)]
public class FacilityEvolutionRecordTokenDefinitionSO : DataScriptableObject
{
    public string tokenId;
    public string displayName;
    [TextArea] public string description;

    [Header("Source")]
    public string sourceMetric;
    public float threshold;
    public string decayPolicy;

    [Header("Evolution")]
    public FacilityEvolutionRecordTokenConsumePolicy consumePolicy =
        FacilityEvolutionRecordTokenConsumePolicy.ConsumeRequiredAmount;
    public string[] recipeTags = Array.Empty<string>();
    [TextArea] public string uiHint;

    public string EffectiveId => !string.IsNullOrWhiteSpace(tokenId) ? tokenId : name;
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName : EffectiveId;
}

public interface IFacilityEvolutionRecordTokenDefinitionProvider
{
    IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> GetDefinitions();
    FacilityEvolutionRecordTokenDefinitionSO GetDefinition(string tokenId);
}

public sealed class EmptyFacilityEvolutionRecordTokenDefinitionProvider :
    IFacilityEvolutionRecordTokenDefinitionProvider
{
    public IReadOnlyList<FacilityEvolutionRecordTokenDefinitionSO> GetDefinitions()
    {
        return Array.Empty<FacilityEvolutionRecordTokenDefinitionSO>();
    }

    public FacilityEvolutionRecordTokenDefinitionSO GetDefinition(string tokenId)
    {
        return null;
    }
}

public interface IFacilityEvolutionRecordTokenConsumer
{
    bool TryConsume(
        FacilityEvolutionRecord record,
        IEnumerable<FacilityEvolutionTokenRequirement> requirements,
        bool consumeRequestedByRecipe,
        out string reason);
}

public sealed class DefaultFacilityEvolutionRecordTokenConsumer : IFacilityEvolutionRecordTokenConsumer
{
    private readonly IFacilityEvolutionRecordTokenDefinitionProvider definitionProvider;

    public DefaultFacilityEvolutionRecordTokenConsumer(
        IFacilityEvolutionRecordTokenDefinitionProvider definitionProvider)
    {
        this.definitionProvider =
            definitionProvider ?? throw new ArgumentNullException(nameof(definitionProvider));
    }

    public bool TryConsume(
        FacilityEvolutionRecord record,
        IEnumerable<FacilityEvolutionTokenRequirement> requirements,
        bool consumeRequestedByRecipe,
        out string reason)
    {
        reason = string.Empty;
        if (requirements == null)
        {
            return true;
        }

        List<FacilityEvolutionTokenRequirement> normalized = requirements
            .Where((requirement) => !string.IsNullOrWhiteSpace(requirement.key))
            .ToList();
        if (normalized.Count == 0)
        {
            return true;
        }

        if (record == null)
        {
            reason = "기록 없음";
            return false;
        }

        if (!consumeRequestedByRecipe)
        {
            foreach (FacilityEvolutionTokenRequirement requirement in normalized)
            {
                int required = Mathf.Max(1, requirement.minCount);
                int current = record.GetToken(requirement.key);
                if (current < required)
                {
                    reason = $"{requirement.key} {current}/{required}";
                    return false;
                }
            }
            return true;
        }

        Dictionary<string, int> consumption =
            new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (FacilityEvolutionTokenRequirement requirement in normalized)
        {
            FacilityEvolutionRecordTokenDefinitionSO definition =
                definitionProvider.GetDefinition(requirement.key);
            FacilityEvolutionRecordTokenConsumePolicy policy = definition != null
                ? definition.consumePolicy
                : FacilityEvolutionRecordTokenConsumePolicy.ConsumeRequiredAmount;

            if (policy == FacilityEvolutionRecordTokenConsumePolicy.Preserve)
            {
                int required = Mathf.Max(1, requirement.minCount);
                if (record.GetToken(requirement.key) < required)
                {
                    reason = $"{requirement.key} {record.GetToken(requirement.key)}/{required}";
                    return false;
                }
                continue;
            }

            if (policy == FacilityEvolutionRecordTokenConsumePolicy.ConsumeAll)
            {
                consumption[requirement.key] = record.GetToken(requirement.key);
                continue;
            }
            consumption[requirement.key] = Mathf.Max(1, requirement.minCount);
        }

        try
        {
            FacilityEvolutionDomain.FacilityEvolutionRecordSnapshot next =
                FacilityEvolutionDomain.FacilityEvolutionRecordRules.ConsumeTokens(
                    record.ToDomainSnapshot(),
                    consumption);
            record.ReplaceWith(next);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            reason = ex.Message;
            return false;
        }
    }
}
