#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class BuildingDemolitionOutcomeDebugScenarios
{
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
            "demolition outcome did not commit");

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
            "demolition outcome acknowledgement did not converge");

        GameplayEntityId facility = new(
            BuildingDemolitionOutcomeIds.FacilityKind,
            facilityId.Value);
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
        Require(
            replay.Status == ProductionFacilityDestructiveDrainStepStatus.Applied
            && ledger.GetForEntity(
                facility,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1,
            "demolition replay duplicated or rejected the canonical result");

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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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
