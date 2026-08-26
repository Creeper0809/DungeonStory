using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ProductionFacilityDestructiveDrainJournal :
    IProductionFacilityDestructiveDrainJournalQuery,
    IProductionFacilityDestructiveDrainJournalCommand,
    IProductionFacilityDestructiveDrainPersistence
{
    public static readonly string EmptyRegistryFingerprint =
        ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
            string.Empty);

    private readonly DungeonRuntimeAggregateRootStore roots;
    private readonly string registryFingerprint;
    private readonly IProductionFacilityDestructiveDrainParticipantRegistry
        registry;

    public ProductionFacilityDestructiveDrainJournal(
        DungeonRuntimeAggregateRootStore roots)
        : this(roots, EmptyRegistryFingerprint)
    {
    }

    public ProductionFacilityDestructiveDrainJournal(
        DungeonRuntimeAggregateRootStore roots,
        IProductionFacilityDestructiveDrainParticipantRegistry registry)
    {
        this.roots = roots ?? throw new ArgumentNullException(nameof(roots));
        this.registry = registry
            ?? throw new ArgumentNullException(nameof(registry));
        registryFingerprint = registry.RegistryFingerprint;
        if (!ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                registryFingerprint))
        {
            throw new ArgumentException(
                "The destructive-drain registry fingerprint is invalid.",
                nameof(registry));
        }
    }

    internal ProductionFacilityDestructiveDrainJournal(
        DungeonRuntimeAggregateRootStore roots,
        string registryFingerprint)
    {
        this.roots = roots ?? throw new ArgumentNullException(nameof(roots));
        registry = null;
        if (!ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                registryFingerprint))
        {
            throw new ArgumentException(
                "The destructive-drain registry fingerprint is invalid.",
                nameof(registryFingerprint));
        }
        this.registryFingerprint = registryFingerprint;
    }

    public int Version => State.Version;

    public IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>
        CaptureOpen() => State.Entries.Values
        .OrderBy(value => value.facilityId, StringComparer.Ordinal)
        .Select(value => value.Clone())
        .ToArray();

    public bool TryGet(
        ProductionFacilityDestructiveDrainOperationId operationId,
        out ProductionFacilityDestructiveDrainEntrySaveData entry)
    {
        entry = null;
        if (!operationId.IsValid
            || !State.Entries.TryGetValue(
                operationId.Value,
                out ProductionFacilityDestructiveDrainEntrySaveData found))
        {
            return false;
        }
        entry = found.Clone();
        return true;
    }

    public bool TryRequest(
        ProductionFacilityDestructiveDrainCause cause,
        BuildingInstanceId facilityId,
        string initiatingMutationOperationId,
        string preparedLifecycleFingerprint,
        IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
            participants,
        out ProductionFacilityDestructiveDrainEntrySaveData entry,
        out string failureReason)
    {
        entry = null;
        failureReason = string.Empty;
        if (!Enum.IsDefined(typeof(ProductionFacilityDestructiveDrainCause), cause)
            || cause == ProductionFacilityDestructiveDrainCause.None
            || !facilityId.IsValid
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                preparedLifecycleFingerprint))
        {
            failureReason = "production-facility-destructive-drain-request-invalid";
            return false;
        }
        string expectedMutation =
            ProductionFacilityDestructiveDrainCanonical
                .BuildInitiatingMutationOperationId(cause, facilityId);
        if (!string.Equals(
                initiatingMutationOperationId,
                expectedMutation,
                StringComparison.Ordinal))
        {
            failureReason =
                "production-facility-destructive-drain-mutation-id-mismatch";
            return false;
        }

        ProductionFacilityDestructiveDrainOperationId operationId =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(
                facilityId);
        List<ProductionFacilityDestructiveDrainParticipantSaveData>
            normalizedParticipants = CloneParticipants(participants);
        ValidateParticipants(operationId, normalizedParticipants);
        ProductionFacilityDestructiveDrainAggregateState writable = Writable;
        if (writable.Entries.TryGetValue(operationId.Value, out var existing))
        {
            if (string.Equals(
                    existing.initiatingMutationOperationId,
                    expectedMutation,
                    StringComparison.Ordinal)
                && existing.cause == cause
                && string.Equals(
                    existing.preparedLifecycleFingerprint,
                    preparedLifecycleFingerprint,
                    StringComparison.Ordinal)
                && ImmutableParticipantPlanEquals(
                    existing.participants,
                    normalizedParticipants))
            {
                entry = existing.Clone();
                return true;
            }
            failureReason =
                "production-facility-destructive-drain-operation-conflict";
            return false;
        }

        ProductionFacilityDestructiveDrainEntrySaveData created = new()
        {
            operationId = operationId.Value,
            initiatingMutationOperationId = expectedMutation,
            cause = cause,
            facilityId = facilityId.Value,
            destinationId = ProductionOutputDestinationId
                .FromFacility(facilityId).Value,
            phase = ProductionFacilityDestructiveDrainPhase.Prepared,
            preparedLifecycleFingerprint = preparedLifecycleFingerprint,
            expectedCurrentLifecycleFingerprint = preparedLifecycleFingerprint,
            revision = 1L,
            participants = normalizedParticipants
        };
        ValidateEntry(created);
        writable.Entries.Add(operationId.Value, created);
        writable.AdvanceVersion();
        entry = created.Clone();
        return true;
    }

    public bool TryAdvance(
        ProductionFacilityDestructiveDrainOperationId operationId,
        long expectedRevision,
        ProductionFacilityDestructiveDrainPhase nextPhase,
        string expectedCurrentLifecycleFingerprint,
        IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
            participants,
        out ProductionFacilityDestructiveDrainEntrySaveData entry,
        out string failureReason)
    {
        entry = null;
        failureReason = string.Empty;
        if (!operationId.IsValid
            || expectedRevision <= 0L
            || !Enum.IsDefined(
                typeof(ProductionFacilityDestructiveDrainPhase),
                nextPhase)
            || nextPhase == ProductionFacilityDestructiveDrainPhase.None
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                expectedCurrentLifecycleFingerprint))
        {
            failureReason =
                "production-facility-destructive-drain-advance-invalid";
            return false;
        }

        ProductionFacilityDestructiveDrainAggregateState writable = Writable;
        if (!writable.Entries.TryGetValue(operationId.Value, out var current))
        {
            failureReason =
                "production-facility-destructive-drain-operation-missing";
            return false;
        }
        if (current.revision != expectedRevision)
        {
            failureReason =
                "production-facility-destructive-drain-revision-stale";
            return false;
        }
        if ((int)nextPhase < (int)current.phase
            || (int)nextPhase > (int)current.phase + 1)
        {
            failureReason =
                "production-facility-destructive-drain-phase-transition-invalid";
            return false;
        }

        List<ProductionFacilityDestructiveDrainParticipantSaveData>
            normalizedParticipants = CloneParticipants(participants);
        ValidateParticipants(operationId, normalizedParticipants);
        if (!ImmutableParticipantPlanEquals(
                current.participants,
                normalizedParticipants)
            || !IsValidParticipantStateAdvance(
                current.participants,
                normalizedParticipants))
        {
            failureReason =
                "production-facility-destructive-drain-participant-transition-invalid";
            return false;
        }

        ProductionFacilityDestructiveDrainEntrySaveData replacement =
            current.Clone();
        replacement.phase = nextPhase;
        replacement.expectedCurrentLifecycleFingerprint =
            expectedCurrentLifecycleFingerprint;
        replacement.participants = normalizedParticipants;
        replacement.revision = checked(current.revision + 1L);
        ValidateEntry(replacement);
        writable.Entries[operationId.Value] = replacement;
        writable.AdvanceVersion();
        entry = replacement.Clone();
        return true;
    }

    public bool TryRemoveCheckpointed(
        ProductionFacilityDestructiveDrainOperationId operationId,
        long expectedRevision,
        out string failureReason)
    {
        failureReason = string.Empty;
        ProductionFacilityDestructiveDrainAggregateState writable = Writable;
        if (!operationId.IsValid
            || expectedRevision <= 0L
            || !writable.Entries.TryGetValue(operationId.Value, out var current)
            || current.revision != expectedRevision
            || current.phase != ProductionFacilityDestructiveDrainPhase
                .WorldRemovedAwaitingCheckpointGc)
        {
            failureReason =
                "production-facility-destructive-drain-checkpoint-gc-invalid";
            return false;
        }
        writable.Entries.Remove(operationId.Value);
        writable.AdvanceVersion();
        return true;
    }

    public DungeonProductionFacilityDestructiveDrainSaveData Capture() => new()
    {
        version = DungeonProductionFacilityDestructiveDrainSaveData
            .CurrentVersion,
        registryFingerprint = registryFingerprint,
        entries = CaptureOpen().Select(value => value.Clone()).ToList()
    };

    public ProductionFacilityDestructiveDrainRestoreCandidate BuildRestore(
        DungeonProductionFacilityDestructiveDrainSaveData payload)
    {
        ValidatePayload(payload);
        return new ProductionFacilityDestructiveDrainRestoreCandidate(
            ClonePayload(payload));
    }

    public void Restore(
        ProductionFacilityDestructiveDrainRestoreCandidate candidate)
    {
        if (candidate == null)
            throw new ArgumentNullException(nameof(candidate));
        ValidatePayload(candidate.Payload);
        roots.Replace(new ProductionFacilityDestructiveDrainAggregateState(
            candidate.Payload.entries));
    }

    private void ValidatePayload(
        DungeonProductionFacilityDestructiveDrainSaveData payload)
    {
        if (payload == null
            || payload.version != DungeonProductionFacilityDestructiveDrainSaveData
                .CurrentVersion
            || !string.Equals(
                payload.registryFingerprint,
                registryFingerprint,
                StringComparison.Ordinal)
            || payload.entries == null)
        {
            throw new InvalidOperationException(
                "Production destructive-drain payload header is invalid.");
        }

        string previousFacility = null;
        HashSet<string> operations = new(StringComparer.Ordinal);
        foreach (ProductionFacilityDestructiveDrainEntrySaveData entry in
                 payload.entries)
        {
            ValidateEntry(entry);
            if (previousFacility != null
                && string.CompareOrdinal(previousFacility, entry.facilityId) >= 0)
            {
                throw new InvalidOperationException(
                    "Production destructive-drain entries are not strictly sorted.");
            }
            if (!operations.Add(entry.operationId))
                throw new InvalidOperationException(
                    "Production destructive-drain operation is duplicated.");
            previousFacility = entry.facilityId;
        }
    }

    private void ValidateEntry(
        ProductionFacilityDestructiveDrainEntrySaveData entry)
    {
        if (entry == null
            || !ProductionFacilityDestructiveDrainOperationId.TryParse(
                entry.operationId,
                out var operationId)
            || !((BuildingInstanceId)entry.facilityId).IsValid
            || !string.Equals(
                operationId.Value,
                ProductionFacilityDestructiveDrainOperationId.FromFacility(
                    (BuildingInstanceId)entry.facilityId).Value,
                StringComparison.Ordinal)
            || !ProductionOutputDestinationId.TryParse(
                entry.destinationId,
                out _)
            || !string.Equals(
                entry.destinationId,
                ProductionOutputDestinationId.FromFacility(
                    (BuildingInstanceId)entry.facilityId).Value,
                StringComparison.Ordinal)
            || !Enum.IsDefined(
                typeof(ProductionFacilityDestructiveDrainCause),
                entry.cause)
            || entry.cause == ProductionFacilityDestructiveDrainCause.None
            || !string.Equals(
                entry.initiatingMutationOperationId,
                ProductionFacilityDestructiveDrainCanonical
                    .BuildInitiatingMutationOperationId(
                        entry.cause,
                        (BuildingInstanceId)entry.facilityId),
                StringComparison.Ordinal)
            || !Enum.IsDefined(
                typeof(ProductionFacilityDestructiveDrainPhase),
                entry.phase)
            || entry.phase == ProductionFacilityDestructiveDrainPhase.None
            || entry.revision <= 0L
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                entry.preparedLifecycleFingerprint)
            || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                entry.expectedCurrentLifecycleFingerprint)
            || entry.participants == null)
        {
            throw new InvalidOperationException(
                "Production destructive-drain entry is invalid.");
        }

        ValidateParticipants(operationId, entry.participants);
    }

    private void ValidateParticipants(
        ProductionFacilityDestructiveDrainOperationId operationId,
        IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
            participants)
    {
        if (participants == null)
            throw new InvalidOperationException(
                "Production destructive-drain participants are missing.");

        string previousParticipant = null;
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData participant
                 in participants)
        {
            if (participant == null
                || !ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                    participant.participantId)
                || participant.contractVersion <= 0
                || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                    participant.preparedContributionFingerprint)
                || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                    participant.expectedCurrentContributionFingerprint)
                || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                    participant.planFingerprint)
                || participant.owners == null
                || (previousParticipant != null
                    && string.CompareOrdinal(
                        previousParticipant,
                        participant.participantId) >= 0))
            {
                throw new InvalidOperationException(
                    "Production destructive-drain participant is invalid or unsorted.");
            }
            previousParticipant = participant.participantId;
            ValidateOwners(operationId, participant);
        }

        if (registry == null)
            return;

        if (participants.Count != registry.ExecutionOrder.Count)
            throw new InvalidOperationException(
                "Production destructive-drain entry does not contain the exact registry participant set.");
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData participant
                 in participants)
        {
            if (!registry.TryGet(participant.participantId, out var registered)
                || registered.ContractVersion != participant.contractVersion)
            {
                throw new InvalidOperationException(
                    "Production destructive-drain participant contract does not match the registry: "
                    + participant.participantId);
            }
        }
    }

    private static bool ImmutableParticipantPlanEquals(
        IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
            left,
        IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
            right)
    {
        if (left == null || right == null || left.Count != right.Count)
            return false;
        for (int participantIndex = 0;
             participantIndex < left.Count;
             participantIndex++)
        {
            ProductionFacilityDestructiveDrainParticipantSaveData a =
                left[participantIndex];
            ProductionFacilityDestructiveDrainParticipantSaveData b =
                right[participantIndex];
            if (a == null || b == null
                || !string.Equals(
                    a.participantId,
                    b.participantId,
                    StringComparison.Ordinal)
                || a.contractVersion != b.contractVersion
                || !string.Equals(
                    a.preparedContributionFingerprint,
                    b.preparedContributionFingerprint,
                    StringComparison.Ordinal)
                || !string.Equals(
                    a.planFingerprint,
                    b.planFingerprint,
                    StringComparison.Ordinal)
                || a.owners == null
                || b.owners == null
                || a.owners.Count != b.owners.Count)
            {
                return false;
            }

            for (int ownerIndex = 0;
                 ownerIndex < a.owners.Count;
                 ownerIndex++)
            {
                ProductionFacilityDestructiveDrainOwnerSaveData ownerA =
                    a.owners[ownerIndex];
                ProductionFacilityDestructiveDrainOwnerSaveData ownerB =
                    b.owners[ownerIndex];
                if (ownerA == null || ownerB == null
                    || !string.Equals(
                        ownerA.ownerStableId,
                        ownerB.ownerStableId,
                        StringComparison.Ordinal)
                    || ownerA.disposition != ownerB.disposition
                    || !string.Equals(
                        ownerA.targetDestinationId,
                        ownerB.targetDestinationId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ownerA.stepOperationId,
                        ownerB.stepOperationId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        ownerA.requestFingerprint,
                        ownerB.requestFingerprint,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static bool IsValidParticipantStateAdvance(
        IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
            current,
        IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
            next)
    {
        for (int participantIndex = 0;
             participantIndex < current.Count;
             participantIndex++)
        {
            ProductionFacilityDestructiveDrainParticipantSaveData a =
                current[participantIndex];
            ProductionFacilityDestructiveDrainParticipantSaveData b =
                next[participantIndex];
            for (int ownerIndex = 0;
                 ownerIndex < a.owners.Count;
                 ownerIndex++)
            {
                ProductionFacilityDestructiveDrainOwnerSaveData ownerA =
                    a.owners[ownerIndex];
                ProductionFacilityDestructiveDrainOwnerSaveData ownerB =
                    b.owners[ownerIndex];
                int delta = (int)ownerB.phase - (int)ownerA.phase;
                if (delta < 0 || delta > 1)
                    return false;

                if (ownerA.phase !=
                        ProductionFacilityDestructiveDrainStepPhase.Planned
                    && (!string.Equals(
                            ownerA.commitId,
                            ownerB.commitId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            ownerA.receiptFingerprint,
                            ownerB.receiptFingerprint,
                            StringComparison.Ordinal)))
                {
                    return false;
                }
                if (delta == 0
                    && (!string.Equals(
                            ownerA.commitId,
                            ownerB.commitId,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            ownerA.receiptFingerprint,
                            ownerB.receiptFingerprint,
                            StringComparison.Ordinal)))
                {
                    return false;
                }
            }
        }
        return true;
    }

    private static void ValidateOwners(
        ProductionFacilityDestructiveDrainOperationId operationId,
        ProductionFacilityDestructiveDrainParticipantSaveData participant)
    {
        string previousOwner = null;
        foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in
                 participant.owners)
        {
            bool transfer = owner?.disposition ==
                ProductionFacilityDestructiveDrainDisposition.Transfer;
            if (owner == null
                || !ProductionFacilityDestructiveDrainCanonical.IsCanonicalToken(
                    owner.ownerStableId)
                || !Enum.IsDefined(
                    typeof(ProductionFacilityDestructiveDrainDisposition),
                    owner.disposition)
                || !Enum.IsDefined(
                    typeof(ProductionFacilityDestructiveDrainStepPhase),
                    owner.phase)
                || transfer != ProductionFacilityDestructiveDrainCanonical
                    .IsCanonicalToken(owner.targetDestinationId)
                || !string.Equals(
                    owner.stepOperationId,
                    ProductionFacilityDestructiveDrainCanonical
                        .BuildStepOperationId(
                            operationId,
                            participant.participantId,
                            owner.ownerStableId),
                    StringComparison.Ordinal)
                || !ProductionFacilityDestructiveDrainCanonical.IsFingerprint(
                    owner.requestFingerprint)
                || (owner.phase == ProductionFacilityDestructiveDrainStepPhase
                        .Planned
                    ? !string.IsNullOrEmpty(owner.commitId)
                        || !string.IsNullOrEmpty(owner.receiptFingerprint)
                    : !ProductionFacilityDestructiveDrainCanonical
                            .IsCanonicalToken(owner.commitId)
                        || !ProductionFacilityDestructiveDrainCanonical
                            .IsFingerprint(owner.receiptFingerprint))
                || (previousOwner != null
                    && string.CompareOrdinal(
                        previousOwner,
                        owner.ownerStableId) >= 0))
            {
                throw new InvalidOperationException(
                    "Production destructive-drain owner is invalid or unsorted.");
            }
            previousOwner = owner.ownerStableId;
        }
    }

    private static List<ProductionFacilityDestructiveDrainParticipantSaveData>
        CloneParticipants(
            IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
                participants) => (participants
                ?? Array.Empty<
                    ProductionFacilityDestructiveDrainParticipantSaveData>())
            .Select(value => value?.Clone())
            .OrderBy(value => value?.participantId, StringComparer.Ordinal)
            .ToList();

    private static DungeonProductionFacilityDestructiveDrainSaveData ClonePayload(
        DungeonProductionFacilityDestructiveDrainSaveData payload) => new()
    {
        version = payload.version,
        registryFingerprint = payload.registryFingerprint,
        entries = payload.entries.Select(value => value?.Clone()).ToList()
    };

    private ProductionFacilityDestructiveDrainAggregateState State =>
        roots.GetOrCreate(() =>
            new ProductionFacilityDestructiveDrainAggregateState());

    private ProductionFacilityDestructiveDrainAggregateState Writable =>
        roots.GetOrCreateWritable(
            () => new ProductionFacilityDestructiveDrainAggregateState(),
            value => value.Clone());

}

internal sealed class ProductionFacilityDestructiveDrainAggregateState
{
    internal ProductionFacilityDestructiveDrainAggregateState()
    {
    }

    internal ProductionFacilityDestructiveDrainAggregateState(
        IEnumerable<ProductionFacilityDestructiveDrainEntrySaveData> entries)
    {
        foreach (ProductionFacilityDestructiveDrainEntrySaveData entry in
                 entries
                 ?? Array.Empty<
                     ProductionFacilityDestructiveDrainEntrySaveData>())
        {
            Entries.Add(entry.operationId, entry.Clone());
        }
    }

    internal Dictionary<string,
        ProductionFacilityDestructiveDrainEntrySaveData> Entries { get; } =
        new(StringComparer.Ordinal);
    internal int Version { get; private set; }

    internal void AdvanceVersion() => Version = checked(Version + 1);

    internal ProductionFacilityDestructiveDrainAggregateState Clone()
    {
        ProductionFacilityDestructiveDrainAggregateState clone = new(
            Entries.Values);
        clone.Version = Version;
        return clone;
    }
}
