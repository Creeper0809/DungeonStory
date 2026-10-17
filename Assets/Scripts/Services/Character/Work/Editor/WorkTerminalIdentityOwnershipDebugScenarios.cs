#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;

public static class WorkTerminalIdentityOwnershipDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Work/Verify Terminal Identity Ownership")]
    public static void RunFromMenu()
    {
        RunOrThrow();
        Debug.Log("Work terminal identity ownership PASS.");
    }

    public static void RunOrThrow()
    {
        GameEventBus bus = new();
        CharacterIdentityEventPublisher publisher = new(bus);
        List<WorkCompletedIdentityEvent> observed = new();
        using IDisposable subscription = bus.Subscribe<WorkCompletedIdentityEvent>(
            observed.Add);

        VerifyCropHarvest(publisher, observed);
        VerifyGenericWhenDomainDidNotPublish(publisher, observed);
        VerifyEnrichedGenericReference(publisher, observed);
        RunCompletionObserverFocused();
    }

    public static void RunCompletionObserverFocused()
    {
        VerifySurgeryFrozenCompletionInputsAreRejected();
        VerifyMalformedSurgeryCompletionProofIsRejected();
        VerifySurgeryUncommittedOutcomeIsRejected();
        VerifySurgeryMissingObserverAndReconnect();
        VerifySurgeryDeferredRestore();
        VerifySurgeryClinicalFailure();
        VerifySurgeryKnownConflict();
        VerifySurgeryUnknownAcknowledgementRestore();
        VerifySurgeryNotificationFailure();
    }

    private static void VerifyCropHarvest(
        CharacterIdentityEventPublisher publisher,
        List<WorkCompletedIdentityEvent> observed)
    {
        CropHarvestOutputSaveData pending = new()
        {
            operationId = "crop-harvest:building:w05-plot:0",
            operationSequence = 0,
            harvesterId = "character:w05-harvester",
            outcomeId = "normal",
            completionAbsoluteDay = 4
        };
        WorkCompletionIdentityDeliveryRequest request =
            CropHarvestCompletionObservation.CreateSavedRequest(
                new BuildingInstanceId("building:w05-plot"),
                pending,
                new CropCycleExecutionReceiptSaveData
                {
                    plotId = "building:w05-plot",
                    correlationId = "crop-cycle:w05-plot",
                    harvestContributions = new List<CraftContributionSaveData>
                    {
                        new()
                        {
                            characterId = pending.harvesterId,
                            contributedWork = 2f
                        }
                    }
                });
        WorkCompletionIdentityDeliveryLedger cursor = new();
        Require(
            cursor.Commit(request, out _) ==
                WorkCompletionIdentityDeliveryStatus.Applied,
            "crop harvest terminal delivery was not accepted");
        publisher.Publish(request.ToEvent());
        PublishGenericIfUnowned(
            publisher,
            true,
            request.Character,
            BuiltInWorkTypeIds.Harvest.Value,
            string.Empty);
        Require(
            observed.Count == 1
            && observed[0].WorkId == BuiltInWorkTypeIds.Harvest.Value
            && observed[0].ProductId == "building:w05-plot:normal"
            && cursor.Inspect(request, out _) ==
                WorkCompletionIdentityDeliveryStatus.AlreadyApplied,
            "crop harvest generated a second generic terminal identity");
    }

    private static void VerifySurgeryFrozenCompletionInputsAreRejected()
    {
        using CompletionFixture fixture = new();
        SurgeryOrder missingDoctor = CreateCommittedOrder(
            "surgery:5004",
            true,
            CharacterCommandOrigin.Autonomous,
            1f);
        missingDoctor.completionDoctorId = string.Empty;
        RequireRejected(
            () => fixture.TryDeliver(missingDoctor),
            "missing frozen surgery completion doctor was accepted");

        SurgeryOrder negativeDay = CreateCommittedOrder(
            "surgery:5005",
            true,
            CharacterCommandOrigin.Autonomous,
            1f);
        negativeDay.completionAbsoluteDay = -1;
        RequireRejected(
            () => fixture.TryDeliver(negativeDay),
            "negative frozen surgery completion day was accepted");
        Require(fixture.Observed.Count == 0,
            "invalid frozen surgery inputs emitted an identity event");
    }

    private static void VerifyMalformedSurgeryCompletionProofIsRejected()
    {
        using CompletionFixture fixture = new();
        SurgeryOrder order = CreateCommittedOrder(
            "surgery:5006",
            true,
            CharacterCommandOrigin.Autonomous,
            1f);
        order.completionFactDelivered = true;
        RequireRejected(
            () => fixture.TryDeliver(order),
            "surgery delivered flag without completion proof was accepted");
        Require(fixture.Observed.Count == 0,
            "malformed surgery completion proof emitted an identity event");
    }

    private static void VerifySurgeryUncommittedOutcomeIsRejected()
    {
        using CompletionFixture fixture = new();
        SurgeryOrder order = CreateCommittedOrder(
            "surgery:5000",
            true,
            CharacterCommandOrigin.Autonomous,
            1f);
        order.resolutionFactAccepted = false;
        order.resolutionFactDelivered = false;
        order.resolutionPublicationId = string.Empty;
        order.resolutionFingerprint = string.Empty;
        RequireRejected(
            () => fixture.TryDeliver(order),
            "uncommitted surgery outcome published a terminal identity");
        Require(!order.completionFactDelivered
            && string.IsNullOrEmpty(order.completionPublicationKey)
            && fixture.Observed.Count == 0,
            "uncommitted surgery outcome changed completion authority");
    }

    private static void VerifySurgeryMissingObserverAndReconnect()
    {
        SurgeryOrder missing = CreateCommittedOrder(
            "surgery:5007",
            true,
            CharacterCommandOrigin.Autonomous,
            2f);
        using (DomainFactDispatcher dispatcher = new())
        {
            Require(
                !SurgeryWorkCompletionPublication.TryDeliver(
                    dispatcher,
                    dispatcher,
                    missing)
                && !missing.completionFactDelivered
                && string.IsNullOrEmpty(missing.completionPublicationKey),
                "missing surgery completion observer accepted a fact");
        }

        using CompletionFixture fixture = new();
        fixture.Disconnect();
        SurgeryOrder disconnected = CreateCommittedOrder(
            "surgery:5008",
            true,
            CharacterCommandOrigin.Autonomous,
            2f);
        Require(!fixture.TryDeliver(disconnected)
            && !disconnected.completionFactDelivered
            && string.IsNullOrEmpty(disconnected.completionPublicationKey)
            && fixture.Observed.Count == 0,
            "disconnected surgery completion observer accepted a fact");
        fixture.Reconnect();
        Require(fixture.TryDeliver(disconnected)
            && disconnected.completionFactDelivered
            && fixture.Observed.Count == 1,
            "reconnected surgery completion observer did not accept the fact");
    }

    private static void VerifySurgeryDeferredRestore()
    {
        SurgeryOrder order = CreateCommittedOrder(
            "surgery:5001",
            true,
            CharacterCommandOrigin.DirectPlayerOrder,
            12.5f);
        string frozenResultHash = SurgeryResolvedFactMapping.ComputeContextSha256(
            order.resolvedFact);
        using (CompletionFixture deferred = new(
                   statuses: new[]
                   {
                       WorkCompletionIdentityDeliveryStatus.Deferred
                   }))
        {
            Require(!deferred.TryDeliver(order)
                && !order.completionFactDelivered
                && !string.IsNullOrWhiteSpace(order.completionPublicationKey)
                && string.IsNullOrEmpty(order.completionPublicationId)
                && string.IsNullOrEmpty(order.completionFingerprint)
                && order.statusData.code == SurgeryStatusCode.ProcedurePaused
                && order.statusData.primaryId.StartsWith(
                    "completion-observation-incomplete:",
                    StringComparison.Ordinal)
                && deferred.Observed.Count == 0,
                "deferred surgery delivery changed the terminal publication state");
        }

        SurgeryOrder restored = JsonUtility.FromJson<SurgeryOrder>(
            JsonUtility.ToJson(SurgeryStateCloner.CloneOrder(order)));
        using CompletionFixture applied = new(
            statuses: new[] { WorkCompletionIdentityDeliveryStatus.Applied });
        Require(applied.TryDeliver(restored),
            "restored deferred surgery completion was not delivered");
        AssertDeliveredEvent(applied.Observed.Single(), restored);
        Require(restored.completionFactDelivered
            && !string.IsNullOrWhiteSpace(restored.completionPublicationId)
            && restored.completionFingerprint?.Length == 64
            && SurgeryResolvedFactMapping.ComputeContextSha256(
                restored.resolvedFact) == frozenResultHash
            && restored.statusData.code ==
                SurgeryStatusCode.RecoveryObservation
            && applied.Deliveries.AppliedCommitCount == 1,
            "restored surgery completion lost its frozen result, proof, or recovery status");
        Require(applied.TryDeliver(restored)
            && applied.Observed.Count == 1
            && applied.Deliveries.EnsureAppliedCount == 1,
            "delivered surgery completion was not idempotent");
        PublishGenericIfUnowned(
            applied.Publisher,
            restored.completionFactDelivered,
            new CharacterId(restored.completionDoctorId),
            BuiltInWorkTypeIds.Surgery.Value,
            string.Empty);
        Require(applied.Observed.Count == 1
            && applied.Observed[0].Origin ==
                CharacterCommandOrigin.DirectPlayerOrder
            && WorkIdentityEventAdapter.ResolveDirectOrderActionTag(
                applied.Observed[0].WorkId) ==
                BuiltInWorkTypeIds.Surgery.Value,
            "surgery success emitted a second generic identity");
    }

    private static void VerifySurgeryClinicalFailure()
    {
        using CompletionFixture fixture = new(
            statuses: new[]
            {
                WorkCompletionIdentityDeliveryStatus.Deferred,
                WorkCompletionIdentityDeliveryStatus.Applied
            });
        SurgeryOrder order = CreateCommittedOrder(
            "surgery:5002",
            false,
            CharacterCommandOrigin.Autonomous,
            7.25f);
        Require(!fixture.TryDeliver(order)
            && order.statusData.code == SurgeryStatusCode.ProcedurePaused
            && !order.completionFactDelivered
            && fixture.Observed.Count == 0,
            "deferred clinical failure did not retain its pending completion");
        Require(fixture.TryDeliver(order),
            "surgery clinical-failure identity was not published");
        WorkCompletedIdentityEvent actual = fixture.Observed.Single();
        AssertDeliveredEvent(actual, order);
        Require(actual.ProductId == "outcome:failure"
            && actual.WorkId == "surgery:" + order.procedureId,
            "surgery clinical failure lost its result identity");
        Require(order.state == SurgeryOrderState.Failed
            && !order.resultSucceeded
            && order.failureSeverity == SurgeryFailureSeverity.Minor
            && order.statusData.code ==
                SurgeryStatusCode.CompletedWithMinorFailure,
            "completion recovery converted a clinical failure into success");
        PublishGenericIfUnowned(
            fixture.Publisher,
            order.completionFactDelivered,
            new CharacterId(order.completionDoctorId),
            BuiltInWorkTypeIds.Surgery.Value,
            "outcome:failure");
        Require(fixture.Observed.Count == 1,
            "surgery clinical failure emitted a second generic identity");
    }

    private static void VerifySurgeryKnownConflict()
    {
        using CompletionFixture fixture = new(
            statuses: new[] { WorkCompletionIdentityDeliveryStatus.Conflict });
        SurgeryOrder order = CreateCommittedOrder(
            "surgery:5009",
            true,
            CharacterCommandOrigin.Autonomous,
            3f);
        Require(!fixture.TryDeliver(order)
            && !order.completionFactDelivered
            && !string.IsNullOrWhiteSpace(order.completionPublicationKey)
            && string.IsNullOrEmpty(order.completionPublicationId)
            && fixture.Observed.Count == 0
            && fixture.Deliveries.AppliedCommitCount == 0,
            "known surgery completion conflict was treated as delivered");
    }

    private static void VerifySurgeryUnknownAcknowledgementRestore()
    {
        SurgeryOrder order = CreateCommittedOrder(
            "surgery:5010",
            true,
            CharacterCommandOrigin.Autonomous,
            4f);
        string ownerJson;
        string cursorJson;
        using (CompletionFixture first = new(
                   statuses: new[]
                   {
                       WorkCompletionIdentityDeliveryStatus.Applied
                   }))
        {
            UncertainFactPublisher uncertain = new(first.Dispatcher);
            first.EnsureCommittedSurgeryOutcome(order);
            Require(!SurgeryWorkCompletionPublication.TryDeliver(
                    uncertain,
                    uncertain,
                    order)
                && !order.completionFactDelivered
                && !string.IsNullOrWhiteSpace(order.completionPublicationKey)
                && string.IsNullOrEmpty(order.completionPublicationId)
                && first.Observed.Count == 1
                && first.Deliveries.EnsureAppliedCount == 1
                && first.Deliveries.AppliedCommitCount == 1,
                "unknown surgery completion acknowledgement lost its committed cursor");
            ownerJson = JsonUtility.ToJson(SurgeryStateCloner.CloneOrder(order));
            cursorJson = JsonUtility.ToJson(new CursorEnvelope
            {
                cursors = first.Cursor.Capture().ToList()
            });
        }

        SurgeryOrder restored = JsonUtility.FromJson<SurgeryOrder>(ownerJson);
        CursorEnvelope restoredCursor = JsonUtility.FromJson<CursorEnvelope>(
            cursorJson);
        using CompletionFixture replay = new(restoredCursor.cursors);
        Require(replay.TryDeliver(restored)
            && restored.completionFactDelivered
            && restored.statusData.code ==
                SurgeryStatusCode.RecoveryObservation
            && replay.Observed.Count == 0
            && replay.Deliveries.EnsureAppliedCount == 1
            && replay.Deliveries.AppliedCommitCount == 0,
            "restored unknown acknowledgement repeated the completion effect or notification");
        Require(replay.TryDeliver(restored)
            && replay.Deliveries.EnsureAppliedCount == 1
            && replay.Observed.Count == 0,
            "restored surgery completion was not exact-replay idempotent");
    }

    private static void VerifySurgeryNotificationFailure()
    {
        using CompletionFixture fixture = new(
            statuses: new[] { WorkCompletionIdentityDeliveryStatus.Applied });
        SurgeryOrder order = CreateCommittedOrder(
            "surgery:5003",
            true,
            CharacterCommandOrigin.Autonomous,
            3f);
        bool faulted = false;
        using IDisposable fault = fixture.Bus.Subscribe<WorkCompletedIdentityEvent>(
            gameEvent =>
            {
                if (!faulted
                    && gameEvent.WorkId == "surgery:" + order.procedureId)
                {
                    faulted = true;
                    throw new InvalidOperationException(
                        "injected-surgery-notification-fault");
                }
            });
        Require(!fixture.TryDeliver(order)
            && faulted
            && !order.completionFactDelivered
            && !string.IsNullOrWhiteSpace(order.completionPublicationKey)
            && fixture.Deliveries.EnsureAppliedCount == 1
            && fixture.Deliveries.AppliedCommitCount == 1
            && fixture.Observed.Count == 1,
            "notification exception did not preserve the committed delivery cursor");
        Require(fixture.TryDeliver(order)
            && order.completionFactDelivered
            && fixture.Deliveries.EnsureAppliedCount == 2
            && fixture.Deliveries.AppliedCommitCount == 1
            && fixture.Observed.Count == 1,
            "notification failure retry repeated the callback or completion effect");
        PublishGenericIfUnowned(
            fixture.Publisher,
            order.completionFactDelivered,
            new CharacterId(order.completionDoctorId),
            BuiltInWorkTypeIds.Surgery.Value,
            string.Empty);
        Require(fixture.Observed.Count == 1,
            "notification failure recovery emitted a second generic identity");
    }

    private static void VerifyGenericWhenDomainDidNotPublish(
        CharacterIdentityEventPublisher publisher,
        List<WorkCompletedIdentityEvent> observed)
    {
        PublishGenericIfUnowned(
            publisher,
            false,
            new CharacterId("character:w05-doctor"),
            BuiltInWorkTypeIds.Surgery.Value,
            "outcome:failure");
        Require(observed.Count == 2
            && observed[1].WorkId == BuiltInWorkTypeIds.Surgery.Value,
            "generic failed-attempt identity was incorrectly suppressed");
    }

    private static void VerifyEnrichedGenericReference(
        CharacterIdentityEventPublisher publisher,
        List<WorkCompletedIdentityEvent> observed)
    {
        WorkCompletedIdentityEvent enriched = new(
            new CharacterId("character:w01-worker"),
            BuiltInWorkTypeIds.Repair.Value,
            string.Empty,
            CharacterCommandOrigin.DirectPlayerOrder,
            6,
            "work-run:character:w01-worker:73",
            "repair-work-handler",
            "maintenance.repair|repair:w01:73|1|0",
            "building:w01-target",
            12.5f);
        WorkCompletionIdentityDeliveryLedger deliveries = new();
        WorkCompletionIdentityDeliveryRequest request =
            deliveries.CreateNextDirectRequest(enriched);
        WorkCompletionIdentityDeliveryRequest legacy = new(
            "work-completion-direct:0",
            "work-completion-direct-events",
            0,
            enriched.Character,
            enriched.WorkId,
            enriched.ProductId,
            enriched.Origin,
            enriched.AbsoluteDay);

        publisher.Publish(request.ToEvent());
        WorkCompletedIdentityEvent actual = observed[^1];
        Require(
            observed.Count == 3
            && actual.OperationId == enriched.OperationId
            && actual.TerminalOwnerId == enriched.TerminalOwnerId
            && actual.ChildResultKey == enriched.ChildResultKey
            && actual.TargetEntityId == enriched.TargetEntityId
            && Math.Abs(
                actual.ContributionWorkUnits
                - enriched.ContributionWorkUnits) < 0.00001f
            && request.PayloadFingerprint != legacy.PayloadFingerprint
            && WorkCompletionIdentityDeliveryRequest.ComputeFingerprint(
                legacy.DeliveryId,
                legacy.ProducerStreamId,
                legacy.OperationSequence,
                legacy.Character,
                legacy.WorkId,
                legacy.ProductId,
                legacy.Origin,
                legacy.AbsoluteDay) == legacy.PayloadFingerprint,
            "enriched generic work identity lost its real references or broke the legacy fingerprint");
    }

    private static void AssertDeliveredEvent(
        WorkCompletedIdentityEvent actual,
        SurgeryOrder order)
    {
        GameplayResultKey child = SurgeryOutcomeIds.ResultKey(order.orderId);
        Require(actual.Character.Value == order.completionDoctorId
            && actual.AbsoluteDay == order.completionAbsoluteDay
            && actual.OperationId == child.OperationId.Value
            && actual.TerminalOwnerId == child.ProducerId
            && actual.ChildResultKey == child.ToString()
            && actual.TargetEntityId == order.facilityId
            && Math.Abs(actual.ContributionWorkUnits - order.completedWork)
                < 0.00001f,
            "surgery completion identity lost its frozen terminal reference");
    }

    private static SurgeryOrder CreateCommittedOrder(
        string orderId,
        bool succeeded,
        CharacterCommandOrigin origin,
        float completedWork)
    {
        string procedureId = succeeded
            ? "procedure:w05-success"
            : "procedure:w05-failure";
        string subjectId = "character:w05-patient";
        string doctorId = "character:w05-terminal-doctor";
        string facilityId = "building:w05-surgery";
        string resultId = succeeded ? "success" : "failure";
        SurgeryFailureSeverity severity = succeeded
            ? SurgeryFailureSeverity.None
            : SurgeryFailureSeverity.Minor;
        SurgeryAnatomyFactData before = CreateAnatomy(70f);
        SurgeryAnatomyFactData after = CreateAnatomy(succeeded ? 80f : 60f);
        SurgeryResolvedFactData fact = new()
        {
            orderId = orderId,
            subjectId = subjectId,
            subjectName = "W05 Patient",
            subjectKind = SurgicalSubjectKind.Character,
            procedureId = procedureId,
            procedureKind = SurgicalProcedureKind.Suture,
            doctorId = doctorId,
            facilityId = facilityId,
            targetNodeId = "heart",
            origin = origin,
            absoluteDay = 5,
            completedWork = completedWork,
            resultId = resultId,
            severity = severity,
            succeeded = succeeded,
            materials = new List<SurgicalMaterialRequirement>(),
            before = before.Clone(),
            after = after.Clone()
        };
        fact.Validate();
        return new SurgeryOrder
        {
            orderId = orderId,
            procedureId = procedureId,
            subject = new SurgicalSubjectRef
            {
                kind = SurgicalSubjectKind.Character,
                subjectId = subjectId,
                displayName = fact.subjectName
            },
            targetNodeId = fact.targetNodeId,
            state = succeeded
                ? SurgeryOrderState.Recovering
                : SurgeryOrderState.Failed,
            resultRolled = true,
            resultSucceeded = succeeded,
            outcomeConsequencesApplied = !succeeded,
            outcomeConsequenceStep = succeeded ? 0 : 2,
            resultOutcomeId = resultId,
            failureSeverity = severity,
            outcomeAnatomyBeforeCaptured = true,
            outcomeAnatomyBefore = before.Clone(),
            resolvedFact = fact.Clone(),
            resolutionPublicationKey =
                SurgeryOutcomeIds.ResultKey(orderId).ToString(),
            resolutionPublicationId = new GameplayOutcomeId(
                new GameplayOutcomeRunId("run:editor:surgery-work-completion"),
                1L).ToString(),
            resolutionFingerprint =
                SurgeryResolvedFactMapping.ComputeContextSha256(fact),
            resolutionFactAccepted = true,
            resolutionFactDelivered = true,
            completionOrigin = origin,
            doctorId = doctorId,
            completionDoctorId = doctorId,
            completionAbsoluteDay = fact.absoluteDay,
            facilityId = facilityId,
            requiredWork = completedWork,
            completedWork = completedWork,
            materials = new List<SurgicalMaterialRequirement>()
        };
    }

    private static SurgeryAnatomyFactData CreateAnatomy(float health) => new()
    {
        availability = SurgeryAnatomyAvailability.Available,
        profileId = "anatomy:humanoid",
        nodeId = "heart",
        alive = true,
        currentHealth = health,
        maxHealth = 100f
    };

    private static void PublishGenericIfUnowned(
        CharacterIdentityEventPublisher publisher,
        bool domainTerminalIdentityPublished,
        CharacterId worker,
        string workId,
        string productId)
    {
        if (WorkCompletionIdentityEmissionPolicy.ShouldPublishGeneric(
                domainTerminalIdentityPublished))
        {
            publisher.Publish(new WorkCompletedIdentityEvent(
                worker,
                workId,
                productId,
                CharacterCommandOrigin.Autonomous,
                5));
        }
    }

    private static void RequireRejected(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    [Serializable]
    private sealed class CursorEnvelope
    {
        public List<WorkCompletionIdentityDeliveryCursorSaveData> cursors = new();
    }

    private sealed class CompletionFixture : IDisposable
    {
        private IDisposable binding;
        private readonly DomainFactDispatcher surgeryFacts;
        private readonly IDisposable surgeryBinding;
        private readonly IDisposable workIdentityBinding;
        private readonly IDisposable observation;

        internal CompletionFixture(
            IEnumerable<WorkCompletionIdentityDeliveryCursorSaveData> cursors = null,
            WorkCompletionIdentityDeliveryStatus[] statuses = null)
        {
            Bus = new GameEventBus();
            Publisher = new CharacterIdentityEventPublisher(Bus);
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new SurgeryOutcomeDescriptor(
                        new KoreanJosaFormatter()),
                    new WorkCompletionIdentityOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new SurgeryOutcomeAdapter(),
                    new WorkCompletionIdentityOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 8,
                    largePageCount: 0,
                    knownResultKeyCapacity: 64),
                new GameplayOutcomeRunId(
                    "run:editor:surgery-work-completion"),
                1L);
            var recorder = new GameplayOutcomeRecorder(
                Ledger,
                registry,
                Bus);
            surgeryFacts = new DomainFactDispatcher();
            surgeryBinding = SurgeryResolvedFactMapping.CreateBinding(
                    registry,
                    Ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(
                        recorder,
                        Ledger))
                .Connect(surgeryFacts);
            workIdentityBinding = WorkIdentityAppliedFactMapping.CreateBinding(
                    registry,
                    Ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(
                        recorder,
                        Ledger),
                    Ledger,
                    Ledger)
                .Connect(surgeryFacts);
            Cursor = new WorkCompletionIdentityDeliveryLedger();
            if (cursors != null)
                Cursor.Restore(cursors);
            Deliveries = new FakeDeliveryCommand(
                Cursor,
                surgeryFacts,
                statuses);
            Dispatcher = new DomainFactDispatcher();
            Observed = new List<WorkCompletedIdentityEvent>();
            observation = Bus.Subscribe<WorkCompletedIdentityEvent>(Observed.Add);
            Reconnect();
        }

        internal GameEventBus Bus { get; }
        internal CharacterIdentityEventPublisher Publisher { get; }
        internal GameplayOutcomeLedger Ledger { get; }
        internal WorkCompletionIdentityDeliveryLedger Cursor { get; }
        internal FakeDeliveryCommand Deliveries { get; }
        internal DomainFactDispatcher Dispatcher { get; }
        internal List<WorkCompletedIdentityEvent> Observed { get; }

        internal bool TryDeliver(SurgeryOrder order)
        {
            EnsureCommittedSurgeryOutcome(order);
            return SurgeryWorkCompletionPublication.TryDeliver(
                Dispatcher,
                Dispatcher,
                order);
        }

        internal void Disconnect()
        {
            binding?.Dispose();
            binding = null;
        }

        internal void Reconnect()
        {
            Disconnect();
            binding = SurgeryWorkCompletionFactMapping.CreateBinding(
                    Deliveries,
                    Cursor,
                    Publisher,
                    new WorkIdentityAppliedFactMapping(
                        Ledger,
                        Ledger))
                .Connect(Dispatcher);
        }

        internal void EnsureCommittedSurgeryOutcome(SurgeryOrder order)
        {
            if (order == null
                || !order.resolutionFactAccepted
                || !order.resolutionFactDelivered)
            {
                return;
            }

            var fact = new SurgeryResolvedFact(order.resolvedFact);
            GameplayResultKey key = SurgeryResolvedFactMapping.ResultKey(
                fact.Snapshot());
            if (Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State is GameplayOutcomeReplayState.Committed
                    or GameplayOutcomeReplayState.DeliveryFaultPending
                    or GameplayOutcomeReplayState.PublishedAwaitingAcknowledgement
                    or GameplayOutcomeReplayState.PublishedAcknowledged
                    or GameplayOutcomeReplayState.Compacted
                    or GameplayOutcomeReplayState.Forgotten)
            {
                return;
            }

            if (!surgeryFacts.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure))
            {
                throw new InvalidOperationException(
                    "Could not reserve surgery child evidence: " + failure);
            }
            DomainFactDelivery delivery = surgeryFacts.Publish(
                lease,
                fact);
            if (!delivery.Completed)
            {
                throw new InvalidOperationException(
                    "Could not publish surgery child evidence: "
                    + delivery.Detail);
            }
        }

        public void Dispose()
        {
            Disconnect();
            observation.Dispose();
            Dispatcher.Dispose();
            workIdentityBinding.Dispose();
            surgeryBinding.Dispose();
            surgeryFacts.Dispose();
        }
    }

    private sealed class FakeDeliveryCommand :
        IWorkCompletionIdentityDeliveryCommand
    {
        private readonly WorkCompletionIdentityDeliveryLedger cursor;
        private readonly DomainFactDispatcher facts;
        private readonly Queue<WorkCompletionIdentityDeliveryStatus> statuses;

        internal FakeDeliveryCommand(
            WorkCompletionIdentityDeliveryLedger cursor,
            DomainFactDispatcher facts,
            IEnumerable<WorkCompletionIdentityDeliveryStatus> statuses)
        {
            this.cursor = cursor ?? throw new ArgumentNullException(nameof(cursor));
            this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
            this.statuses = new Queue<WorkCompletionIdentityDeliveryStatus>(
                statuses ?? Array.Empty<WorkCompletionIdentityDeliveryStatus>());
        }

        internal int EnsureAppliedCount { get; private set; }
        internal int AppliedCommitCount { get; private set; }

        public WorkCompletionIdentityDeliveryResult EnsureApplied(
            WorkCompletionIdentityDeliveryRequest request)
        {
            EnsureAppliedCount++;
            WorkCompletionIdentityDeliveryStatus status = statuses.Count > 0
                ? statuses.Dequeue()
                : cursor.Inspect(request, out _);
            if (status != WorkCompletionIdentityDeliveryStatus.Applied)
                return new WorkCompletionIdentityDeliveryResult(
                    status,
                    status == WorkCompletionIdentityDeliveryStatus.Deferred
                        ? "injected-surgery-completion-deferred"
                        : status == WorkCompletionIdentityDeliveryStatus.Conflict
                            ? "injected-surgery-completion-conflict"
                            : string.Empty);

            var fact = new WorkIdentityAppliedFact(
                request,
                "W05 Terminal Doctor",
                requiresTerminalDelivery: true);
            if (!facts.TryReserve(
                    fact,
                    out DomainFactLease lease,
                    out string failure))
            {
                return new WorkCompletionIdentityDeliveryResult(
                    WorkCompletionIdentityDeliveryStatus.Deferred,
                    failure);
            }
            string key = lease.PublicationKey;
            DomainFactDelivery delivery = facts.Publish(lease, fact);
            if (!delivery.Completed)
            {
                return new WorkCompletionIdentityDeliveryResult(
                    WorkCompletionIdentityDeliveryStatus.Deferred,
                    delivery.Detail);
            }
            cursor.CommitObservation(
                fact,
                key,
                delivery.PublicationId,
                delivery.Fingerprint,
                complete: true);
            AppliedCommitCount++;
            return new WorkCompletionIdentityDeliveryResult(
                WorkCompletionIdentityDeliveryStatus.Applied,
                string.Empty);
        }

        public bool RetireProducerStream(string producerStreamId) =>
            cursor.RetireProducerStream(producerStreamId);
    }

    private sealed class UncertainFactPublisher :
        IDomainFactPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        private bool injected;

        internal UncertainFactPublisher(DomainFactDispatcher inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public bool TryReserve<TFact>(
            in TFact proposed,
            out DomainFactLease lease,
            out string failure) => inner.TryReserve(
                proposed,
                out lease,
                out failure);

        public DomainFactDelivery Publish<TFact>(
            in DomainFactLease lease,
            in TFact actual)
        {
            DomainFactDelivery delivered = inner.Publish(lease, actual);
            if (injected)
                return delivered;
            injected = true;
            return DomainFactDelivery.Uncertain(
                "injected-surgery-completion-acknowledgement-lost");
        }

        public bool PublishBatch(
            IReadOnlyList<DomainFactEmission> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.PublishBatch(
                facts,
                results,
                out failure);

        public DomainFactDelivery Replay<TFact>(in TFact fact) =>
            inner.Replay(fact);

        public void Cancel(in DomainFactLease lease) => inner.Cancel(lease);

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure) => inner.ReplayBatch(
                facts,
                results,
                out failure);
    }
}
#endif
