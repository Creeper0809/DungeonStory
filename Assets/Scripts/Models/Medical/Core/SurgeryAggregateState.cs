using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public sealed class SurgeryAggregateState
{
    public const string OrderSequenceExhaustedReason =
        "order-sequence-exhausted";
    public const string PartSequenceExhaustedReason =
        "part-sequence-exhausted";
    private const string OrderSequenceInvalidReason =
        "order-sequence-invalid";
    private const string PartSequenceInvalidReason =
        "part-sequence-invalid";

    public readonly List<SurgeryOrder> Orders = new();
    public readonly List<SurgicalPartInstance> Parts = new();
    public readonly Dictionary<string, SurgicalOrganStorageState> OrganStorage =
        new(StringComparer.Ordinal);
    public readonly Dictionary<string, SurgicalCorpseFreshnessState> CorpseFreshness =
        new(StringComparer.Ordinal);
    public readonly Dictionary<string, bool> Policies =
        new(StringComparer.Ordinal);
    public readonly Dictionary<string, HashSet<string>> ExtractedNodesByCorpse =
        new(StringComparer.Ordinal);
    public readonly Dictionary<string, WildlifeAnatomyState> WildlifeAnatomy =
        new(StringComparer.Ordinal);
    public int OrderSequence;
    public int PartSequence;

    public bool TryPrepareNextOrderIdentity(
        out int nextSequence,
        out string orderId,
        out DomainFailure failure) =>
        TryPrepareNextIdentity(
            OrderSequence,
            "surgery:",
            OrderSequenceInvalidReason,
            OrderSequenceExhaustedReason,
            out nextSequence,
            out orderId,
            out failure);

    public bool TryPrepareNextPartIdentity(
        out int nextSequence,
        out string partInstanceId,
        out DomainFailure failure) =>
        TryPrepareNextIdentity(
            PartSequence,
            "surgical-part:",
            PartSequenceInvalidReason,
            PartSequenceExhaustedReason,
            out nextSequence,
            out partInstanceId,
            out failure);

    public SurgeryAggregateState Clone()
    {
        SurgeryAggregateState clone = new()
        {
            OrderSequence = OrderSequence,
            PartSequence = PartSequence
        };
        clone.Orders.AddRange(Orders.Select(SurgeryStateCloner.CloneOrder));
        clone.Parts.AddRange(Parts.Select(SurgeryStateCloner.ClonePart));
        foreach (KeyValuePair<string, SurgicalOrganStorageState> pair in OrganStorage)
        {
            clone.OrganStorage.Add(pair.Key, pair.Value.Clone());
        }
        foreach (KeyValuePair<string, SurgicalCorpseFreshnessState> pair in CorpseFreshness)
        {
            clone.CorpseFreshness.Add(pair.Key, pair.Value.Clone());
        }
        foreach (KeyValuePair<string, bool> pair in Policies)
        {
            clone.Policies.Add(pair.Key, pair.Value);
        }
        foreach (KeyValuePair<string, HashSet<string>> pair in ExtractedNodesByCorpse)
        {
            clone.ExtractedNodesByCorpse.Add(
                pair.Key,
                new HashSet<string>(pair.Value, StringComparer.Ordinal));
        }
        foreach (KeyValuePair<string, WildlifeAnatomyState> pair in WildlifeAnatomy)
        {
            clone.WildlifeAnatomy.Add(
                pair.Key,
                SurgeryStateCloner.CloneWildlifeAnatomy(pair.Value));
        }
        return clone;
    }

    private static bool TryPrepareNextIdentity(
        int currentSequence,
        string prefix,
        string invalidReason,
        string exhaustedReason,
        out int nextSequence,
        out string identity,
        out DomainFailure failure)
    {
        nextSequence = currentSequence;
        identity = string.Empty;
        failure = DomainFailure.None;
        if (currentSequence < 0)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryEffectFailed,
                invalidReason);
            return false;
        }
        if (currentSequence == int.MaxValue)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryEffectFailed,
                exhaustedReason);
            return false;
        }

        nextSequence = currentSequence + 1;
        identity = prefix
            + nextSequence.ToString(CultureInfo.InvariantCulture);
        return true;
    }
}

public interface ISurgeryOrderDemandQuery
{
    IReadOnlyList<SurgeryOrder> ActiveOrders { get; }
}

public sealed class SurgeryAggregateStateStore : ISurgeryOrderDemandQuery
{
    private readonly DungeonRuntimeAggregateRootStore rootStore;

    public SurgeryAggregateStateStore(DungeonRuntimeAggregateRootStore rootStore)
    {
        this.rootStore = rootStore
            ?? throw new ArgumentNullException(nameof(rootStore));
    }

    public SurgeryAggregateState State => rootStore.GetOrCreateWritable(
        () => new SurgeryAggregateState(),
        state => state.Clone());

    public IReadOnlyList<SurgeryOrder> ActiveOrders => State.Orders
        .Where(order => order != null && order.IsActive)
        .ToArray();

    public bool IsRestoreStaging => rootStore.IsRestoreStaging;

    public void Replace(SurgeryAggregateState state)
    {
        rootStore.Replace(
            state ?? throw new ArgumentNullException(nameof(state)));
    }
}

public static class SurgeryStateCloner
{
    public static SurgeryOrder CloneOrder(SurgeryOrder source)
    {
        if (source == null)
        {
            return null;
        }

        return new SurgeryOrder
        {
            orderId = source.orderId ?? string.Empty,
            procedureId = source.procedureId ?? string.Empty,
            subject = source.subject?.Clone() ?? new SurgicalSubjectRef(),
            targetNodeId = source.targetNodeId ?? string.Empty,
            selectedPartInstanceId = source.selectedPartInstanceId ?? string.Empty,
            preferredDoctorId = source.preferredDoctorId ?? string.Empty,
            doctorId = source.doctorId ?? string.Empty,
            facilityId = source.facilityId ?? string.Empty,
            materialDestinationId = source.materialDestinationId ?? string.Empty,
            materialBufferCapacityGrams = source.materialBufferCapacityGrams,
            materialMassAuthorityRevision = source.materialMassAuthorityRevision,
            materialCapacityFingerprint = source.materialCapacityFingerprint
                ?? string.Empty,
            materialSinkOperationId = source.materialSinkOperationId
                ?? string.Empty,
            materialSinkCommitId = source.materialSinkCommitId
                ?? string.Empty,
            materialSinkInputMassGrams = source.materialSinkInputMassGrams,
            materialSinkAcknowledged = source.materialSinkAcknowledged,
            materialTerminalDrainPhase = source.materialTerminalDrainPhase,
            materialTerminalTargetState = source.materialTerminalTargetState,
            materialTerminalParentOperationId =
                source.materialTerminalParentOperationId ?? string.Empty,
            materialTerminalStepOperationId =
                source.materialTerminalStepOperationId ?? string.Empty,
            materialTerminalRequestFingerprint =
                source.materialTerminalRequestFingerprint ?? string.Empty,
            materialTerminalCommitId =
                source.materialTerminalCommitId ?? string.Empty,
            materialTerminalReceiptFingerprint =
                source.materialTerminalReceiptFingerprint ?? string.Empty,
            materialTerminalInputQuantity = source.materialTerminalInputQuantity,
            materialTerminalInputMassGrams = source.materialTerminalInputMassGrams,
            materialTerminalOwnerX = source.materialTerminalOwnerX,
            materialTerminalOwnerY = source.materialTerminalOwnerY,
            state = source.state,
            requiredWork = source.requiredWork,
            completedWork = source.completedWork,
            anesthesiaWork = source.anesthesiaWork,
            incisionWork = source.incisionWork,
            procedureWork = source.procedureWork,
            sutureWork = source.sutureWork,
            materialsRequested = source.materialsRequested,
            materialsConsumed = source.materialsConsumed,
            processFluidConsumed = source.processFluidConsumed,
            anesthesiaConsumed = source.anesthesiaConsumed,
            incisionOpen = source.incisionOpen,
            resultRolled = source.resultRolled,
            patientAdmitted = source.patientAdmitted,
            admissionMoveRequested = source.admissionMoveRequested,
            subjectAiWasPaused = source.subjectAiWasPaused,
            patientTransporterId = source.patientTransporterId ?? string.Empty,
            patientTransportInProgress = source.patientTransportInProgress,
            patientReturnRequested = source.patientReturnRequested,
            patientOriginX = source.patientOriginX,
            patientOriginY = source.patientOriginY,
            admissionX = source.admissionX,
            admissionY = source.admissionY,
            nextAdmissionRetryAt = source.nextAdmissionRetryAt,
            failureSeverity = source.failureSeverity,
            risk = source.risk?.Clone() ?? new SurgeryRiskBreakdown(),
            reachedClinicalStages = (source.reachedClinicalStages
                ?? new List<SurgeryOrderState>()).ToList(),
            materials = (source.materials
                ?? new List<SurgicalMaterialRequirement>())
                .Where(requirement => requirement != null)
                .Select(requirement => requirement.Clone())
                .ToList(),
            statusData = source.statusData?.Clone() ?? new SurgeryStatusData(),
            createdAt = source.createdAt,
            recoveryUntil = source.recoveryUntil,
            environmentResumeStage = source.environmentResumeStage,
            environmentWait = source.environmentWait?.Clone()
                ?? new SurgeryStatusData(),
            environmentStableSeconds = source.environmentStableSeconds,
            environmentRecovery = source.environmentRecovery?.Clone()
                ?? new SurgeryStatusData()
        };
    }

    public static SurgicalPartInstance ClonePart(SurgicalPartInstance source)
    {
        if (source == null)
        {
            return null;
        }

        return new SurgicalPartInstance
        {
            partInstanceId = source.partInstanceId ?? string.Empty,
            kind = source.kind,
            nodeId = source.nodeId ?? string.Empty,
            displayName = source.displayName ?? string.Empty,
            donorId = source.donorId ?? string.Empty,
            donorName = source.donorName ?? string.Empty,
            donorSpeciesId = source.donorSpeciesId ?? string.Empty,
            anatomyFamily = source.anatomyFamily ?? string.Empty,
            quality = source.quality,
            freshnessSeconds = source.freshnessSeconds,
            contamination = source.contamination,
            specialEffectStrength = source.specialEffectStrength,
            specialEffectId = source.specialEffectId ?? string.Empty,
            worldStackId = source.worldStackId ?? string.Empty,
            storedFacilityId = source.storedFacilityId ?? string.Empty,
            reservedOrderId = source.reservedOrderId ?? string.Empty,
            preservationCanisterApplied = source.preservationCanisterApplied,
            preservationOperationId = source.preservationOperationId ?? string.Empty,
            preservationCommitId = source.preservationCommitId ?? string.Empty,
            preservationSourceStackId = source.preservationSourceStackId ?? string.Empty,
            preservationInputMassGrams = source.preservationInputMassGrams,
            preservationOutcomePublished = source.preservationOutcomePublished,
            installed = source.installed,
            installedSubjectId = source.installedSubjectId ?? string.Empty,
            sourceProductionCommitId = source.sourceProductionCommitId
                ?? string.Empty,
            installationOrderId = source.installationOrderId ?? string.Empty,
            installationOperationId = source.installationOperationId
                ?? string.Empty,
            installationCommitId = source.installationCommitId ?? string.Empty,
            installationSourceStackId = source.installationSourceStackId
                ?? string.Empty,
            installationSubjectId = source.installationSubjectId ?? string.Empty
        };
    }

    public static WildlifeAnatomyState CloneWildlifeAnatomy(
        WildlifeAnatomyState source)
    {
        if (source == null)
        {
            return null;
        }

        return new WildlifeAnatomyState
        {
            wildlifeId = source.wildlifeId ?? string.Empty,
            profileId = source.profileId ?? string.Empty,
            nodes = (source.nodes ?? new List<AnatomyNodeHealthState>())
                .Where(node => node != null)
                .Select(CloneAnatomyNode)
                .ToList()
        };
    }

    public static AnatomyNodeHealthState CloneAnatomyNode(
        AnatomyNodeHealthState source)
    {
        return new AnatomyNodeHealthState
        {
            nodeId = source.nodeId ?? string.Empty,
            maxHealth = source.maxHealth,
            currentHealth = source.currentHealth,
            bleedingPerSecond = source.bleedingPerSecond,
            infection = source.infection,
            missing = source.missing,
            installedPartId = source.installedPartId ?? string.Empty,
            installedPartKind = source.installedPartKind,
            installedPartEfficiency = source.installedPartEfficiency,
            rejectionBurden = source.rejectionBurden,
            mutationBurden = source.mutationBurden,
            moduleBonus = source.moduleBonus,
            recoveryPolicy = source.recoveryPolicy
        };
    }
}
