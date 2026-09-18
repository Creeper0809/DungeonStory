#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class OrganPreservationRestoreJoinFixture
{
    public static string Run()
    {
        const string partId="surgical-part:1", source="stack:canister:1"; string op="surgical-organ-preservation:"+partId; const long grams=600; string commit=$"physical-batch-disposition:3:{op}:1:{grams}";
        SurgicalPartInstance part=new(){partInstanceId=partId,kind=SurgicalPartKind.NaturalOrgan,preservationOperationId=op,preservationCommitId=commit,preservationSourceStackId=source,preservationInputMassGrams=grams};
        SurgeryAggregateState state=new(); state.Parts.Add(part);
        PhysicalItemRestoreCandidateDispositionSnapshot receipt=new(PhysicalItemDispositionKind.Sink,op,"organ-preservation-canister-consumed","fixture",new[]{source},1,grams,commit);
        SurgeryRestoreCoordinator.ValidatePreservationPhysicalJoin(state,new Query(receipt));
        Reject(state,new Query()); Reject(new SurgeryAggregateState(),new Query(receipt));
        Reject(state,new Query(new PhysicalItemRestoreCandidateDispositionSnapshot(PhysicalItemDispositionKind.Sink,op,"organ-preservation-canister-consumed","fixture",new[]{source},1,grams+1,commit)));
        VerifyAcknowledgementRecovery();
        VerifyFreshnessPhysicalBoundaries();
        VerifyOwnedExpiryAndManualDiscardBoundaries();
        return "valid/missing/orphan/mismatch joins, owned expiry release, and resumable manual discard are fail-closed";
    }

    static void VerifyFreshnessPhysicalBoundaries()
    {
        VerifyArrivalAndTemperatureBoundaries();
        VerifyInstallationFreshnessBoundary();
        VerifyExpiryTransformBoundaries();
    }

    static void VerifyArrivalAndTemperatureBoundaries()
    {
        using FreshnessFixture arrival = new(safeTemperature: true);
        string arrivingStack = arrival.AddStack(
            SurgeryItemDefinitions.GetOrganItemId("heart"),
            WorldItemStackState.Loose,
            FreshnessFixture.FacilityId,
            arrival.Position);
        SurgicalPartInstance arriving = arrival.AddNaturalPart(
            "surgical-part:014:arriving", arrivingStack, 30f);
        arrival.Runtime.TickFreshness(15f);
        Require(arriving.freshnessSeconds == 15f
            && !arriving.preservationCanisterApplied,
            "organ received preservation before its physical FacilityBuffer arrival");

        using FreshnessFixture chilled = new(safeTemperature: true);
        string chilledStack = chilled.AddStack(
            SurgeryItemDefinitions.GetOrganItemId("heart"),
            WorldItemStackState.FacilityBuffer,
            FreshnessFixture.FacilityId,
            chilled.Position);
        string chilledCanister = chilled.AddStack(
            "medical:organ-preservation-canister",
            WorldItemStackState.FacilityBuffer,
            FreshnessFixture.FacilityId,
            chilled.Position);
        chilled.SetFuelAvailable();
        SurgicalPartInstance cold = chilled.AddNaturalPart(
            "surgical-part:014:cold", chilledStack, 360f);
        chilled.Runtime.TickFreshness(15f);
        Require(Mathf.Approximately(cold.freshnessSeconds, 358f)
            && cold.preservationCanisterApplied
            && cold.storedFacilityId == FreshnessFixture.FacilityId
            && chilled.Repository.GetEditorTestQuantity(chilledCanister) == 0,
            "safe 2-8C FacilityBuffer storage did not consume one canister or use 2/15 freshness rate");

        using FreshnessFixture warm = new(safeTemperature: false);
        string warmStack = warm.AddStack(
            SurgeryItemDefinitions.GetOrganItemId("heart"),
            WorldItemStackState.FacilityBuffer,
            FreshnessFixture.FacilityId,
            warm.Position);
        string warmCanister = warm.AddStack(
            "medical:organ-preservation-canister",
            WorldItemStackState.FacilityBuffer,
            FreshnessFixture.FacilityId,
            warm.Position);
        warm.SetFuelAvailable();
        SurgicalPartInstance hot = warm.AddNaturalPart(
            "surgical-part:014:warm", warmStack, 360f);
        warm.Runtime.TickFreshness(15f);
        Require(hot.freshnessSeconds == 345f
            && !hot.preservationCanisterApplied
            && string.IsNullOrEmpty(hot.storedFacilityId)
            && warm.Repository.GetEditorTestQuantity(warmCanister) == 1,
            "unsafe FacilityBuffer temperature received organ preservation");
    }

    static void VerifyInstallationFreshnessBoundary()
    {
        const string orderId = "surgery:014";
        const string subjectId = "character:014";
        using FreshnessFixture stale = new(safeTemperature: true);
        SurgicalPartInstance unfit = stale.AddNaturalPart(
            "surgical-part:014:stale", "stack:014:stale", 0f, orderId);
        Require(!stale.Runtime.TryConsumeForInstallation(
                unfit.partInstanceId,
                orderId,
                subjectId,
                out _,
                out DomainFailure staleFailure)
            && staleFailure.Code == FailureCode.SurgeryCorpseStale,
            "stale natural organ passed the final installation boundary");

        using FreshnessFixture replay = new(safeTemperature: true);
        string source = replay.AddStack("material:lumber", WorldItemStackState.Loose);
        string partId = "surgical-part:014:installed";
        string operationId = SurgicalPartInstallationIdentity.FormatOperationId(
            orderId, partId);
        Require(replay.Batch.TryCommitPending(
                new[] { new PhysicalItemTransformInput(source, 1) },
                PhysicalItemDispositionKind.Transfer,
                operationId,
                SurgicalPartInstallationOutbox.TransferReason,
                out PhysicalItemBatchDispositionReceipt receipt,
                out string commitFailure),
            "could not stage installed organ replay fixture: " + commitFailure);
        SurgicalPartInstance installed = replay.AddNaturalPart(
            partId, source, 0f);
        installed.installed = true;
        installed.installedSubjectId = subjectId;
        installed.installationOrderId = orderId;
        installed.installationOperationId = operationId;
        installed.installationCommitId = receipt.CommitId;
        installed.installationSourceStackId = source;
        installed.installationSubjectId = subjectId;
        Require(replay.Runtime.TryConsumeForInstallation(
                partId,
                orderId,
                subjectId,
                out _,
                out DomainFailure replayFailure)
            && !replayFailure.IsFailure
            && replay.Repository.GetEditorPendingBatchDispositionCount() == 0,
            "committed installed organ replay was blocked by freshness");
    }

    static void VerifyExpiryTransformBoundaries()
    {
        using FreshnessFixture rejected = new(
            safeTemperature: true,
            transforms: new RejectingTransform());
        string rejectedStack = rejected.AddStack(
            SurgeryItemDefinitions.GetOrganItemId("heart"),
            WorldItemStackState.Loose);
        SurgicalPartInstance pending = rejected.AddNaturalPart(
            "surgical-part:014:retry", rejectedStack, 1f);
        rejected.Runtime.TickFreshness(2f);
        Require(pending.freshnessSeconds == 0f
            && rejected.Runtime.Parts.Contains(pending)
            && rejected.Repository.GetEditorTestQuantity(rejectedStack) == 1,
            "failed organ expiry transform removed protected source or part");

        using FreshnessFixture committed = new(safeTemperature: true);
        string committedStack = committed.AddStack(
            SurgeryItemDefinitions.GetOrganItemId("heart"),
            WorldItemStackState.Loose,
            position: committed.Position);
        committed.AddNaturalPart("surgical-part:014:expired", committedStack, 1f);
        committed.Runtime.TickFreshness(2f);
        Require(committed.Repository.GetEditorTestQuantity(committedStack) == 0
            && committed.Runtime.Parts.Count == 0
            && committed.Transform.WholeStackCalls == 1
            && committed.Transform.LastReceipt.IsCommitted
            && committed.Transform.LastReceipt.OutputQuantity == 1
            && committed.Transform.LastOutputs.Count == 1
            && committed.Transform.LastOutputs[0].ItemId
                == SurgeryItemDefinitions.ContaminatedTissueId
            && committed.Transform.LastOutputs[0].Position == committed.Position,
            "successful organ expiry transform was not whole-stack and terminal");
        committed.Runtime.TickFreshness(2f);
        Require(committed.Transform.WholeStackCalls == 1,
            "organ expiry transform duplicated its output on replay");
    }

    static void VerifyOwnedExpiryAndManualDiscardBoundaries()
    {
        string organItemId = SurgeryItemDefinitions.GetOrganItemId("heart");

        using (FreshnessFixture stored = new(safeTemperature: true))
        {
            string stackId = stored.AddStack(
                organItemId,
                WorldItemStackState.Stored,
                stored.WarehouseDestinationId,
                stored.WarehousePosition);
            stored.AddNaturalPart("surgical-part:1401", stackId, 1f);
            stored.Runtime.TickFreshness(2f);
            Require(stored.Repository.GetEditorTestQuantity(stackId) == 0
                && stored.Runtime.Parts.Count == 0
                && stored.Transform.WholeStackCalls == 1,
                "stored owned organ did not release its exact whole stack before expiry Transform");
        }

        using (FreshnessFixture bufferedRetry = new(
                   safeTemperature: true,
                   failFirstTransform: true))
        {
            string stackId = bufferedRetry.AddStack(
                organItemId,
                WorldItemStackState.FacilityBuffer,
                FreshnessFixture.FacilityId,
                bufferedRetry.Position);
            SurgicalPartInstance part = bufferedRetry.AddNaturalPart(
                "surgical-part:1402",
                stackId,
                1f);
            string instanceId = part.physicalItemInstanceId;
            bufferedRetry.SetFuelAvailable();
            bufferedRetry.Runtime.TickFreshness(2f);
            WorldItemStackSnapshot released = bufferedRetry.GetStack(stackId);
            Require(released != null
                && released.State == WorldItemStackState.Loose
                && string.IsNullOrEmpty(released.DestinationId)
                && released.Quantity == 1
                && released.ItemId == organItemId
                && released.ItemInstanceId == instanceId
                && part.freshnessSeconds == 0f
                && bufferedRetry.Runtime.Parts.Contains(part),
                "failed expiry Transform did not retain the same released stack for retry");
            bufferedRetry.Runtime.TickFreshness(2f);
            Require(bufferedRetry.Repository.GetEditorTestQuantity(stackId) == 0
                && bufferedRetry.Runtime.Parts.Count == 0
                && bufferedRetry.Transform.WholeStackCalls == 1,
                "released FacilityBuffer organ did not complete exactly once on Transform retry");
        }

        using (FreshnessFixture reserved = new(safeTemperature: true))
        {
            string stackId = reserved.AddStack(
                organItemId,
                WorldItemStackState.Stored,
                reserved.WarehouseDestinationId,
                reserved.WarehousePosition);
            SurgicalPartInstance part = reserved.AddNaturalPart(
                "surgical-part:1403",
                stackId,
                1f);
            WorldItemStackSnapshot stack = reserved.GetStack(stackId);
            Require(reserved.Reservations.TryReserve(
                    "qa:wim014:reserved",
                    "character:qa:wim014",
                    ItemReservationPurpose.Medical,
                    "qa:wim014:reserved",
                    new ItemQuantityReservationRequest(
                        (ItemStackId)stackId,
                        1,
                        stack.ReservationSignature),
                    out _,
                    out _),
                "could not reserve the WIM014 protected fixture stack");
            reserved.Runtime.TickFreshness(2f);
            SurgicalPartDiscardResult discard =
                reserved.Runtime.TryDiscardOwnedStack(stackId);
            WorldItemStackSnapshot retained = reserved.GetStack(stackId);
            Require(discard.Status == SurgicalPartDiscardStatus.Rejected
                && retained != null
                && retained.State == WorldItemStackState.Stored
                && retained.Quantity == 1
                && reserved.Runtime.Parts.Contains(part),
                "reserved owned organ was released or discarded");
        }

        using (FreshnessFixture medicalReserved = new(safeTemperature: true))
        {
            const string partId = "surgical-part:1403:medical-reservation";
            const string orderId = "surgery:1403:medical-reservation";
            string stackId = medicalReserved.AddStack(
                organItemId,
                WorldItemStackState.Loose,
                position: medicalReserved.Position);
            SurgicalPartInstance part = medicalReserved.AddNaturalPart(
                partId,
                stackId,
                1f);
            Require(medicalReserved.Runtime.TryReserveForOrder(
                    partId,
                    orderId,
                    out DomainFailure reserveFailure)
                && !reserveFailure.IsFailure,
                "could not create the medical-order reservation fixture");
            medicalReserved.Runtime.TickFreshness(2f);
            Require(part.freshnessSeconds == 0f
                && medicalReserved.Repository.GetEditorTestQuantity(stackId) == 1
                && medicalReserved.Runtime.Parts.Contains(part)
                && medicalReserved.Transform.WholeStackCalls == 0,
                "medical-order reserved organ transformed after reaching zero freshness");
            medicalReserved.Runtime.ReleaseReservation(partId, orderId);
            medicalReserved.Runtime.TickFreshness(1f);
            Require(medicalReserved.Repository.GetEditorTestQuantity(stackId) == 0
                && !medicalReserved.Runtime.Parts.Contains(part)
                && medicalReserved.Transform.WholeStackCalls == 1,
                "expired organ did not retry after its medical-order reservation released");
        }

        using (FreshnessFixture exact = new(safeTemperature: true))
        {
            string looseIdentityId = exact.AddStack(
                organItemId,
                WorldItemStackState.Loose,
                position: exact.Position);
            SurgicalPartInstance wrongLooseIdentity = exact.AddNaturalPart(
                "surgical-part:1408:loose",
                looseIdentityId,
                1f);
            wrongLooseIdentity.physicalItemInstanceId =
                "item-instance:wim014:foreign-loose";
            exact.Runtime.TickFreshness(2f);
            Require(exact.Repository.GetEditorTestQuantity(looseIdentityId) == 1
                && exact.Runtime.Parts.Contains(wrongLooseIdentity)
                && exact.Transform.WholeStackCalls == 0,
                "loose item-instance mismatch passed the common expiry identity boundary");

            string identityId = exact.AddStack(
                organItemId,
                WorldItemStackState.Stored,
                exact.WarehouseDestinationId,
                exact.WarehousePosition);
            SurgicalPartInstance wrongIdentity = exact.AddNaturalPart(
                "surgical-part:1408",
                identityId,
                1f);
            wrongIdentity.itemDefinitionId =
                SurgeryItemDefinitions.ContaminatedTissueId;
            wrongIdentity.physicalItemInstanceId =
                "item-instance:wim014:foreign";
            exact.Runtime.TickFreshness(2f);
            Require(exact.Runtime.TryDiscardOwnedStack(identityId).Status
                    == SurgicalPartDiscardStatus.Rejected
                && exact.GetStack(identityId)?.State
                    == WorldItemStackState.Stored
                && exact.Repository.GetEditorTestQuantity(identityId) == 1,
                "item/instance preflight mismatch mutated an owned organ");

            string quantityId = exact.AddStack(
                organItemId,
                WorldItemStackState.Stored,
                exact.WarehouseDestinationId,
                exact.WarehousePosition,
                quantity: 2);
            exact.AddNaturalPart("surgical-part:1409", quantityId, 1f);
            exact.Runtime.TickFreshness(2f);
            Require(exact.Runtime.TryDiscardOwnedStack(quantityId).Status
                    == SurgicalPartDiscardStatus.Rejected
                && exact.GetStack(quantityId)?.State
                    == WorldItemStackState.Stored
                && exact.Repository.GetEditorTestQuantity(quantityId) == 2,
                "non-whole quantity preflight mutated an owned organ lot");

            string foreignId = exact.AddStack(
                organItemId,
                WorldItemStackState.FacilityBuffer,
                "building:foreign-owner",
                exact.Position);
            exact.AddNaturalPart("surgical-part:1410", foreignId, 1f);
            exact.Runtime.TickFreshness(2f);
            Require(exact.Runtime.TryDiscardOwnedStack(foreignId).Status
                    == SurgicalPartDiscardStatus.Rejected
                && exact.GetStack(foreignId)?.State
                    == WorldItemStackState.FacilityBuffer
                && exact.Repository.GetEditorTestQuantity(foreignId) == 1,
                "foreign FacilityBuffer owner claim was released or discarded");
        }

        using (FreshnessFixture noOutbound = new(
                   safeTemperature: true,
                   outboundAvailable: false))
        {
            string stackId = noOutbound.AddStack(
                organItemId,
                WorldItemStackState.Stored,
                noOutbound.WarehouseDestinationId,
                noOutbound.WarehousePosition);
            SurgicalPartInstance part = noOutbound.AddNaturalPart(
                "surgical-part:1411",
                stackId,
                1f);
            noOutbound.Runtime.TickFreshness(2f);
            Require(noOutbound.GetStack(stackId)?.State
                    == WorldItemStackState.Stored
                && noOutbound.Repository.GetEditorTestQuantity(stackId) == 1
                && noOutbound.Runtime.Parts.Contains(part)
                && noOutbound.Transform.WholeStackCalls == 0,
                "missing legal outbound cell changed the owned organ state");
        }

        using (FreshnessFixture custody = new(safeTemperature: true))
        {
            string carriedId = custody.AddStack(
                organItemId,
                WorldItemStackState.Carried,
                position: custody.Position);
            SurgicalPartInstance carried = custody.AddNaturalPart(
                "surgical-part:1404",
                carriedId,
                1f);
            custody.Runtime.TickFreshness(2f);
            SurgicalPartDiscardResult carriedDiscard =
                custody.Runtime.TryDiscardOwnedStack(carriedId);
            Require(carriedDiscard.Status == SurgicalPartDiscardStatus.Rejected
                && custody.GetStack(carriedId)?.State
                    == WorldItemStackState.Carried
                && custody.Runtime.Parts.Contains(carried),
                "carried owned organ escaped custody through expiry/discard");

            IReadOnlyList<ItemInstanceComponentSaveData> preparedComponents =
                new[]
                {
                    FacilityOutputExactRouteEditorTestFactory
                        .CreateRoutableCustody(
                            "qa:wim014:batch",
                            "qa:wim014:outcome",
                            "qa:wim014:planned",
                            "organ",
                            "qa:wim014:line",
                            0,
                            1,
                            1,
                            800L,
                            organItemId,
                            "qa:wim014:components",
                            "qa:wim014:component-fingerprint",
                            FreshnessFixture.FacilityId,
                            FreshnessFixture.FacilityId,
                            "stack:qa:wim014:origin",
                            "stack:qa:wim014:current",
                            custody.Position,
                            0,
                            1,
                            800L,
                            "qa:wim014:route",
                            "qa:wim014:request",
                            "qa:wim014:receipt")
                };
            string preparedId = custody.AddStack(
                organItemId,
                WorldItemStackState.FacilityBuffer,
                FreshnessFixture.FacilityId,
                custody.Position,
                components: preparedComponents);
            SurgicalPartInstance prepared = custody.AddNaturalPart(
                "surgical-part:1405",
                preparedId,
                1f);
            custody.SetFuelAvailable();
            custody.Runtime.TickFreshness(2f);
            SurgicalPartDiscardResult preparedDiscard =
                custody.Runtime.TryDiscardOwnedStack(preparedId);
            Require(preparedDiscard.Status == SurgicalPartDiscardStatus.Rejected
                && custody.GetStack(preparedId)?.State
                    == WorldItemStackState.FacilityBuffer
                && custody.Runtime.Parts.Contains(prepared),
                "prepared-route owned organ escaped custody through expiry/discard");
        }

        using (FreshnessFixture manual = new(safeTemperature: true))
        {
            string ordinaryId = manual.AddStack(
                "material:lumber",
                WorldItemStackState.Loose,
                position: manual.Position);
            Require(manual.Runtime.TryDiscardOwnedStack(ordinaryId).Status
                    == SurgicalPartDiscardStatus.NotOwned
                && manual.Repository.GetEditorTestQuantity(ordinaryId) == 1,
                "non-medical stack did not return the only generic-delete fallthrough status");

            string stackId = manual.AddStack(
                organItemId,
                WorldItemStackState.Loose,
                position: manual.Position);
            manual.AddNaturalPart("surgical-part:1406", stackId, 20f);
            SurgicalPartDiscardResult completed =
                manual.Runtime.TryDiscardOwnedStack(stackId);
            Require(completed.Status == SurgicalPartDiscardStatus.Completed
                && manual.Repository.GetEditorTestQuantity(stackId) == 0
                && manual.Runtime.Parts.All(part =>
                    part.partInstanceId != "surgical-part:1406")
                && manual.Repository.GetEditorPendingBatchDispositionCount()
                    == 0,
                "manual medical discard did not complete Sink-terminal-ACK exactly once");
        }

        using (FreshnessFixture resume = new(
                   safeTemperature: true,
                   failFirstAcknowledgement: true))
        {
            string stackId = resume.AddStack(
                organItemId,
                WorldItemStackState.Loose,
                position: resume.Position);
            SurgicalPartInstance part = resume.AddNaturalPart(
                "surgical-part:1407",
                stackId,
                20f);
            SurgicalPartDiscardResult pending =
                resume.Runtime.TryDiscardOwnedStack(stackId);
            Require(pending.Status == SurgicalPartDiscardStatus.Pending
                && resume.Repository.GetEditorTestQuantity(stackId) == 0
                && resume.Runtime.Parts.Contains(part)
                && part.discardOutcomePublished
                && resume.Repository.GetEditorPendingBatchDispositionCount()
                    == 1,
                "manual medical discard acknowledgement failure lost pending ownership");

            DungeonSurgerySaveData surgery = JsonUtility.FromJson<
                DungeonSurgerySaveData>(JsonUtility.ToJson(
                resume.CaptureSurgerySave()));
            DungeonPhysicalItemSaveData physical = JsonUtility.FromJson<
                DungeonPhysicalItemSaveData>(JsonUtility.ToJson(
                resume.CapturePhysicalSave()));
            Require(surgery.version == DungeonSurgerySaveData.CurrentVersion
                && physical.version == DungeonPhysicalItemSaveData.CurrentVersion
                && surgery.parts.Single().discardOperationId
                    == part.discardOperationId
                && physical.pendingBatchDispositions.Count == 1,
                "current-format save did not capture manual discard owner and Sink receipt");
            PhysicalItemBatchDispositionSaveData savedReceipt =
                physical.pendingBatchDispositions.Single();
            SurgeryRestoreCoordinator.ValidateManualDiscardPhysicalJoin(
                new SurgeryAggregateState
                {
                    Parts = { SurgeryStateCloner.ClonePart(part) }
                },
                new Query(new PhysicalItemRestoreCandidateDispositionSnapshot(
                    (PhysicalItemDispositionKind)savedReceipt.kind,
                    savedReceipt.operationId,
                    savedReceipt.reasonCode,
                    savedReceipt.requestFingerprint,
                    savedReceipt.sourceStackIds,
                    savedReceipt.quantity,
                    savedReceipt.inputMassGrams,
                    savedReceipt.commitId)));
            SurgicalPartProductionOutputCrossAggregateSaveValidation
                .ValidatePartOwnership(
                    physical,
                    surgery,
                    new DungeonCharacterBodyHealthSaveData());

            using FreshnessFixture restored = new(
                safeTemperature: true,
                physicalRestore: physical,
                surgeryRestore: surgery);
            DungeonPhysicalItemSaveData restoredBeforeAck =
                restored.CapturePhysicalSave();
            PhysicalItemBatchDispositionSaveData restoredReceipt =
                restoredBeforeAck.pendingBatchDispositions.Single();
            SurgicalPartInstance restoredPart = restored.Runtime.Parts.Single();
            Require(restored.Repository.GetEditorTestQuantity(stackId) == 0
                && restored.Repository.GetEditorPendingBatchDispositionCount()
                    == 1
                && restoredReceipt.kind == savedReceipt.kind
                && restoredReceipt.quantity == savedReceipt.quantity
                && restoredReceipt.inputMassGrams
                    == savedReceipt.inputMassGrams
                && restoredReceipt.sourceStackIds.SequenceEqual(
                    savedReceipt.sourceStackIds)
                && restoredPart.discardOutcomePublished
                && restoredPart.discardOperationId == part.discardOperationId,
                "fresh physical/surgery authorities did not restore the exact pending Sink");
            restored.Runtime.TickFreshness(0f);
            Require(restored.Runtime.Parts.Count == 0
                && restored.Repository.GetEditorPendingBatchDispositionCount()
                    == 0
                && restored.Repository.GetEditorTestQuantity(stackId) == 0
                && restored.CapturePhysicalSave()
                    .pendingBatchDispositions.Count == 0,
                "restored manual medical discard did not resume ACK without duplicating Sink");
        }
    }

    sealed class FreshnessFixture : IDisposable
    {
        internal const string FacilityId = "building:qa-organ-storage";
        internal readonly Vector2Int Position = new(14, 9);
        internal readonly Vector2Int WarehousePosition = new(22, 9);

        readonly GameObject facilityObject;
        readonly GameObject warehouseObject;
        readonly SurgeryAggregateStateStore stateStore;
        readonly WorldItemQueryService itemQueries;
        readonly WorldItemPersistenceService physicalPersistence;
        int itemInstanceSequence;

        internal FreshnessFixture(
            bool safeTemperature,
            IPhysicalItemTransformService transforms = null,
            bool failFirstAcknowledgement = false,
            bool failFirstTransform = false,
            bool outboundAvailable = true,
            DungeonPhysicalItemSaveData physicalRestore = null,
            DungeonSurgerySaveData surgeryRestore = null)
        {
            ItemDefinitionSO[] definitions = Resources.LoadAll<ItemDefinitionSO>(
                ItemDefinitionSO.UnifiedResourcePath);
            IItemDefinitionCatalog itemCatalog = new ResourceItemDefinitionCatalog(
                definitions);
            IDungeonItemCatalogProvider dungeonCatalog =
                new ResourceDungeonItemCatalogProvider(itemCatalog);
            PhysicalItemMassQuery mass = new(dungeonCatalog);
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            physicalPersistence = new WorldItemPersistenceService(
                dungeonCatalog,
                new FixtureHaulingSettings(),
                Repository,
                EmptyFacilityOutputExactRouteOutboxPersistence.Instance);
            if (physicalRestore != null)
            {
                physicalPersistence.RestoreForEditorTest(physicalRestore);
            }
            Batch = new PhysicalItemBatchDispositionService(
                Repository,
                mass,
                EditorNullItemMarkerPresenter.Instance);
            WorldItemSpawner spawner = new(
                dungeonCatalog,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
            itemQueries = new WorldItemQueryService(
                dungeonCatalog,
                mass,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
            Transform = new RecordingTransform(new PhysicalItemTransformService(
                Repository,
                spawner,
                mass,
                dungeonCatalog,
                EditorNullItemMarkerPresenter.Instance));
            IPhysicalItemTransformService physicalTransforms = transforms
                ?? (failFirstTransform
                    ? new RejectOnceTransform(Transform)
                    : Transform);
            FacilityBufferDestinationClaimRegistry claims = new();
            FacilityBufferMassAdmissionService admission = new(
                claims,
                new EmptyOccupancy(),
                mass);
            FacilityBufferDestinationLifecycleService lifecycle = new(
                claims,
                claims,
                admission,
                admission);

            BuildingSO storageDefinition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/Medical/M08_장기보관함.asset");
            if (storageDefinition == null)
                throw new InvalidOperationException("QA organ storage definition is missing.");
            facilityObject = new GameObject("QA WIM014 Organ Storage");
            facilityObject.SetActive(false);
            BuildableObject storage = facilityObject.AddComponent<BuildableObject>();
            InjectFixtureBuilding(storage, Repository, dungeonCatalog, mass);
            storage.Initialization(storageDefinition, Position);

            BuildingSO warehouseDefinition =
                AssetDatabase.LoadAssetAtPath<BuildingSO>(
                    "Assets/Resources/SO/Building/Modular/L02_상자더미.asset");
            if (warehouseDefinition == null)
            {
                throw new InvalidOperationException(
                    "QA warehouse definition is missing.");
            }
            warehouseObject = new GameObject("QA WIM014 Warehouse");
            warehouseObject.SetActive(false);
            Warehouse = warehouseObject.AddComponent<Facility>();
            InjectFixtureBuilding(Warehouse, Repository, dungeonCatalog, mass);
            Warehouse.Initialization(warehouseDefinition, WarehousePosition);
            WarehouseDestinationId =
                WarehouseStorageIdentity.RequireDestinationId(Warehouse);

            IWorldItemStackRuntime items = CreateProxy<IWorldItemStackRuntime>(
                (method, _) => method.Name == "GetAllStacks"
                    ? itemQueries.GetAllStacks()
                    : method.Name == "get_CatalogProvider"
                        ? dungeonCatalog
                    : DefaultValue(method.ReturnType));
            IBuildingWorldQuery buildings = CreateProxy<IBuildingWorldQuery>(
                (method, _) => method.Name == "get_Buildings"
                    ? new[] { storage }
                    : DefaultValue(method.ReturnType));
            ISurgicalFacilityQuery facilities = CreateProxy<ISurgicalFacilityQuery>(
                (method, _) => method.Name == "GetFacilityId"
                    ? FacilityId
                    : DefaultValue(method.ReturnType));
            IEnvironmentalFieldQuery environment =
                CreateProxy<IEnvironmentalFieldQuery>((method, _) =>
                    method.Name == "IsOrganPreservationSafe"
                        ? safeTemperature
                        : DefaultValue(method.ReturnType));
            IAnatomyProfileCatalog anatomy = CreateProxy<IAnatomyProfileCatalog>(
                (method, _) => DefaultValue(method.ReturnType));
            IGameClock clock = CreateProxy<IGameClock>(
                (method, _) => DefaultValue(method.ReturnType));
            Reservations = new ItemQuantityReservationService(
                Repository,
                EditorNullItemMarkerPresenter.Instance,
                clock);
            IItemReservationService legacyReservations =
                new ItemReservationService(
                    Repository,
                    EditorNullItemMarkerPresenter.Instance,
                    Reservations);
            IBufferStackAggregationService aggregation =
                new BufferStackAggregationService(
                    dungeonCatalog,
                    Repository,
                    EditorNullItemMarkerPresenter.Instance,
                    Reservations,
                    Reservations);
            IGridSystemProvider gridProvider = outboundAvailable
                ? new FixedGridProvider(CreateWalkableGrid())
                : new NoGridProvider();
            ICharacterAiWorldRegistry worldRegistry =
                CreateProxy<ICharacterAiWorldRegistry>((method, _) =>
                    method.Name == "get_Warehouses"
                        ? new IWarehouseFacility[] { Warehouse }
                        : DefaultValue(method.ReturnType));
            WorldItemWarehouseService warehouseService = new(
                dungeonCatalog,
                Repository,
                worldRegistry,
                spawner,
                EditorNullItemMarkerPresenter.Instance,
                gridProvider,
                CreateProxy<ICharacterIdRegistry>((method, _) =>
                    DefaultValue(method.ReturnType)),
                legacyReservations,
                Reservations,
                facilityBufferMassAdmission: admission,
                facilityBufferDestinationClaims: claims);
            IItemTransferService itemTransfers = new ItemTransferService(
                new WorldItemReadServices(
                    dungeonCatalog,
                    mass,
                    CreateProxy<IItemHaulingSettingsProvider>((method, _) =>
                        DefaultValue(method.ReturnType)),
                    itemQueries,
                    EditorNullItemMarkerPresenter.Instance,
                    CreateProxy<ICharacterAiPerformanceRecorder>((method, _) =>
                        DefaultValue(method.ReturnType)),
                    DisabledDungeonDebugRuleQuery.Instance,
                    CreateProxy<IFacilityOutputClearanceTelemetrySink>(
                        (method, _) => DefaultValue(method.ReturnType))),
                CreateProxy<ICharacterIdRegistry>((method, _) =>
                    DefaultValue(method.ReturnType)),
                gridProvider,
                worldRegistry,
                claims,
                CreateProxy<ICombatEquipmentCatalog>((method, _) =>
                    DefaultValue(method.ReturnType)),
                new GameEventBus(),
                Repository,
                spawner,
                warehouseService,
                Reservations,
                Reservations,
                aggregation,
                new DeterministicPhysicalItemRelocationOutcomeFixture()
                    .CreateService(
                        Repository,
                        mass,
                        dungeonCatalog,
                        EditorNullItemMarkerPresenter.Instance),
                facilityBufferMassAdmission: admission);
            stateStore = new SurgeryAggregateStateStore(
                new DungeonRuntimeAggregateRootStore());
            if (surgeryRestore != null)
            {
                if (physicalRestore == null)
                {
                    throw new InvalidOperationException(
                        "QA surgery restore requires its physical save authority.");
                }
                SurgeryAggregateState restoredState =
                    SurgerySaveValidation.CreateState(surgeryRestore);
                Query physicalCandidate = CreatePhysicalCandidateQuery(
                    physicalRestore);
                SurgeryRestoreCoordinator.ValidatePreservationPhysicalJoin(
                    restoredState,
                    physicalCandidate);
                SurgeryRestoreCoordinator.ValidateManualDiscardPhysicalJoin(
                    restoredState,
                    physicalCandidate);
                SurgicalPartProductionOutputCrossAggregateSaveValidation
                    .ValidatePartOwnership(
                        physicalRestore,
                        surgeryRestore,
                        new DungeonCharacterBodyHealthSaveData());
                stateStore.Replace(restoredState);
            }
            IPhysicalItemBatchDispositionService runtimeDispositions =
                failFirstAcknowledgement
                    ? new FailOnce(Batch) { FailNext = true }
                    : Batch;
            Runtime = new SurgicalPartRuntime(
                items,
                itemTransfers,
                buildings,
                facilities,
                environment,
                anatomy,
                clock,
                stateStore,
                runtimeDispositions,
                physicalTransforms,
                itemCatalog,
                mass,
                claims,
                admission,
                lifecycle,
                NoopRelease.Instance);
        }

        internal WorldItemRepository Repository { get; }
        internal PhysicalItemBatchDispositionService Batch { get; }
        internal ItemQuantityReservationService Reservations { get; }
        internal SurgicalPartRuntime Runtime { get; }
        internal RecordingTransform Transform { get; }
        internal Facility Warehouse { get; }
        internal string WarehouseDestinationId { get; }

        internal string AddStack(
            string itemId,
            WorldItemStackState state,
            string destinationId = "",
            Vector2Int position = default,
            int quantity = 1,
            string sourceStorageDestinationId = "",
            IReadOnlyList<ItemInstanceComponentSaveData> components = null)
        {
            string itemInstanceId = "item-instance:wim014:"
                + (++itemInstanceSequence).ToString(
                    CultureInfo.InvariantCulture);
            string stackId = WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                itemId,
                quantity,
                state,
                destinationId: destinationId,
                sourceStorageDestinationId: sourceStorageDestinationId,
                position: position,
                itemInstanceId: itemInstanceId,
                components: components);
            return stackId;
        }

        internal SurgicalPartInstance AddNaturalPart(
            string partId,
            string stackId,
            float freshnessSeconds,
            string reservedOrderId = "")
        {
            WorldItemStackSnapshot physical = itemQueries.GetAllStacks()
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.StackId,
                        stackId,
                        StringComparison.Ordinal));
            SurgicalPartInstance part = new()
            {
                partInstanceId = partId,
                itemDefinitionId = physical?.ItemId
                    ?? SurgeryItemDefinitions.GetOrganItemId("heart"),
                physicalItemInstanceId = physical?.ItemInstanceId
                    ?? "item-instance:wim014:detached",
                kind = SurgicalPartKind.NaturalOrgan,
                nodeId = "heart",
                displayName = "QA heart",
                freshnessSeconds = freshnessSeconds,
                worldStackId = stackId,
                reservedOrderId = reservedOrderId
            };
            stateStore.State.Parts.Add(part);
            return part;
        }

        internal WorldItemStackSnapshot GetStack(string stackId) =>
            itemQueries.GetAllStacks().SingleOrDefault(value => value != null
                && string.Equals(
                    value.StackId,
                    stackId,
                    StringComparison.Ordinal));

        internal DungeonSurgerySaveData CaptureSurgerySave() =>
            new SurgeryPersistence(stateStore).Capture();

        internal DungeonPhysicalItemSaveData CapturePhysicalSave() =>
            physicalPersistence.Capture();

        internal void SetFuelAvailable() => stateStore.State.OrganStorage[FacilityId] =
            new SurgicalOrganStorageState
            {
                facilityId = FacilityId,
                fuelSecondsRemaining = 360f
            };

        public void Dispose()
        {
            if (facilityObject != null)
                UnityEngine.Object.DestroyImmediate(facilityObject);
            if (warehouseObject != null)
                UnityEngine.Object.DestroyImmediate(warehouseObject);
        }
    }

    static void InjectFixtureBuilding(
        BuildableObject building,
        WorldItemRepository repository,
        IDungeonItemCatalogProvider catalog,
        IPhysicalItemMassQuery mass)
    {
        building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        building.ConstructBuildableObject(
            CreateProxy<IBuildingResearchWorkPort>((method, _) =>
                DefaultValue(method.ReturnType)),
            CreateProxy<IBuildingFacilityStateChangePort>((method, _) =>
                DefaultValue(method.ReturnType)),
            CreateProxy<IBuildingRoomPolicyPort>((method, _) =>
                DefaultValue(method.ReturnType)),
            combatEquipmentRuntime: null,
            worldRegistry: null,
            worldItemStackRuntime: null,
            abilityRuntimeDispatcher: null,
            gameClock: null,
            paidFacilityContracts: null,
            evolutionState: CreateProxy<IBuildingEvolutionStatePort>(
                (method, _) => DefaultValue(method.ReturnType)));
        if (building is Facility facility)
        {
            facility.ConstructFacility(
                roomEnvironmentExperienceService: null,
                stockQuery: new PhysicalStockQuery(
                    repository,
                    catalog,
                    mass),
                mealConsumptionRuntime: null,
                waterFixtureUseRuntime: null,
                wastewaterNetworkRuntime: CreateProxy<IFluidWastewaterTransaction>(
                    (method, _) => DefaultValue(method.ReturnType)),
                serviceSessionRuntime: null,
                serviceRoomLinkRuntime: null,
                stockCategoryCatalog: CreateProxy<IStockCategoryDefinitionCatalog>(
                    (method, _) => DefaultValue(method.ReturnType)));
        }
    }

    static Query CreatePhysicalCandidateQuery(
        DungeonPhysicalItemSaveData physical) => new(
        (physical?.pendingBatchDispositions
            ?? new List<PhysicalItemBatchDispositionSaveData>())
        .Where(value => value != null)
        .Select(value => new PhysicalItemRestoreCandidateDispositionSnapshot(
            (PhysicalItemDispositionKind)value.kind,
            value.operationId,
            value.reasonCode,
            value.requestFingerprint,
            value.sourceStackIds,
            value.quantity,
            value.inputMassGrams,
            value.commitId))
        .ToArray());

    sealed class FixtureHaulingSettings : IItemHaulingSettingsProvider
    {
        float multiplier = CharacterCarryTuning.DefaultMaxCarryMultiplier;

        public float MaxCarryMultiplier => multiplier;

        public ItemHaulingSettingsSnapshot Capture() => new()
        {
            maxCarryMultiplier = multiplier
        };

        public void Restore(ItemHaulingSettingsSnapshot snapshot)
        {
            snapshot?.Normalize();
            multiplier = snapshot?.maxCarryMultiplier
                ?? CharacterCarryTuning.DefaultMaxCarryMultiplier;
        }
    }

    sealed class EmptyOccupancy : IFacilityBufferPhysicalOccupancyQuery
    {
        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(0L, 0L);

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "qa-no-physical-lot";
            return false;
        }
    }

    sealed class NoopRelease : IFacilityBufferDestinationReleaseService
    {
        internal static readonly NoopRelease Instance = new();

        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            releasedQuantity = 0;
            failureReason = "qa-no-release";
            return false;
        }
    }

    sealed class FixedGridProvider : IGridSystemProvider
    {
        readonly Grid grid;

        internal FixedGridProvider(Grid grid) =>
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));

        public GridSystemManager Manager => null;
        public Grid Grid => grid;

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid resolved)
        {
            resolved = grid;
            return true;
        }
    }

    static Grid CreateWalkableGrid()
    {
        Grid grid = new(32, 16);
        for (int y = 0; y < grid.height; y++)
        {
            for (int x = 0; x < grid.width; x++)
            {
                grid.SetAreaType(
                    new Vector2Int(x, y),
                    GridCellAreaType.ExteriorPath);
            }
        }
        return grid;
    }

    sealed class NoGridProvider : IGridSystemProvider
    {
        public GridSystemManager Manager => null;
        public Grid Grid => null;

        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }

        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
    }

    sealed class RejectOnceTransform : IPhysicalItemTransformService
    {
        readonly IPhysicalItemTransformService inner;
        bool rejectNext = true;

        internal RejectOnceTransform(IPhysicalItemTransformService inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public bool TryTransformWholeStack(
            string sourceStackId,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason)
        {
            if (rejectNext)
            {
                rejectNext = false;
                receipt = default;
                failureCode =
                    PhysicalItemTransformFailureCode.OutputCommitFailed;
                failureReason = "qa-first-transform-rejected";
                return false;
            }
            return inner.TryTransformWholeStack(
                sourceStackId,
                outputs,
                operationId,
                reasonCode,
                out receipt,
                out failureCode,
                out failureReason);
        }

        public bool TryTransformQuantity(
            string sourceStackId,
            int sourceQuantity,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason) => inner.TryTransformQuantity(
                sourceStackId,
                sourceQuantity,
                outputs,
                operationId,
                reasonCode,
                out receipt,
                out failureCode,
                out failureReason);

        public bool TryTransformQuantities(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason) => inner.TryTransformQuantities(
                inputs,
                outputs,
                operationId,
                reasonCode,
                out receipt,
                out failureCode,
                out failureReason);
    }

    sealed class RejectingTransform : IPhysicalItemTransformService
    {
        public bool TryTransformWholeStack(
            string sourceStackId,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason) => Reject(
                out receipt,
                out failureCode,
                out failureReason);

        public bool TryTransformQuantity(
            string sourceStackId,
            int sourceQuantity,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason) => Reject(
                out receipt,
                out failureCode,
                out failureReason);

        public bool TryTransformQuantities(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason) => Reject(
                out receipt,
                out failureCode,
                out failureReason);

        static bool Reject(
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason)
        {
            receipt = default;
            failureCode = PhysicalItemTransformFailureCode.OutputCommitFailed;
            failureReason = "qa-transform-rejected";
            return false;
        }
    }

    internal sealed class RecordingTransform : IPhysicalItemTransformService
    {
        readonly IPhysicalItemTransformService inner;

        internal RecordingTransform(IPhysicalItemTransformService inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal int WholeStackCalls { get; private set; }
        internal PhysicalItemTransformReceipt LastReceipt { get; private set; }
        internal IReadOnlyList<PhysicalItemTransformOutput> LastOutputs { get; private set; }
            = Array.Empty<PhysicalItemTransformOutput>();

        public bool TryTransformWholeStack(
            string sourceStackId,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason)
        {
            WholeStackCalls++;
            LastOutputs = (outputs ?? Array.Empty<PhysicalItemTransformOutput>())
                .ToArray();
            bool transformed = inner.TryTransformWholeStack(
                sourceStackId,
                outputs,
                operationId,
                reasonCode,
                out receipt,
                out failureCode,
                out failureReason);
            if (transformed)
                LastReceipt = receipt;
            return transformed;
        }

        public bool TryTransformQuantity(
            string sourceStackId,
            int sourceQuantity,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason) => inner.TryTransformQuantity(
                sourceStackId,
                sourceQuantity,
                outputs,
                operationId,
                reasonCode,
                out receipt,
                out failureCode,
                out failureReason);

        public bool TryTransformQuantities(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            IReadOnlyList<PhysicalItemTransformOutput> outputs,
            string operationId,
            string reasonCode,
            out PhysicalItemTransformReceipt receipt,
            out PhysicalItemTransformFailureCode failureCode,
            out string failureReason) => inner.TryTransformQuantities(
                inputs,
                outputs,
                operationId,
                reasonCode,
                out receipt,
                out failureCode,
                out failureReason);
    }

    static T CreateProxy<T>(Func<MethodInfo, object[], object> handler)
        where T : class
    {
        T proxy = DispatchProxy.Create<T, ConfigurableDispatchProxy>();
        ((ConfigurableDispatchProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class ConfigurableDispatchProxy : DispatchProxy
    {
        public Func<MethodInfo, object[], object> Handler { get; set; }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].ParameterType.IsByRef)
                    args[index] = DefaultValue(
                        parameters[index].ParameterType.GetElementType());
            }
            return Handler?.Invoke(targetMethod, args)
                ?? DefaultValue(targetMethod.ReturnType);
        }
    }

    static object DefaultValue(Type type) => type == typeof(void)
        ? null
        : type != null && type.IsValueType
            ? Activator.CreateInstance(type)
            : null;

    static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    static void VerifyAcknowledgementRecovery()
    {
        IDungeonItemCatalogProvider catalog=EditorItemCatalogFactory.Create();
        WorldItemRepository repository=new(new GuidPersistentIdGenerator(),new DungeonRuntimeAggregateRootStore());
        PhysicalItemBatchDispositionService inner=new(repository,new PhysicalItemMassQuery(catalog),EditorNullItemMarkerPresenter.Instance);
        FailOnce dispositions=new(inner){FailNext=true};
        string stack=WorldItemRepositoryEditorAccess.AddStack(repository,"medical:organ-preservation-canister",2,WorldItemStackState.FacilityBuffer,position:new Vector2Int(2,2),destinationId:"building:organ-storage");
        SurgicalPartInstance part=new(){partInstanceId="surgical-part:9",kind=SurgicalPartKind.NaturalOrgan};
        string operation=SurgicalOrganPreservationOutbox.FormatOperationId(part.partInstanceId);
        if(!dispositions.TryCommitPending(new[]{new PhysicalItemTransformInput(stack,1)},PhysicalItemDispositionKind.Sink,operation,SurgicalOrganPreservationOutbox.ReasonCode,out PhysicalItemBatchDispositionReceipt receipt,out _))throw new InvalidOperationException("Could not stage organ canister Sink.");
        SurgicalOrganPreservationOutbox.Record(part,receipt);
        if(SurgicalOrganPreservationOutbox.TryFinalize(part,dispositions,out _)||!part.preservationCanisterApplied)throw new InvalidOperationException("Injected organ acknowledgement failure did not preserve outcome.");
        SurgicalPartInstance restored=SurgeryStateCloner.ClonePart(part);
        if(!SurgicalOrganPreservationOutbox.TryFinalize(restored,dispositions,out _)||repository.GetEditorTestQuantity(stack)!=1||SurgicalOrganPreservationOutbox.HasPending(restored))throw new InvalidOperationException("Organ preservation recovery duplicated Sink or retained pending state.");
    }
    static void Reject(SurgeryAggregateState state,Query query){try{SurgeryRestoreCoordinator.ValidatePreservationPhysicalJoin(state,query);}catch(InvalidOperationException){return;}throw new InvalidOperationException("Organ preservation join accepted invalid provenance.");}
    sealed class Query:IPhysicalItemRestoreCandidateQuery{readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> values;public Query(params PhysicalItemRestoreCandidateDispositionSnapshot[] values)=>this.values=values;public bool IsCandidateAvailable=>true;public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> PendingBatchDispositions=>values;public bool TryGetPendingBatchDisposition(string id,out PhysicalItemRestoreCandidateDispositionSnapshot value){foreach(var item in values)if(item.OperationId==id){value=item;return true;}value=null;return false;}}
    sealed class FailOnce:IPhysicalItemBatchDispositionService{readonly IPhysicalItemBatchDispositionService inner;public bool FailNext;public FailOnce(IPhysicalItemBatchDispositionService inner)=>this.inner=inner;public bool TryCommit(IReadOnlyList<PhysicalItemTransformInput> i,PhysicalItemDispositionKind k,string o,string r,out PhysicalItemBatchDispositionReceipt x,out string f)=>inner.TryCommit(i,k,o,r,out x,out f);public bool TryCommitPending(IReadOnlyList<PhysicalItemTransformInput> i,PhysicalItemDispositionKind k,string o,string r,out PhysicalItemBatchDispositionReceipt x,out string f)=>inner.TryCommitPending(i,k,o,r,out x,out f);public bool TryGetPending(string o,out PhysicalItemBatchDispositionReceipt x)=>inner.TryGetPending(o,out x);public bool Acknowledge(string c,out string f){if(FailNext){FailNext=false;f="injected";return false;}return inner.Acknowledge(c,out f);}}
}
#endif
