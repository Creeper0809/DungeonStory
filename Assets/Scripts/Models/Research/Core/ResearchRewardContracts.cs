using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ResearchRewardKind
{
    Facility = 0,
    ProductionItem = 1,
    ProductionRecipe = 2,
    CombatEquipment = 3,
    MedicalProcedure = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ResearchRewardEntry
{
    public ResearchRewardEntry(
        string researchId,
        ResearchRewardKind kind,
        string rewardId,
        string displayName)
    {
        ResearchId = researchId?.Trim() ?? string.Empty;
        Kind = kind;
        RewardId = rewardId?.Trim() ?? string.Empty;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? RewardId
            : displayName.Trim();
    }

    public string ResearchId { get; }
    public ResearchRewardKind Kind { get; }
    public string RewardId { get; }
    public string DisplayName { get; }
}

public interface IResearchRewardCatalog
{
    IReadOnlyList<ResearchRewardEntry> All { get; }
    IReadOnlyList<ResearchRewardEntry> GetRewards(ResearchProjectId researchId);
    bool TryGetRequiredResearch(
        ResearchRewardKind kind,
        string rewardId,
        out ResearchProjectId researchId);
    IReadOnlyList<string> Validate();
}

public sealed class ResearchRewardIndex
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<ResearchRewardEntry>> byResearch;
    private readonly IReadOnlyDictionary<string, ResearchProjectId> requirementByReward;

    public ResearchRewardIndex(IEnumerable<ResearchRewardEntry> rewards)
    {
        All = (rewards ?? Array.Empty<ResearchRewardEntry>())
            .Where(entry => !string.IsNullOrWhiteSpace(entry.ResearchId)
                && !string.IsNullOrWhiteSpace(entry.RewardId))
            .GroupBy(
                entry => $"{entry.ResearchId}|{(int)entry.Kind}|{entry.RewardId}",
                StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.ResearchId, StringComparer.Ordinal)
            .ThenBy(entry => entry.Kind)
            .ThenBy(entry => entry.RewardId, StringComparer.Ordinal)
            .ToArray();
        byResearch = All
            .GroupBy(entry => entry.ResearchId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ResearchRewardEntry>)group.ToArray(),
                StringComparer.Ordinal);
        requirementByReward = All
            .GroupBy(entry => RewardKey(entry.Kind, entry.RewardId), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => new ResearchProjectId(group.First().ResearchId),
                StringComparer.Ordinal);
    }

    public IReadOnlyList<ResearchRewardEntry> All { get; }

    public IReadOnlyList<ResearchRewardEntry> GetRewards(ResearchProjectId researchId) =>
        byResearch.TryGetValue(researchId.Value, out IReadOnlyList<ResearchRewardEntry> rewards)
            ? rewards
            : Array.Empty<ResearchRewardEntry>();

    public bool TryGetRequiredResearch(
        ResearchRewardKind kind,
        string rewardId,
        out ResearchProjectId researchId) =>
        requirementByReward.TryGetValue(RewardKey(kind, rewardId), out researchId);

    public static string RewardKey(ResearchRewardKind kind, string rewardId) =>
        $"{(int)kind}:{rewardId?.Trim() ?? string.Empty}";
}
