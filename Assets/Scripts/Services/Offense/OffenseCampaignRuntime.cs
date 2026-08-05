using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public sealed class DungeonOffenseCampaignSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public int reconLevel;
    public string selectedTargetId = string.Empty;
    public List<string> knownTargetIds = new List<string>();
    public List<string> completedTargetIds = new List<string>();
    public string revealedTruthTargetId = string.Empty;
}

public interface IOffenseCampaignRuntime
{
    IOffenseWorldMapStateView State { get; }
    DungeonOffenseCampaignSaveData Capture();
    OffenseCampaignRestoreCandidate BuildRestoreCandidate(
        DungeonOffenseCampaignSaveData source);
    void PublishRestoreCandidate(OffenseCampaignRestoreCandidate candidate);
}

/// <summary>
/// Mutable campaign-state boundary used only by the world-map command adapter.
/// The implementation is a scoped plain runtime object, never the scene
/// MonoBehaviour, so save ownership cannot silently return to the view adapter.
/// </summary>
public interface IOffenseCampaignStateAuthority
{
    IOffenseWorldMapStateView State { get; }
    OffenseWorldMapState MutableState { get; }
    void ConfigureTargets(IEnumerable<OffenseTargetDefinition> definitions);
    void Reset(int reconLevel);
}

public interface IOffenseCampaignQuery
{
    IOffenseWorldMapStateView State { get; }
    IReadOnlyList<OffenseTargetDefinition> TargetDefinitions { get; }
    IReadOnlyList<OffenseTargetSnapshot> VisibleTargets { get; }
    float CurrentScanRange { get; }
    int CampaignTargetCount { get; }
    bool TryGetKnownTargetSnapshot(
        string targetId,
        out OffenseTargetSnapshot snapshot);
    bool TryGetTargetDefinition(
        string targetId,
        out OffenseTargetDefinition definition);
}

public interface IOffenseCampaignCommands
{
    bool TryOpenWorldMap();
    bool TryUpgradeRecon(out string message);
    bool TrySelectTarget(
        string targetId,
        out OffenseTargetSnapshot snapshot,
        out string message);
    bool TryRecordSuccessfulExpedition(
        string targetId,
        out OffenseTargetSnapshot completedTarget,
        out string message);
    bool TryRecordStrategicTruthReveal(
        string targetId,
        out string message);
}

public sealed class OffenseCampaignRestoreCandidate
{
    internal OffenseCampaignRestoreCandidate(OffenseWorldMapState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal OffenseWorldMapState State { get; }
}

/// <summary>
/// Canonical runtime authority for campaign reconnaissance, discovery, selection,
/// completion, and truth-reveal state. The world-map MonoBehaviour is only a
/// presentation/side-effect adapter over this aggregate module.
/// </summary>
public sealed class OffenseCampaignRuntime :
    IOffenseCampaignRuntime,
    IOffenseCampaignStateAuthority
{
    private OffenseWorldMapState state = new OffenseWorldMapState();
    private HashSet<string> targetIds = new HashSet<string>(StringComparer.Ordinal);
    private HashSet<string> truthTargetIds = new HashSet<string>(StringComparer.Ordinal);

    public IOffenseWorldMapStateView State => state;
    public OffenseWorldMapState MutableState => state;

    public void ConfigureTargets(IEnumerable<OffenseTargetDefinition> definitions)
    {
        List<OffenseTargetDefinition> targets = definitions?
            .Where(value => value != null && value.IsValid)
            .ToList()
            ?? throw new ArgumentNullException(nameof(definitions));
        if (targets.Count == 0)
        {
            throw new InvalidOperationException(
                "Offense campaign requires at least one valid target definition.");
        }

        targetIds = targets.Select(value => value.id)
            .ToHashSet(StringComparer.Ordinal);
        if (targetIds.Count != targets.Count)
        {
            throw new InvalidOperationException(
                "Offense campaign target definitions contain duplicate IDs.");
        }

        truthTargetIds = targets.Where(value => value.revealsTruth)
            .Select(value => value.id)
            .ToHashSet(StringComparer.Ordinal);
    }

    public void Reset(int reconLevel)
    {
        state.Reset(reconLevel);
    }

    public DungeonOffenseCampaignSaveData Capture()
    {
        RequireConfigured();
        return new DungeonOffenseCampaignSaveData
        {
            version = DungeonOffenseCampaignSaveData.CurrentVersion,
            reconLevel = state.ReconLevel,
            selectedTargetId = state.SelectedTargetId,
            knownTargetIds = state.KnownTargetIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            completedTargetIds = state.CompletedTargetIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList(),
            revealedTruthTargetId = state.RevealedTruthTargetId
        };
    }

    public OffenseCampaignRestoreCandidate BuildRestoreCandidate(
        DungeonOffenseCampaignSaveData source)
    {
        RequireConfigured();
        if (source == null
            || source.version != DungeonOffenseCampaignSaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported offense campaign payload version {source?.version.ToString() ?? "null"}; expected {DungeonOffenseCampaignSaveData.CurrentVersion}.");
        }
        if (source.reconLevel < 0
            || source.knownTargetIds == null
            || source.completedTargetIds == null
            || source.knownTargetIds.Any(value => !targetIds.Contains(value))
            || source.completedTargetIds.Any(value => !targetIds.Contains(value))
            || (!string.IsNullOrWhiteSpace(source.revealedTruthTargetId)
                && !truthTargetIds.Contains(source.revealedTruthTargetId)))
        {
            throw new InvalidOperationException(
                "Offense campaign restore references invalid or non-canonical target state.");
        }

        OffenseWorldMapState candidate = new OffenseWorldMapState();
        candidate.Restore(
            source.reconLevel,
            source.selectedTargetId,
            source.knownTargetIds,
            source.completedTargetIds,
            source.revealedTruthTargetId);
        if (!string.Equals(candidate.SelectedTargetId,
                source.selectedTargetId,
                StringComparison.Ordinal)
            || !string.Equals(candidate.RevealedTruthTargetId,
                source.revealedTruthTargetId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Offense campaign selection or revealed truth state is non-canonical.");
        }

        return new OffenseCampaignRestoreCandidate(candidate);
    }

    public void PublishRestoreCandidate(OffenseCampaignRestoreCandidate candidate)
    {
        state = (candidate ?? throw new ArgumentNullException(nameof(candidate))).State;
    }

    private void RequireConfigured()
    {
        if (targetIds.Count == 0)
        {
            throw new InvalidOperationException(
                "Offense campaign target definitions have not been configured.");
        }
    }
}
