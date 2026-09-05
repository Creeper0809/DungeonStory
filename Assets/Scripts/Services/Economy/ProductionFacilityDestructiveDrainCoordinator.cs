using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Drives the durable destructive-drain journal through all participant
/// effects. Authority revoke and world removal are explicit acknowledgement
/// boundaries so a failed world mutation is retried forward instead of
/// rolling already-published physical effects back into the facility.
/// </summary>
public sealed class ProductionFacilityDestructiveDrainCoordinator :
    IProductionFacilityDestructiveDrainCoordinator
{
    private readonly IProductionFacilityDestructiveDrainStartPreflight preflight;
    private readonly IProductionFacilityDestructiveDrainParticipantRegistry registry;
    private readonly IProductionFacilityDestructiveDrainJournalQuery query;
    private readonly IProductionFacilityDestructiveDrainJournalCommand journal;
    private readonly IProductionOutputDestinationLifecycleQuery lifecycle;
    private readonly IProductionFacilityDestructiveDrainAuthorityStateQuery
        authorityState;

    public ProductionFacilityDestructiveDrainCoordinator(
        IProductionFacilityDestructiveDrainStartPreflight preflight,
        IProductionFacilityDestructiveDrainParticipantRegistry registry,
        IProductionFacilityDestructiveDrainJournalQuery query,
        IProductionFacilityDestructiveDrainJournalCommand journal,
        IProductionOutputDestinationLifecycleQuery lifecycle,
        IProductionFacilityDestructiveDrainAuthorityStateQuery authorityState)
    {
        this.preflight = preflight ?? throw new ArgumentNullException(nameof(preflight));
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.query = query ?? throw new ArgumentNullException(nameof(query));
        this.journal = journal ?? throw new ArgumentNullException(nameof(journal));
        this.lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        this.authorityState = authorityState
            ?? throw new ArgumentNullException(nameof(authorityState));
    }

    public ProductionFacilityDestructiveDrainDriveResult DriveToAuthorityRevoke(
        ProductionFacilityDestructiveDrainCause cause,
        BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid
            || cause == ProductionFacilityDestructiveDrainCause.None
            || !Enum.IsDefined(typeof(ProductionFacilityDestructiveDrainCause), cause))
        {
            throw new ArgumentException("A valid destructive-drain request is required.");
        }

        ProductionFacilityDestructiveDrainOperationId operationId =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(facilityId);
        if (!query.TryGet(operationId, out ProductionFacilityDestructiveDrainEntrySaveData entry))
        {
            ProductionFacilityDestructiveDrainStartPreflightResult assessed =
                preflight.Assess(facilityId);
            if (!assessed.CanStart)
            {
                return InitialFailure(
                    operationId,
                    assessed.Status ==
                        ProductionFacilityDestructiveDrainStartPreflightStatus.Conflict,
                    assessed.ReasonCode);
            }

            ProductionOutputDestinationLifecycleSnapshot prepared =
                lifecycle.Capture(facilityId);
            ProductionFacilityDestructiveDrainPrepareContext context = new(
                operationId,
                cause,
                facilityId,
                prepared.DestinationId,
                prepared.DurableSemanticFingerprint);
            IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
                participants;
            try
            {
                participants = PrepareParticipants(context, prepared);
            }
            catch (Exception exception)
            {
                return InitialFailure(
                    operationId,
                    conflict: true,
                    "production-facility-destructive-drain-prepare-failed:"
                    + exception.GetType().Name + ":" + exception.Message);
            }

            ProductionOutputDestinationLifecycleSnapshot frozen =
                lifecycle.Capture(facilityId);
            if (!frozen.DestinationId.Equals(prepared.DestinationId)
                || !string.Equals(
                    frozen.DurableSemanticFingerprint,
                    prepared.DurableSemanticFingerprint,
                    StringComparison.Ordinal))
            {
                return InitialFailure(
                    operationId,
                    conflict: true,
                    "production-facility-destructive-drain-plan-freeze-drift");
            }

            if (!journal.TryRequest(
                    cause,
                    facilityId,
                    ProductionFacilityDestructiveDrainCanonical
                        .BuildInitiatingMutationOperationId(cause, facilityId),
                    prepared.DurableSemanticFingerprint,
                    participants,
                    out entry,
                    out string requestFailure))
            {
                return InitialFailure(operationId, conflict: true, requestFailure);
            }
        }
        else if (!entry.facilityId.Equals(facilityId.Value, StringComparison.Ordinal)
            || entry.cause != cause)
        {
            return Failure(
                entry,
                conflict: true,
                "production-facility-destructive-drain-existing-operation-conflict");
        }

        int maximumTransitions = Math.Max(
            8,
            4 + entry.participants.Sum(value => value.owners.Count) * 2);
        while (maximumTransitions-- > 0)
        {
            switch (entry.phase)
            {
                case ProductionFacilityDestructiveDrainPhase.Prepared:
                    if (!TryPrepareDurableOwners(entry, out string durableFailure))
                        return Failure(entry, conflict: false, durableFailure);
                    if (!TryAdvancePhase(
                            entry,
                            ProductionFacilityDestructiveDrainPhase.DrainingParticipants,
                            lifecycle.Capture(facilityId).DurableSemanticFingerprint,
                            entry.participants,
                            out entry,
                            out string drainingFailure))
                    {
                        return Failure(entry, conflict: true, drainingFailure);
                    }
                    continue;

                case ProductionFacilityDestructiveDrainPhase.DrainingParticipants:
                    if (TryFindNextOwner(
                            entry,
                            out IProductionFacilityDestructiveDrainParticipant participant,
                            out int participantIndex,
                            out int ownerIndex))
                    {
                        ProductionFacilityDestructiveDrainDriveResult step =
                            DriveOwner(
                                entry,
                                participant,
                                participantIndex,
                                ownerIndex,
                                out ProductionFacilityDestructiveDrainEntrySaveData advanced);
                        if (step.Status is
                            ProductionFacilityDestructiveDrainDriveStatus.Deferred
                            or ProductionFacilityDestructiveDrainDriveStatus.Conflict)
                        {
                            return step;
                        }
                        entry = advanced;
                        continue;
                    }

                    if (!TryVerifyParticipantClosure(entry, out string closureFailure))
                        return Failure(entry, conflict: true, closureFailure);
                    if (!TryAdvancePhase(
                            entry,
                            ProductionFacilityDestructiveDrainPhase.AwaitingEmptyVerification,
                            lifecycle.Capture(facilityId).DurableSemanticFingerprint,
                            entry.participants,
                            out entry,
                            out string verifyPhaseFailure))
                    {
                        return Failure(entry, conflict: true, verifyPhaseFailure);
                    }
                    continue;

                case ProductionFacilityDestructiveDrainPhase.AwaitingEmptyVerification:
                    if (!TryVerifyParticipantClosure(entry, out string emptyFailure))
                        return Failure(entry, conflict: true, emptyFailure);
                    if (!TryAdvancePhase(
                            entry,
                            ProductionFacilityDestructiveDrainPhase.AwaitingAuthorityRevoke,
                            lifecycle.Capture(facilityId).DurableSemanticFingerprint,
                            entry.participants,
                            out entry,
                            out string revokePhaseFailure))
                    {
                        return Failure(entry, conflict: true, revokePhaseFailure);
                    }
                    continue;

                case ProductionFacilityDestructiveDrainPhase.AwaitingAuthorityRevoke:
                    return Success(
                        entry,
                        ProductionFacilityDestructiveDrainDriveStatus
                            .AwaitingAuthorityRevoke);

                case ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval:
                    return Success(
                        entry,
                        ProductionFacilityDestructiveDrainDriveStatus
                            .AwaitingWorldRemoval);

                case ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc:
                    return Success(
                        entry,
                        ProductionFacilityDestructiveDrainDriveStatus
                            .WorldRemovedAwaitingCheckpointGc);

                default:
                    return Failure(
                        entry,
                        conflict: true,
                        "production-facility-destructive-drain-phase-unsupported");
            }
        }

        return Failure(
            entry,
            conflict: true,
            "production-facility-destructive-drain-transition-budget-exhausted");
    }

    public ProductionFacilityDestructiveDrainDriveResult RecordAuthorityRevoked(
        ProductionFacilityDestructiveDrainOperationId operationId)
    {
        ProductionFacilityDestructiveDrainEntrySaveData entry = RequireEntry(operationId);
        if (entry.phase == ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval)
        {
            return Success(
                entry,
                ProductionFacilityDestructiveDrainDriveStatus.AwaitingWorldRemoval);
        }
        if (entry.phase != ProductionFacilityDestructiveDrainPhase.AwaitingAuthorityRevoke)
        {
            return Failure(
                entry,
                conflict: true,
                "production-facility-destructive-drain-authority-revoke-phase-invalid");
        }

        ProductionOutputDestinationLifecycleSnapshot current =
            lifecycle.Capture((BuildingInstanceId)entry.facilityId);
        ProductionFacilityDestructiveDrainAuthoritySnapshot authorities =
            authorityState.Capture((BuildingInstanceId)entry.facilityId);
        if (authorities.HasInvalidPair
            || !authorities.AllAbsent
            || current.HasAnyAuthority
            || !current.CanRevokeEmpty)
        {
            return Failure(
                entry,
                conflict: true,
                authorities.HasInvalidPair
                    ? authorities.FailureReason
                    : "production-facility-destructive-drain-authority-still-present");
        }
        if (!TryRebaseClosedBoundaryContributions(
                entry,
                current,
                "authority-revoke",
                out IReadOnlyList<
                    ProductionFacilityDestructiveDrainParticipantSaveData>
                    revokedParticipants,
                out string rebaseFailure))
        {
            return Failure(entry, conflict: true, rebaseFailure);
        }
        if (!TryAdvancePhase(
                entry,
                ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval,
                current.DurableSemanticFingerprint,
                revokedParticipants,
                out ProductionFacilityDestructiveDrainEntrySaveData advanced,
                out string failureReason))
        {
            return Failure(entry, conflict: true, failureReason);
        }
        return Success(
            advanced,
            ProductionFacilityDestructiveDrainDriveStatus.AwaitingWorldRemoval);
    }

    public ProductionFacilityDestructiveDrainDriveResult RecordWorldRemoved(
        ProductionFacilityDestructiveDrainOperationId operationId)
    {
        ProductionFacilityDestructiveDrainEntrySaveData entry = RequireEntry(operationId);
        if (entry.phase == ProductionFacilityDestructiveDrainPhase
                .WorldRemovedAwaitingCheckpointGc)
        {
            return Success(
                entry,
                ProductionFacilityDestructiveDrainDriveStatus
                    .WorldRemovedAwaitingCheckpointGc);
        }
        if (entry.phase != ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval)
        {
            return Failure(
                entry,
                conflict: true,
                "production-facility-destructive-drain-world-remove-phase-invalid");
        }

        ProductionOutputDestinationLifecycleSnapshot current =
            lifecycle.Capture((BuildingInstanceId)entry.facilityId);
        ProductionFacilityDestructiveDrainAuthoritySnapshot authorities =
            authorityState.Capture((BuildingInstanceId)entry.facilityId);
        if (authorities.HasInvalidPair
            || !authorities.AllAbsent
            || current.HasAnyAuthority
            || !current.CanRevokeEmpty)
        {
            return Failure(
                entry,
                conflict: true,
                authorities.HasInvalidPair
                    ? authorities.FailureReason
                    : "production-facility-destructive-drain-world-remove-authority-present");
        }
        if (!TryRebaseClosedBoundaryContributions(
                entry,
                current,
                "world-remove",
                out IReadOnlyList<
                    ProductionFacilityDestructiveDrainParticipantSaveData>
                    terminalParticipants,
                out string rebaseFailure))
        {
            return Failure(entry, conflict: true, rebaseFailure);
        }
        if (!TryAdvancePhase(
                entry,
                ProductionFacilityDestructiveDrainPhase
                    .WorldRemovedAwaitingCheckpointGc,
                current.DurableSemanticFingerprint,
                terminalParticipants,
                out ProductionFacilityDestructiveDrainEntrySaveData advanced,
                out string failureReason))
        {
            return Failure(entry, conflict: true, failureReason);
        }
        return Success(
            advanced,
            ProductionFacilityDestructiveDrainDriveStatus
                .WorldRemovedAwaitingCheckpointGc);
    }

    public bool TryCollectCheckpointed(
        ProductionFacilityDestructiveDrainOperationId operationId,
        long expectedRevision,
        out string failureReason)
    {
        // The upper journal cannot be collected independently from participant
        // tombstones. Until the checkpoint transaction can collect every lower
        // owner and this journal atomically, retaining the terminal entry is the
        // only crash-safe behavior.
        failureReason =
            "production-facility-destructive-drain-checkpoint-gc-not-atomic";
        return false;
    }

    private IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
        PrepareParticipants(
            ProductionFacilityDestructiveDrainPrepareContext context,
            ProductionOutputDestinationLifecycleSnapshot snapshot)
    {
        Dictionary<string, ProductionOutputDestinationLifecycleContribution>
            contributions = snapshot.Contributions.ToDictionary(
                value => value.ContributorId,
                StringComparer.Ordinal);
        List<ProductionFacilityDestructiveDrainParticipantSaveData> result = new();
        foreach (IProductionFacilityDestructiveDrainParticipant participant in
                 registry.ExecutionOrder)
        {
            ProductionFacilityDestructiveDrainParticipantPlan plan =
                participant.Prepare(context)
                ?? throw new InvalidOperationException(
                    "participant returned no destructive-drain plan: "
                    + participant.ParticipantId);
            if (!string.Equals(
                    plan.ParticipantId,
                    participant.ParticipantId,
                    StringComparison.Ordinal)
                || plan.ContractVersion != participant.ContractVersion
                || !contributions.TryGetValue(
                    participant.ParticipantId,
                    out ProductionOutputDestinationLifecycleContribution contribution)
                || !string.Equals(
                    plan.DurableContributionFingerprint,
                    contribution.DurableSemanticFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "participant destructive-drain plan drifted from the durable lifecycle: "
                    + participant.ParticipantId);
            }

            result.Add(new ProductionFacilityDestructiveDrainParticipantSaveData
            {
                participantId = plan.ParticipantId,
                contractVersion = plan.ContractVersion,
                preparedContributionFingerprint =
                    plan.DurableContributionFingerprint,
                expectedCurrentContributionFingerprint =
                    plan.DurableContributionFingerprint,
                planFingerprint = plan.PlanFingerprint,
                owners = plan.Owners.Select(owner =>
                    new ProductionFacilityDestructiveDrainOwnerSaveData
                    {
                        ownerStableId = owner.OwnerStableId,
                        disposition = owner.Disposition,
                        targetDestinationId = owner.TargetDestinationId,
                        stepOperationId = ProductionFacilityDestructiveDrainCanonical
                            .BuildStepOperationId(
                                context.OperationId,
                                plan.ParticipantId,
                                owner.OwnerStableId),
                        phase = ProductionFacilityDestructiveDrainStepPhase.Planned,
                        requestFingerprint = owner.RequestFingerprint,
                        commitId = string.Empty,
                        receiptFingerprint = string.Empty
                    }).ToList()
            });
        }
        return result.OrderBy(value => value.participantId, StringComparer.Ordinal)
            .ToArray();
    }

    private bool TryPrepareDurableOwners(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        out string failureReason)
    {
        failureReason = string.Empty;
        ProductionFacilityDestructiveDrainOperationId operationId =
            ParseOperation(entry.operationId);
        foreach (IProductionFacilityDestructiveDrainParticipant participant in
                 registry.ExecutionOrder)
        {
            ProductionFacilityDestructiveDrainParticipantSaveData row =
                FindParticipant(entry, participant.ParticipantId);
            if (participant is not
                IProductionFacilityDestructiveDrainDurablePrepareParticipant durable)
            {
                continue;
            }
            foreach (ProductionFacilityDestructiveDrainOwnerSaveData owner in row.owners)
            {
                ProductionFacilityDestructiveDrainStepContext context = new(
                    operationId,
                    (BuildingInstanceId)entry.facilityId,
                    participant.ParticipantId,
                    owner,
                    row.expectedCurrentContributionFingerprint);
                if (!durable.TryPrepareDurable(context, out failureReason))
                {
                    failureReason = string.IsNullOrEmpty(failureReason)
                        ? "production-facility-destructive-drain-durable-prepare-deferred:"
                            + participant.ParticipantId + ":" + owner.ownerStableId
                        : failureReason;
                    return false;
                }
            }
        }
        return true;
    }

    private static bool TryRebaseClosedBoundaryContributions(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        ProductionOutputDestinationLifecycleSnapshot current,
        string boundary,
        out IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
            participants,
        out string failureReason)
    {
        participants = Array.Empty<
            ProductionFacilityDestructiveDrainParticipantSaveData>();
        failureReason = string.Empty;
        if (entry.participants.Any(value => value.owners.Any(owner =>
                owner.phase !=
                    ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged)))
        {
            failureReason =
                "production-facility-destructive-drain-" + boundary
                + "-owner-not-acknowledged";
            return false;
        }

        Dictionary<string, ProductionOutputDestinationLifecycleContribution>
            contributions = new(StringComparer.Ordinal);
        foreach (ProductionOutputDestinationLifecycleContribution contribution in
                 current.Contributions)
        {
            if (contribution == null
                || !contributions.TryAdd(
                    contribution.ContributorId,
                    contribution))
            {
                failureReason =
                    "production-facility-destructive-drain-" + boundary
                    + "-contribution-set-invalid";
                return false;
            }
        }
        if (contributions.Count != entry.participants.Count)
        {
            failureReason =
                "production-facility-destructive-drain-" + boundary
                + "-contribution-set-invalid";
            return false;
        }

        List<ProductionFacilityDestructiveDrainParticipantSaveData> rebased =
            entry.participants.Select(value => value.Clone()).ToList();
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData row in
                 rebased)
        {
            if (!contributions.TryGetValue(
                    row.participantId,
                    out ProductionOutputDestinationLifecycleContribution
                        contribution))
            {
                failureReason =
                    "production-facility-destructive-drain-" + boundary
                    + "-contribution-set-invalid";
                return false;
            }
            row.expectedCurrentContributionFingerprint =
                contribution.DurableSemanticFingerprint;
        }
        participants = rebased;
        return true;
    }

    private bool TryFindNextOwner(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        out IProductionFacilityDestructiveDrainParticipant participant,
        out int participantIndex,
        out int ownerIndex)
    {
        participant = null;
        participantIndex = -1;
        ownerIndex = -1;
        foreach (IProductionFacilityDestructiveDrainParticipant candidate in
                 registry.ExecutionOrder)
        {
            ProductionFacilityDestructiveDrainParticipantSaveData row =
                FindParticipant(entry, candidate.ParticipantId);
            for (int index = 0; index < row.owners.Count; index++)
            {
                if (row.owners[index].phase ==
                    ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged)
                {
                    continue;
                }
                participant = candidate;
                participantIndex = entry.participants.FindIndex(value =>
                    string.Equals(
                        value.participantId,
                        candidate.ParticipantId,
                        StringComparison.Ordinal));
                ownerIndex = index;
                return true;
            }
        }
        return false;
    }

    private ProductionFacilityDestructiveDrainDriveResult DriveOwner(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        IProductionFacilityDestructiveDrainParticipant participant,
        int participantIndex,
        int ownerIndex,
        out ProductionFacilityDestructiveDrainEntrySaveData advanced)
    {
        advanced = entry;
        ProductionFacilityDestructiveDrainParticipantSaveData row =
            entry.participants[participantIndex];
        ProductionFacilityDestructiveDrainOwnerSaveData owner = row.owners[ownerIndex];
        ProductionFacilityDestructiveDrainStepContext context = new(
            ParseOperation(entry.operationId),
            (BuildingInstanceId)entry.facilityId,
            participant.ParticipantId,
            owner,
            row.expectedCurrentContributionFingerprint);

        ProductionFacilityDestructiveDrainStepResult step;
        try
        {
            ProductionFacilityDestructiveDrainRecoveryResult recovery =
                participant.Recover(context);
            if (recovery.Action == ProductionFacilityDestructiveDrainRecoveryAction.Conflict
                || recovery.Step.Status == ProductionFacilityDestructiveDrainStepStatus.Conflict)
            {
                return Failure(
                    entry,
                    conflict: true,
                    "production-facility-destructive-drain-owner-recovery-conflict:"
                    + participant.ParticipantId + ":" + owner.ownerStableId);
            }

            if (owner.phase == ProductionFacilityDestructiveDrainStepPhase.Planned)
            {
                step = recovery.Step.Status is
                    ProductionFacilityDestructiveDrainStepStatus.Applied
                    or ProductionFacilityDestructiveDrainStepStatus.Replay
                        ? recovery.Step
                        : participant.TryCommit(context);
            }
            else
            {
                step = recovery.Action ==
                        ProductionFacilityDestructiveDrainRecoveryAction
                            .AlreadyAcknowledged
                    ? recovery.Step
                    : participant.TryAcknowledge(context);
            }
        }
        catch (Exception exception)
        {
            return Failure(
                entry,
                conflict: true,
                "production-facility-destructive-drain-owner-step-threw:"
                + participant.ParticipantId + ":" + owner.ownerStableId + ":"
                + exception.GetType().Name);
        }

        if (step.Status == ProductionFacilityDestructiveDrainStepStatus.Deferred)
        {
            return Failure(
                entry,
                conflict: false,
                "production-facility-destructive-drain-owner-deferred:"
                + participant.ParticipantId + ":" + owner.ownerStableId);
        }
        if (step.Status == ProductionFacilityDestructiveDrainStepStatus.Conflict)
        {
            return Failure(
                entry,
                conflict: true,
                "production-facility-destructive-drain-owner-conflict:"
                + participant.ParticipantId + ":" + owner.ownerStableId);
        }

        ProductionOutputDestinationLifecycleSnapshot current =
            lifecycle.Capture((BuildingInstanceId)entry.facilityId);
        ProductionOutputDestinationLifecycleContribution currentContribution =
            current.Contributions.SingleOrDefault(value => string.Equals(
                value.ContributorId,
                participant.ParticipantId,
                StringComparison.Ordinal));
        if (currentContribution == null
            || !string.Equals(
                currentContribution.DurableSemanticFingerprint,
                step.CurrentDurableContributionFingerprint,
                StringComparison.Ordinal))
        {
            return Failure(
                entry,
                conflict: true,
                "production-facility-destructive-drain-owner-result-fingerprint-drift:"
                + participant.ParticipantId + ":" + owner.ownerStableId);
        }

        List<ProductionFacilityDestructiveDrainParticipantSaveData> participants =
            entry.participants.Select(value => value.Clone()).ToList();
        ProductionFacilityDestructiveDrainParticipantSaveData nextRow =
            participants[participantIndex];
        ProductionFacilityDestructiveDrainOwnerSaveData nextOwner =
            nextRow.owners[ownerIndex];
        nextOwner.commitId = step.CommitId;
        nextOwner.receiptFingerprint = step.ReceiptFingerprint;
        nextOwner.phase = owner.phase == ProductionFacilityDestructiveDrainStepPhase.Planned
            ? ProductionFacilityDestructiveDrainStepPhase
                .EffectCommittedAwaitingOwnerAck
            : ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged;
        nextRow.expectedCurrentContributionFingerprint =
            step.CurrentDurableContributionFingerprint;

        if (!TryAdvancePhase(
                entry,
                ProductionFacilityDestructiveDrainPhase.DrainingParticipants,
                current.DurableSemanticFingerprint,
                participants,
                out advanced,
                out string advanceFailure))
        {
            return Failure(entry, conflict: true, advanceFailure);
        }
        return Success(
            advanced,
            ProductionFacilityDestructiveDrainDriveStatus.AwaitingAuthorityRevoke);
    }

    private bool TryVerifyParticipantClosure(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (entry.participants.Any(value => value.owners.Any(owner =>
                owner.phase !=
                    ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged)))
        {
            failureReason =
                "production-facility-destructive-drain-owner-not-acknowledged";
            return false;
        }

        ProductionOutputDestinationLifecycleSnapshot current =
            lifecycle.Capture((BuildingInstanceId)entry.facilityId);
        if (!current.CanRevokeEmpty)
        {
            failureReason =
                "production-facility-destructive-drain-lifecycle-not-empty";
            return false;
        }
        foreach (ProductionFacilityDestructiveDrainParticipantSaveData row in
                 entry.participants)
        {
            ProductionOutputDestinationLifecycleContribution contribution =
                current.Contributions.SingleOrDefault(value => string.Equals(
                    value.ContributorId,
                    row.participantId,
                    StringComparison.Ordinal));
            if (contribution == null
                || !string.Equals(
                    contribution.DurableSemanticFingerprint,
                    row.expectedCurrentContributionFingerprint,
                    StringComparison.Ordinal))
            {
                failureReason =
                    "production-facility-destructive-drain-contribution-not-closed:"
                    + row.participantId;
                return false;
            }
        }
        return true;
    }

    private bool TryAdvancePhase(
        ProductionFacilityDestructiveDrainEntrySaveData current,
        ProductionFacilityDestructiveDrainPhase phase,
        string lifecycleFingerprint,
        IReadOnlyList<ProductionFacilityDestructiveDrainParticipantSaveData>
            participants,
        out ProductionFacilityDestructiveDrainEntrySaveData advanced,
        out string failureReason) => journal.TryAdvance(
        ParseOperation(current.operationId),
        current.revision,
        phase,
        lifecycleFingerprint,
        participants,
        out advanced,
        out failureReason);

    private ProductionFacilityDestructiveDrainEntrySaveData RequireEntry(
        ProductionFacilityDestructiveDrainOperationId operationId)
    {
        if (!operationId.IsValid || !query.TryGet(operationId, out var entry))
            throw new InvalidOperationException(
                "The destructive-drain operation does not exist.");
        return entry;
    }

    private static ProductionFacilityDestructiveDrainParticipantSaveData
        FindParticipant(
            ProductionFacilityDestructiveDrainEntrySaveData entry,
            string participantId) => entry.participants.Single(value =>
        string.Equals(value.participantId, participantId, StringComparison.Ordinal));

    private static ProductionFacilityDestructiveDrainOperationId ParseOperation(
        string value)
    {
        if (!ProductionFacilityDestructiveDrainOperationId.TryParse(
                value,
                out ProductionFacilityDestructiveDrainOperationId operationId))
        {
            throw new InvalidOperationException(
                "The destructive-drain operation identity is invalid.");
        }
        return operationId;
    }

    private static ProductionFacilityDestructiveDrainDriveResult InitialFailure(
        ProductionFacilityDestructiveDrainOperationId operationId,
        bool conflict,
        string reason) => new(
        conflict
            ? ProductionFacilityDestructiveDrainDriveStatus.Conflict
            : ProductionFacilityDestructiveDrainDriveStatus.Deferred,
        operationId,
        ProductionFacilityDestructiveDrainPhase.Prepared,
        1L,
        string.IsNullOrEmpty(reason)
            ? "production-facility-destructive-drain-start-deferred"
            : reason);

    private static ProductionFacilityDestructiveDrainDriveResult Failure(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        bool conflict,
        string reason) => new(
        conflict
            ? ProductionFacilityDestructiveDrainDriveStatus.Conflict
            : ProductionFacilityDestructiveDrainDriveStatus.Deferred,
        ParseOperation(entry.operationId),
        entry.phase,
        entry.revision,
        string.IsNullOrEmpty(reason)
            ? "production-facility-destructive-drain-deferred"
            : reason);

    private static ProductionFacilityDestructiveDrainDriveResult Success(
        ProductionFacilityDestructiveDrainEntrySaveData entry,
        ProductionFacilityDestructiveDrainDriveStatus status) => new(
        status,
        ParseOperation(entry.operationId),
        entry.phase,
        entry.revision,
        string.Empty);
}
