using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public enum RepairWorkOutcomeKind
{
    GeneralDamage = 0,
    StructuralIntegrity = 1,
    DefenseFacility = 2,
    EquipmentDurability = 3,
    AutomationMaintenance = 4
}

public enum RepairWorkPrimaryUnit
{
    Flag = 0,
    Point = 1,
    Percent = 2,
    Ratio = 3
}

[Serializable]
public sealed class RepairWorkMaterialSourceSaveData
{
    public string stackId = string.Empty;
    public string itemId = string.Empty;
    public int quantity;

    public RepairWorkMaterialSourceSaveData Clone() => new()
    {
        stackId = stackId?.Trim() ?? string.Empty,
        itemId = itemId?.Trim() ?? string.Empty,
        quantity = quantity
    };
}

[Serializable]
public sealed class RepairWorkOutcomeSaveData
{
    public string operationId = string.Empty;
    public RepairWorkOutcomeKind kind;
    public string facilityId = string.Empty;
    public string targetEntityId = string.Empty;
    public string targetDefinitionId = string.Empty;
    public string initiatorCharacterId = string.Empty;
    public int startedAbsoluteDay;
    public int completedAbsoluteDay;
    public bool boundsKnown;
    public RepairWorkPrimaryUnit primaryUnit;
    public float primaryBefore;
    public float primaryAfter;
    public float secondaryBefore;
    public float secondaryAfter;
    public bool terminal;
    public string completionWorkerId = string.Empty;
    public CharacterCommandOrigin completionOrigin;
    public float completionContributionWork;
    public string publicationKey = string.Empty;
    public bool publicationConfirmed;
    public string completionPublicationKey = string.Empty;
    public List<CraftContributionSaveData> contributions = new();
    public string materialOperationId = string.Empty;
    public string materialCommitId = string.Empty;
    public long materialMassGrams;
    public List<RepairWorkMaterialSourceSaveData> materialSources = new();

    public RepairWorkOutcomeSaveData Clone() => new()
    {
        operationId = operationId?.Trim() ?? string.Empty,
        kind = kind,
        facilityId = facilityId?.Trim() ?? string.Empty,
        targetEntityId = targetEntityId?.Trim() ?? string.Empty,
        targetDefinitionId = targetDefinitionId?.Trim() ?? string.Empty,
        initiatorCharacterId = initiatorCharacterId?.Trim() ?? string.Empty,
        startedAbsoluteDay = startedAbsoluteDay,
        completedAbsoluteDay = completedAbsoluteDay,
        boundsKnown = boundsKnown,
        primaryUnit = primaryUnit,
        primaryBefore = primaryBefore,
        primaryAfter = primaryAfter,
        secondaryBefore = secondaryBefore,
        secondaryAfter = secondaryAfter,
        terminal = terminal,
        completionWorkerId = completionWorkerId,
        completionOrigin = completionOrigin,
        completionContributionWork = completionContributionWork,
        publicationKey = publicationKey,
        publicationConfirmed = publicationConfirmed,
        completionPublicationKey = completionPublicationKey,
        contributions = (contributions ?? new List<CraftContributionSaveData>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .ToList(),
        materialOperationId = materialOperationId?.Trim() ?? string.Empty,
        materialCommitId = materialCommitId?.Trim() ?? string.Empty,
        materialMassGrams = materialMassGrams,
        materialSources = (materialSources
                ?? new List<RepairWorkMaterialSourceSaveData>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToList()
    };
}

public readonly struct RepairWorkOutcomeHandle
{
    public RepairWorkOutcomeHandle(string operationId)
    {
        OperationId = operationId?.Trim() ?? string.Empty;
    }

    public string OperationId { get; }
    public bool IsValid => DomainFactValueSyntax.IsIdentity(OperationId);
}

public readonly struct RepairWorkMaterialReceipt
{
    public RepairWorkMaterialReceipt(
        string operationId,
        string commitId,
        long massGrams,
        IReadOnlyList<RepairWorkMaterialSourceSaveData> sources)
    {
        OperationId = operationId?.Trim() ?? string.Empty;
        CommitId = commitId?.Trim() ?? string.Empty;
        MassGrams = massGrams;
        Sources = (sources ?? Array.Empty<RepairWorkMaterialSourceSaveData>())
            .Where(value => value != null)
            .Select(value => value.Clone())
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .ToArray();
    }

    public string OperationId { get; }
    public string CommitId { get; }
    public long MassGrams { get; }
    public IReadOnlyList<RepairWorkMaterialSourceSaveData> Sources { get; }
    public bool HasMaterial => OperationId.Length > 0;
    public static RepairWorkMaterialReceipt None => new(
        string.Empty,
        string.Empty,
        0L,
        Array.Empty<RepairWorkMaterialSourceSaveData>());
}

public interface IRepairWorkOutcomeRuntime
{
    RepairWorkOutcomeHandle BeginOrResume(
        RepairWorkOutcomeKind kind,
        string facilityId,
        string targetEntityId,
        string targetDefinitionId,
        CharacterId initiator,
        int absoluteDay,
        bool boundsKnown,
        RepairWorkPrimaryUnit primaryUnit,
        float primaryBefore,
        float secondaryBefore);

    void RecordProgress(
        RepairWorkOutcomeHandle handle,
        CharacterId contributor,
        float contributedWork,
        bool boundsKnown,
        float primaryAfter,
        float secondaryAfter);

    bool TryComplete(
        RepairWorkOutcomeHandle handle,
        CharacterId completionWorker,
        CharacterCommandOrigin completionOrigin,
        float contributionWork,
        int absoluteDay,
        bool boundsKnown,
        RepairWorkPrimaryUnit primaryUnit,
        float primaryBefore,
        float primaryAfter,
        float secondaryBefore,
        float secondaryAfter,
        in RepairWorkMaterialReceipt material,
        out string resultKey,
        out string failureReason);
}


internal static class RepairWorkOutcomeValidation
{
    internal const int MaxPending = 4096;

    internal static bool IsValid(
        RepairWorkOutcomeSaveData value,
        bool requireTerminal,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (value == null
            || !DomainFactValueSyntax.IsIdentity(value.operationId)
            || !Enum.IsDefined(typeof(RepairWorkOutcomeKind), value.kind)
            || !DomainFactValueSyntax.IsIdentity(value.facilityId)
            || !DomainFactValueSyntax.IsIdentity(value.targetEntityId)
            || (!string.IsNullOrEmpty(value.targetDefinitionId)
                && !DomainFactValueSyntax.IsIdentity(
                    value.targetDefinitionId))
            || !DomainFactValueSyntax.IsIdentity(
                value.initiatorCharacterId)
            || value.startedAbsoluteDay < 1
            || value.completedAbsoluteDay < 0
            || !Enum.IsDefined(
                typeof(RepairWorkPrimaryUnit), value.primaryUnit)
            || !FiniteNonNegative(value.primaryBefore)
            || !FiniteNonNegative(value.primaryAfter)
            || !FiniteNonNegative(value.secondaryBefore)
            || !FiniteNonNegative(value.secondaryAfter)
            || (value.terminal && !value.boundsKnown)
            || (requireTerminal && !value.terminal)
            || (value.terminal
                && value.completedAbsoluteDay < value.startedAbsoluteDay)
            || (!value.terminal && value.completedAbsoluteDay != 0)
            || !ValidContributors(value.contributions)
            || !ValidCompletion(value)
            || !ValidMaterial(value))
        {
            failureReason = "repair-work-outcome-owner-invalid";
            return false;
        }
        return true;
    }

    private static bool ValidCompletion(RepairWorkOutcomeSaveData value)
    {
        if (!value.terminal)
            return string.IsNullOrEmpty(value.completionWorkerId)
                && value.completionOrigin == default && value.completionContributionWork == 0f
                && string.IsNullOrEmpty(value.publicationKey) && !value.publicationConfirmed
                && string.IsNullOrEmpty(value.completionPublicationKey);
        if (!DomainFactValueSyntax.IsIdentity(value.completionWorkerId)
            || !Enum.IsDefined(typeof(CharacterCommandOrigin), value.completionOrigin)
            || !FiniteNonNegative(value.completionContributionWork)) return false;
        float approved = value.contributions?.FirstOrDefault(row =>
            row.characterId == value.completionWorkerId)?.contributedWork ?? 0f;
        return value.completionContributionWork <= approved + 0.0001f
            && DomainFactValueSyntax.IsText(value.publicationKey, 1024, true)
            && DomainFactValueSyntax.IsText(value.completionPublicationKey, 1024, true)
            && (!value.publicationConfirmed || !string.IsNullOrWhiteSpace(value.publicationKey))
            && (string.IsNullOrEmpty(value.completionPublicationKey) || value.publicationConfirmed);
    }

    internal static bool TryParseSequence(string operationId, out int sequence)
    {
        const string prefix = "repair-work:";
        sequence = 0;
        return operationId != null
            && operationId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(
                operationId.Substring(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out sequence)
            && sequence > 0
            && string.Equals(operationId,
                prefix + sequence.ToString("D8", CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }

    private static bool ValidContributors(
        IReadOnlyList<CraftContributionSaveData> values)
    {
        if (values == null)
            return false;
        string previous = string.Empty;
        foreach (CraftContributionSaveData value in values)
        {
            if (value == null
                || !DomainFactValueSyntax.IsIdentity(value.characterId)
                || !float.IsFinite(value.contributedWork)
                || value.contributedWork <= 0f
                || !float.IsFinite(value.relevantSkill)
                || value.relevantSkill < 0f
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, value.characterId) >= 0))
            {
                return false;
            }
            previous = value.characterId;
        }
        return true;
    }

    private static bool ValidMaterial(RepairWorkOutcomeSaveData value)
    {
        IReadOnlyList<RepairWorkMaterialSourceSaveData> sources =
            value.materialSources;
        if (sources == null)
            return false;
        bool hasOperation = !string.IsNullOrEmpty(value.materialOperationId);
        if (!hasOperation)
        {
            return string.IsNullOrEmpty(value.materialCommitId)
                && value.materialMassGrams == 0L
                && sources.Count == 0;
        }
        if (value.kind != RepairWorkOutcomeKind.EquipmentDurability
            || !value.terminal
            || !DomainFactValueSyntax.IsIdentity(
                value.materialOperationId)
            || string.IsNullOrWhiteSpace(value.materialCommitId)
            || !string.Equals(value.materialCommitId,
                value.materialCommitId.Trim(), StringComparison.Ordinal)
            || value.materialMassGrams <= 0L
            || sources.Count == 0)
        {
            return false;
        }
        string previous = string.Empty;
        foreach (RepairWorkMaterialSourceSaveData source in sources)
        {
            if (source == null
                || !DomainFactValueSyntax.IsIdentity(source.stackId)
                || !DomainFactValueSyntax.IsIdentity(source.itemId)
                || source.quantity <= 0
                || (previous.Length > 0
                    && string.CompareOrdinal(previous, source.stackId) >= 0))
            {
                return false;
            }
            previous = source.stackId;
        }
        return true;
    }

    private static bool FiniteNonNegative(float value) =>
        float.IsFinite(value) && value >= 0f;
}

public sealed partial class WorkOrderRuntime
{
    private IDomainFactPublisher repairFacts;

    private static string RequireRepairIdentity(string value, string parameter)
    {
        if (!DomainFactValueSyntax.IsIdentity(value))
            throw new ArgumentException("A stable repair identity is required.", parameter);
        return value;
    }

    public RepairWorkOutcomeHandle BeginOrResume(
        RepairWorkOutcomeKind kind,
        string facilityId,
        string targetEntityId,
        string targetDefinitionId,
        CharacterId initiator,
        int absoluteDay,
        bool boundsKnown,
        RepairWorkPrimaryUnit primaryUnit,
        float primaryBefore,
        float secondaryBefore)
    {
        string facility = RequireRepairIdentity(
            facilityId, nameof(facilityId));
        string target = RequireRepairIdentity(
            targetEntityId, nameof(targetEntityId));
        string definition = targetDefinitionId?.Trim() ?? string.Empty;
        if (!Enum.IsDefined(typeof(RepairWorkOutcomeKind), kind)
            || !initiator.IsValid
            || absoluteDay < 1
            || (definition.Length > 0
                && !DomainFactValueSyntax.IsIdentity(definition))
            || !Enum.IsDefined(typeof(RepairWorkPrimaryUnit), primaryUnit)
            || !float.IsFinite(primaryBefore)
            || primaryBefore < 0f
            || !float.IsFinite(secondaryBefore)
            || secondaryBefore < 0f)
        {
            throw new ArgumentException("Repair work owner is invalid.");
        }

        WorkOrderAggregateState state = WritableState;
        RepairWorkOutcomeSaveData existing = state.RepairOutcomesById.Values
            .SingleOrDefault(value => value.kind == kind
                && string.Equals(value.facilityId, facility,
                    StringComparison.Ordinal)
                && string.Equals(value.targetEntityId, target,
                    StringComparison.Ordinal));
        if (existing != null)
        {
            if (existing.terminal)
                throw new InvalidOperationException(
                    "A terminal repair outcome is awaiting publication.");
            if (boundsKnown && existing.boundsKnown
                && (!Approximately(existing.primaryAfter, primaryBefore)
                    || !Approximately(existing.secondaryAfter,
                        secondaryBefore)))
            {
                throw new InvalidOperationException(
                    "Repair work resume state conflicts with the domain state.");
            }
            return new RepairWorkOutcomeHandle(existing.operationId);
        }

        int sequence = Math.Max(1, state.NextRepairOutcomeSequence);
        state.NextRepairOutcomeSequence = checked(sequence + 1);
        string operationId = "repair-work:"
            + sequence.ToString("D8", CultureInfo.InvariantCulture);
        RepairWorkOutcomeSaveData created = new()
        {
            operationId = operationId,
            kind = kind,
            facilityId = facility,
            targetEntityId = target,
            targetDefinitionId = definition,
            initiatorCharacterId = initiator.Value,
            startedAbsoluteDay = absoluteDay,
            boundsKnown = boundsKnown,
            primaryUnit = primaryUnit,
            primaryBefore = primaryBefore,
            primaryAfter = primaryBefore,
            secondaryBefore = secondaryBefore,
            secondaryAfter = secondaryBefore
        };
        state.RepairOutcomesById.Add(operationId, created);
        unchecked { state.CandidateVersion++; }
        return new RepairWorkOutcomeHandle(operationId);
    }

    public void RecordProgress(
        RepairWorkOutcomeHandle handle,
        CharacterId contributor,
        float contributedWork,
        bool boundsKnown,
        float primaryAfter,
        float secondaryAfter)
    {
        if (!handle.IsValid
            || !contributor.IsValid
            || !float.IsFinite(contributedWork)
            || contributedWork < 0f
            || !float.IsFinite(primaryAfter)
            || primaryAfter < 0f
            || !float.IsFinite(secondaryAfter)
            || secondaryAfter < 0f)
        {
            throw new ArgumentException("Repair progress is invalid.");
        }
        WorkOrderAggregateState state = WritableState;
        if (!state.RepairOutcomesById.TryGetValue(
                handle.OperationId,
                out RepairWorkOutcomeSaveData owner)
            || owner.terminal)
        {
            throw new InvalidOperationException(
                "Repair outcome owner is missing or terminal.");
        }
        bool changed = boundsKnown
            && (!owner.boundsKnown
                || !Approximately(owner.primaryAfter, primaryAfter)
                || !Approximately(owner.secondaryAfter, secondaryAfter));
        if (contributedWork <= 0f && !changed)
            return;

        if (boundsKnown)
        {
            if (!owner.boundsKnown)
            {
                owner.boundsKnown = true;
                owner.primaryBefore = primaryAfter;
                owner.secondaryBefore = secondaryAfter;
            }
            owner.primaryAfter = primaryAfter;
            owner.secondaryAfter = secondaryAfter;
        }
        if (contributedWork > 0f)
        {
            CraftContributionSaveData contribution = owner.contributions
                .FirstOrDefault(value => string.Equals(
                    value.characterId,
                    contributor.Value,
                    StringComparison.Ordinal));
            if (contribution == null)
            {
                contribution = new CraftContributionSaveData
                {
                    characterId = contributor.Value
                };
                owner.contributions.Add(contribution);
            }
            contribution.contributedWork += contributedWork;
            owner.contributions = owner.contributions
                .OrderBy(value => value.characterId, StringComparer.Ordinal)
                .ToList();
        }
        unchecked { state.CandidateVersion++; }
    }

    public bool TryComplete(
        RepairWorkOutcomeHandle handle,
        CharacterId completionWorker,
        CharacterCommandOrigin completionOrigin,
        float contributionWork,
        int absoluteDay,
        bool boundsKnown,
        RepairWorkPrimaryUnit primaryUnit,
        float primaryBefore,
        float primaryAfter,
        float secondaryBefore,
        float secondaryAfter,
        in RepairWorkMaterialReceipt material,
        out string resultKey,
        out string failureReason)
    {
        resultKey = string.Empty;
        failureReason = string.Empty;
        WorkOrderAggregateState state = WritableState;
        if (!handle.IsValid
            || absoluteDay < 1
            || !state.RepairOutcomesById.TryGetValue(
                handle.OperationId,
                out RepairWorkOutcomeSaveData owner))
        {
            failureReason = "repair-work-outcome-owner-missing";
            return false;
        }
        if (!owner.terminal)
        {
            float approved = owner.contributions.FirstOrDefault(row =>
                row.characterId == completionWorker.Value)?.contributedWork ?? 0f;
            if (!completionWorker.IsValid
                || !Enum.IsDefined(typeof(CharacterCommandOrigin), completionOrigin)
                || !FiniteNonNegative(contributionWork) || contributionWork > approved + 0.0001f
                || !boundsKnown
                || !Enum.IsDefined(typeof(RepairWorkPrimaryUnit), primaryUnit)
                || !FiniteNonNegative(primaryBefore)
                || !FiniteNonNegative(primaryAfter)
                || !FiniteNonNegative(secondaryBefore)
                || !FiniteNonNegative(secondaryAfter))
            {
                failureReason = "repair-work-outcome-terminal-bounds-invalid";
                return false;
            }
            if (owner.boundsKnown
                && (!Approximately(owner.primaryBefore, primaryBefore)
                    || !Approximately(owner.secondaryBefore,
                        secondaryBefore)))
            {
                failureReason = "repair-work-outcome-initial-bounds-conflict";
                return false;
            }
            RepairWorkOutcomeSaveData candidate = owner.Clone();
            candidate.boundsKnown = true;
            candidate.primaryUnit = primaryUnit;
            candidate.primaryBefore = primaryBefore;
            candidate.primaryAfter = primaryAfter;
            candidate.secondaryBefore = secondaryBefore;
            candidate.secondaryAfter = secondaryAfter;
            candidate.completedAbsoluteDay = absoluteDay;
            candidate.completionWorkerId = completionWorker.Value;
            candidate.completionOrigin = completionOrigin;
            candidate.completionContributionWork = contributionWork;
            candidate.terminal = true;
            if (material.HasMaterial)
            {
                candidate.materialOperationId = material.OperationId;
                candidate.materialCommitId = material.CommitId;
                candidate.materialMassGrams = material.MassGrams;
                candidate.materialSources = material.Sources
                    .Select(value => value.Clone())
                    .OrderBy(value => value.stackId, StringComparer.Ordinal)
                    .ToList();
            }
            if (!RepairWorkOutcomeValidation.IsValid(candidate, true, out failureReason))
            {
                LastRepairOutcomeFailureForDebug = failureReason;
                return false;
            }
            state.RepairOutcomesById[handle.OperationId] = candidate;
            owner = candidate;
            unchecked { state.CandidateVersion++; }
        }
        else if (owner.completionWorkerId != completionWorker.Value
            || owner.completionOrigin != completionOrigin
            || owner.completionContributionWork != contributionWork
            || owner.completedAbsoluteDay != absoluteDay)
        {
            failureReason = "repair-work-completion-retry-conflict";
            LastRepairOutcomeFailureForDebug = failureReason;
            return false;
        }
        return TryPublishRepairCompletion(state, owner, out resultKey, out failureReason);
    }

    internal string LastRepairOutcomeFailureForDebug { get; private set; } =
        string.Empty;

    private void ContinuePendingRepairOutcomes()
    {
        WorkOrderAggregateState state = WritableState;
        foreach (RepairWorkOutcomeSaveData pending in state.RepairOutcomesById
                     .Values
                     .Where(value => value.terminal)
                     .OrderBy(value => value.operationId, StringComparer.Ordinal)
                     .ToArray())
        {
            if (!TryPublishRepairCompletion(state, pending, out _, out _))
            {
                return;
            }
        }
    }

    private bool TryPublishRepairCompletion(WorkOrderAggregateState state,
        RepairWorkOutcomeSaveData owner, out string resultKey, out string failureReason)
    {
        resultKey = string.Empty;
        failureReason = string.Empty;
        if (!owner.publicationConfirmed)
        {
            DomainFactDelivery child = PublishRepairFact(state, ref owner.publicationKey,
                new RepairWorkCompletedFact(owner));
            bool confirmed = child.Phase is DomainFactDeliveryPhase.Delivered
                    or DomainFactDeliveryPhase.AcceptedPendingDelivery;
            if (!confirmed || child.OperationKey != owner.publicationKey
                || string.IsNullOrEmpty(child.PublicationId) || child.Fingerprint.Length != 64)
            {
                failureReason = "repair-work-publication-unconfirmed:" + child.Detail;
                LastRepairOutcomeFailureForDebug = failureReason;
                return false;
            }
            owner.publicationConfirmed = true;
            unchecked { state.CandidateVersion++; }
        }
        resultKey = owner.publicationKey;
        // Only frozen facts are retried. Neither physical repair nor worker
        // effects are applied by this owner during delivery or recovery.
        DomainFactDelivery completion = PublishRepairFact(state, ref owner.completionPublicationKey,
            new RepairWorkCompletionFact(owner));
        if (completion.OperationKey == owner.completionPublicationKey && completion.Completed)
        {
            state.RepairOutcomesById.Remove(owner.operationId);
            unchecked { state.CandidateVersion++; }
            LastRepairOutcomeFailureForDebug = string.Empty;
            return true;
        }
        failureReason = "repair-completion-unconfirmed:" + completion.Detail;
        LastRepairOutcomeFailureForDebug = failureReason;
        return completion.Phase == DomainFactDeliveryPhase.AcceptedPendingDelivery
            && completion.OperationKey == owner.completionPublicationKey;
    }

    private DomainFactDelivery PublishRepairFact<TFact>(WorkOrderAggregateState state,
        ref string publicationKey, TFact fact)
    {
        IDomainFactPublisher publisher = repairFacts ??= ResolveOptional<IDomainFactPublisher>();
        if (publisher == null || publisher is not IDomainFactRecoveryPublisher recovery)
            return DomainFactDelivery.Reject("repair-work-fact-publication-unavailable");
        try
        {
            if (string.IsNullOrEmpty(publicationKey))
            {
                if (!publisher.TryReserve(fact, out DomainFactLease lease, out string failure))
                    return DomainFactDelivery.Reject(failure);
                string reservedKey = lease.PublicationKey;
                if (!DomainFactValueSyntax.IsText(reservedKey, 1024, false))
                {
                    publisher.Cancel(lease);
                    return DomainFactDelivery.Reject("repair-work-publication-key-missing");
                }
                publicationKey = reservedKey;
                unchecked { state.CandidateVersion++; }
                DomainFactDelivery delivered = publisher.Publish(lease, fact);
                if (delivered.Phase == DomainFactDeliveryPhase.Rejected)
                {
                    // A known rejection is the only case that permits a new
                    // reservation. Unknown acknowledgement retains this key.
                    publicationKey = string.Empty;
                    unchecked { state.CandidateVersion++; }
                }
                return delivered;
            }
            var results = new DomainFactDelivery[1];
            return recovery.ReplayBatch(new[] { DomainFactReplay.Create(publicationKey, fact) },
                results, out string replayFailure)
                ? results[0] : DomainFactDelivery.Reject(replayFailure);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            return DomainFactDelivery.Uncertain("repair-work-publication-unconfirmed:" + exception.Message);
        }
    }

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.0001f;

    private static bool FiniteNonNegative(float value) =>
        float.IsFinite(value) && value >= 0f;
}
