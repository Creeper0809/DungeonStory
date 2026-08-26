using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public class FacilityEvolutionHistoryEntry
{
    public string evolutionId;
    public string fromFacility;
    public string toFacility;
    public string summary;
    public int sequence;

    public FacilityEvolutionHistoryEntry Clone()
    {
        return new FacilityEvolutionHistoryEntry
        {
            evolutionId = evolutionId,
            fromFacility = fromFacility,
            toFacility = toFacility,
            summary = summary,
            sequence = sequence
        };
    }
}

public enum FacilityEvolutionMaterialCommitPhase
{
    None = 0,
    MaterialCommitted = 1,
    DomainApplied = 2
}

[Serializable]
public sealed class FacilityEvolutionPendingMaterialCommitSnapshot
{
    public string operationId;
    public string reasonCode;
    public string commitId;
    public string[] sourceStackIds = Array.Empty<string>();
    public int quantity;
    public long inputMassGrams;
    public string recipeId;
    public string sourceFacilityPersistentId;
    public string sourceFacilityDefinitionId;
    public string resultFacilityDefinitionId;
    public int historySequence;
    public FacilityEvolutionMaterialCommitPhase phase;
    public string[] resolvedMutationTags = Array.Empty<string>();
    public string resolvedResultPayload;

    public FacilityEvolutionPendingMaterialCommitSnapshot Clone()
    {
        return new FacilityEvolutionPendingMaterialCommitSnapshot
        {
            operationId = operationId,
            reasonCode = reasonCode,
            commitId = commitId,
            sourceStackIds = (sourceStackIds ?? Array.Empty<string>()).ToArray(),
            quantity = quantity,
            inputMassGrams = inputMassGrams,
            recipeId = recipeId,
            sourceFacilityPersistentId = sourceFacilityPersistentId,
            sourceFacilityDefinitionId = sourceFacilityDefinitionId,
            resultFacilityDefinitionId = resultFacilityDefinitionId,
            historySequence = historySequence,
            phase = phase,
            resolvedMutationTags = (resolvedMutationTags ?? Array.Empty<string>()).ToArray(),
            resolvedResultPayload = resolvedResultPayload
        };
    }

    public FacilityEvolutionStateSnapshot ReadResolvedResultState()
    {
        if (string.IsNullOrWhiteSpace(resolvedResultPayload))
        {
            throw new InvalidOperationException(
                "Facility evolution pending resolved-result payload is missing.");
        }

        FacilityEvolutionStateSnapshot resolved =
            JsonUtility.FromJson<FacilityEvolutionStateSnapshot>(
                resolvedResultPayload);
        if (resolved == null)
        {
            throw new InvalidOperationException(
                "Facility evolution pending resolved-result payload is invalid.");
        }
        return resolved;
    }
}

[Serializable]
public sealed class FacilityEvolutionStateSnapshot
{
    public string baseFacilityId;
    public string currentFacilityId;
    public int starGrade = 1;
    public string[] lineageTags = Array.Empty<string>();
    public string[] mutationTags = Array.Empty<string>();
    public string lastIdentitySummary;
    public FacilityEvolutionValue[] lastIdentityPressures = Array.Empty<FacilityEvolutionValue>();
    public string[] dominantIdentityTags = Array.Empty<string>();
    public List<FacilityEvolutionHistoryEntry> evolutionHistory = new List<FacilityEvolutionHistoryEntry>();
    public FacilityEvolutionState instanceEvolution = new FacilityEvolutionState();
    public bool hasRecordSnapshot;
    public FacilityEvolutionValue[] recordMetrics = Array.Empty<FacilityEvolutionValue>();
    public FacilityEvolutionTokenValue[] recordTokens = Array.Empty<FacilityEvolutionTokenValue>();
    public string[] recordRecentEvents = Array.Empty<string>();
    public FacilityEvolutionPendingMaterialCommitSnapshot pendingMaterialCommit;
}

public class FacilityEvolutionStateComponent : MonoBehaviour, IBuildingStateModule
{
    [SerializeField] private string baseFacilityId;
    [SerializeField] private string currentFacilityId;
    [SerializeField] private int starGrade = 1;
    [SerializeField] private string[] lineageTags = Array.Empty<string>();
    [SerializeField] private string[] mutationTags = Array.Empty<string>();
    [SerializeField] private string lastIdentitySummary;
    [SerializeField] private FacilityEvolutionValue[] lastIdentityPressures = Array.Empty<FacilityEvolutionValue>();
    [SerializeField] private string[] dominantIdentityTags = Array.Empty<string>();
    [SerializeField] private List<FacilityEvolutionHistoryEntry> evolutionHistory =
        new List<FacilityEvolutionHistoryEntry>();
    [SerializeField] private FacilityEvolutionState instanceEvolution =
        new FacilityEvolutionState();
    [SerializeField] private FacilityEvolutionValue[] recordMetrics =
        Array.Empty<FacilityEvolutionValue>();
    [SerializeField] private FacilityEvolutionTokenValue[] recordTokens =
        Array.Empty<FacilityEvolutionTokenValue>();
    [SerializeField] private string[] recordRecentEvents = Array.Empty<string>();
    [SerializeField] private FacilityEvolutionPendingMaterialCommitSnapshot pendingMaterialCommit;

    public string ModuleId => BuildingStateModuleIds.FacilityEvolution;
    public int CurrentVersion => 6;

    public string BaseFacilityId => baseFacilityId;
    public string CurrentFacilityId => currentFacilityId;
    public int StarGrade => Mathf.Max(1, starGrade);
    public IReadOnlyList<string> LineageTags => EventPayloadSnapshot.Copy(lineageTags);
    public IReadOnlyList<string> MutationTags => EventPayloadSnapshot.Copy(mutationTags);
    public string LastIdentitySummary => lastIdentitySummary ?? string.Empty;
    public IReadOnlyList<FacilityEvolutionValue> LastIdentityPressures =>
        EventPayloadSnapshot.Copy(lastIdentityPressures);
    public IReadOnlyList<string> DominantIdentityTags => EventPayloadSnapshot.Copy(dominantIdentityTags);
    public IReadOnlyList<FacilityEvolutionHistoryEntry> EvolutionHistory => Array.AsReadOnly(
        (evolutionHistory ?? new List<FacilityEvolutionHistoryEntry>())
            .Where((entry) => entry != null)
            .Select((entry) => entry.Clone())
            .ToArray());
    public FacilityEvolutionState InstanceEvolution =>
        (instanceEvolution ??= new FacilityEvolutionState()).Clone();
    public string FacilityPersistentId =>
        instanceEvolution?.facilityPersistentId ?? string.Empty;
    public bool HasPendingMaterialCommit =>
        pendingMaterialCommit != null
        && pendingMaterialCommit.phase != FacilityEvolutionMaterialCommitPhase.None;
    public FacilityEvolutionPendingMaterialCommitSnapshot PendingMaterialCommit =>
        pendingMaterialCommit?.Clone();

    public FacilityEvolutionStateSnapshot CreateSnapshot()
    {
        FacilityEvolutionRecord record = GetRecord();
        return new FacilityEvolutionStateSnapshot
        {
            baseFacilityId = baseFacilityId,
            currentFacilityId = currentFacilityId,
            starGrade = StarGrade,
            lineageTags = LineageTags.ToArray(),
            mutationTags = MutationTags.ToArray(),
            lastIdentitySummary = LastIdentitySummary,
            lastIdentityPressures = LastIdentityPressures.ToArray(),
            dominantIdentityTags = DominantIdentityTags.ToArray(),
            evolutionHistory = evolutionHistory
                .Where((entry) => entry != null)
                .Select((entry) => entry.Clone())
                .ToList(),
            instanceEvolution = instanceEvolution?.Clone() ??
                new FacilityEvolutionState(),
            hasRecordSnapshot = true,
            recordMetrics = record.Metrics
                .Select(entry => new FacilityEvolutionValue(entry.Key, entry.Value))
                .ToArray(),
            recordTokens = record.Tokens
                .Select(entry => new FacilityEvolutionTokenValue(entry.Key, entry.Value))
                .ToArray(),
            recordRecentEvents = record.RecentEvents.ToArray(),
            pendingMaterialCommit = pendingMaterialCommit?.Clone()
        };
    }

    public void ApplySnapshot(FacilityEvolutionStateSnapshot snapshot)
    {
        FacilityEvolutionPreparedState prepared =
            FacilityEvolutionAggregateAdapter.Prepare(snapshot);
        PublishPrepared(prepared);
    }

    private void PublishPrepared(FacilityEvolutionPreparedState prepared)
    {
        FacilityEvolutionStateSnapshot snapshot = prepared.SerializableSnapshot;

        baseFacilityId = snapshot.baseFacilityId ?? string.Empty;
        currentFacilityId = snapshot.currentFacilityId ?? string.Empty;
        starGrade = Mathf.Max(1, snapshot.starGrade);
        lineageTags = snapshot.lineageTags?
            .Where((tag) => !string.IsNullOrWhiteSpace(tag))
            .Distinct()
            .ToArray()
            ?? Array.Empty<string>();
        mutationTags = snapshot.mutationTags?
            .Where((tag) => !string.IsNullOrWhiteSpace(tag))
            .Distinct()
            .ToArray()
            ?? Array.Empty<string>();
        lastIdentitySummary = snapshot.lastIdentitySummary ?? string.Empty;
        lastIdentityPressures = snapshot.lastIdentityPressures?
            .Where((entry) => !string.IsNullOrWhiteSpace(entry.key))
            .ToArray()
            ?? Array.Empty<FacilityEvolutionValue>();
        dominantIdentityTags = snapshot.dominantIdentityTags?
            .Where((tag) => !string.IsNullOrWhiteSpace(tag))
            .Distinct()
            .ToArray()
            ?? Array.Empty<string>();
        evolutionHistory = snapshot.evolutionHistory?
            .Where((entry) => entry != null)
            .Select((entry) => entry.Clone())
            .ToList()
            ?? new List<FacilityEvolutionHistoryEntry>();
        instanceEvolution = snapshot.instanceEvolution?.Clone() ??
            new FacilityEvolutionState();

        recordMetrics = (snapshot.recordMetrics ?? Array.Empty<FacilityEvolutionValue>())
            .ToArray();
        recordTokens = (snapshot.recordTokens ?? Array.Empty<FacilityEvolutionTokenValue>())
            .ToArray();
        recordRecentEvents = (snapshot.recordRecentEvents ?? Array.Empty<string>())
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .TakeLast(12)
            .ToArray();
        pendingMaterialCommit = snapshot.pendingMaterialCommit?.Clone();
    }

    public FacilityEvolutionRecord GetRecord()
    {
        FacilityEvolutionRecord record = new FacilityEvolutionRecord();
        foreach (FacilityEvolutionValue metric in recordMetrics ?? Array.Empty<FacilityEvolutionValue>())
        {
            record.AddMetric(metric.key, metric.value);
        }
        foreach (FacilityEvolutionTokenValue token in recordTokens ?? Array.Empty<FacilityEvolutionTokenValue>())
        {
            record.AddToken(token.key, token.count);
        }
        foreach (string entry in recordRecentEvents ?? Array.Empty<string>())
        {
            record.AddEvent(entry);
        }
        return record;
    }

    public void ReplaceRecord(FacilityEvolutionRecord record)
    {
        record ??= new FacilityEvolutionRecord();
        recordMetrics = record.Metrics
            .Select(entry => new FacilityEvolutionValue(entry.Key, entry.Value))
            .ToArray();
        recordTokens = record.Tokens
            .Select(entry => new FacilityEvolutionTokenValue(entry.Key, entry.Value))
            .ToArray();
        recordRecentEvents = record.RecentEvents
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .TakeLast(12)
            .ToArray();
    }

    public void SetRecordMetric(string key, float value)
    {
        FacilityEvolutionRecord record = GetRecord();
        record.AddMetric(key, value);
        ReplaceRecord(record);
    }

    public void AddRecordToken(string key, int count)
    {
        FacilityEvolutionRecord record = GetRecord();
        record.AddToken(key, count);
        ReplaceRecord(record);
    }

    public void AddRecordRecentEvent(string text)
    {
        FacilityEvolutionRecord record = GetRecord();
        record.AddEvent(text);
        ReplaceRecord(record);
    }

    public void InitializeIfNeeded(BuildableObject facility)
    {
        if (facility == null)
        {
            return;
        }

        string facilityId = FacilityEvolutionUtility.GetFacilityId(facility.BuildingData);
        if (string.IsNullOrWhiteSpace(baseFacilityId))
        {
            baseFacilityId = facilityId;
        }

        if (string.IsNullOrWhiteSpace(currentFacilityId))
        {
            currentFacilityId = facilityId;
        }

        starGrade = Mathf.Max(StarGrade, FacilityShopService.GetBuildingStar(facility.BuildingData));
        if (lineageTags == null || lineageTags.Length == 0)
        {
            lineageTags = FacilityEvolutionUtility.GetDefaultLineageTags(facility.BuildingData).ToArray();
        }

        instanceEvolution ??= new FacilityEvolutionState();
        if (string.IsNullOrWhiteSpace(instanceEvolution.facilityPersistentId))
        {
            BuildingInstanceId persistentId = facility.PersistentInstanceId;
            if (!persistentId.IsValid)
            {
                throw new InvalidOperationException(
                    $"Facility '{facility.name}' has no persistent building ID.");
            }

            instanceEvolution.facilityPersistentId = persistentId.Value;
        }
        instanceEvolution.generation = Mathf.Max(0, instanceEvolution.generation);
        instanceEvolution.mastery = Mathf.Max(0f, instanceEvolution.mastery);
        instanceEvolution.usageLedger ??= new UsageLedger();
        instanceEvolution.evolutionNodes ??= new List<EvolutionNode>();
        foreach (EvolutionNode node in instanceEvolution.evolutionNodes
                     .Where(node => node != null && !node.historical))
        {
            node.playerVisible = true;
        }
        instanceEvolution.pendingCandidates ??= new List<FacilityGenerationCandidate>();
        instanceEvolution.activeNodeIds ??= new List<string>();
        instanceEvolution.dormantNodeIds ??= new List<string>();
        instanceEvolution.narrativeRequests ??=
            new List<EvolutionNarrativeRequestSnapshot>();
    }

    public void ReplaceInstanceEvolution(FacilityEvolutionState state)
    {
        instanceEvolution = state?.Clone() ?? new FacilityEvolutionState();
    }

    public void RecordPendingMaterialCommit(
        FacilityEvolutionMaterialCommitReceipt receipt,
        FacilityEvolutionRecipeSO recipe,
        string sourceFacilityDefinitionId,
        int historySequence,
        FacilityEvolutionStateSnapshot resolvedResultState,
        IReadOnlyList<string> resolvedMutationTags,
        FacilityEvolutionMaterialCommitPhase phase =
            FacilityEvolutionMaterialCommitPhase.MaterialCommitted)
    {
        if (!receipt.IsCommitted)
        {
            throw new InvalidOperationException(
                "Facility evolution pending material receipt is incomplete.");
        }
        if (recipe == null || recipe.resultBuilding == null)
        {
            throw new InvalidOperationException(
                "Facility evolution pending material receipt requires an exact recipe result.");
        }
        if (resolvedResultState == null)
        {
            throw new InvalidOperationException(
                "Facility evolution pending material receipt requires a resolved result state.");
        }

        pendingMaterialCommit = new FacilityEvolutionPendingMaterialCommitSnapshot
        {
            operationId = receipt.OperationId,
            reasonCode = receipt.ReasonCode,
            commitId = receipt.CommitId,
            sourceStackIds = receipt.SourceStackIds.ToArray(),
            quantity = receipt.Quantity,
            inputMassGrams = receipt.InputMassGrams,
            recipeId = recipe.EffectiveId,
            sourceFacilityPersistentId = FacilityPersistentId,
            sourceFacilityDefinitionId = sourceFacilityDefinitionId ?? string.Empty,
            resultFacilityDefinitionId =
                FacilityEvolutionUtility.GetFacilityId(recipe.resultBuilding),
            historySequence = historySequence,
            phase = phase,
            resolvedMutationTags = EventPayloadSnapshot.Copy(
                    resolvedMutationTags)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToArray(),
            resolvedResultPayload = JsonUtility.ToJson(CloneSnapshot(
                resolvedResultState,
                includePendingMaterialCommit: false))
        };

        FacilityEvolutionAggregateAdapter.ValidatePendingMaterialCommit(
            CreateSnapshot());
    }

    public void MarkPendingMaterialCommitDomainApplied()
    {
        if (!HasPendingMaterialCommit
            || pendingMaterialCommit.phase
                != FacilityEvolutionMaterialCommitPhase.MaterialCommitted)
        {
            throw new InvalidOperationException(
                "Facility evolution has no material-committed operation to advance.");
        }

        pendingMaterialCommit.phase = FacilityEvolutionMaterialCommitPhase.DomainApplied;
    }

    public void ClearPendingMaterialCommit(string expectedCommitId)
    {
        if (!HasPendingMaterialCommit)
        {
            return;
        }
        if (!string.Equals(
                pendingMaterialCommit.commitId,
                expectedCommitId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Facility evolution pending material commit identity changed before acknowledgement.");
        }

        pendingMaterialCommit = null;
    }

    public void AddMastery(float amount)
    {
        instanceEvolution ??= new FacilityEvolutionState();
        instanceEvolution.mastery = Mathf.Max(
            0f,
            instanceEvolution.mastery + Mathf.Max(0f, amount));
    }

    public string CaptureState()
    {
        return JsonUtility.ToJson(CreateSnapshot());
    }

    public bool TryRestoreState(int version, string payload, out string error)
    {
        error = string.Empty;
        if (version != CurrentVersion)
        {
            error = $"Unsupported facility evolution state version {version}.";
            return false;
        }

        try
        {
            FacilityEvolutionStateSnapshot snapshot =
                JsonUtility.FromJson<FacilityEvolutionStateSnapshot>(
                    payload ?? string.Empty);
            if (snapshot == null)
            {
                error = "Facility evolution state payload was empty.";
                return false;
            }

            FacilityEvolutionPreparedState prepared =
                FacilityEvolutionAggregateAdapter.Prepare(snapshot);
            PublishPrepared(prepared);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public void ApplyEvolution(
        BuildableObject fromFacility,
        BuildableObject toFacility,
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionProposal proposal,
        string fromFacilityName = null,
        RoomProfile profile = null,
        IReadOnlyList<string> resolvedMutationTags = null)
    {
        if (toFacility == null || recipe == null)
        {
            return;
        }

        InitializeIfNeeded(toFacility);
        ApplySnapshot(BuildResolvedEvolutionSnapshot(
            CreateSnapshot(),
            fromFacility != null ? fromFacility.BuildingData : null,
            toFacility.BuildingData,
            recipe,
            proposal,
            fromFacilityName,
            profile,
            resolvedMutationTags,
            GetRecord()));
    }

    public static FacilityEvolutionStateSnapshot BuildResolvedEvolutionSnapshot(
        FacilityEvolutionStateSnapshot source,
        BuildingSO sourceBuilding,
        BuildingSO resultBuilding,
        FacilityEvolutionRecipeSO recipe,
        FacilityEvolutionProposal proposal,
        string sourceFacilityName,
        RoomProfile profile,
        IReadOnlyList<string> resolvedMutationTags,
        FacilityEvolutionRecord resolvedRecord)
    {
        if (source == null || resultBuilding == null || recipe == null)
        {
            throw new InvalidOperationException(
                "Facility evolution cannot resolve a result from incomplete authority.");
        }

        FacilityEvolutionStateSnapshot result = CloneSnapshot(
            source,
            includePendingMaterialCommit: false);
        result.currentFacilityId = FacilityEvolutionUtility.GetFacilityId(resultBuilding);
        result.starGrade = Mathf.Max(1, recipe.resultStarGrade);
        result.lineageTags = (result.lineageTags ?? Array.Empty<string>())
            .Concat(FacilityEvolutionUtility.GetDefaultLineageTags(resultBuilding))
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();

        HashSet<string> nextMutation = new HashSet<string>(
            (result.mutationTags ?? Array.Empty<string>())
                .Where(tag => !string.IsNullOrWhiteSpace(tag)),
            StringComparer.Ordinal);
        IEnumerable<string> mutationCandidates = resolvedMutationTags
            ?? proposal.MutationTagSuggestions;
        foreach (string tag in mutationCandidates ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(tag)
                && recipe.allowedMutationTags != null
                && recipe.allowedMutationTags.Contains(tag))
            {
                nextMutation.Add(tag);
            }
        }
        result.mutationTags = nextMutation
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToArray();
        result.lastIdentitySummary = proposal.FacilityIdentitySummary ?? string.Empty;
        CaptureIdentitySnapshot(
            profile,
            out result.lastIdentityPressures,
            out result.dominantIdentityTags);
        result.evolutionHistory ??= new List<FacilityEvolutionHistoryEntry>();
        result.evolutionHistory.Add(new FacilityEvolutionHistoryEntry
        {
            evolutionId = recipe.EffectiveId,
            fromFacility = !string.IsNullOrWhiteSpace(sourceFacilityName)
                ? sourceFacilityName
                : FacilityShopService.GetBuildingName(sourceBuilding),
            toFacility = FacilityShopService.GetBuildingName(resultBuilding),
            summary = proposal.FlavorText,
            sequence = result.evolutionHistory.Count + 1
        });

        FacilityEvolutionRecord record = resolvedRecord ?? new FacilityEvolutionRecord();
        result.hasRecordSnapshot = true;
        result.recordMetrics = record.Metrics
            .Select(entry => new FacilityEvolutionValue(entry.Key, entry.Value))
            .ToArray();
        result.recordTokens = record.Tokens
            .Select(entry => new FacilityEvolutionTokenValue(entry.Key, entry.Value))
            .ToArray();
        result.recordRecentEvents = record.RecentEvents.ToArray();
        result.pendingMaterialCommit = null;
        return result;
    }

    internal static FacilityEvolutionStateSnapshot CloneSnapshot(
        FacilityEvolutionStateSnapshot source,
        bool includePendingMaterialCommit)
    {
        if (source == null)
        {
            return null;
        }

        return new FacilityEvolutionStateSnapshot
        {
            baseFacilityId = source.baseFacilityId,
            currentFacilityId = source.currentFacilityId,
            starGrade = source.starGrade,
            lineageTags = (source.lineageTags ?? Array.Empty<string>()).ToArray(),
            mutationTags = (source.mutationTags ?? Array.Empty<string>()).ToArray(),
            lastIdentitySummary = source.lastIdentitySummary,
            lastIdentityPressures = (source.lastIdentityPressures
                ?? Array.Empty<FacilityEvolutionValue>()).ToArray(),
            dominantIdentityTags = (source.dominantIdentityTags
                ?? Array.Empty<string>()).ToArray(),
            evolutionHistory = (source.evolutionHistory
                ?? new List<FacilityEvolutionHistoryEntry>())
                .Where(entry => entry != null)
                .Select(entry => entry.Clone())
                .ToList(),
            instanceEvolution = source.instanceEvolution?.Clone()
                ?? new FacilityEvolutionState(),
            hasRecordSnapshot = source.hasRecordSnapshot,
            recordMetrics = (source.recordMetrics
                ?? Array.Empty<FacilityEvolutionValue>()).ToArray(),
            recordTokens = (source.recordTokens
                ?? Array.Empty<FacilityEvolutionTokenValue>()).ToArray(),
            recordRecentEvents = (source.recordRecentEvents
                ?? Array.Empty<string>()).ToArray(),
            pendingMaterialCommit = includePendingMaterialCommit
                ? source.pendingMaterialCommit?.Clone()
                : null
        };
    }

    private void CaptureIdentity(RoomProfile profile)
    {
        if (profile == null || profile.IdentityPressures == null)
        {
            lastIdentityPressures = Array.Empty<FacilityEvolutionValue>();
            dominantIdentityTags = Array.Empty<string>();
            return;
        }

        lastIdentityPressures = profile.IdentityPressures
            .Where((entry) => entry.Value > 0.01f)
            .OrderByDescending((entry) => entry.Value)
            .Take(12)
            .Select((entry) => new FacilityEvolutionValue(entry.Key, entry.Value))
            .ToArray();
        dominantIdentityTags = lastIdentityPressures
            .Where((entry) => entry.value >= 0.35f)
            .Select((entry) => entry.key)
            .Take(6)
            .ToArray();
    }

    private static void CaptureIdentitySnapshot(
        RoomProfile profile,
        out FacilityEvolutionValue[] pressures,
        out string[] dominantTags)
    {
        if (profile == null || profile.IdentityPressures == null)
        {
            pressures = Array.Empty<FacilityEvolutionValue>();
            dominantTags = Array.Empty<string>();
            return;
        }

        pressures = profile.IdentityPressures
            .Where(entry => entry.Value > 0.01f)
            .OrderByDescending(entry => entry.Value)
            .ThenBy(entry => entry.Key, StringComparer.Ordinal)
            .Take(12)
            .Select(entry => new FacilityEvolutionValue(entry.Key, entry.Value))
            .ToArray();
        dominantTags = pressures
            .Where(entry => entry.value >= 0.35f)
            .Select(entry => entry.key)
            .Take(6)
            .ToArray();
    }
}
