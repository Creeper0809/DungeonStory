using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(
    fileName = "OffenseCampaignCatalog",
    menuName = "DungeonStory/Offense/Campaign Catalog")]
public sealed class OffenseCampaignCatalogSO : ScriptableObject
{
    [SerializeField] private List<OffenseTargetDefinition> targets = new();

    public IReadOnlyList<OffenseTargetDefinition> CreateRuntimeDefinitions()
    {
        IReadOnlyList<string> errors = ValidateDefinition();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Offense campaign catalog is invalid:\n"
                + string.Join("\n", errors));
        }

        return targets
            .Select(value => value.CreateRuntimeCopy())
            .OrderBy(value => value.campaignOrder)
            .ThenBy(value => value.id, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<string> ValidateDefinition()
    {
        List<string> errors = new();
        if (targets == null || targets.Count == 0)
        {
            errors.Add("Campaign catalog has no target definitions.");
            return errors;
        }

        for (int index = 0; index < targets.Count; index++)
        {
            OffenseTargetDefinition target = targets[index];
            if (target == null)
            {
                errors.Add($"Campaign target {index} is missing.");
            }
            else if (!target.IsValid)
            {
                errors.Add($"Campaign target {index} ('{target.id}') is invalid.");
            }
            else if (target.campaignOrder <= 0)
            {
                errors.Add($"Campaign target '{target.id}' has no positive order.");
            }
        }

        OffenseTargetDefinition[] valid = targets
            .Where(value => value != null && value.IsValid)
            .ToArray();
        foreach (IGrouping<string, OffenseTargetDefinition> duplicate in valid
                     .GroupBy(value => value.id, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Campaign target ID '{duplicate.Key}' is duplicated.");
        }
        foreach (IGrouping<int, OffenseTargetDefinition> duplicate in valid
                     .GroupBy(value => value.campaignOrder)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Campaign order {duplicate.Key} is duplicated.");
        }

        HashSet<string> ids = valid.Select(value => value.id)
            .ToHashSet(StringComparer.Ordinal);
        foreach (OffenseTargetDefinition target in valid)
        {
            if (!string.IsNullOrWhiteSpace(target.prerequisiteTargetId)
                && !ids.Contains(target.prerequisiteTargetId))
            {
                errors.Add(
                    $"Campaign target '{target.id}' references missing prerequisite '{target.prerequisiteTargetId}'.");
            }
            if (string.Equals(target.id, target.prerequisiteTargetId,
                    StringComparison.Ordinal))
            {
                errors.Add($"Campaign target '{target.id}' requires itself.");
            }
        }

        foreach (OffenseTargetDefinition origin in valid)
        {
            HashSet<string> visited = new(StringComparer.Ordinal);
            OffenseTargetDefinition cursor = origin;
            while (cursor != null
                && !string.IsNullOrWhiteSpace(cursor.prerequisiteTargetId))
            {
                if (!visited.Add(cursor.id))
                {
                    errors.Add(
                        $"Campaign prerequisite cycle reaches '{cursor.id}'.");
                    break;
                }
                cursor = valid.FirstOrDefault(value => string.Equals(
                    value.id,
                    cursor.prerequisiteTargetId,
                    StringComparison.Ordinal));
            }
        }

        if (valid.Count(value => value.revealsTruth) != 1)
        {
            errors.Add("Campaign catalog must contain exactly one truth-revealing target.");
        }
        return errors;
    }

#if UNITY_EDITOR
    public void SetDefinitionsForMigration(
        IEnumerable<OffenseTargetDefinition> definitions)
    {
        if (definitions == null)
        {
            throw new ArgumentNullException(nameof(definitions));
        }
        targets = definitions
            .Where(value => value != null)
            .Select(value => value.CreateRuntimeCopy())
            .ToList();
    }
#endif
}

public interface IOffenseCampaignCatalog
{
    IReadOnlyList<OffenseTargetDefinition> Targets { get; }
    bool TryGet(string targetId, out OffenseTargetDefinition definition);
}

public sealed class ResourceOffenseCampaignCatalog : IOffenseCampaignCatalog
{
    private readonly IReadOnlyList<OffenseTargetDefinition> targets;
    private readonly IReadOnlyDictionary<string, OffenseTargetDefinition> byId;

    public ResourceOffenseCampaignCatalog(IGameContentCatalog content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        OffenseCampaignCatalogSO asset =
            content.RequireSingle<OffenseCampaignCatalogSO>();
        targets = asset.CreateRuntimeDefinitions();
        byId = targets.ToDictionary(value => value.id, StringComparer.Ordinal);
    }

    public IReadOnlyList<OffenseTargetDefinition> Targets => targets;

    public bool TryGet(
        string targetId,
        out OffenseTargetDefinition definition)
    {
        definition = null;
        if (string.IsNullOrWhiteSpace(targetId)
            || !byId.TryGetValue(targetId, out OffenseTargetDefinition value))
        {
            return false;
        }
        definition = value.CreateRuntimeCopy();
        return true;
    }
}
