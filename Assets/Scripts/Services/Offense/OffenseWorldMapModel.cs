using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public abstract class OffenseRewardGrantSpec
{
    public abstract string RewardTypeId { get; }
    public abstract OffenseRewardCategory Category { get; }
}

[Serializable]
public sealed class OffenseMoneyRewardSpec : OffenseRewardGrantSpec
{
    public override string RewardTypeId => OffenseRewardTypeIds.Money;
    public override OffenseRewardCategory Category => OffenseRewardCategory.Money;
}

[Serializable]
public sealed class OffenseStockRewardSpec : OffenseRewardGrantSpec
{
    [SerializeField] private StockCategory stockCategory;

    public OffenseStockRewardSpec()
    {
    }

    public OffenseStockRewardSpec(StockCategory stockCategory)
    {
        this.stockCategory = stockCategory;
    }

    public StockCategory StockCategory => stockCategory;
    public override string RewardTypeId => OffenseRewardTypeIds.Stock;
    public override OffenseRewardCategory Category => OffenseRewardCategory.Stock;
}

[Serializable]
public sealed class OffenseRareFacilityRewardSpec : OffenseRewardGrantSpec
{
    public override string RewardTypeId => OffenseRewardTypeIds.RareFacility;
    public override OffenseRewardCategory Category => OffenseRewardCategory.RareFacility;
}

[Serializable]
public abstract class OffenseBlueprintRewardSpec : OffenseRewardGrantSpec
{
    public override string RewardTypeId => OffenseRewardTypeIds.Blueprint;
    public override OffenseRewardCategory Category => OffenseRewardCategory.Blueprint;
    public abstract bool IsEligible(
        FacilityBlueprintSO blueprint,
        IReadOnlyCollection<BuildingSO> buildings);
}

[Serializable]
public sealed class OffenseAnyBlueprintRewardSpec : OffenseBlueprintRewardSpec
{
    public override bool IsEligible(
        FacilityBlueprintSO blueprint,
        IReadOnlyCollection<BuildingSO> buildings)
    {
        return blueprint != null;
    }
}

[Serializable]
public sealed class OffenseDefenseBlueprintRewardSpec : OffenseBlueprintRewardSpec
{
    public override bool IsEligible(
        FacilityBlueprintSO blueprint,
        IReadOnlyCollection<BuildingSO> buildings)
    {
        if (blueprint == null || buildings == null)
        {
            return false;
        }

        HashSet<int> defenseBuildingIds = buildings
            .Where((building) => building?.Defense != null && building.Defense.IsDefenseFacility)
            .Select((building) => building.id)
            .ToHashSet();
        return blueprint.Unlocks
            .OfType<IBlueprintBuildingUnlock>()
            .Any((unlock) => defenseBuildingIds.Contains(unlock.BuildingId));
    }
}

[Serializable]
public sealed class OffenseSpecialBlueprintRewardSpec : OffenseBlueprintRewardSpec
{
    public override bool IsEligible(
        FacilityBlueprintSO blueprint,
        IReadOnlyCollection<BuildingSO> buildings)
    {
        return blueprint != null && blueprint.rarity != FacilityShopRarity.Common;
    }
}

[Serializable]
public sealed class OffenseSpecificBlueprintRewardSpec : OffenseBlueprintRewardSpec
{
    [SerializeField] private int blueprintId;

    public OffenseSpecificBlueprintRewardSpec()
    {
    }

    public OffenseSpecificBlueprintRewardSpec(int blueprintId)
    {
        this.blueprintId = blueprintId;
    }

    public int BlueprintId => blueprintId;

    public override bool IsEligible(
        FacilityBlueprintSO blueprint,
        IReadOnlyCollection<BuildingSO> buildings)
    {
        return blueprint != null && blueprint.id == blueprintId;
    }
}

[Serializable]
public sealed class OffenseRegionalPressureRewardSpec : OffenseRewardGrantSpec
{
    public override string RewardTypeId => OffenseRewardTypeIds.RegionalPressure;
    public override OffenseRewardCategory Category => OffenseRewardCategory.StrategicPressure;
}

[Serializable]
public sealed class OffenseRecruitCandidateRewardSpec : OffenseRewardGrantSpec
{
    public override string RewardTypeId => OffenseRewardTypeIds.RecruitCandidate;
    public override OffenseRewardCategory Category => OffenseRewardCategory.RecruitCandidate;
}

[Serializable]
public sealed class OffensePrisonerRewardSpec : OffenseRewardGrantSpec
{
    public override string RewardTypeId => OffenseRewardTypeIds.Prisoner;
    public override OffenseRewardCategory Category => OffenseRewardCategory.Prisoner;
}

[Serializable]
public sealed class OffenseSpecialMonsterRewardSpec : OffenseRewardGrantSpec
{
    public override string RewardTypeId => OffenseRewardTypeIds.SpecialMonster;
    public override OffenseRewardCategory Category => OffenseRewardCategory.Prisoner;
}

[Serializable]
public sealed class OffenseRewardPreview
{
    [SerializeReference] private OffenseRewardGrantSpec grantSpec;
    [SerializeField] private string displayLabel;
    [SerializeField, Min(0)] private int configuredAmount;

    public OffenseRewardPreview(
        string label,
        int amount,
        OffenseRewardGrantSpec grantSpec)
    {
        displayLabel = label ?? string.Empty;
        configuredAmount = Mathf.Max(0, amount);
        this.grantSpec = grantSpec ?? throw new ArgumentNullException(nameof(grantSpec));
    }

    public OffenseRewardGrantSpec GrantSpec => grantSpec;
    public OffenseRewardCategory category => grantSpec?.Category ?? OffenseRewardCategory.Money;
    public string label => displayLabel;
    public int amount => Mathf.Max(0, configuredAmount);
    public bool IsConfigured => grantSpec != null && !string.IsNullOrWhiteSpace(grantSpec.RewardTypeId);

    public string ToSummaryText()
    {
        string name = string.IsNullOrWhiteSpace(label) ? category.ToString() : label;
        return amount > 0 ? $"{name} x{amount}" : name;
    }
}

[Serializable]
public class OffenseTargetDefinition
{
    public string id;
    public string title;
    [TextArea] public string description;
    public OffenseTargetKind kind;
    public string regionId;
    public string regionDisplayName;
    public string factionId;
    public StrategicPressureAxis strategicPressureAxis;
    [Min(0f)] public float strategicPressureAmount = 15f;
    [Min(1)] public int campaignOrder = 1;
    public string prerequisiteTargetId;
    public bool revealsTruth;
    [TextArea] public string truthText;
    [Min(0f)] public float distance;
    [Min(0f)] public float danger;
    [Min(1f)] public float durationSeconds = 90f;
    [Min(1)] public int requiredMembers = 1;
    [Min(0f)] public float requiredPower;
    public OffenseRewardPreview[] rewards = Array.Empty<OffenseRewardPreview>();

    public bool IsValid => !string.IsNullOrWhiteSpace(id)
        && !string.IsNullOrWhiteSpace(title)
        && distance >= 0f
        && requiredMembers > 0;

    public OffenseTargetSnapshot ToSnapshot(
        bool preciseIntel,
        IOffenseWorldMapStateView campaignState = null)
    {
        bool completed = campaignState != null && campaignState.IsTargetCompleted(id);
        bool prerequisiteMet = string.IsNullOrWhiteSpace(prerequisiteTargetId)
            || (campaignState != null && campaignState.IsTargetCompleted(prerequisiteTargetId));
        bool available = !completed
            && prerequisiteMet
            && (campaignState == null || !campaignState.TruthRevealed);
        string status = completed
            ? revealsTruth ? "진실 공개 완료" : "완료"
            : !prerequisiteMet
                ? "선행 목표 필요"
                : campaignState != null && campaignState.TruthRevealed
                    ? "캠페인 완료"
                    : "출정 가능";

        return new OffenseTargetSnapshot(
            id,
            title,
            description,
            kind,
            distance,
            preciseIntel ? danger : Mathf.Max(1f, Mathf.Round(danger / 5f) * 5f),
            durationSeconds,
            requiredMembers,
            preciseIntel ? requiredPower : Mathf.Max(0f, Mathf.Round(requiredPower / 5f) * 5f),
            rewards?
                .Where((reward) => reward != null)
                .Select((reward) => reward.ToSummaryText())
                .ToArray()
                ?? Array.Empty<string>(),
            campaignOrder,
            prerequisiteTargetId,
            available,
            completed,
            revealsTruth,
            status,
            truthText,
            regionId,
            regionDisplayName,
            factionId,
            strategicPressureAxis,
            strategicPressureAmount);
    }

    internal OffenseTargetDefinition CreateRuntimeCopy()
    {
        return new OffenseTargetDefinition
        {
            id = id,
            title = title,
            description = description,
            kind = kind,
            regionId = regionId,
            regionDisplayName = regionDisplayName,
            factionId = factionId,
            strategicPressureAxis = strategicPressureAxis,
            strategicPressureAmount = strategicPressureAmount,
            campaignOrder = campaignOrder,
            prerequisiteTargetId = prerequisiteTargetId,
            revealsTruth = revealsTruth,
            truthText = truthText,
            distance = distance,
            danger = danger,
            durationSeconds = durationSeconds,
            requiredMembers = requiredMembers,
            requiredPower = requiredPower,
            rewards = rewards != null
                ? (OffenseRewardPreview[])rewards.Clone()
                : Array.Empty<OffenseRewardPreview>()
        };
    }
}

[Serializable]
public sealed class OffenseTargetSnapshot
{
    public OffenseTargetSnapshot(
        string id,
        string title,
        string description,
        OffenseTargetKind kind,
        float distance,
        float danger,
        float durationSeconds,
        int requiredMembers,
        float requiredPower,
        IReadOnlyList<string> rewards,
        int campaignOrder = 1,
        string prerequisiteTargetId = "",
        bool isAvailable = true,
        bool isCompleted = false,
        bool revealsTruth = false,
        string statusMessage = "",
        string truthText = "",
        string regionId = "",
        string regionDisplayName = "",
        string factionId = "",
        StrategicPressureAxis strategicPressureAxis = StrategicPressureAxis.None,
        float strategicPressureAmount = 0f)
    {
        this.id = id ?? string.Empty;
        this.title = title ?? string.Empty;
        this.description = description ?? string.Empty;
        this.kind = kind;
        this.distance = Mathf.Max(0f, distance);
        this.danger = Mathf.Max(0f, danger);
        this.durationSeconds = Mathf.Max(1f, durationSeconds);
        this.requiredMembers = Mathf.Max(1, requiredMembers);
        this.requiredPower = Mathf.Max(0f, requiredPower);
        this.rewards = EventPayloadSnapshot.Copy(rewards);
        this.campaignOrder = Mathf.Max(1, campaignOrder);
        this.prerequisiteTargetId = prerequisiteTargetId ?? string.Empty;
        this.isAvailable = isAvailable;
        this.isCompleted = isCompleted;
        this.revealsTruth = revealsTruth;
        this.statusMessage = statusMessage ?? string.Empty;
        this.truthText = truthText ?? string.Empty;
        this.regionId = regionId ?? string.Empty;
        this.regionDisplayName = regionDisplayName ?? string.Empty;
        this.factionId = factionId ?? string.Empty;
        this.strategicPressureAxis = strategicPressureAxis;
        this.strategicPressureAmount = Mathf.Max(0f, strategicPressureAmount);
    }

    public string id { get; }
    public string title { get; }
    public string description { get; }
    public OffenseTargetKind kind { get; }
    public float distance { get; }
    public float danger { get; }
    public float durationSeconds { get; }
    public int requiredMembers { get; }
    public float requiredPower { get; }
    public IReadOnlyList<string> rewards { get; }
    public int campaignOrder { get; }
    public string prerequisiteTargetId { get; }
    public bool isAvailable { get; }
    public bool isCompleted { get; }
    public bool revealsTruth { get; }
    public string statusMessage { get; }
    public string truthText { get; }
    public string regionId { get; }
    public string regionDisplayName { get; }
    public string factionId { get; }
    public StrategicPressureAxis strategicPressureAxis { get; }
    public float strategicPressureAmount { get; }

    public OffenseTargetSnapshot Copy()
    {
        return this;
    }

    public string ToDetailText()
    {
        List<string> lines = new List<string>
        {
            string.IsNullOrWhiteSpace(title) ? "원정 대상" : title,
            $"캠페인 단계: {campaignOrder}",
            $"진행: {statusMessage}",
            $"종류: {GetKindName(kind)}",
            $"거리: {distance:0.#}",
            $"위험도: {danger:0.#}",
            $"필요 인력: {requiredMembers}",
            $"권장 전투력: {requiredPower:0.#}",
            $"적 편성: {OffenseEncounterCatalog.GetEnemySummary(campaignOrder)}",
            "진행 방식: 행동과 대상을 직접 선택하는 턴제 전투"
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            lines.Add(string.Empty);
            lines.Add(description);
        }

        if (rewards.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("예상 보상:");
            foreach (string reward in rewards)
            {
                if (!string.IsNullOrWhiteSpace(reward))
                {
                    lines.Add($"- {reward}");
                }
            }
        }

        return string.Join("\n", lines);
    }

    private static string GetKindName(OffenseTargetKind kind)
    {
        return kind switch
        {
            OffenseTargetKind.HumanOutpost => "인간 거점",
            OffenseTargetKind.ResourceSite => "자원지",
            OffenseTargetKind.SpecialEvent => "특수 지점",
            OffenseTargetKind.RivalDungeon => "경쟁 던전",
            _ => kind.ToString()
        };
    }
}

public interface IOffenseWorldMapStateView
{
    int ReconLevel { get; }
    string SelectedTargetId { get; }
    IReadOnlyCollection<string> KnownTargetIds { get; }
    IReadOnlyCollection<string> CompletedTargetIds { get; }
    string RevealedTruthTargetId { get; }
    bool TruthRevealed { get; }
    int CompletedTargetCount { get; }
    bool KnowTarget(string targetId);
    bool IsTargetCompleted(string targetId);
}

public sealed class OffenseWorldMapState : IOffenseWorldMapStateView
{
    private readonly HashSet<string> knownTargetIds = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> completedTargetIds = new HashSet<string>(StringComparer.Ordinal);

    public int ReconLevel { get; private set; }
    public string SelectedTargetId { get; private set; }
    public IReadOnlyCollection<string> KnownTargetIds => Array.AsReadOnly(knownTargetIds.ToArray());
    public IReadOnlyCollection<string> CompletedTargetIds => Array.AsReadOnly(completedTargetIds.ToArray());
    public string RevealedTruthTargetId { get; private set; }
    public bool TruthRevealed => !string.IsNullOrWhiteSpace(RevealedTruthTargetId);
    public int CompletedTargetCount => completedTargetIds.Count;

    internal void Reset(int reconLevel = 0)
    {
        ReconLevel = Mathf.Max(0, reconLevel);
        SelectedTargetId = string.Empty;
        knownTargetIds.Clear();
        completedTargetIds.Clear();
        RevealedTruthTargetId = string.Empty;
    }

    internal void Restore(int reconLevel, string selectedTargetId, IEnumerable<string> restoredKnownTargetIds)
    {
        Restore(
            reconLevel,
            selectedTargetId,
            restoredKnownTargetIds,
            Array.Empty<string>(),
            string.Empty);
    }

    internal void Restore(
        int reconLevel,
        string selectedTargetId,
        IEnumerable<string> restoredKnownTargetIds,
        IEnumerable<string> restoredCompletedTargetIds,
        string revealedTruthTargetId)
    {
        ReconLevel = Mathf.Max(0, reconLevel);
        knownTargetIds.Clear();
        completedTargetIds.Clear();
        foreach (string targetId in restoredKnownTargetIds ?? Array.Empty<string>())
        {
            AddKnownTarget(targetId);
        }

        foreach (string targetId in restoredCompletedTargetIds ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(targetId))
            {
                completedTargetIds.Add(targetId);
                AddKnownTarget(targetId);
            }
        }

        RevealedTruthTargetId = completedTargetIds.Contains(revealedTruthTargetId)
            ? revealedTruthTargetId
            : string.Empty;
        SelectedTargetId = KnowTarget(selectedTargetId)
            && !IsTargetCompleted(selectedTargetId)
            && !TruthRevealed
                ? selectedTargetId
                : string.Empty;
    }

    public bool KnowTarget(string targetId)
    {
        return !string.IsNullOrWhiteSpace(targetId) && knownTargetIds.Contains(targetId);
    }

    public bool IsTargetCompleted(string targetId)
    {
        return !string.IsNullOrWhiteSpace(targetId) && completedTargetIds.Contains(targetId);
    }

    internal bool AddKnownTarget(string targetId)
    {
        return !string.IsNullOrWhiteSpace(targetId) && knownTargetIds.Add(targetId);
    }

    internal void SetSelectedTarget(string targetId)
    {
        SelectedTargetId = targetId ?? string.Empty;
    }

    internal bool MarkTargetCompleted(string targetId)
    {
        if (string.IsNullOrWhiteSpace(targetId) || !completedTargetIds.Add(targetId))
        {
            return false;
        }

        AddKnownTarget(targetId);
        if (string.Equals(SelectedTargetId, targetId, StringComparison.Ordinal))
        {
            SelectedTargetId = string.Empty;
        }

        return true;
    }

    internal void RevealTruth(string targetId)
    {
        if (!string.IsNullOrWhiteSpace(targetId) && IsTargetCompleted(targetId))
        {
            RevealedTruthTargetId = targetId;
            SelectedTargetId = string.Empty;
        }
    }

    internal bool TryUpgradeRecon(int maxLevel)
    {
        int safeMax = Mathf.Max(0, maxLevel);
        if (ReconLevel >= safeMax)
        {
            return false;
        }

        ReconLevel++;
        return true;
    }
}
