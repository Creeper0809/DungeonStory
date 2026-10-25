using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class OffenseCampaignRegionDefinition
{
    public string id;
    public string displayName;
    public string factionId;

    public OffenseCampaignRegionDefinition Copy() => new()
        { id = id, displayName = displayName, factionId = factionId };
}

[CreateAssetMenu(
    fileName = "OffenseCampaignCatalog",
    menuName = "DungeonStory/Offense/Campaign Catalog")]
public sealed class OffenseCampaignCatalogSO : ScriptableObject
{
    [SerializeField] private List<OffenseTargetDefinition> targets = new();
    [SerializeField] private List<OffenseCampaignRegionDefinition> regions = CreateDefaultRegions();
    public IReadOnlyList<OffenseTargetDefinition> Targets => targets;
    public IReadOnlyList<OffenseCampaignRegionDefinition> Regions => regions;

    // Initial authored defaults also describe the legacy three-region save.
    // Additional campaign regions are explicit definitions, never target typos.
    public static List<OffenseCampaignRegionDefinition> CreateDefaultRegions() => new()
    {
        new() { id = OffenseRegionRuntime.BorderTradeRegionId, displayName = "변경 교역권", factionId = OffenseRegionRuntime.HumanFactionId },
        new() { id = OffenseRegionRuntime.RivalOutpostRegionId, displayName = "경쟁 던전 전초권", factionId = OffenseRegionRuntime.RivalFactionId },
        new() { id = OffenseRegionRuntime.SealedZoneRegionId, displayName = "봉인 지대", factionId = OffenseRegionRuntime.SealFactionId }
    };

    public static IReadOnlyList<string> ValidateRegions(IEnumerable<OffenseCampaignRegionDefinition> definitions)
    {
        var errors = new List<string>();
        var values = definitions?.ToArray() ?? Array.Empty<OffenseCampaignRegionDefinition>();
        if (values.Length == 0 || values.Any(value => value == null
                || string.IsNullOrWhiteSpace(value.id) || value.id != value.id.Trim()
                || string.IsNullOrWhiteSpace(value.factionId) || value.factionId != value.factionId.Trim()
                || string.IsNullOrWhiteSpace(value.displayName)))
            errors.Add("Campaign regions require canonical IDs, factions and display names.");
        if (values.Where(value => value != null).GroupBy(value => value.id, StringComparer.Ordinal)
                .Any(group => group.Count() > 1))
            errors.Add("Campaign region IDs must be unique.");
        foreach (var legacy in CreateDefaultRegions())
            if (!values.Any(value => value != null && value.id == legacy.id && value.factionId == legacy.factionId))
                errors.Add($"Campaign regions must preserve legacy region/faction '{legacy.id}/{legacy.factionId}'.");
        return errors;
    }

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
        errors.AddRange(ValidateRegions(regions));
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
            if (target != null && !(regions ?? new()).Any(region => region != null
                    && region.id == target.regionId && region.factionId == target.factionId))
                errors.Add($"Campaign target '{target.id}' has an unregistered region/faction pair '{target.regionId}/{target.factionId}'.");
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
    IReadOnlyList<OffenseCampaignRegionDefinition> Regions { get; }
    bool TryGet(string targetId, out OffenseTargetDefinition definition);
}

public sealed class ResourceOffenseCampaignCatalog : IOffenseCampaignCatalog
{
    private readonly IReadOnlyList<OffenseTargetDefinition> targets;
    private readonly IReadOnlyDictionary<string, OffenseTargetDefinition> byId;
    private readonly OffenseCampaignRegionDefinition[] regions;

    public ResourceOffenseCampaignCatalog(IGameContentCatalog content)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }
        OffenseCampaignCatalogSO asset =
            content.RequireSingle<OffenseCampaignCatalogSO>();
        targets = asset.CreateRuntimeDefinitions();
        regions = asset.Regions.Select(region => region.Copy()).ToArray();
        byId = targets.ToDictionary(value => value.id, StringComparer.Ordinal);
    }

    public IReadOnlyList<OffenseTargetDefinition> Targets => targets;
    public IReadOnlyList<OffenseCampaignRegionDefinition> Regions => regions.Select(region => region.Copy()).ToArray();

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
