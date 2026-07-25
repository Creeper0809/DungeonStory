using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        if (!consumeRequestedByRecipe)
        {
            return true;
        }

        foreach (FacilityEvolutionTokenRequirement requirement in normalized)
        {
            FacilityEvolutionRecordTokenDefinitionSO definition =
                definitionProvider.GetDefinition(requirement.key);
            FacilityEvolutionRecordTokenConsumePolicy policy = definition != null
                ? definition.consumePolicy
                : FacilityEvolutionRecordTokenConsumePolicy.ConsumeRequiredAmount;

            if (policy == FacilityEvolutionRecordTokenConsumePolicy.Preserve)
            {
                continue;
            }

            if (policy == FacilityEvolutionRecordTokenConsumePolicy.ConsumeAll)
            {
                record.SetToken(requirement.key, 0);
                continue;
            }

            if (!record.TryConsumeToken(requirement.key, Mathf.Max(1, requirement.minCount), out reason))
            {
                return false;
            }
        }

        return true;
    }
}
