using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class CombatEquipmentCraftTransactionFixture
{
    public static string LastFailureReason { get; private set; } = string.Empty;
    public static string LastEec06Summary { get; private set; } = string.Empty;

    private static readonly IReadOnlyDictionary<string, int> Requirements =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["material:lumber"] = 2,
            ["material:iron-ingot"] = 1
        };

    public static bool Run()
    {
        LastFailureReason = string.Empty;
        if (!VerifyCommonOutputTransaction())
        {
            return Fail("common output transaction");
        }
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        if (!VerifyMissingInputAtomic(catalog))
        {
            return Fail("missing-input atomicity");
        }
        if (!VerifyRejectedDismantleReplay(catalog))
        {
            return Fail("rejected-dismantle replay");
        }

        FixtureGateway gateway = new(catalog) { FailNextAcknowledgement = true };
        CombatEquipmentCraftOrderSaveData order = CreateOrder("qa");
        string lumberA = gateway.Add("material:lumber", 1, order.materialDestinationId);
        string lumberB = gateway.Add("material:lumber", 1, order.materialDestinationId);
        string iron = gateway.Add("material:iron-ingot", 1, order.materialDestinationId);
        if (!CombatEquipmentCraftMaterialOutbox.TryCommitOrResume(
                order,
                Requirements,
                gateway.GetAllStacks(),
                gateway,
                out _)
            || !order.materialsReady
            || order.materialTransferInputs.Count != 3
            || gateway.Quantity(lumberA) != 0
            || gateway.Quantity(lumberB) != 0
            || gateway.Quantity(iron) != 0
            || !gateway.TryGetPendingBatchPhysicalDisposition(
                order.materialTransferOperationId,
                out PhysicalItemBatchDispositionReceipt receipt))
        {
            return Fail("material Transfer commit");
        }

        order.attemptOutcomeResolved = true;
        order.resolvedQuality = CombatEquipmentQuality.Normal;
        order.completionEffectsPublished = true;
        order.outputOperationId = CombatEquipmentCraftOutputOutbox
            .FormatOperationId(order.orderId, order.qualityAttemptIndex);
        order.outputItemId = CombatItemDefinitions.ArrowItemId;
        order.outputQuantity = 20;
        const string outputDestination = "production-output:building:qa";
        bool firstOutput = CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                order,
                gateway,
                Vector2Int.zero,
                outputDestination,
                out string firstOutputFailure);
        int firstOutputCount = CountCommittedOutput(
            gateway,
            order.outputCommitId);
        bool replayOutput = CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                order,
                gateway,
                Vector2Int.zero,
                outputDestination,
                out string replayOutputFailure);
        int replayOutputCount = CountCommittedOutput(
            gateway,
            order.outputCommitId);
        if (!firstOutput
            || !order.outputPublished
            || firstOutputCount != 20
            || !replayOutput
            || replayOutputCount != 20)
        {
            return Fail(
                "generic output exact-once publication"
                + $":first={firstOutput}/{firstOutputFailure}"
                + $",published={order.outputPublished}"
                + $",firstCount={firstOutputCount}"
                + $",replay={replayOutput}/{replayOutputFailure}"
                + $",replayCount={replayOutputCount}"
                + $",commit={order.outputCommitId}");
        }

        if (CombatEquipmentCraftMaterialOutbox.TryAcknowledgeOutcome(
                order,
                Requirements,
                gateway,
                out _)
            || order.materialTransferAcknowledged)
        {
            return Fail("injected material acknowledgement fault");
        }

        PhysicalItemRestoreCandidateDispositionSnapshot candidate = new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            order.materialTransferRequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
        CombatEquipmentCraftOrderSaveData restored =
            JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                JsonUtility.ToJson(order));
        if (!CombatEquipmentCraftMaterialOutbox.TryCommitOrResume(
                restored,
                Requirements,
                gateway.GetAllStacks(),
                gateway,
                out _)
            || !CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                restored,
                gateway,
                Vector2Int.zero,
                outputDestination,
                out _)
            || !CombatEquipmentCraftMaterialOutbox.TryAcknowledgeOutcome(
                restored,
                Requirements,
                gateway,
                out _)
            || !restored.materialTransferAcknowledged
            || CountCommittedOutput(gateway, restored.outputCommitId) != 20
            || gateway.Quantity(lumberA) != 0
            || gateway.Quantity(lumberB) != 0
            || gateway.Quantity(iron) != 0)
        {
            return Fail("JSON replay acknowledgement/output recovery");
        }

        Validate(new[] { order }, candidate);
        if (!Reject(new[] { order })
            || !Reject(Array.Empty<CombatEquipmentCraftOrderSaveData>(), candidate)
            || !Reject(new[] { order }, Copy(candidate, mass: candidate.InputMassGrams + 1))
            || !Reject(new[] { order }, Copy(candidate, fingerprint: candidate.RequestFingerprint + ":bad")))
        {
            return Fail("incoming material receipt join rejection");
        }
        Validate(new[] { restored });
        return Reject(new[] { restored }, candidate)
            || Fail("terminal owner accepted stale incoming receipt");
    }

    public static bool RunEec06()
    {
        LastFailureReason = string.Empty;
        LastEec06Summary = string.Empty;
        GameObject facilityObject = new("EEC06 Combat Craft Facility");
        GameObject workerObject = new("EEC06 Combat Craft Worker");
        try
        {
            ResourceGameContentCatalog rootCatalog = new(
                new UnityGameContentRootLoader());
            ResourceCombatEquipmentCatalog equipmentCatalog = new(rootCatalog);
            ResourceEconomyContentCatalog materialCatalog = new(
                Resources.LoadAll<ResourceItemDefinitionSO>(
                    ResourceItemDefinitionSO.ResourcePath),
                Resources.LoadAll<ProductionRecipeSO>(
                    ProductionRecipeSO.ResourcePath),
                Resources.LoadAll<CropDefinitionSO>(
                    CropDefinitionSO.ResourcePath),
                Resources.LoadAll<CraftMaterialDefinitionSO>(
                    CraftMaterialDefinitionSO.ResourcePath));
            BuildingSO facilityData = Resources.LoadAll<BuildingSO>(string.Empty)
                .Where(value => value != null
                    && value.GetAbility<BuildingEquipmentCraftingAbility>()?
                        .CraftableEquipmentIds.Contains(
                            "weapon:longsword",
                            StringComparer.Ordinal) == true)
                .First();
            BuildableObject facility = facilityObject
                .AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(facilityData, new Vector2Int(9, 4));

            CharacterActor worker = workerObject.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.Inject(workerObject);
            worker.EnsureRuntimeState();
            worker.Initialize(Resources.LoadAll<CharacterSO>(string.Empty)
                .First(value => value != null
                    && value.id > 0
                    && value.species != null));
            const string WorkerId = "character:qa:eec06-smith";
            worker.Identity.SetPersistentId(WorkerId);

            IDungeonItemCatalogProvider itemCatalog =
                EditorItemCatalogFactory.Create();
            ProductionDomainOutputPublicationDebugScenarios.DomainFixture domain =
                new(runtimeObject: facility, itemCatalog: itemCatalog,
                    facilityPersistentId: facility.RequirePersistentInstanceId().Value);
            if (domain.Handle.InstanceId.Value != facility.RequirePersistentInstanceId().Value)
                return Fail("EEC06 output fixture does not refer to the actual crafting facility.");
            FixtureGateway physical = new(itemCatalog, domain.Repository);
            AcceptingCraftInputDestinations destinations = new();
            EvolutionGameplayOutcomeEditorFixture qualityOutcomes = new(
                "run:eec06-combat-craft-quality");
            using DomainFactDispatcher qualityFacts = new();
            var qualityCommits = new GameplayDomainFactCommitGroup(
                qualityOutcomes.Recorder,
                qualityOutcomes.Ledger);
            using IDisposable qualityObservation = qualityFacts.Subscribe(
                "test:combat.craft-quality",
                new GameplayDomainFactObserver<
                    CombatEquipmentQualityResolvedFact,
                    ProductQualityOutcomeReceipt>(
                        CombatEquipmentQualityFactMapping.Capture,
                        qualityOutcomes.Registry,
                        qualityOutcomes.Ledger,
                        qualityOutcomes.Recorder,
                        qualityCommits));
            MigratedProducerOutcomeEditorFixture completionOutcomes = new(
                "run:eec06-combat-craft-completion");
            MigratedProducerOutcomeProbeTransaction completionProbe = new(
                completionOutcomes.Transaction,
                completionOutcomes.Recorder)
            {
                TargetKind = MigratedProducerOutcomeKind
                    .CombatEquipmentCraftCompletionResult
            };
            using DomainFactDispatcher completionFacts = new();
            var completionCommits = new GameplayDomainFactCommitGroup(
                completionOutcomes.Recorder,
                completionOutcomes.Ledger);
            var completionObserver =
                new CombatEquipmentCraftCompletionFactObserver(
                    completionOutcomes.Transaction,
                    completionCommits);
            using IDisposable completionObservation = completionFacts.Subscribe(
                "test:combat.craft-completion",
                new CombatCraftCompletionFactProbeObserver(
                    completionObserver,
                    completionProbe));
            var outputPublication = new ObservedOutputProbe(
                CreateObservedPublication(
                    domain,
                    completionFacts,
                    completionOutcomes));
            FixedExcellentCraftQualityResolver qualityResolver = new();

            CombatEquipmentRuntime runtime = CreateEec06Runtime(
                equipmentCatalog,
                materialCatalog,
                domain,
                facility,
                physical,
                destinations,
                qualityFacts,
                outputPublication,
                qualityResolver);
            string nonexhaustedJson = CombatEquipmentSaveJson.Serialize(
                runtime.Capture());
            DungeonCombatEquipmentSaveData exhausted =
                DungeonSaveJson.Deserialize<DungeonCombatEquipmentSaveData>(
                    nonexhaustedJson);
            exhausted.nextCraftSequence = int.MaxValue;
            runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(
                exhausted));
            string exhaustedJson = CombatEquipmentSaveJson.Serialize(
                runtime.Capture());
            if (runtime.TryQueueCraft(
                    "weapon:longsword",
                    "material:blacksteel",
                    facility,
                    out string exhaustedFailure)
                || !string.Equals(
                    exhaustedFailure,
                    "equipment.craft.sequence_exhausted",
                    StringComparison.Ordinal)
                || destinations.OpenCalls != 0
                || destinations.RequestCalls != 0
                || destinations.CloseCalls != 0
                || !string.Equals(
                    CombatEquipmentSaveJson.Serialize(runtime.Capture()),
                    exhaustedJson,
                    StringComparison.Ordinal))
            {
                return Fail("EEC06 exhausted craft sequence mutated owner or input destination"
                    + $":failure={exhaustedFailure}"
                    + $",open={destinations.OpenCalls}"
                    + $",request={destinations.RequestCalls}"
                    + $",close={destinations.CloseCalls}");
            }
            runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(
                DungeonSaveJson.Deserialize<DungeonCombatEquipmentSaveData>(
                    nonexhaustedJson)));
            string allowFailure = string.Empty;
            string priorityFailure = string.Empty;
            if (!runtime.SetCraftMaterialAllowed(
                    "weapon:longsword",
                    "material:blacksteel",
                    facility,
                    true,
                    out allowFailure)
                || !runtime.SetCraftMaterialAllowed(
                    "weapon:longsword",
                    "material:blacksteel",
                    facility,
                    true,
                    out _)
                || !runtime.MoveCraftMaterialPriority(
                    "weapon:longsword",
                    "material:blacksteel",
                    facility,
                    -1,
                    out priorityFailure))
            {
                return Fail("EEC06 public material commands: "
                    + allowFailure + ":" + priorityFailure);
            }
            if (runtime.SetCraftMaterialAllowed(
                    "weapon:longsword",
                    "material:not-authored",
                    facility,
                    true,
                    out _))
            {
                return Fail("EEC06 accepted an unauthored material command");
            }
            CombatEquipmentCraftMaterialPolicySaveData policy =
                runtime.GetCraftMaterialPolicy("weapon:longsword", facility);
            if (policy.commandRevision != 2L
                || !policy.commandReceipts.Select(value => value.commandKind)
                    .SequenceEqual(new[]
                    {
                        "material-allowed",
                        "material-priority"
                    }))
            {
                return Fail("EEC06 material policy receipts");
            }
            if (!runtime.TryQueueCraft(
                    "weapon:longsword",
                    "material:blacksteel",
                    facility,
                    out string queueFailure))
            {
                return Fail("EEC06 public queue: " + queueFailure);
            }
            CombatEquipmentCraftOrderSaveData queued = runtime.CraftQueue.Single();
            WorkerSelectionPolicySaveData workerPolicy =
                WorkerSelectionPolicySaveData.Anyone(
                    WorkerCandidateSortMode.Fastest);
            string workerFailure = string.Empty;
            string qualityFailure = string.Empty;
            if (!runtime.SetCraftWorkerPolicy(
                    queued.orderId,
                    workerPolicy,
                    out workerFailure)
                || !runtime.SetCraftWorkerPolicy(
                    queued.orderId,
                    workerPolicy,
                    out _)
                || !runtime.SetCraftQualityTarget(
                    queued.orderId,
                    CraftsmanshipQualityTier.Good,
                    RejectedOutputDisposition.MarkForSale,
                    QualityRepeatLimitMode.SafeLimits,
                    3,
                    144f,
                    1,
                    out qualityFailure)
                || !runtime.SetCraftQualityTarget(
                    queued.orderId,
                    CraftsmanshipQualityTier.Good,
                    RejectedOutputDisposition.MarkForSale,
                    QualityRepeatLimitMode.SafeLimits,
                    3,
                    144f,
                    1,
                    out _)
                || runtime.SetCraftWorkerPolicy(
                    "combat-craft:missing",
                    workerPolicy,
                    out _))
            {
                return Fail("EEC06 public order commands: "
                    + workerFailure + ":" + qualityFailure);
            }
            queued = runtime.CraftQueue.Single();
            string[] commandKinds = queued.commandReceipts
                .Select(value => value.commandKind)
                .ToArray();
            if (queued.commandRevision != 5L
                || !commandKinds.SequenceEqual(new[]
                {
                    "material-allowed",
                    "material-priority",
                    "order-queued",
                    "worker-policy",
                    "quality-target"
                })
                || completionOutcomes.Ledger.CaptureGameplayOutcomes()
                    .exactOutcomes.Count != 0)
            {
                return Fail("EEC06 command context was duplicated or became a terminal");
            }

            string blacksteelStack = physical.Add(
                "material:blacksteel-ingot",
                2,
                queued.materialDestinationId);
            string frameStack = physical.Add(
                "component:growth-frame",
                1,
                queued.materialDestinationId);

            int firstWorkCompleted = runtime.ApplyCraftWork(
                new[] { "weapon:longsword" },
                1f,
                worker,
                80f,
                out _,
                out _,
                out _);
            CombatEquipmentCraftOrderSaveData partial = runtime.CraftQueue.Single();
            CombatEquipmentSaveSection partialSection = new(
                runtime,
                ProductionOutputLifecycleRestoreCandidatePublisher.IsolatedSectionFixtureOnly);
            string partialJson = partialSection.Capture();
            var partialTokens = Newtonsoft.Json.Linq.JObject.Parse(partialJson);
            var partialOrderTokens = partialTokens["craftOrders"][0];
            if (firstWorkCompleted != 0
                || partial.completedWork != 1f
                || !partial.materialsReady
                || partial.attemptOutcomeResolved
                || partial.outputPreparedComponent != null
                || partial.resolvedMythicProvenance != null
                || partialOrderTokens["outputPreparedComponent"].Type
                    != Newtonsoft.Json.Linq.JTokenType.Null
                || partialOrderTokens["resolvedMythicProvenance"].Type
                    != Newtonsoft.Json.Linq.JTokenType.Null
                || physical.Quantity(blacksteelStack) != 0
                || physical.Quantity(frameStack) != 0
                || qualityOutcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count != 0
                || completionOutcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count != 0)
            {
                return Fail("EEC06 partial owner capture lost absent output or WIP consumption");
            }
            // Neither the former JsonUtility encoding nor a non-null empty
            // optional is accepted. Reject before publication, leaving the owner intact.
            for (int malformedCase = 0; malformedCase < 3; malformedCase++)
            {
                DungeonCombatEquipmentSaveData malformed = DungeonSaveJson.Deserialize<
                    DungeonCombatEquipmentSaveData>(malformedCase == 0
                        ? JsonUtility.ToJson(runtime.Capture())
                        : partialJson);
                if (malformedCase == 1)
                    malformed.craftOrders[0].outputPreparedComponent = new ItemInstanceComponentSaveData();
                if (malformedCase == 2)
                    malformed.craftOrders[0].resolvedMythicProvenance = new MythicProvenanceSaveData();
                bool rejected = false;
                try
                {
                    _ = runtime.BuildRestoreCandidate(malformed);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }
                if (!rejected || partialSection.Capture() != partialJson)
                    return Fail("EEC06 invalid non-null optional was accepted or changed owner");
            }
            CombatEquipmentRuntime partialRestored = CreateEec06Runtime(
                equipmentCatalog, materialCatalog, domain, facility, physical,
                destinations, qualityFacts, outputPublication,
                qualityResolver);
            partialRestored.PublishRestoreCandidate(partialRestored.BuildRestoreCandidate(
                DungeonSaveJson.Deserialize<DungeonCombatEquipmentSaveData>(partialJson)));
            CombatEquipmentCraftOrderSaveData resumedPartial = partialRestored.CraftQueue.Single();
            if (resumedPartial.completedWork != partial.completedWork
                || resumedPartial.materialTransferCommitId != partial.materialTransferCommitId
                || resumedPartial.outputPreparedComponent != null
                || resumedPartial.resolvedMythicProvenance != null)
                return Fail("EEC06 unresolved first-attempt WIP changed across fresh JSON restore");
            runtime = partialRestored;
            completionProbe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            int interrupted = runtime.ApplyCraftWork(
                new[] { "weapon:longsword" },
                queued.requiredWork,
                worker,
                80f,
                out _,
                out _,
                out _);
            CombatEquipmentCraftOrderSaveData pending = runtime.CraftQueue.Single();
            int blacksteelRemaining = physical.Quantity(blacksteelStack);
            int frameRemaining = physical.Quantity(frameStack);
            int completionExactAfterInterrupt = completionOutcomes.Ledger
                .CaptureGameplayOutcomes().exactOutcomes.Count;
            int qualityExactAfterInterrupt = qualityOutcomes.Ledger
                .CaptureGameplayOutcomes().exactOutcomes.Count;
            if (interrupted != 0
                || !pending.attemptOutcomeResolved
                || !pending.qualityOutcomeCommitted
                || string.IsNullOrWhiteSpace(pending.qualityOutcomeResultKey)
                || pending.completionFact?.IsEmpty != true
                || pending.outputPublication == null
                || pending.outputPublication.outputPublished
                || pending.outputPublication.observationExpected
                || pending.outputPublication.stacks.Count != 0
                || pending.materialTransferInputs.Count != 2
                || pending.materialTransferCommitId != partial.materialTransferCommitId
                || blacksteelRemaining != 0
                || frameRemaining != 0
                || completionExactAfterInterrupt != 0
                || qualityExactAfterInterrupt != 1)
            {
                return Fail("EEC06 completion interruption did not preserve one durable child"
                    + $":completed={interrupted}"
                    + $",resolved={pending.attemptOutcomeResolved}"
                    + $",qualityCommitted={pending.qualityOutcomeCommitted}"
                    + $",qualityKey={!string.IsNullOrWhiteSpace(pending.qualityOutcomeResultKey)}"
                    + $",completionFactEmpty={pending.completionFact?.IsEmpty}"
                    + $",outputEnvelope={pending.outputPublication != null}"
                    + $",outputPublished={pending.outputPublication?.outputPublished}"
                    + $",outputStacks={pending.outputPublication?.stacks?.Count ?? -1}"
                    + $",inputRecords={pending.materialTransferInputs.Count}"
                    + $",blacksteelRemaining={blacksteelRemaining}"
                    + $",frameRemaining={frameRemaining}"
                    + $",completionExact={completionExactAfterInterrupt}"
                    + $",qualityExact={qualityExactAfterInterrupt}");
            }
            string qualityResultKey = pending.qualityOutcomeResultKey;
            int physicalCountAfterInterrupted = physical.GetAllStacks().Count;

            DungeonCombatEquipmentSaveData saved = DungeonSaveJson.Deserialize<
                DungeonCombatEquipmentSaveData>(
                CombatEquipmentSaveJson.Serialize(runtime.Capture()));
            CombatEquipmentRuntime restored = CreateEec06Runtime(
                equipmentCatalog,
                materialCatalog,
                domain,
                facility,
                physical,
                destinations,
                qualityFacts,
                outputPublication,
                qualityResolver);
            restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(saved));
            CombatEquipmentCraftOrderSaveData restoredPending =
                restored.CraftQueue.Single();
            if (restoredPending.commandRevision != 5L
                || !restoredPending.commandReceipts
                    .Select(value => value.commandKind)
                    .SequenceEqual(commandKinds)
                || !string.Equals(
                    restoredPending.qualityOutcomeResultKey,
                    qualityResultKey,
                    StringComparison.Ordinal)
                || restoredPending.completionFact?.IsEmpty != true
                || restoredPending.outputPublication.outputPublished
                || restoredPending.outputPublication.observationExpected
                || restoredPending.outputPublication.stacks.Count != 0)
            {
                return Fail("EEC06 fresh runtime restore changed frozen evidence");
            }

            completionProbe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            int completed = restored.ApplyCraftWork(
                new[] { "weapon:longsword" },
                1f,
                worker,
                80f,
                out string completedDefinition,
                out string completedMaterial,
                out CombatEquipmentQuality completedQuality);
            GameplayOutcomeLedgerSaveData completionLedger =
                completionOutcomes.Ledger.CaptureGameplayOutcomes();
            if (completed != 1
                || completedDefinition != "weapon:longsword"
                || completedMaterial != "material:blacksteel"
                || completedQuality != CombatEquipmentQuality.Excellent
                || restored.CraftQueue.Count != 0
                || completionLedger.exactOutcomes.Count != 1
                || restoredPending.completionFact?.IsEmpty != false
                || !restoredPending.outputPublication.observationDelivered
                || physical.GetAllStacks().Count != physicalCountAfterInterrupted + 1)
            {
                return Fail("EEC06 restored production order did not complete exactly once"
                    + $":completed={completed},definition={completedDefinition},material={completedMaterial},quality={completedQuality}"
                    + $",queue={restored.CraftQueue.Count},completionRows={completionLedger.exactOutcomes.Count}"
                    + $",completionFactEmpty={restoredPending.completionFact?.IsEmpty},delivered={restoredPending.outputPublication.observationDelivered}"
                    + $",physicalStacks={physical.GetAllStacks().Count},expectedStacks={physicalCountAfterInterrupted + 1}"
                    + $",outputPhase={restoredPending.outputPhase},outputPublished={restoredPending.outputPublication.outputPublished}"
                    + $",observationExpected={restoredPending.outputPublication.observationExpected}"
                    + $",materialAck={restoredPending.materialTransferAcknowledged},outputAck={restoredPending.outputPublication.outputAcknowledged}"
                    + $",outputAckFailure={outputPublication.LastAcknowledgementFailure},recoveryFailure={outputPublication.LastRecoveryFailure}"
                    + $",completionEffects={restoredPending.completionEffectsPublished}");
            }
            string outputStackId = restoredPending.outputPublication.stacks
                .Single().stackId;
            string outputInstanceId = restoredPending.outputPublication.stacks
                .Single().itemInstanceId;
            GameplayOutcomeSnapshot exact = completionLedger.exactOutcomes.Single();
            string commandContext = SnapshotFact(exact, "craft-command-context");
            string inputSources = SnapshotFact(exact, "input-source-stacks");
            string outputStacks = SnapshotFact(exact, "output-stacks");
            string contributors = SnapshotFact(exact, "contributors-and-work");
            string qualityFact = SnapshotFact(exact, "quality-result-key");
            string completionResultKey = completionProbe.LatestCommittedKey(
                MigratedProducerOutcomeKind.CombatEquipmentCraftCompletionResult)
                .ToString();
            if (!commandKinds.All(kind => commandContext.Contains(
                    kind,
                    StringComparison.Ordinal))
                || !inputSources.Contains(blacksteelStack, StringComparison.Ordinal)
                || !inputSources.Contains(frameStack, StringComparison.Ordinal)
                || !outputStacks.Contains(outputStackId, StringComparison.Ordinal)
                || !outputStacks.Contains(outputInstanceId, StringComparison.Ordinal)
                || !contributors.Contains(WorkerId + "=", StringComparison.Ordinal)
                || qualityFact != qualityResultKey
                || qualityFact == completionResultKey
                || restoredPending.outputPublication.observationKey
                    != completionResultKey
                || !exact.subjects.Any(value => value.entityId == WorkerId)
                || !exact.subjects.Any(value => value.entityId == outputInstanceId))
            {
                return Fail("EEC06 terminal omitted command/material/product/contributor/quality identity");
            }
            int exactBeforeReplay = completionLedger.exactOutcomes.Count;
            int physicalBeforeReplay = physical.GetAllStacks().Count;
            int replayed = restored.ApplyCraftWork(
                new[] { "weapon:longsword" },
                1f,
                worker,
                80f,
                out _,
                out _,
                out _);
            if (replayed != 0
                || completionOutcomes.Ledger.CaptureGameplayOutcomes()
                    .exactOutcomes.Count != exactBeforeReplay
                || physical.GetAllStacks().Count != physicalBeforeReplay)
            {
                return Fail("EEC06 terminal replay duplicated output or parent");
            }

            LastEec06Summary = "commands=5; inputStacks=2; outputStacks=1; "
                + "contributors=1; qualityChild=1; completionParent=1; "
                + "unresolvedWipJsonRestore=pass; absentOutputFields=2; "
                + "malformedOptionalRejects=3; rejectOwnerMutation=0; "
                + "sequenceExhaustionMutation=0; sequenceRetry=pass; "
                + "freshRestore=pass; replayDuplicate=0";
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(workerObject);
            UnityEngine.Object.DestroyImmediate(facilityObject);
        }
    }

    private static CombatEquipmentRuntime CreateEec06Runtime(
        ResourceCombatEquipmentCatalog equipmentCatalog,
        ResourceEconomyContentCatalog materialCatalog,
        ProductionDomainOutputPublicationDebugScenarios.DomainFixture domain,
        BuildableObject facility,
        FixtureGateway physical,
        ICombatEquipmentCraftInputDestinationRuntime inputDestinations,
        IDomainFactStagingPublisher qualityFacts,
        IProductionDomainOutputPublicationService outputPublication,
        ICraftQualityResolver qualityResolver) =>
        CombatEquipmentEditorTestFactory.Create(
            equipmentCatalog,
            domain.Repository,
            new CharacterCarryInventoryRegistry(),
            materialCatalog,
            EmptyEvolutionModuleRegistry.Instance,
            EditorAllResearchRuntimeProvider.Instance,
            EmptyEquipmentModuleCatalog.Instance,
            physical,
            new EditorFixedGameCalendar(137, 9),
            qualityResolver: qualityResolver,
            inputDestinations: inputDestinations,
            craftOutputTransactionFactory: stateStore =>
                new CombatEquipmentCraftOutputTransaction(
                    stateStore,
                    new FixedBuildingWorld(facility),
                    outputPublication,
                    new FixedRejectedSaleDestination(
                        new Vector2Int(14, 6),
                        domain.Claims),
                    physical,
                    qualityFacts,
                    (IObservedProductionDomainOutputPublicationService)
                        outputPublication));

    private static string SnapshotFact(
        GameplayOutcomeSnapshot snapshot,
        string factId) => snapshot.facts.Single(value => string.Equals(
        value.factId,
        factId,
        StringComparison.Ordinal)).value;

    private static bool Fail(string reason)
    {
        LastFailureReason = reason ?? "unknown";
        return false;
    }

    private static bool VerifyMissingInputAtomic(IDungeonItemCatalogProvider catalog)
    {
        FixtureGateway gateway = new(catalog);
        CombatEquipmentCraftOrderSaveData order = CreateOrder("missing");
        string lumber = gateway.Add("material:lumber", 2, order.materialDestinationId);
        return !CombatEquipmentCraftMaterialOutbox.TryCommitOrResume(
                order,
                Requirements,
                gateway.GetAllStacks(),
                gateway,
                out _)
            && gateway.Quantity(lumber) == 2
            && string.IsNullOrEmpty(order.materialTransferOperationId);
    }

    private static bool VerifyRejectedDismantleReplay(
        IDungeonItemCatalogProvider catalog)
    {
        FixtureGateway gateway = new(catalog)
        {
            FailNextAcknowledgement = true
        };
        CombatEquipmentCraftOrderSaveData order = CreateOrder("dismantle");
        order.definitionId = "weapon:dagger";
        order.dismantlingRejectedOutput = true;
        order.materialsReady = true;
        order.rejectedInstanceId = "equipment:rejected:qa";
        CombatEquipmentInstance rejectedEquipment = new()
        {
            instanceId = order.rejectedInstanceId,
            definitionId = order.definitionId,
            materialId = "material:iron",
            quality = CombatEquipmentQuality.Normal,
            durabilityRatio = 1f,
            powerCharge = 100f,
            loadedAmmunition = new LoadedAmmunitionBatch(),
            worldState = CombatEquipmentWorldState.Loose,
            ownerCharacterId = string.Empty,
            sourceStackId = string.Empty,
            evolution = new EquipmentEvolutionState(),
            moduleSlots = new List<EquipmentModuleSlotState>()
        };
        order.rejectedStackId = gateway.AddUnique(
            PhysicalItemIds.ForEquipment(order.definitionId),
            order.rejectedInstanceId,
            WorldItemStackState.FacilityOutputBuffer,
            "production-output:building:qa",
            new[] { EquipmentItemStateCodec.Encode(rejectedEquipment) });
        order.recoveryOutputs.Add(new CombatCraftRecoveryOutputSaveData
        {
            itemId = "material:iron-ingot",
            amount = 1
        });
        order.spawnedRecoveryAmounts.Add(0);
        long recoveryMass = new PhysicalItemMassQuery(catalog)
            .GetDefinitionUnitMass((ItemDefinitionId)"material:iron-ingot")
            .Value;
        order.rejectedRecoveryFactorsCaptured = true;
        order.rejectedRecoveryWorkerSkill = 0f;
        order.rejectedRecoverySalvageMultiplier = 1f;
        order.rejectedRecoveryProjected = true;
        order.rejectedRecoveryDesiredMassGrams = recoveryMass;
        order.rejectedRecoveryOutputMassGrams = recoveryMass;
        order.rejectedRecoverySourceDigest = "fixture:rejected-recovery";
        if (!CombatEquipmentRejectedDismantleOutbox.TryCommitOrResume(
                order,
                gateway,
                out _)
            || gateway.Quantity(order.rejectedStackId) != 0
            || !gateway.TryGetPendingBatchPhysicalDisposition(
                order.rejectedDismantleOperationId,
                out PhysicalItemBatchDispositionReceipt receipt))
        {
            return false;
        }
        string recoveryOperation = CombatEquipmentRejectedDismantleOutbox
            .FormatRecoveryOperationId(order.orderId, 0, 0);
        if (!CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                "material:iron-ingot",
                1,
                recoveryOperation,
                gateway,
                Vector2Int.zero,
                "production-output:building:qa",
                out string recoveryCommit,
                out _))
        {
            return false;
        }
        order.spawnedRecoveryAmounts[0] = 1;
        order.rejectedRecoveryPublished = true;
        if (CombatEquipmentRejectedDismantleOutbox.TryAcknowledgeRecovery(
                order,
                gateway,
                out _))
        {
            return false;
        }
        PhysicalItemRestoreCandidateDispositionSnapshot candidate = new(
            receipt.Kind,
            receipt.OperationId,
            receipt.ReasonCode,
            order.rejectedDismantleRequestFingerprint,
            receipt.SourceStackIds,
            receipt.Quantity,
            receipt.InputMassGrams,
            receipt.CommitId);
        CombatEquipmentCraftOrderSaveData restored =
            JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                JsonUtility.ToJson(order));
        if (!CombatEquipmentRejectedDismantleOutbox.TryCommitOrResume(
                restored,
                gateway,
                out _)
            || !CombatEquipmentCraftOutputOutbox.TryEnsureGenericOutput(
                "material:iron-ingot",
                1,
                recoveryOperation,
                gateway,
                Vector2Int.zero,
                "production-output:building:qa",
                out string replayCommit,
                out _)
            || !string.Equals(
                recoveryCommit,
                replayCommit,
                StringComparison.Ordinal)
            || !CombatEquipmentRejectedDismantleOutbox.TryAcknowledgeRecovery(
                restored,
                gateway,
                out _)
            || !restored.rejectedDismantleAcknowledged
            || CountCommittedOutput(gateway, recoveryCommit) != 1)
        {
            return false;
        }
        CandidateOutputQuery outputCandidate = CandidateOutputQuery.Capture(
            gateway,
            recoveryCommit,
            recoveryMass);
        CombatEquipmentCraftMaterialRestoreGuard.ValidateOwnerSet(
            new[] { order },
            GetRequirements,
            new CandidateQuery(new[] { candidate }),
            outputCandidate);
        CombatEquipmentCraftMaterialRestoreGuard.ValidateOwnerSet(
            new[] { restored },
            GetRequirements,
            new CandidateQuery(Array.Empty<
                PhysicalItemRestoreCandidateDispositionSnapshot>()),
            outputCandidate);
        return Reject(new[] { order })
            && Reject(Array.Empty<CombatEquipmentCraftOrderSaveData>(), candidate)
            && Reject(new[] { restored }, candidate);
    }

    private static bool VerifyCommonOutputTransaction()
    {
        GameObject facilityObject = new("CombatCommonOutputFixture");
        try
        {
            Facility facility = facilityObject.AddComponent<Facility>();
            facility.RestorePersistentIdentity(
                (BuildingInstanceId)"building:qa:combat-common-output");
            string facilityId = facility.RequirePersistentInstanceId().Value;
            EvolutionGameplayOutcomeEditorFixture outcomes = new(
                "run:combat-craft-quality-editor");
            using DomainFactDispatcher qualityFacts = new();
            var qualityCommits = new GameplayDomainFactCommitGroup(
                outcomes.Recorder,
                outcomes.Ledger);
            using IDisposable qualityObservation = qualityFacts.Subscribe(
                "test:combat.craft-quality",
                new GameplayDomainFactObserver<
                    CombatEquipmentQualityResolvedFact,
                    ProductQualityOutcomeReceipt>(
                        CombatEquipmentQualityFactMapping.Capture,
                        outcomes.Registry,
                        outcomes.Ledger,
                        outcomes.Recorder,
                        qualityCommits));
            MigratedProducerOutcomeEditorFixture completionOutcomes = new(
                "run:combat-craft-completion-editor");
            MigratedProducerOutcomeProbeTransaction completionProbe = new(
                completionOutcomes.Transaction,
                completionOutcomes.Recorder)
            {
                TargetKind = MigratedProducerOutcomeKind
                    .CombatEquipmentCraftCompletionResult
            };
            using DomainFactDispatcher completionFacts = new();
            var completionCommits = new GameplayDomainFactCommitGroup(
                completionOutcomes.Recorder,
                completionOutcomes.Ledger);
            var completionObserver =
                new CombatEquipmentCraftCompletionFactObserver(
                    completionOutcomes.Transaction,
                    completionCommits);
            using IDisposable completionObservation = completionFacts.Subscribe(
                "test:combat.craft-completion",
                new CombatCraftCompletionFactProbeObserver(
                    completionObserver,
                    completionProbe));

            ProductionDomainOutputPublicationDebugScenarios.DomainFixture unique =
                new(runtimeObject: facility,
                    facilityPersistentId: facility.RequirePersistentInstanceId().Value);
            IProductionDomainOutputPublicationService uniquePublication =
                CreateObservedPublication(
                    unique,
                    completionFacts,
                    completionOutcomes);
            DeferringProductQualityOutcomeCommitter deferredQuality = new();
            CombatEquipmentCraftOutputTransaction deferredTransaction = new(
                new CombatEquipmentRuntimeStateStore(
                    new DungeonRuntimeAggregateRootStore()),
                new FixedBuildingWorld(facility),
                uniquePublication,
                new FixedRejectedSaleDestination(
                    new Vector2Int(14, 6),
                    unique.Claims),
                UnavailableEquipmentPhysicalItemGateway.Instance,
                deferredQuality,
                (IObservedProductionDomainOutputPublicationService)
                    uniquePublication);
            CombatEquipmentCraftOrderSaveData deferredOrder = new()
            {
                orderId = "combat-craft:quality-deferred",
                definitionId = "weapon:dagger",
                facilityPersistentId = facilityId,
                qualityAttemptIndex = 0,
                attemptOutcomeResolved = true,
                resolvedQuality = CombatEquipmentQuality.Good,
                resolvedMakerCharacterId = "character:qa:smith",
                resolvedMakerDisplayName = "시험 대장장이",
                resolvedAbsoluteDay = 4,
                qualityOutcomeSchemaVersion = 1,
                outputOperationId = CombatEquipmentCraftOutputOutbox
                    .FormatOperationId(
                        "combat-craft:quality-deferred",
                        0),
                outputPhase = CombatEquipmentCraftOutputPhase
                    .ResolvedWaitingForPublication
            };
            ProductionDomainOutputPublicationResult deferred =
                deferredTransaction.EnsureCommitted(deferredOrder);
            if (deferred.Status != ProductionDomainOutputPublicationStatus.Pending
                || deferredQuality.PrepareCalls != 1
                || deferredOrder.qualityOutcomeCommitted
                || unique.Query.GetAllStacks().Count != 0)
            {
                return false;
            }
            CombatEquipmentCraftOutputTransaction uniqueTransaction = new(
                new CombatEquipmentRuntimeStateStore(
                    new DungeonRuntimeAggregateRootStore()),
                new FixedBuildingWorld(facility),
                uniquePublication,
                new FixedRejectedSaleDestination(
                    new Vector2Int(14, 6),
                    unique.Claims),
                UnavailableEquipmentPhysicalItemGateway.Instance,
                qualityFacts,
                (IObservedProductionDomainOutputPublicationService)
                    uniquePublication);
            const string InstanceId =
                "item-instance:combat-common-output:dagger:001";
            CombatEquipmentInstance prepared = new()
            {
                instanceId = InstanceId,
                definitionId = "weapon:dagger",
                materialId = "material:iron",
                quality = CombatEquipmentQuality.Normal,
                durabilityRatio = 1f,
                worldState = CombatEquipmentWorldState.Loose,
                ownerCharacterId = string.Empty,
                sourceStackId = string.Empty,
                evolution = new EquipmentEvolutionState(),
                moduleSlots = new List<EquipmentModuleSlotState>()
            };
            CombatEquipmentCraftOrderSaveData equipmentOrder = new()
            {
                orderId = "combat-craft:common-equipment",
                definitionId = "weapon:dagger",
                facilityPersistentId = facilityId,
                qualityAttemptIndex = 0,
                attemptOutcomeResolved = true,
                resolvedQuality = CombatEquipmentQuality.Normal,
                resolvedMakerCharacterId = "character:qa:smith",
                resolvedMakerDisplayName = "시험 대장장이",
                resolvedAbsoluteDay = 4,
                qualityOutcomeSchemaVersion = 1,
                minimumQuality = CraftsmanshipQualityTier.Good,
                rejectedDisposition = RejectedOutputDisposition.MarkForSale,
                outputItemId = PhysicalItemIds.ForEquipment("weapon:dagger"),
                outputOperationId = CombatEquipmentCraftOutputOutbox
                    .FormatOperationId(
                        "combat-craft:common-equipment",
                        0),
                outputQuantity = 1,
                outputCapability = FreezeCapability(
                    CombatEquipmentCraftOutputCapability.OutputLineId,
                    PhysicalItemIds.ForEquipment("weapon:dagger"),
                    ProductionOutputCapabilityIds.CombatEquipmentCraft,
                    ProductionOutputCapabilityIds.CombatEquipmentCraftVersion,
                    ProductionOutputCapabilityIds.CombatEquipmentStateCodec,
                    ProductionOutputCapabilityIds
                        .CombatEquipmentStateCodecVersion),
                outputInstanceId = InstanceId,
                outputPreparedComponent = EquipmentItemStateCodec.Encode(prepared),
                outputPhase = CombatEquipmentCraftOutputPhase
                    .ResolvedWaitingForPublication
            };
            StampCompletionContext(
                equipmentOrder,
                "world-item-stack:qa:combat-input:iron");
            completionProbe.FaultMode =
                MigratedProducerOutcomeFaultMode.RejectReservation;
            ProductionDomainOutputPublicationResult rejectedEquipmentCommit =
                uniqueTransaction.EnsureCommitted(equipmentOrder);
            int equipmentOutputCountAfterRejection =
                unique.Query.GetAllStacks().Count;
            bool rejectedEquipmentOwnerClean =
                equipmentOrder.outputPublication != null
                && !equipmentOrder.outputPublication.outputPublished
                && !equipmentOrder.outputPublication.observationExpected
                && equipmentOrder.outputPublication.stacks.Count == 0
                && equipmentOrder.completionFact?.IsEmpty == true
                && unique.Repository.EquipmentInstances.Count == 0;
            CombatEquipmentCraftOrderSaveData restoredEquipment =
                JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                    JsonUtility.ToJson(equipmentOrder));
            completionProbe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            ProductionDomainOutputPublicationResult equipmentCommit =
                uniqueTransaction.EnsureCommitted(restoredEquipment);
            bool equipmentObservation =
                uniqueTransaction.TryEnsureObservationDelivered(
                restoredEquipment,
                out _);
            equipmentOrder = restoredEquipment;
            if (rejectedEquipmentCommit.IsCommitted
                || equipmentOutputCountAfterRejection != 0
                || !rejectedEquipmentOwnerClean)
            {
                return false;
            }
            if (!equipmentCommit.IsCommitted
                || !equipmentObservation
                || string.IsNullOrWhiteSpace(
                    equipmentOrder.outputPublication.observationKey)
                || !equipmentOrder.outputPublication.observationDelivered
                || equipmentOrder.completionFact?.IsEmpty != false
                || equipmentOrder.outputPhase != CombatEquipmentCraftOutputPhase
                    .PublishedAwaitingInputAcknowledgement
                || equipmentOrder.outputPublication.stacks.Count != 1
                || equipmentOrder.outputPublication.stacks[0].itemInstanceId
                    != InstanceId
                || !equipmentOrder.outputPublication.releaseHasDestination
                || equipmentOrder.outputPublication.releaseDestinationId
                    != QualityRejectedOutputRules.MarketDestinationId
                || equipmentOrder.outputPublication.releaseDestinationX != 14
                || equipmentOrder.outputPublication.releaseDestinationY != 6
                || unique.Query.GetAllStacks().Count != 1
                || unique.Repository.EquipmentInstances.Count != 1
                || !uniqueTransaction.TryAcknowledgeAndRoute(
                    equipmentOrder,
                    markForSale: true,
                    out _)
                || !uniqueTransaction.TryAcknowledgeAndRoute(
                    equipmentOrder,
                    markForSale: true,
                    out _)
                || !equipmentOrder.outputPublication.outputAcknowledged
                || !equipmentOrder.outputMarketRouted
                || unique.Repository.EquipmentInstances.Count != 1
                || unique.Query.GetAllStacks().Any(stack =>
                    stack.State != WorldItemStackState.Loose
                    || stack.DestinationId
                        != QualityRejectedOutputRules.MarketDestinationId
                    || !stack.HasDestinationPosition
                    || stack.DestinationPosition != new Vector2Int(14, 6)))
            {
                return false;
            }

            ProductionDomainOutputPublicationDebugScenarios.DomainFixture ammo =
                new(runtimeObject: facility,
                    facilityPersistentId: facility.RequirePersistentInstanceId().Value);
            IProductionDomainOutputPublicationService ammoPublication =
                CreateObservedPublication(
                    ammo,
                    completionFacts,
                    completionOutcomes);
            CombatEquipmentCraftOutputTransaction ammoTransaction = new(
                new CombatEquipmentRuntimeStateStore(
                    new DungeonRuntimeAggregateRootStore()),
                new FixedBuildingWorld(facility),
                ammoPublication,
                new FixedRejectedSaleDestination(
                    new Vector2Int(14, 6),
                    ammo.Claims),
                UnavailableEquipmentPhysicalItemGateway.Instance,
                qualityFacts,
                (IObservedProductionDomainOutputPublicationService)
                    ammoPublication);
            CombatEquipmentCraftOrderSaveData ammoOrder = new()
            {
                orderId = "combat-craft:common-ammunition",
                definitionId = CombatItemDefinitions.ArrowBundleRecipeId,
                facilityPersistentId = facilityId,
                qualityAttemptIndex = 0,
                attemptOutcomeResolved = true,
                resolvedQuality = CombatEquipmentQuality.Normal,
                resolvedMakerCharacterId = "character:qa:fletcher",
                resolvedMakerDisplayName = "시험 화살장이",
                resolvedAbsoluteDay = 4,
                qualityOutcomeSchemaVersion = 1,
                outputItemId = "item:qa:a",
                outputOperationId = CombatEquipmentCraftOutputOutbox
                    .FormatOperationId(
                        "combat-craft:common-ammunition",
                        0),
                outputQuantity = 5,
                outputCapability = FreezeCapability(
                    CombatAmmunitionCraftOutputCapability.OutputLineId,
                    "item:qa:a",
                    ProductionOutputCapabilityIds.CombatAmmunitionCraft,
                    ProductionOutputCapabilityIds.CombatAmmunitionCraftVersion,
                    ProductionOutputCapabilityIds.CombatAmmunitionStateCodec,
                    ProductionOutputCapabilityIds
                        .CombatAmmunitionStateCodecVersion),
                outputPhase = CombatEquipmentCraftOutputPhase
                    .ResolvedWaitingForPublication
            };
            StampCompletionContext(
                ammoOrder,
                "world-item-stack:qa:combat-input:wood");
            completionProbe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
            ProductionDomainOutputPublicationResult rejectedAmmoCommit =
                ammoTransaction.EnsureCommitted(ammoOrder);
            int ammoOutputCountAfterRejection = ammo.Query.GetAllStacks().Count;
            bool rejectedAmmoOwnerClean = ammoOrder.outputPublication != null
                && !ammoOrder.outputPublication.outputPublished
                && !ammoOrder.outputPublication.observationExpected
                && ammoOrder.outputPublication.stacks.Count == 0
                && ammoOrder.completionFact?.IsEmpty == true;
            CombatEquipmentCraftOrderSaveData restoredAmmo =
                JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                    JsonUtility.ToJson(ammoOrder));
            string beforeParent = JsonUtility.ToJson(restoredAmmo);
            completionProbe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            ProductionDomainOutputPublicationResult ammoCommit =
                ammoTransaction.EnsureCommitted(restoredAmmo);
            bool ammoObservation =
                ammoTransaction.TryEnsureObservationDelivered(
                    restoredAmmo,
                    out _);
            ammoOrder = restoredAmmo;
            bool valid = ammoCommit.IsCommitted
                && !rejectedAmmoCommit.IsCommitted
                && ammoOutputCountAfterRejection == 0
                && rejectedAmmoOwnerClean
                && ammoObservation
                && string.IsNullOrWhiteSpace(
                    ammoOrder.outputPublication.observationKey) == false
                && ammoOrder.outputPublication.observationDelivered
                && ammoOrder.completionFact?.IsEmpty == false
                && equipmentOrder.qualityOutcomeCommitted
                && ammoOrder.qualityOutcomeCommitted
                && ammoOrder.outputPublication.stacks.Count == 3
                && !ammoOrder.outputPublication.releaseHasDestination
                && ammoOrder.outputPublication.stacks.All(value =>
                    string.IsNullOrEmpty(value.itemInstanceId))
                && ammo.Repository.EquipmentInstances.Count == 0
                && ammoTransaction.TryAcknowledgeAndRoute(
                    ammoOrder,
                    markForSale: false,
                    out _)
                && ammoOrder.outputPublication.outputAcknowledged
                && !ammoOrder.outputMarketRouted
                && ammo.Query.GetAllStacks().All(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.IsNullOrEmpty(stack.DestinationId)
                    && !stack.HasDestinationPosition);
            if (!valid
                || completionProbe.CommitCount(MigratedProducerOutcomeKind
                    .CombatEquipmentCraftCompletionResult) != 2)
            {
                return false;
            }

            GameplayOutcomeReplayIdentity identity =
                MigratedProducerOutcomeE2EAssertions.RequireExact(
                    completionOutcomes,
                    MigratedProducerOutcomeKind
                        .CombatEquipmentCraftCompletionResult,
                    completionProbe,
                    beforeParent,
                    JsonUtility.ToJson(ammoOrder));
            return completionOutcomes.Ledger.TryGetExact(
                    identity.OutcomeId,
                    out GameplayOutcomeSnapshot exact)
                && exact.facts.Any(value =>
                    value.factId == "craft-command-context"
                    && value.value.Contains("material-allowed", StringComparison.Ordinal)
                    && value.value.Contains("material-priority", StringComparison.Ordinal)
                    && value.value.Contains("order-queued", StringComparison.Ordinal)
                    && value.value.Contains("worker-policy", StringComparison.Ordinal)
                    && value.value.Contains("quality-target", StringComparison.Ordinal))
                && exact.facts.Any(value =>
                    value.factId == "input-source-stacks"
                    && value.value != "none")
                && exact.facts.Any(value =>
                    value.factId == "output-stacks"
                    && value.value != "none");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
        }
    }

    private static ProductionOutputCapabilitySaveData FreezeCapability(
        string outputLineId,
        string itemId,
        string capabilityId,
        int capabilityVersion,
        string codecId,
        int codecVersion) => ProductionOutputCapabilitySaveData.Freeze(
        new ProductionOutputCapabilityDescriptor(
            outputLineId,
            itemId,
            capabilityId,
            capabilityVersion,
            codecId,
            codecVersion,
            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                outputLineId,
                itemId,
                capabilityId,
                capabilityVersion,
                codecId,
                codecVersion)));

    private static CombatEquipmentCraftOrderSaveData CreateOrder(string suffix) =>
        new()
        {
            orderId = "combat-craft:" + suffix,
            definitionId = CombatItemDefinitions.ArrowBundleRecipeId,
            materialDestinationId = "facility-input:combat-craft:" + suffix,
            facilityPersistentId = "building:qa",
            requiredWork = 4f,
            craftWorkPerAttempt = 4f,
            qualityAttemptIndex = 0
        };

    private static int CountCommittedOutput(
        FixtureGateway gateway,
        string commitId) => gateway.GetAllStacks()
        .Where(stack => ProductionOutputCommitComponentCodec.Matches(
            stack.Components,
            commitId))
        .Sum(stack => stack.Quantity);

    private static void Validate(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> orders,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts) =>
        CombatEquipmentCraftMaterialRestoreGuard.ValidateOwnerSet(
            orders,
            GetRequirements,
            new CandidateQuery(receipts));

    private static bool Reject(
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> orders,
        params PhysicalItemRestoreCandidateDispositionSnapshot[] receipts)
    {
        try
        {
            Validate(orders, receipts);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool GetRequirements(
        CombatEquipmentCraftOrderSaveData order,
        out IReadOnlyDictionary<string, int> requirements)
    {
        requirements = Requirements;
        return order != null;
    }

    private static PhysicalItemRestoreCandidateDispositionSnapshot Copy(
        PhysicalItemRestoreCandidateDispositionSnapshot value,
        string fingerprint = null,
        long? mass = null) => new(
        value.Kind,
        value.OperationId,
        value.ReasonCode,
        fingerprint ?? value.RequestFingerprint,
        value.SourceStackIds,
        value.Quantity,
        mass ?? value.InputMassGrams,
        value.CommitId);

    private sealed class CandidateQuery : IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            values;

        internal CandidateQuery(
            params PhysicalItemRestoreCandidateDispositionSnapshot[] values) =>
            this.values = values ?? Array.Empty<PhysicalItemRestoreCandidateDispositionSnapshot>();

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => values;

        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot value)
        {
            value = values.FirstOrDefault(candidate => string.Equals(
                candidate.OperationId,
                operationId,
                StringComparison.Ordinal));
            return value != null;
        }
    }

    private static void StampCompletionContext(
        CombatEquipmentCraftOrderSaveData order,
        string sourceStackId)
    {
        order.materialId = "material:iron";
        order.requiredWork = 12f;
        order.completedWork = 12f;
        order.materialsReady = true;
        order.materialTransferCommitId = "batch-commit:qa:" + order.orderId;
        order.materialTransferMassGrams = 2500L;
        order.materialTransferInputs = new List<
            CombatEquipmentCraftMaterialTransferInput>
        {
            new()
            {
                itemId = "material:iron-ingot",
                sourceStackId = sourceStackId,
                quantity = 2
            }
        };
        order.contributions = new List<CraftContributionSaveData>
        {
            new()
            {
                characterId = order.resolvedMakerCharacterId,
                contributedWork = 12f,
                relevantSkill = 7f
            }
        };
        order.commandRevision = 5L;
        order.commandReceipts = new List<CombatCraftCommandReceiptSaveData>
        {
            Command("material-allowed", 1L, "false", "true"),
            Command("material-priority", 2L, "4", "1"),
            Command("order-queued", 3L, "none", order.orderId),
            Command("worker-policy", 4L, "anyone", "best-quality"),
            Command("quality-target", 5L, "awful", "good")
        };
    }

    private static CombatCraftCommandReceiptSaveData Command(
        string kind,
        long revision,
        string before,
        string after) => new()
    {
        commandKind = kind,
        revision = revision,
        beforeValue = before,
        afterValue = after,
        absoluteDay = 4
    };

    private static IProductionDomainOutputPublicationService
        CreateObservedPublication(
            ProductionDomainOutputPublicationDebugScenarios.DomainFixture domain,
            DomainFactDispatcher facts,
            MigratedProducerOutcomeEditorFixture outcomes) =>
        ProductionDomainOutputPublicationDebugScenarios.Service(
            domain,
            facts: facts,
            recovery: facts,
            deliveries: new GameplayDomainFactDeliveryQuery(
                outcomes.Recorder,
                outcomes.Ledger));

    // Observes the actual service result without issuing an extra producer/ACK command.
    private sealed class ObservedOutputProbe : IProductionDomainOutputPublicationService,
        IObservedProductionDomainOutputPublicationService
    {
        private readonly IProductionDomainOutputPublicationService inner;
        private readonly IObservedProductionDomainOutputPublicationService observed;
        internal string LastAcknowledgementFailure { get; private set; } = string.Empty;
        internal string LastRecoveryFailure { get; private set; } = string.Empty;
        internal ObservedOutputProbe(IProductionDomainOutputPublicationService inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            observed = inner as IObservedProductionDomainOutputPublicationService
                ?? throw new InvalidOperationException("EEC06 requires the real observed publication service.");
        }
        public ProductionDomainOutputPublicationResult EnsureCommitted(
            ProductionDomainOutputPublicationSaveData owner, ProductionDomainOutputPublicationPlan plan) =>
            inner.EnsureCommitted(owner, plan);
        public ProductionDomainOutputPublicationResult EnsureCommitted<TFact>(
            ProductionDomainOutputPublicationSaveData owner, ProductionDomainOutputPublicationPlan plan,
            Func<ProductionDomainOutputFactPreview, TFact> capture) => observed.EnsureCommitted(owner, plan, capture);
        public bool TryRecover<TFact>(ProductionDomainOutputPublicationSaveData owner, string ownerId,
            Func<ProductionDomainOutputFactPreview, TFact> capture, out string failureReason)
        {
            bool recovered = observed.TryRecover(owner, ownerId, capture, out failureReason);
            LastRecoveryFailure = failureReason;
            return recovered;
        }
        public bool TryAcknowledge(ProductionDomainOutputPublicationSaveData owner, out string failureReason)
        {
            bool acknowledged = inner.TryAcknowledge(owner, out failureReason);
            LastAcknowledgementFailure = failureReason;
            return acknowledged;
        }
    }

    private sealed class CombatCraftCompletionFactProbeObserver :
        IDurableDomainFactObserver<CombatEquipmentCraftCompletedFact>,
        IDurableDomainFactStagingObserver<CombatEquipmentCraftCompletedFact>,
        IDurableDomainFactRecoveryObserver<CombatEquipmentCraftCompletedFact>
    {
        private static readonly MigratedProducerOutcomeKind Kind =
            MigratedProducerOutcomeKind.CombatEquipmentCraftCompletionResult;

        private readonly CombatEquipmentCraftCompletionFactObserver inner;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private ProbeCommitGroup commits;

        internal CombatCraftCompletionFactProbeObserver(
            CombatEquipmentCraftCompletionFactObserver inner,
            MigratedProducerOutcomeProbeTransaction probe)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.probe = probe ?? throw new ArgumentNullException(nameof(probe));
        }

        public bool TryReserve(
            in CombatEquipmentCraftCompletedFact fact,
            out object reservation,
            out string failure)
        {
            reservation = null;
            if (!probe.TryObserveDomainFactReservation(Kind, out failure))
                return false;
            return inner.TryReserve(fact, out reservation, out failure);
        }

        public bool TryPrepare(
            in CombatEquipmentCraftCompletedFact fact,
            object reservation,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            if (!inner.TryPrepare(
                    fact,
                    reservation,
                    out IDomainFactPreparedPublication innerPrepared,
                    out failure))
            {
                return false;
            }
            prepared = Wrap(
                innerPrepared,
                ResultKey(fact));
            return true;
        }

        public bool TryStage(
            in CombatEquipmentCraftCompletedFact fact,
            out object reservation,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            reservation = null;
            prepared = null;
            if (!probe.TryObserveDomainFactReservation(Kind, out failure))
                return false;
            if (!inner.TryStage(
                    fact,
                    out reservation,
                    out IDomainFactPreparedPublication innerPrepared,
                    out failure))
            {
                return false;
            }
            prepared = Wrap(
                innerPrepared,
                ResultKey(fact));
            return true;
        }

        public bool TryPrepareReplay(
            string publicationKey,
            in CombatEquipmentCraftCompletedFact fact,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            if (!inner.TryPrepareReplay(
                    publicationKey,
                    fact,
                    out IDomainFactPreparedPublication innerPrepared,
                    out failure))
            {
                return false;
            }
            prepared = Wrap(
                innerPrepared,
                ResultKey(fact));
            return true;
        }

        public DomainFactDelivery Replay(
            in CombatEquipmentCraftCompletedFact fact) => inner.Replay(fact);

        public void Cancel(object reservation) => inner.Cancel(reservation);

        private static GameplayResultKey ResultKey(
            CombatEquipmentCraftCompletedFact fact) =>
            MigratedProducerOutcomeCatalog.ResultKey(
                Kind,
                fact.OwnerIdentity,
                1L);

        private IDomainFactPreparedPublication Wrap(
            IDomainFactPreparedPublication prepared,
            GameplayResultKey resultKey)
        {
            if (prepared?.CommitGroup == null)
                throw new InvalidOperationException(
                    "Combat craft probe received no commit authority.");
            commits ??= new ProbeCommitGroup(prepared.CommitGroup, probe);
            commits.Require(prepared.CommitGroup);
            return new ProbePublication(commits, prepared, resultKey);
        }

        private sealed class ProbePublication :
            IDomainFactPreparedPublication,
            IDomainFactPreparedIdentity
        {
            internal ProbePublication(
                ProbeCommitGroup commits,
                IDomainFactPreparedPublication inner,
                GameplayResultKey resultKey)
            {
                CommitGroup = commits;
                Inner = inner;
                ResultKey = resultKey;
                Identity = inner as IDomainFactPreparedIdentity
                    ?? throw new InvalidOperationException(
                        "Combat craft probe publication has no exact identity.");
            }

            public IDomainFactCommitGroup CommitGroup { get; }
            internal IDomainFactPreparedPublication Inner { get; }
            internal GameplayResultKey ResultKey { get; }
            private IDomainFactPreparedIdentity Identity { get; }
            public string OperationKey => Identity.OperationKey;
            public long OwnerRevision => Identity.OwnerRevision;
            public bool IsReplay => Identity.IsReplay;
            public void Cancel() => Inner.Cancel();
        }

        private sealed class ProbeCommitGroup : IDomainFactCommitGroup
        {
            private readonly IDomainFactCommitGroup inner;
            private readonly MigratedProducerOutcomeProbeTransaction probe;

            internal ProbeCommitGroup(
                IDomainFactCommitGroup inner,
                MigratedProducerOutcomeProbeTransaction probe)
            {
                this.inner = inner
                    ?? throw new ArgumentNullException(nameof(inner));
                this.probe = probe
                    ?? throw new ArgumentNullException(nameof(probe));
            }

            internal void Require(IDomainFactCommitGroup value)
            {
                if (!ReferenceEquals(inner, value))
                {
                    throw new InvalidOperationException(
                        "Combat craft probe commit authority changed.");
                }
            }

            public bool Commit(
                IReadOnlyList<IDomainFactPreparedPublication> publications,
                DomainFactDelivery[] results,
                out string failure)
            {
                if (publications == null
                    || results == null
                    || publications.Count == 0
                    || publications.Count != results.Length)
                {
                    throw new ArgumentException(
                        "Invalid combat craft probe publication cohort.");
                }

                var values = new ProbePublication[publications.Count];
                bool reject = false;
                for (int index = 0; index < publications.Count; index++)
                {
                    if (publications[index] is not ProbePublication value
                        || !ReferenceEquals(value.CommitGroup, this))
                    {
                        throw new ArgumentException(
                            "Mismatched combat craft probe commit group.");
                    }
                    values[index] = value;
                    if (!value.IsReplay)
                    {
                        reject |= probe.ObserveDomainFactCommitAttempt(Kind);
                    }
                }

                if (reject)
                {
                    failure =
                        "migrated-producer-e2e-injected-batch-commit-rejection";
                    for (int index = 0; index < values.Length; index++)
                    {
                        values[index].Cancel();
                        results[index] = DomainFactDelivery.Reject(failure);
                    }
                    return false;
                }

                IDomainFactPreparedPublication[] innerPublications = values
                    .Select(value => value.Inner)
                    .ToArray();
                bool committed = inner.Commit(
                    innerPublications,
                    results,
                    out failure);
                if (committed)
                {
                    for (int index = 0; index < values.Length; index++)
                    {
                        if (!values[index].IsReplay
                            && results[index].MayBeCommitted)
                        {
                            probe.ObserveDomainFactCommitted(
                                Kind,
                                values[index].ResultKey);
                        }
                    }
                }
                return committed;
            }
        }
    }

    private sealed class DeferringProductQualityOutcomeCommitter :
        IDomainFactStagingPublisher
    {
        public int PrepareCalls { get; private set; }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failureReason)
        {
            PrepareCalls++;
            stage = null;
            failureReason = "injected-product-quality-capacity-deferral";
            return false;
        }
    }

    private sealed class CandidateOutputQuery :
        IPhysicalItemRestoreCandidateOutputQuery
    {
        private readonly IReadOnlyList<
            PhysicalItemRestoreCandidateOutputSnapshot> values;

        private CandidateOutputQuery(
            IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot> values)
        {
            this.values = values;
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot>
            CommittedOutputs => values;

        public bool TryGetCommittedOutput(
            string commitId,
            out IReadOnlyList<PhysicalItemRestoreCandidateOutputSnapshot> outputs)
        {
            PhysicalItemRestoreCandidateOutputSnapshot[] matches = values
                .Where(value => value != null && string.Equals(
                    value.CommitId,
                    commitId,
                    StringComparison.Ordinal))
                .ToArray();
            outputs = Array.AsReadOnly(matches);
            return matches.Length > 0;
        }

        internal static CandidateOutputQuery Capture(
            FixtureGateway gateway,
            string commitId,
            long massGrams)
        {
            WorldItemStackSnapshot[] stacks = gateway.GetAllStacks()
                .Where(value => value != null
                    && ProductionOutputCommitComponentCodec.Matches(
                        value.Components,
                        commitId))
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .ToArray();
            return new CandidateOutputQuery(Array.AsReadOnly(stacks
                .Select(value => new PhysicalItemRestoreCandidateOutputSnapshot(
                    commitId,
                    value.StackId,
                    value.ItemId,
                    value.Quantity,
                    massGrams,
                    value.State,
                    value.Position,
                    value.DestinationId))
                .ToArray()));
        }
    }

    private sealed class FixedRejectedSaleDestination :
        IQualityRejectedSaleDestinationAuthority
    {
        private readonly Vector2Int position;
        private readonly IFacilityBufferDestinationClaimCommand claims;

        internal FixedRejectedSaleDestination(
            Vector2Int position,
            IFacilityBufferDestinationClaimCommand claims)
        {
            this.position = position;
            this.claims = claims;
        }

        public bool TryEnsureTarget(
            out FacilityBufferAcknowledgedOutputReleaseTarget target,
            out string failureReason)
        {
            FacilityBufferDestinationClaim claim = new(
                QualityRejectedOutputRules.MarketDestinationId,
                position,
                QualityRejectedSaleDestinationAuthority.OwnerDomain,
                QualityRejectedSaleDestinationAuthority.OwnerOperationId,
                null,
                FacilityBufferDestinationAnchorKind.ReservedTarget);
            if (!claims.TryReplaceOwnedClaims(
                    QualityRejectedSaleDestinationAuthority.OwnerDomain,
                    new[] { claim },
                    out _,
                    out failureReason))
            {
                target = default;
                return false;
            }
            target = new FacilityBufferAcknowledgedOutputReleaseTarget(
                QualityRejectedOutputRules.MarketDestinationId,
                position);
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FixedBuildingWorld : IBuildingWorldQuery
    {
        internal FixedBuildingWorld(params BuildableObject[] buildings) =>
            Buildings = buildings ?? Array.Empty<BuildableObject>();

        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    private sealed class FixtureGateway : IEquipmentPhysicalItemGateway
    {
        private readonly WorldItemRepository repository;
        private readonly WorldItemQueryService query;
        private readonly IPhysicalItemBatchDispositionService dispositions;

        internal FixtureGateway(
            IDungeonItemCatalogProvider catalog,
            WorldItemRepository sharedRepository = null)
        {
            repository = sharedRepository ?? new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            PhysicalItemMassQuery mass = new(catalog);
            query = new WorldItemQueryService(
                catalog,
                mass,
                repository,
                EditorNullItemMarkerPresenter.Instance);
            dispositions = new PhysicalItemBatchDispositionService(
                repository,
                mass,
                EditorNullItemMarkerPresenter.Instance);
        }

        internal bool FailNextAcknowledgement { get; set; }

        internal string Add(string itemId, int quantity, string destinationId) =>
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                quantity,
                WorldItemStackState.FacilityBuffer,
                destinationId: destinationId);

        internal string AddUnique(
            string itemId,
            string itemInstanceId,
            WorldItemStackState state,
            string destinationId,
            IReadOnlyList<ItemInstanceComponentSaveData> components) =>
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                1,
                state,
                destinationId: destinationId,
                itemInstanceId: itemInstanceId,
                components: components);

        internal int Quantity(string stackId) =>
            repository.GetEditorTestQuantity(stackId);

        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            query.GetAllStacks();

        public bool SpawnItemAtWithComponents(
            string itemId,
            int amount,
            Vector2Int position,
            WorldItemStackState state,
            string destinationId,
            IReadOnlyList<ItemInstanceComponentSaveData> components,
            out int spawned)
        {
            WorldItemRepositoryEditorAccess.AddStack(
                repository,
                itemId,
                amount,
                state,
                destinationId: destinationId,
                position: position,
                components: components);
            spawned = amount;
            return true;
        }

        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => dispositions.TryCommitPending(
            inputs, kind, operationId, reasonCode, out receipt, out failureReason);

        public bool TryGetPendingBatchPhysicalDisposition(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            dispositions.TryGetPending(operationId, out receipt);

        public bool AcknowledgeBatchPhysicalDisposition(
            string commitId,
            out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "injected-acknowledgement-failure";
                return false;
            }
            return dispositions.Acknowledge(commitId, out failureReason);
        }

        public bool SpawnItemAt(string itemId, int amount, Vector2Int position,
            WorldItemStackState state, string destinationId, out int spawned) =>
            SpawnItemAtWithComponents(itemId, amount, position, state,
                destinationId, Array.Empty<ItemInstanceComponentSaveData>(), out spawned);
        public bool SpawnExistingUniqueItemAt(string itemId, ItemInstanceId itemInstanceId,
            Vector2Int position, WorldItemStackState state, string destinationId,
            out string stackId)
        {
            stackId = WorldItemRepositoryEditorAccess.AddStack(repository, itemId, 1,
                state, destinationId: destinationId, position: position,
                itemInstanceId: itemInstanceId.Value);
            return true;
        }
        public bool TryAbsorbUniqueItemStack(string stackId, ItemInstanceId expectedInstanceId) => false;
        public bool TryRequestItemDelivery(string itemId, int amount,
            Vector2Int destinationPosition, string destinationId, out int requested,
            out string failureReason) { requested = 0; failureReason = "not-used"; return false; }
        public bool TryConsumeFacilityItemBuffer(string destinationId,
            IReadOnlyDictionary<string, int> costs, out string failureReason)
        { failureReason = "forbidden"; return false; }
        public bool DeleteStack(string stackId) =>
            WorldItemRepositoryEditorAccess.TryRemoveStack(repository, stackId);
        public bool TryConsumeStackQuantity(string stackId, int quantity,
            out WorldItemStackSnapshot consumed) { consumed = null; return false; }
        public bool TryCommitBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind, string operationId, string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt, out string failureReason) =>
            dispositions.TryCommit(inputs, kind, operationId, reasonCode,
                out receipt, out failureReason);
        public bool TrySetInstanceComponent(string stackId,
            ItemInstanceComponentSaveData component) => false;
        public bool TryRemoveInstanceComponent(string stackId, string componentTypeId) => false;
        public int ReleaseStacksByDestination(string destinationId,
            Vector2Int releasePosition) => 0;
    }

    private sealed class AcceptingCraftInputDestinations :
        ICombatEquipmentCraftInputDestinationRuntime
    {
        internal int OpenCalls { get; private set; }
        internal int RequestCalls { get; private set; }
        internal int CloseCalls { get; private set; }

        public bool TryOpen(
            CombatEquipmentCraftOrderSaveData order,
            BuildableObject facility,
            IReadOnlyDictionary<string, int> requirements,
            out string failureReason)
        {
            OpenCalls++;
            failureReason = string.Empty;
            return true;
        }

        public bool TryRequest(
            CombatEquipmentCraftOrderSaveData order,
            IReadOnlyDictionary<string, int> requirements,
            out string failureReason)
        {
            RequestCalls++;
            failureReason = string.Empty;
            return true;
        }

        public bool TryClear(
            CombatEquipmentCraftOrderSaveData order,
            string reasonCode,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryClose(
            CombatEquipmentCraftOrderSaveData order,
            string reasonCode,
            out string failureReason)
        {
            CloseCalls++;
            failureReason = string.Empty;
            return true;
        }

        public bool TryValidateProjection(
            CombatEquipmentCraftOrderSaveData order,
            IReadOnlyDictionary<string, int> requirements,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryValidateAuthority(
            CombatEquipmentCraftOrderSaveData order,
            IReadOnlyDictionary<string, int> requirements,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryReplace(
            IReadOnlyList<CombatEquipmentCraftInputDestinationProjection> desired,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FixedExcellentCraftQualityResolver :
        ICraftQualityResolver
    {
        public CraftQualityRollSaveData Roll(
            ulong runSeed,
            string pipelineId,
            string definitionId,
            int attemptIndex) => new()
        {
            attemptIndex = Math.Max(0, attemptIndex)
        };

        public CraftQualityResolution Resolve(
            CraftQualityRollSaveData roll,
            float weightedSkill,
            float facilityBonus,
            float toolBonus,
            float complexityPenalty) => new(
            75f,
            CraftsmanshipQualityTier.Excellent);
    }
}
