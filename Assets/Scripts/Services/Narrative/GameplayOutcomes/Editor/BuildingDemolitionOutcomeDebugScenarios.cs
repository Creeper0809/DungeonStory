#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class BuildingDemolitionOutcomeDebugScenarios
{
    public static void RunOrThrow()
    {
        if (!RunAll(true))
            throw new InvalidOperationException(
                "B0 G05 building demolition outcome scenarios failed.");
    }

    public static bool RunAll(bool logSuccess = false)
    {
        GameplayOutcomeRegistry outcomeRegistry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new BuildingDemolitionOutcomeDescriptor(
                    new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new BuildingDemolitionOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = new(
            outcomeRegistry,
            new GameplayOutcomeBufferLimits(),
            new GameplayOutcomeRunId("run:building-demolition-editor"),
            1L);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            outcomeRegistry,
            new GameEventBus());
        BuildingDemolitionGameplayOutcomeBridge bridge = new(recorder, ledger);
        BuildingDemolitionDestructiveDrainParticipant demolition = new(bridge);

        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa-demolition-outcome";
        ProductionFacilityDestructiveDrainOperationId operationId =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(
                facilityId);
        ProductionFacilityDestructiveDrainOutcomeSnapshot snapshot = new(
            "building-definition:qa-smithy",
            "대장간",
            8,
            5,
            17,
            1L);
        string contribution = BuildingDemolitionOutcomeLifecycle
            .ProjectContribution(facilityId);
        ProductionFacilityDestructiveDrainPrepareContext prepareContext = new(
            operationId,
            ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
            facilityId,
            ProductionOutputDestinationId.FromFacility(facilityId),
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:demolition-lifecycle"),
            snapshot);
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            demolition.Prepare(prepareContext);
        Require(
            plan.Owners.Count == 1
            && string.Equals(
                plan.DurableContributionFingerprint,
                contribution,
                StringComparison.Ordinal),
            "explicit demolition did not freeze one canonical owner");
        ProductionFacilityDestructiveDrainOwnerSaveData plannedOwner =
            Owner(operationId, plan.ParticipantId, plan.Owners.Single());
        ProductionFacilityDestructiveDrainStepContext planned = new(
            operationId,
            facilityId,
            demolition.ParticipantId,
            plannedOwner,
            contribution,
            snapshot);
        Require(
            demolition.TryPrepareDurable(planned, out string prepareFailure),
            "demolition durable prepare failed: " + prepareFailure);
        ProductionFacilityDestructiveDrainStepResult committed =
            demolition.TryCommit(planned);
        Require(
            committed.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied,
            "demolition drain owner did not commit its pre-removal marker");
        GameplayEntityId facility = new(
            BuildingDemolitionOutcomeIds.FacilityKind,
            facilityId.Value);
        Require(
            ledger.GetForEntity(
                facility,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 0,
            "demolition outcome committed before world removal");

        ProductionFacilityDestructiveDrainOwnerSaveData awaiting =
            plannedOwner.Clone();
        awaiting.phase = ProductionFacilityDestructiveDrainStepPhase
            .EffectCommittedAwaitingOwnerAck;
        awaiting.commitId = committed.CommitId;
        awaiting.receiptFingerprint = committed.ReceiptFingerprint;
        ProductionFacilityDestructiveDrainStepContext awaitingContext = new(
            operationId,
            facilityId,
            demolition.ParticipantId,
            awaiting,
            contribution,
            snapshot);
        ProductionFacilityDestructiveDrainStepResult acknowledged =
            demolition.TryAcknowledge(awaitingContext);
        Require(
            acknowledged.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied,
            "demolition drain owner acknowledgement did not converge");
        Require(
            ledger.GetForEntity(
                facility,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 0,
            "demolition outcome committed before acknowledged world removal");

        ProductionFacilityDestructiveDrainOwnerSaveData acknowledgedOwner =
            awaiting.Clone();
        acknowledgedOwner.phase =
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged;
        var terminalEntry = new ProductionFacilityDestructiveDrainEntrySaveData
        {
            operationId = operationId.Value,
            cause = ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
            facilityId = facilityId.Value,
            destinationId = ProductionOutputDestinationId
                .FromFacility(facilityId).Value,
            phase = ProductionFacilityDestructiveDrainPhase
                .AwaitingWorldRemoval,
            outcomeSnapshotVersion = snapshot.Version,
            outcomeBuildingDefinitionId = snapshot.BuildingDefinitionId,
            outcomeBuildingDisplayName = snapshot.BuildingDisplayName,
            outcomePositionX = snapshot.PositionX,
            outcomePositionY = snapshot.PositionY,
            outcomeAbsoluteDay = snapshot.AbsoluteDay,
            outcomeOwnerRevision = snapshot.OwnerRevision,
            participants = new List<
                ProductionFacilityDestructiveDrainParticipantSaveData>
            {
                new()
                {
                    participantId = demolition.ParticipantId,
                    contractVersion = demolition.ContractVersion,
                    preparedContributionFingerprint = contribution,
                    expectedCurrentContributionFingerprint = contribution,
                    planFingerprint = plan.PlanFingerprint,
                    owners = new List<
                        ProductionFacilityDestructiveDrainOwnerSaveData>
                    {
                        acknowledgedOwner
                    }
                }
            }
        };
        VerifyDemolitionSaveOwnerJoin(terminalEntry);
        var gcContext = new ProductionFacilityDestructiveDrainCheckpointGcContext(
            1, new string('a', 64), "qa-demolition");
        var gcEntry = JsonUtility.FromJson<ProductionFacilityDestructiveDrainEntrySaveData>(
            JsonUtility.ToJson(terminalEntry));
        gcEntry.phase = ProductionFacilityDestructiveDrainPhase.WorldRemovedAwaitingCheckpointGc;
        Require(demolition.PrepareCheckpointGarbageCollection(gcContext, new[] { gcEntry },
                out var prematureCandidate).Status == ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred
                && prematureCandidate == null,
            "GC accepted a demolition operation whose durable receipt is still prepared");
        Require(
            demolition.TryFinalizeAfterWorldRemoval(
                terminalEntry,
                out string finalizeFailure),
            "demolition outcome did not finalize after world removal: "
                + finalizeFailure);

        GameplayOutcomeQueryPage facilityPage = ledger.GetForEntity(
            facility,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            facilityPage.Items.Count == 1,
            "facility query did not expose exactly one demolition outcome");
        GameplayOutcomeSnapshot exact = facilityPage.Items.Single().Exact;
        Require(
            exact.outcomeTypeId ==
                BuildingDemolitionOutcomeIds.Demolished.Value
            && exact.operationId == operationId.Value
            && exact.ownerRevision == 1L
            && exact.absoluteDay == 17
            && exact.locationX == 8
            && exact.locationY == 5
            && exact.participants.Count == 1
            && exact.participants[0].displayText == "대장간"
            && exact.subjects.Count == 1
            && exact.facts.Any(value =>
                value.factId ==
                    BuildingDemolitionOutcomeIds.DefinitionIdFact.Value
                && value.value == "building-definition:qa-smithy"),
            "demolition outcome lost its frozen identity or location");

        Require(
            demolition.TryPrepareDurable(planned, out string replayFailure),
            "demolition canonical replay prepare failed: " + replayFailure);
        ProductionFacilityDestructiveDrainStepResult replay =
            demolition.TryCommit(planned);
        string replayFinalizeFailure = string.Empty;
        Require(
            replay.Status == ProductionFacilityDestructiveDrainStepStatus.Applied
            && demolition.TryFinalizeAfterWorldRemoval(
                terminalEntry,
                out replayFinalizeFailure)
            && ledger.GetForEntity(
                facility,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1,
            "demolition replay duplicated or rejected the canonical result: "
                + replayFinalizeFailure);

        ProductionFacilityDestructiveDrainOutcomeSnapshot driftedSnapshot = new(
            "building-definition:qa-smithy",
            "낡은 대장간",
            8,
            5,
            17,
            1L);
        ProductionFacilityDestructiveDrainPrepareContext driftedPrepare = new(
            operationId,
            ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
            facilityId,
            ProductionOutputDestinationId.FromFacility(facilityId),
            prepareContext.DurableLifecycleFingerprint,
            driftedSnapshot);
        ProductionFacilityDestructiveDrainParticipantPlan driftedPlan =
            demolition.Prepare(driftedPrepare);
        ProductionFacilityDestructiveDrainStepContext drifted = new(
            operationId,
            facilityId,
            demolition.ParticipantId,
            Owner(operationId, demolition.ParticipantId,
                driftedPlan.Owners.Single()),
            contribution,
            driftedSnapshot);
        Require(
            !demolition.TryPrepareDurable(drifted, out _),
            "same result key accepted a drifted demolition snapshot");

        ProductionFacilityDestructiveDrainPrepareContext structural = new(
            operationId,
            ProductionFacilityDestructiveDrainCause.StructuralIntegrity,
            facilityId,
            ProductionOutputDestinationId.FromFacility(facilityId),
            prepareContext.DurableLifecycleFingerprint);
        Require(
            demolition.Prepare(structural).Owners.Count == 0,
            "non-demolition destruction created a demolition outcome owner");

        VerifyJournalRoundTripAndLegacyMigration(
            demolition,
            prepareContext,
            snapshot);
        VerifyPostRemovalCommitFailureRetry();

        VerifyCheckpointGc(demolition, bridge, planned, terminalEntry, gcContext, gcEntry, ledger);
        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = new(
            outcomeRegistry,
            new GameplayOutcomeBufferLimits(),
            new GameplayOutcomeRunId("run:building-demolition-restore"),
            1L);
        restored.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(
            restored.GetForEntity(
                facility,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Single().Exact.immutablePayloadHash
                == exact.immutablePayloadHash,
            "demolition ledger result changed across save round trip");

        if (logSuccess)
            Debug.Log("[Building Demolition Outcome] PASS");
        return true;
    }

    private static void VerifyDemolitionSaveOwnerJoin(
        ProductionFacilityDestructiveDrainEntrySaveData source)
    {
        string original = JsonUtility.ToJson(source);
        ProductionFacilityDestructiveDrainEntrySaveData Copy() =>
            JsonUtility.FromJson<ProductionFacilityDestructiveDrainEntrySaveData>(original);
        void Validate(ProductionFacilityDestructiveDrainEntrySaveData entry)
        {
            ProductionFacilityDestructiveDrainOwnerJoinValidation.ValidateBuildingDemolitionOutcomeJoin(entry);
            if (entry.phase == ProductionFacilityDestructiveDrainPhase.Prepared)
                ProductionFacilityDestructiveDrainParticipantSaveValidation.ValidatePreparedOwnerBijection(
                    entry, new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal));
        }
        Validate(Copy()); // The actual participant's acknowledged receipt.
        var awaiting = Copy();
        awaiting.participants[0].owners[0].phase =
            ProductionFacilityDestructiveDrainStepPhase.EffectCommittedAwaitingOwnerAck;
        Validate(awaiting);
        var planned = Copy();
        planned.phase = ProductionFacilityDestructiveDrainPhase.Prepared;
        planned.participants[0].owners[0].phase = ProductionFacilityDestructiveDrainStepPhase.Planned;
        planned.participants[0].owners[0].commitId = string.Empty;
        planned.participants[0].owners[0].receiptFingerprint = string.Empty;
        Validate(planned);
        var legacy = Copy();
        legacy.phase = ProductionFacilityDestructiveDrainPhase.Prepared;
        legacy.outcomeSnapshotVersion = 0;
        legacy.outcomeBuildingDefinitionId = legacy.outcomeBuildingDisplayName = string.Empty;
        legacy.outcomePositionX = legacy.outcomePositionY = legacy.outcomeAbsoluteDay = 0;
        legacy.outcomeOwnerRevision = 0;
        legacy.participants[0].owners.Clear();
        Validate(legacy); // Existing V4 snapshot-free explicit-demolition compatibility.
        Action<ProductionFacilityDestructiveDrainEntrySaveData>[] corruptions =
        {
            entry => entry.participants[0].owners.Clear(),
            entry => entry.participants[0].owners.Add(entry.participants[0].owners[0].Clone()),
            entry => entry.participants[0].owners[0].requestFingerprint = new string('a', 64),
            entry => entry.participants[0].owners[0].stepOperationId += ":drift",
            entry => entry.participants[0].owners[0].receiptFingerprint = new string('b', 64),
            entry => entry.participants[0].owners[0].commitId += ":drift",
            entry => entry.participants[0].owners[0].targetDestinationId = "target:unexpected",
            entry => entry.participants[0].expectedCurrentContributionFingerprint = new string('c', 64),
            entry => entry.outcomeBuildingDisplayName += "-changed",
            entry => entry.cause = ProductionFacilityDestructiveDrainCause.StructuralIntegrity
        };
        foreach (var corrupt in corruptions)
        {
            var candidate = Copy();
            corrupt(candidate);
            bool rejected = false;
            try { Validate(candidate); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "demolition save self-join accepted a corrupted owner/snapshot/receipt");
        }
        Require(original == JsonUtility.ToJson(source), "demolition save validation mutated its source");
        Validate(Copy());
        Debug.Log("ARCHITECTURE_DEMOLITION_SAVE_JOIN=PASS; actual receipt; planned/committed/ack; legacy; corruptions=10; valid retry");
    }

    private static void VerifyPostRemovalCommitFailureRetry()
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new BuildingDemolitionOutcomeDescriptor(
                    new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new BuildingDemolitionOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = new(
            registry,
            new GameplayOutcomeBufferLimits(),
            new GameplayOutcomeRunId(
                "run:building-demolition-post-removal-retry"),
            1L);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new GameEventBus());
        var committer = new RejectOnceBuildingDemolitionOutcomeCommitter(
            new BuildingDemolitionGameplayOutcomeBridge(recorder, ledger));
        BuildingDemolitionDestructiveDrainParticipant participant = new(
            committer);
        BuildingInstanceId facilityId =
            (BuildingInstanceId)"building:qa-demolition-outcome-retry";
        ProductionFacilityDestructiveDrainOperationId operationId =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(
                facilityId);
        ProductionFacilityDestructiveDrainOutcomeSnapshot snapshot = new(
            "building-definition:qa-smithy-retry",
            "재시도 대장간",
            9,
            6,
            18,
            2L);
        string contribution = BuildingDemolitionOutcomeLifecycle
            .ProjectContribution(facilityId);
        ProductionFacilityDestructiveDrainPrepareContext prepare = new(
            operationId,
            ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
            facilityId,
            ProductionOutputDestinationId.FromFacility(facilityId),
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(
                "qa:demolition-post-removal-retry"),
            snapshot);
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            participant.Prepare(prepare);
        ProductionFacilityDestructiveDrainOwnerSaveData owner = Owner(
            operationId,
            participant.ParticipantId,
            plan.Owners.Single());
        ProductionFacilityDestructiveDrainStepContext planned = new(
            operationId,
            facilityId,
            participant.ParticipantId,
            owner,
            contribution,
            snapshot);
        Require(
            participant.TryPrepareDurable(planned, out string prepareFailure),
            "post-removal retry durable prepare failed: " + prepareFailure);
        ProductionFacilityDestructiveDrainStepResult committed =
            participant.TryCommit(planned);
        Require(
            committed.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied,
            "post-removal retry pre-removal marker did not commit");

        ProductionFacilityDestructiveDrainOwnerSaveData awaiting =
            owner.Clone();
        awaiting.phase = ProductionFacilityDestructiveDrainStepPhase
            .EffectCommittedAwaitingOwnerAck;
        awaiting.commitId = committed.CommitId;
        awaiting.receiptFingerprint = committed.ReceiptFingerprint;
        ProductionFacilityDestructiveDrainStepContext awaitingContext = new(
            operationId,
            facilityId,
            participant.ParticipantId,
            awaiting,
            contribution,
            snapshot);
        Require(
            participant.TryAcknowledge(awaitingContext).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Applied,
            "post-removal retry pre-removal marker did not acknowledge");
        ProductionFacilityDestructiveDrainOwnerSaveData acknowledged =
            awaiting.Clone();
        acknowledged.phase =
            ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged;
        var terminal = new ProductionFacilityDestructiveDrainEntrySaveData
        {
            operationId = operationId.Value,
            cause = ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
            facilityId = facilityId.Value,
            destinationId = ProductionOutputDestinationId
                .FromFacility(facilityId).Value,
            phase = ProductionFacilityDestructiveDrainPhase
                .AwaitingWorldRemoval,
            outcomeSnapshotVersion = snapshot.Version,
            outcomeBuildingDefinitionId = snapshot.BuildingDefinitionId,
            outcomeBuildingDisplayName = snapshot.BuildingDisplayName,
            outcomePositionX = snapshot.PositionX,
            outcomePositionY = snapshot.PositionY,
            outcomeAbsoluteDay = snapshot.AbsoluteDay,
            outcomeOwnerRevision = snapshot.OwnerRevision,
            participants = new List<
                ProductionFacilityDestructiveDrainParticipantSaveData>
            {
                new()
                {
                    participantId = participant.ParticipantId,
                    contractVersion = participant.ContractVersion,
                    preparedContributionFingerprint = contribution,
                    expectedCurrentContributionFingerprint = contribution,
                    planFingerprint = plan.PlanFingerprint,
                    owners = new List<
                        ProductionFacilityDestructiveDrainOwnerSaveData>
                    {
                        acknowledged
                    }
                }
            }
        };
        GameplayEntityId entity = new(
            BuildingDemolitionOutcomeIds.FacilityKind,
            facilityId.Value);
        committer.RejectNextCommit = true;
        Require(
            !participant.TryFinalizeAfterWorldRemoval(
                terminal,
                out string rejectedFailure)
            && rejectedFailure ==
                "w02-injected-post-removal-ledger-rejection"
            && committer.CommitAttemptCount == 1
            && committer.SuccessfulCommitCount == 0
            && ledger.GetForEntity(
                entity,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 0,
            "post-removal ledger rejection created a false demolition result");
        Require(
            participant.TryFinalizeAfterWorldRemoval(
                terminal,
                out string retryFailure)
            && string.IsNullOrEmpty(retryFailure)
            && committer.CommitAttemptCount == 2
            && committer.SuccessfulCommitCount == 1
            && ledger.GetForEntity(
                entity,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1,
            "post-removal ledger retry did not commit exactly once: "
                + retryFailure);
    }

    private static void VerifyJournalRoundTripAndLegacyMigration(
        BuildingDemolitionDestructiveDrainParticipant demolition,
        ProductionFacilityDestructiveDrainPrepareContext context,
        ProductionFacilityDestructiveDrainOutcomeSnapshot snapshot)
    {
        IProductionFacilityDestructiveDrainParticipant[] participants =
            CreateParticipants(demolition);
        ProductionFacilityDestructiveDrainParticipantRegistry registry =
            new(participants);
        List<ProductionFacilityDestructiveDrainParticipantSaveData> rows =
            participants
                .Select(value => ToSaveRow(
                    context.OperationId,
                    value.Prepare(context)))
                .OrderBy(value => value.participantId, StringComparer.Ordinal)
                .ToList();
        string lifecycle = ProductionOutputDestinationDurableSaveProjector
            .ComposeAggregateFixture(
                context.FacilityId,
                rows.Select(value => new KeyValuePair<string, string>(
                    value.participantId,
                    value.preparedContributionFingerprint)));
        ProductionFacilityDestructiveDrainJournal source = new(
            new DungeonRuntimeAggregateRootStore(),
            registry);
        Require(
            source.TryRequest(
                context.Cause,
                context.FacilityId,
                ProductionFacilityDestructiveDrainCanonical
                    .BuildInitiatingMutationOperationId(
                        context.Cause,
                        context.FacilityId),
                lifecycle,
                snapshot,
                rows,
                out ProductionFacilityDestructiveDrainEntrySaveData created,
                out string failureReason),
            "demolition journal request failed: " + failureReason);
        DungeonProductionFacilityDestructiveDrainSaveData saved =
            source.Capture();
        ProductionFacilityDestructiveDrainJournal restored = new(
            new DungeonRuntimeAggregateRootStore(),
            registry);
        ProductionFacilityDestructiveDrainRestoreCandidate candidate =
            restored.BuildRestore(saved);
        restored.Restore(candidate);
        Require(
            restored.TryGet(context.OperationId, out var roundTripped)
            && roundTripped.OutcomeSnapshot.IsValid
            && roundTripped.OutcomeSnapshot.ComputeFingerprint(
                    context.FacilityId)
                == created.OutcomeSnapshot.ComputeFingerprint(
                    context.FacilityId),
            "demolition journal snapshot changed across save round trip");

        DungeonProductionFacilityDestructiveDrainSaveData legacy =
            saved;
        legacy.version = 4;
        legacy.registryFingerprint =
            ProductionFacilityDestructiveDrainParticipantRegistry
                .PreviousV4RegistryFingerprint;
        ProductionFacilityDestructiveDrainEntrySaveData legacyEntry =
            legacy.entries.Single();
        legacyEntry.participants.RemoveAll(value => value.participantId ==
            ProductionFacilityDestructiveDrainParticipantIds
                .BuildingDemolitionOutcome);
        legacyEntry.outcomeSnapshotVersion = 0;
        legacyEntry.outcomeBuildingDefinitionId = string.Empty;
        legacyEntry.outcomeBuildingDisplayName = string.Empty;
        legacyEntry.outcomePositionX = 0;
        legacyEntry.outcomePositionY = 0;
        legacyEntry.outcomeAbsoluteDay = 0;
        legacyEntry.outcomeOwnerRevision = 0L;
        DungeonProductionFacilityDestructiveDrainSaveData migrated =
            ProductionFacilityDestructiveDrainJournal.MigrateV4Payload(legacy);
        ProductionFacilityDestructiveDrainEntrySaveData migratedEntry =
            migrated.entries.Single();
        Require(
            migrated.version ==
                DungeonProductionFacilityDestructiveDrainSaveData.CurrentVersion
            && !migratedEntry.OutcomeSnapshot.IsPresent
            && migratedEntry.participants.Single(value => value.participantId ==
                ProductionFacilityDestructiveDrainParticipantIds
                    .BuildingDemolitionOutcome).owners.Count == 0,
            "legacy V4 demolition row invented narrative history");
    }

    private static ProductionFacilityDestructiveDrainOwnerSaveData Owner(
        ProductionFacilityDestructiveDrainOperationId operationId,
        string participantId,
        ProductionFacilityDestructiveDrainOwnerPlan source) => new()
    {
        ownerStableId = source.OwnerStableId,
        disposition = source.Disposition,
        targetDestinationId = source.TargetDestinationId,
        stepOperationId = ProductionFacilityDestructiveDrainCanonical
            .BuildStepOperationId(
                operationId,
                participantId,
                source.OwnerStableId),
        phase = ProductionFacilityDestructiveDrainStepPhase.Planned,
        requestFingerprint = source.RequestFingerprint,
        commitId = string.Empty,
        receiptFingerprint = string.Empty
    };

    private static ProductionFacilityDestructiveDrainParticipantSaveData
        ToSaveRow(
            ProductionFacilityDestructiveDrainOperationId operationId,
            ProductionFacilityDestructiveDrainParticipantPlan plan) => new()
        {
            participantId = plan.ParticipantId,
            contractVersion = plan.ContractVersion,
            preparedContributionFingerprint =
                plan.DurableContributionFingerprint,
            expectedCurrentContributionFingerprint =
                plan.DurableContributionFingerprint,
            planFingerprint = plan.PlanFingerprint,
            owners = plan.Owners.Select(value =>
                Owner(operationId, plan.ParticipantId, value)).ToList()
        };

    private static IProductionFacilityDestructiveDrainParticipant[]
        CreateParticipants(
            BuildingDemolitionDestructiveDrainParticipant demolition) =>
        new IProductionFacilityDestructiveDrainParticipant[]
        {
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .ApparelWorkOrders,
                1),
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .CapacityRoutingOutbox,
                1,
                new[]
                {
                    ProductionFacilityDestructiveDrainParticipantIds
                        .ApparelWorkOrders,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .CombatEquipmentCrafting,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .GenericProductionBills
                }),
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .CombatEquipmentCrafting,
                1),
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .GenericProductionBills,
                1),
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .PhysicalCustodyCarryRecovery,
                1,
                new[]
                {
                    ProductionFacilityDestructiveDrainParticipantIds
                        .CapacityRoutingOutbox
                }),
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .StockSensorEmbeddedSalvage,
                2,
                new[]
                {
                    ProductionFacilityDestructiveDrainParticipantIds
                        .PhysicalCustodyCarryRecovery
                }),
            new FakeParticipant(
                ProductionFacilityDestructiveDrainParticipantIds
                    .EnvironmentalFireDamageOutcome,
                1,
                new[]
                {
                    ProductionFacilityDestructiveDrainParticipantIds
                        .StockSensorEmbeddedSalvage
                }),
            demolition
        };

    private static void VerifyCheckpointGc(
        BuildingDemolitionDestructiveDrainParticipant participant,
        IBuildingDemolitionOutcomeCommitter bridge,
        ProductionFacilityDestructiveDrainStepContext planned,
        ProductionFacilityDestructiveDrainEntrySaveData terminal,
        ProductionFacilityDestructiveDrainCheckpointGcContext context,
        ProductionFacilityDestructiveDrainEntrySaveData eligible,
        GameplayOutcomeLedger ledger)
    {
        string before = JsonUtility.ToJson(ledger.CaptureGameplayOutcomes());
        var invalid = new IReadOnlyList<ProductionFacilityDestructiveDrainEntrySaveData>[]
        {
            null, new ProductionFacilityDestructiveDrainEntrySaveData[] { null },
            new[] { terminal }, new[] { eligible, eligible },
            new[] { new ProductionFacilityDestructiveDrainEntrySaveData
                { phase = ProductionFacilityDestructiveDrainPhase.WorldRemovedAwaitingCheckpointGc } }
        };
        foreach (var entries in invalid)
            Require(participant.PrepareCheckpointGarbageCollection(context, entries, out var rejected).Status
                    == ProductionFacilityDestructiveDrainCheckpointGcStatus.Corruption && rejected == null,
                "GC accepted a malformed or nonterminal demolition entry");
        Require(participant.PrepareCheckpointGarbageCollection(context, new[] { eligible }, out var candidate).Status
                == ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied
                && candidate.ParticipantId == participant.ParticipantId
                && candidate.OperationIds.SequenceEqual(new[] { eligible.operationId }),
            "GC did not capture the exact demolition candidate");
        Require(participant.PrepareCheckpointGarbageCollection(context, new[] { eligible }, out var overlap).Status
                == ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred && overlap == null,
            "GC replaced an active candidate");
        bool foreignRejected = false;
        try { new BuildingDemolitionDestructiveDrainParticipant(bridge).PublishCheckpointGarbageCollection(candidate); }
        catch (InvalidOperationException) { foreignRejected = true; }
        Require(foreignRejected, "Another participant instance accepted a foreign GC candidate");
        Require(participant.PublishCheckpointGarbageCollection(candidate).Status
                == ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied, "GC publication failed");
        participant.RollbackCheckpointGarbageCollection(candidate);
        participant.CompleteCheckpointGarbageCollection(candidate);
        bool staleRejected = false;
        try { participant.PublishCheckpointGarbageCollection(candidate); }
        catch (InvalidOperationException) { staleRejected = true; }
        Require(staleRejected && JsonUtility.ToJson(ledger.CaptureGameplayOutcomes()) == before,
            "GC accepted a stale candidate or changed canonical demolition history");

        Require(participant.PrepareCheckpointGarbageCollection(context, new[] { eligible }, out candidate).Status
                == ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied, "GC could not retry after rollback");
        Require(participant.TryPrepareDurable(planned, out _), "Could not create a late live receipt for GC rejection");
        Require(participant.PublishCheckpointGarbageCollection(candidate).Status
                == ProductionFacilityDestructiveDrainCheckpointGcStatus.Deferred,
            "GC publication ignored a newly prepared live demolition receipt");
        participant.RollbackCheckpointGarbageCollection(candidate);
        participant.CompleteCheckpointGarbageCollection(candidate);
        Require(participant.TryFinalizeAfterWorldRemoval(terminal, out _), "Late receipt could not complete its canonical replay");
        Require(participant.PrepareCheckpointGarbageCollection(context,
                Array.Empty<ProductionFacilityDestructiveDrainEntrySaveData>(), out candidate).Status
                == ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied, "Empty GC checkpoint was rejected");
        Require(participant.PublishCheckpointGarbageCollection(candidate).Status
                == ProductionFacilityDestructiveDrainCheckpointGcStatus.Applied, "Empty GC checkpoint publication failed");
        participant.CompleteCheckpointGarbageCollection(candidate);
        Require(JsonUtility.ToJson(ledger.CaptureGameplayOutcomes()) == before,
            "GC or canonical late receipt replay changed the durable demolition history");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RejectOnceBuildingDemolitionOutcomeCommitter :
        IBuildingDemolitionOutcomeCommitter
    {
        private readonly IBuildingDemolitionOutcomeCommitter inner;

        internal RejectOnceBuildingDemolitionOutcomeCommitter(
            IBuildingDemolitionOutcomeCommitter inner)
        {
            this.inner = inner
                ?? throw new ArgumentNullException(nameof(inner));
        }

        internal bool RejectNextCommit { get; set; }
        internal int CommitAttemptCount { get; private set; }
        internal int SuccessfulCommitCount { get; private set; }

        public bool TryPrepare(
            in BuildingDemolitionOutcomeReceipt receipt,
            out PreparedBuildingDemolitionOutcome prepared,
            out string failureReason) =>
            inner.TryPrepare(receipt, out prepared, out failureReason);

        public OwnerOutcomeCommitResult Commit(
            in PreparedBuildingDemolitionOutcome prepared)
        {
            CommitAttemptCount++;
            if (RejectNextCommit)
            {
                RejectNextCommit = false;
                return new OwnerOutcomeCommitResult(
                    OwnerOutcomeCommitPhase.Rejected,
                    prepared.ResultKey,
                    default,
                    string.Empty,
                    "w02-injected-post-removal-ledger-rejection");
            }

            OwnerOutcomeCommitResult result = inner.Commit(prepared);
            if (result.DurablyCommitted)
                SuccessfulCommitCount++;
            return result;
        }

        public OwnerOutcomeCommitResult Reconcile(
            GameplayResultKey resultKey) => inner.Reconcile(resultKey);

        public void Cancel(in PreparedBuildingDemolitionOutcome prepared) =>
            inner.Cancel(prepared);
    }

    private sealed class FakeParticipant :
        IProductionFacilityDestructiveDrainParticipant
    {
        internal FakeParticipant(
            string participantId,
            int contractVersion,
            IReadOnlyList<string> dependencies = null)
        {
            ParticipantId = participantId;
            ContractVersion = contractVersion;
            DependsOnParticipantIds = dependencies ?? Array.Empty<string>();
        }

        public string ParticipantId { get; }
        public int ContractVersion { get; }
        public IReadOnlyList<string> DependsOnParticipantIds { get; }

        public ProductionFacilityDestructiveDrainParticipantPlan Prepare(
            ProductionFacilityDestructiveDrainPrepareContext context)
        {
            string contribution = ProductionFacilityDestructiveDrainCanonical
                .ComputeFingerprint(
                    "qa:demolition-contribution:" + ParticipantId + ":"
                    + context.FacilityId.Value);
            CanonicalSemanticDigestBuilder plan = new();
            plan.Append("qa-building-demolition-fake-plan@1");
            plan.Append(ParticipantId);
            plan.Append(context.FacilityId.Value);
            return new ProductionFacilityDestructiveDrainParticipantPlan(
                ParticipantId,
                ContractVersion,
                contribution,
                plan.ComputeSha256(),
                Array.Empty<ProductionFacilityDestructiveDrainOwnerPlan>());
        }

        public ProductionFacilityDestructiveDrainStepResult TryCommit(
            ProductionFacilityDestructiveDrainStepContext context) =>
            throw new NotSupportedException();
        public ProductionFacilityDestructiveDrainStepResult TryAcknowledge(
            ProductionFacilityDestructiveDrainStepContext context) =>
            throw new NotSupportedException();
        public ProductionFacilityDestructiveDrainRecoveryResult Recover(
            ProductionFacilityDestructiveDrainStepContext context) =>
            throw new NotSupportedException();
    }
}
#endif
