#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CombatEquipmentTerminalDrainOutboxDebugScenarios
{
    private const int PendingQuantity = 3;
    private const long PendingMassGrams = 3_000L;
    private const int WipQuantity = 2;
    private const long WipMassGrams = 2_000L;
    private const long WipOutputMassGrams = 700L;
    private const long WipLossMassGrams = 1_300L;

    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Combat Equipment Terminal Drain Outbox")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_COMBAT_EQUIPMENT_TERMINAL_DRAIN_OUTBOX=PASS");
    }

    public static void RunAll()
    {
        Require(string.Equals(
                CombatEquipmentTerminalDrainCanonical.ParticipantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .CombatEquipmentCrafting,
                StringComparison.Ordinal),
            "Craft and repair terminal producers did not share the one combat participant id.");
        VerifyCraftRepairFrozenPrepareReplayAndDrift();
        VerifyProducerChildAndEffectAheadCrashWindows();
        VerifyExactTerminalReceiptsAcknowledgementAndChildFirstGc();
        VerifyRestoreDuplicateOrphanTamperAndAtomicity();
    }

    private static void VerifyCraftRepairFrozenPrepareReplayAndDrift()
    {
        Fixture fixture = new();
        DrainCase craft = fixture.AddCraft("frozen-craft", withChild: true);
        DrainCase repair = fixture.AddRepair("frozen-repair", withChild: false);

        Require(fixture.Outbox.TryPrepare(craft.Request).Status ==
                CombatEquipmentTerminalDrainStatus.Applied
            && fixture.Outbox.TryPrepare(craft.Request).Status ==
                CombatEquipmentTerminalDrainStatus.Replay,
            "Craft producer prepare/replay was not stable.");
        Require(fixture.Outbox.TryPrepare(repair.Request).Status ==
                CombatEquipmentTerminalDrainStatus.Applied,
            "Repair producer did not share the combat-equipment participant outbox.");

        CombatEquipmentTerminalDrainSaveData[] frozen = fixture.Outbox
            .CaptureCurrentFormat().ToArray();
        Require(frozen.Length == 2
            && frozen.Select(value => value.source.sourceKind).SequenceEqual(
                new[]
                {
                    CombatEquipmentTerminalSourceKind.CraftOrder,
                    CombatEquipmentTerminalSourceKind.RepairOrder
                })
            && frozen[0].source.sourcePayload.Contains(
                craft.Source.SourceId,
                StringComparison.Ordinal)
            && frozen[1].source.sourcePayload.Contains(
                repair.Source.SourceId,
                StringComparison.Ordinal),
            "Full frozen craft/repair source payloads were not retained canonically.");

        CombatEquipmentTerminalDrainRequest fingerprintDrift = new(
            craft.ParentOperationId,
            craft.StepOperationId + ":fingerprint-drift",
            craft.Source,
            craft.ChildStepOperationId + ":fingerprint-drift",
            craft.ChildReceipt.requestFingerprint,
            new string('f', 64));
        Require(fixture.Outbox.TryPrepare(fingerprintDrift).Status ==
                CombatEquipmentTerminalDrainStatus.Conflict,
            "A request fingerprint drift was accepted.");

        CombatEquipmentCraftOrderSaveData sameOrder =
            JsonUtility.FromJson<CombatEquipmentCraftOrderSaveData>(
                craft.Source.SourcePayload);
        Require(CombatEquipmentTerminalFrozenSubject.TryCreateCraftOrder(
                sameOrder,
                new CombatEquipmentTerminalMassAccounting(
                    PendingQuantity,
                    PendingMassGrams - 1L,
                    WipQuantity,
                    WipMassGrams,
                    WipOutputMassGrams,
                    WipLossMassGrams),
                out CombatEquipmentTerminalFrozenSubject oneGramDrift,
                out string oneGramFailure),
            "One-gram drift subject construction unexpectedly failed: "
            + oneGramFailure);
        Require(string.Equals(
                oneGramDrift.SourceFingerprint,
                craft.Source.SourceFingerprint,
                StringComparison.Ordinal),
            "Pending custody incorrectly remained part of stable source identity.");
        ProductionInputDestinationCustodyDrainSaveData gramDriftChild =
            CreateChildReceipt(
                craft.ParentOperationId,
                craft.ChildStepOperationId + ":gram-drift",
                oneGramDrift,
                PendingQuantity,
                PendingMassGrams);
        ProductionInputDestinationCustodySourceSnapshot originalCustody = new(
            gramDriftChild.sourceDestinationId,
            1L,
            gramDriftChild.sourceOwnershipFingerprint,
            gramDriftChild.sourceStacks,
            gramDriftChild.sourceOperations,
            gramDriftChild.sourceActors,
            gramDriftChild.inputQuantity,
            gramDriftChild.inputMassGrams);
        Require(!CombatEquipmentTerminalPreparedSource.TryCreate(
                oneGramDrift,
                originalCustody,
                out _,
                out _),
            "Prepared source accepted a one-gram custody mismatch.");
        CombatEquipmentTerminalDrainRequest gramDrift = CreateRequest(
            craft.ParentOperationId,
            craft.StepOperationId + ":gram-drift",
            oneGramDrift,
            gramDriftChild);
        Require(fixture.Outbox.TryPrepare(gramDrift).Status ==
                CombatEquipmentTerminalDrainStatus.Conflict,
            "A one-gram child custody drift was accepted.");

        CombatEquipmentTerminalDrainSaveData kindDrift = frozen[0].Clone();
        kindDrift.source.sourceKind =
            CombatEquipmentTerminalSourceKind.RepairOrder;
        kindDrift.source.ownerStableId =
            ProductionFacilityDestructiveDrainOwnerStableIds
                .EquipmentRepairOrder(kindDrift.source.sourceId);
        kindDrift.source.sourceFingerprint =
            CombatEquipmentTerminalDrainCanonical.CreateSourceFingerprint(
                kindDrift.source);
        Require(!fixture.Outbox.TryRestoreCurrentFormat(
                    new[] { kindDrift },
                    Array.Empty<ProductionInputDestinationCustodyDrainSaveData>(),
                    out _),
            "Owner-kind drift survived full frozen payload validation.");
    }

    private static void VerifyProducerChildAndEffectAheadCrashWindows()
    {
        Fixture fixture = new();
        DrainCase subject = fixture.AddCraft("ahead", withChild: true);
        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                CombatEquipmentTerminalDrainStatus.Applied,
            "Producer-first prepare failed.");
        string producerOnly = Serialize(fixture.Outbox.CaptureCurrentFormat());
        CombatEquipmentTerminalDrainResult missingChild = fixture.Outbox
            .TryProgress(subject.StepOperationId);
        Require(missingChild.Status == CombatEquipmentTerminalDrainStatus.Deferred
            && string.Equals(
                Serialize(fixture.Outbox.CaptureCurrentFormat()),
                producerOnly,
                StringComparison.Ordinal),
            "Missing child did not preserve a mutation-free producer-ahead checkpoint.");

        fixture.Child.PublishCommitted(subject.ChildReceipt);
        Require(fixture.Outbox.TryRecover(subject.StepOperationId).Phase ==
                CombatEquipmentTerminalDrainPhase
                    .InputDestinationReceiptRecordedAwaitingAcknowledgement
            && fixture.Outbox.TryProgress(subject.StepOperationId).Phase ==
                CombatEquipmentTerminalDrainPhase
                    .InputDestinationAcknowledgedAwaitingTerminalEffects,
            "Child-ahead receipt and acknowledgement did not recover exactly.");

        CombatEquipmentTerminalWipLossReceiptSaveData wip =
            CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(
                subject.Source);
        CombatEquipmentTerminalSourceRemovalReceiptSaveData removal =
            CombatEquipmentTerminalDrainCanonical.CreateSourceRemovalReceipt(
                subject.Source);
        CombatEquipmentTerminalInputDispositionEvidence inputEvidence =
            Evidence(subject.ChildReceipt);
        Require(fixture.Source.TryPublishWipLossReceipt(
                    wip,
                    inputEvidence).Status ==
                CombatEquipmentTerminalEffectStatus.Applied
            && fixture.Source.TryRemoveExactSource(
                    subject.Source,
                    removal,
                    inputEvidence).Status ==
                CombatEquipmentTerminalEffectStatus.Applied,
            "Effect-ahead crash fixture could not publish exact WIP/removal effects.");

        CombatEquipmentTerminalDrainResult recovered = fixture.Outbox
            .TryRecover(subject.StepOperationId);
        Require(recovered.Status == CombatEquipmentTerminalDrainStatus.Applied
            && recovered.Phase == CombatEquipmentTerminalDrainPhase
                .TerminalEffectsCommittedAwaitingOwnerAcknowledgement
            && fixture.Source.WipPublishCount == 1
            && fixture.Source.RemovalCount == 1,
            "Effect-ahead recovery duplicated or lost a terminal effect.");

        Fixture childOnly = new();
        DrainCase orphan = childOnly.AddCraft("child-orphan", withChild: true);
        childOnly.Child.PublishCommitted(orphan.ChildReceipt);
        Require(childOnly.Outbox.TryRecover(orphan.StepOperationId).Status ==
                CombatEquipmentTerminalDrainStatus.Conflict
            && childOnly.Outbox.CaptureCurrentFormat().Count == 0,
            "A child-only orphan was treated as a producer checkpoint.");
    }

    private static void
        VerifyExactTerminalReceiptsAcknowledgementAndChildFirstGc()
    {
        Fixture fixture = new();
        DrainCase subject = fixture.AddCraft("terminal", withChild: true);
        fixture.Child.PublishCommitted(subject.ChildReceipt);
        Require(fixture.Outbox.TryPrepare(subject.Request).Status ==
                CombatEquipmentTerminalDrainStatus.Applied,
            "Terminal fixture prepare failed.");
        AdvanceToTerminal(fixture, subject);

        Require(fixture.Outbox.TryCapture(
                subject.StepOperationId,
                out CombatEquipmentTerminalDrainSaveData terminal)
            && terminal.releasedInputQuantity == PendingQuantity
            && terminal.releasedInputMassGrams == PendingMassGrams
            && terminal.wipLossCommitId.StartsWith(
                CombatEquipmentTerminalDrainCanonical.WipCommitPrefix,
                StringComparison.Ordinal)
            && terminal.sourceRemovalCommitId.StartsWith(
                CombatEquipmentTerminalDrainCanonical.RemovalCommitPrefix,
                StringComparison.Ordinal)
            && fixture.Source.TryCaptureWipLossReceipt(
                terminal.wipLossCommitId,
                out CombatEquipmentTerminalWipLossReceiptSaveData wip)
            && wip.inputQuantity == WipQuantity
            && wip.inputMassGrams == WipMassGrams
            && wip.committedOutputMassGrams == WipOutputMassGrams
            && wip.declaredLossMassGrams == WipLossMassGrams
            && !fixture.Source.TryCaptureLiveSource(
                subject.Source.OwnerStableId,
                out _,
                out _),
            "Exact child/WIP/removal receipts did not close source authority.");

        Require(fixture.Outbox.TryAcknowledge(
                    subject.StepOperationId,
                    terminal.receiptFingerprint).Status ==
                CombatEquipmentTerminalDrainStatus.Applied
            && fixture.Outbox.TryAcknowledge(
                    subject.StepOperationId,
                    terminal.receiptFingerprint).Status ==
                CombatEquipmentTerminalDrainStatus.Replay,
            "Owner acknowledgement was not replay-safe.");

        bool childCollectedWhileProducerPresent = false;
        fixture.Child.OnGarbageCollected = collectedStepId =>
            childCollectedWhileProducerPresent = fixture.Outbox.TryCapture(
                subject.StepOperationId,
                out _);
        Require(fixture.Outbox.TryGarbageCollect(
                    subject.StepOperationId,
                    terminal.receiptFingerprint).Status ==
                CombatEquipmentTerminalDrainStatus.Applied
            && childCollectedWhileProducerPresent
            && !fixture.Outbox.TryCapture(subject.StepOperationId, out _)
            && fixture.Child.GarbageCollectionCount == 1,
            "Checkpoint GC did not retire child authority before producer authority.");
    }

    private static void VerifyRestoreDuplicateOrphanTamperAndAtomicity()
    {
        Fixture fixture = new();
        DrainCase first = fixture.AddCraft("restore-a", withChild: true);
        DrainCase second = fixture.AddRepair("restore-b", withChild: false);
        Require(fixture.Outbox.TryPrepare(first.Request).Status ==
                CombatEquipmentTerminalDrainStatus.Applied
            && fixture.Outbox.TryPrepare(second.Request).Status ==
                CombatEquipmentTerminalDrainStatus.Applied,
            "Restore fixture prepare failed.");
        CombatEquipmentTerminalDrainSaveData[] captured = fixture.Outbox
            .CaptureCurrentFormat().Select(value => value.Clone()).ToArray();
        string pristine = Serialize(captured);

        Fixture restored = fixture.CloneWithSameAuthorities();
        Require(restored.Outbox.TryRestoreCurrentFormat(
                captured,
                Array.Empty<ProductionInputDestinationCustodyDrainSaveData>(),
                out string restoreFailure)
            && string.Equals(
                Serialize(restored.Outbox.CaptureCurrentFormat()),
                pristine,
                StringComparison.Ordinal),
            "Producer-ahead current-format restore failed: " + restoreFailure);

        CombatEquipmentTerminalDrainSaveData duplicate = captured[0].Clone();
        Require(!restored.Outbox.TryRestoreCurrentFormat(
                    new[] { captured[0], duplicate },
                    Array.Empty<ProductionInputDestinationCustodyDrainSaveData>(),
                    out _)
            && string.Equals(
                Serialize(restored.Outbox.CaptureCurrentFormat()),
                pristine,
                StringComparison.Ordinal),
            "Duplicate restore was not rejected atomically.");

        ProductionInputDestinationCustodyDrainSaveData orphan =
            CreateChildReceipt(
                "drain:qa:orphan",
                "drain:qa:orphan:child",
                first.Source,
                PendingQuantity,
                PendingMassGrams);
        Require(!restored.Outbox.TryRestoreCurrentFormat(
                    captured,
                    new[] { orphan },
                    out string orphanFailure)
            && orphanFailure.Contains("orphan", StringComparison.Ordinal)
            && string.Equals(
                Serialize(restored.Outbox.CaptureCurrentFormat()),
                pristine,
                StringComparison.Ordinal),
            "Child-only restore orphan was not rejected atomically.");

        ProductionInputDestinationCustodyDrainSaveData oneGramChild =
            CreateChildReceipt(
                first.ParentOperationId,
                first.ChildStepOperationId,
                first.Source,
                PendingQuantity,
                PendingMassGrams - 1L);
        Require(!restored.Outbox.TryRestoreCurrentFormat(
                    captured,
                    new[] { oneGramChild },
                    out _)
            && string.Equals(
                Serialize(restored.Outbox.CaptureCurrentFormat()),
                pristine,
                StringComparison.Ordinal),
            "A valid but one-gram mismatched child restore was accepted.");
    }

    private static void AdvanceToTerminal(Fixture fixture, DrainCase subject)
    {
        Require(fixture.Outbox.TryProgress(subject.StepOperationId).Status ==
                CombatEquipmentTerminalDrainStatus.Applied
            && fixture.Outbox.TryProgress(subject.StepOperationId).Status ==
                CombatEquipmentTerminalDrainStatus.Applied
            && fixture.Outbox.TryProgress(subject.StepOperationId).Status ==
                CombatEquipmentTerminalDrainStatus.Applied,
            "Combat producer did not advance through child, ack, and terminal effects.");
    }

    private static CombatEquipmentTerminalDrainRequest CreateRequest(
        string parentOperationId,
        string stepOperationId,
        CombatEquipmentTerminalFrozenSubject source,
        ProductionInputDestinationCustodyDrainSaveData child)
    {
        ProductionInputDestinationCustodyDrainRequest childRequest =
            child == null
                ? null
                : new ProductionInputDestinationCustodyDrainRequest(
                    child.parentOperationId,
                    child.stepOperationId,
                    child.ownerStableId,
                    child.billId,
                    child.facilityId,
                    child.sourceDestinationId,
                    child.ownerGridX,
                    child.ownerGridY,
                    child.sourceClaimFingerprint,
                    child.sourceOwnershipFingerprint,
                    child.sourceStacks,
                    child.sourceOperations,
                    child.sourceActors,
                    child.inputQuantity,
                    child.inputMassGrams,
                    child.requestFingerprint);
        string fingerprint = CombatEquipmentTerminalDrainCanonical
            .CreateRequestFingerprint(
                parentOperationId,
                stepOperationId,
                source,
                childRequest?.StepOperationId ?? string.Empty,
                childRequest?.RequestFingerprint ?? string.Empty);
        return new CombatEquipmentTerminalDrainRequest(
            parentOperationId,
            stepOperationId,
            source,
            childRequest,
            fingerprint);
    }

    private static ProductionInputDestinationCustodyDrainSaveData
        CreateChildReceipt(
            string parentOperationId,
            string stepOperationId,
            CombatEquipmentTerminalFrozenSubject source,
            int quantity,
            long massGrams)
    {
        ProductionInputDestinationDrainStackSaveData stack = new()
        {
            stackId = "stack:qa:" + stepOperationId,
            itemId = "material:qa:combat-terminal",
            componentFingerprint = new string('c', 64),
            quantity = quantity,
            massGrams = massGrams,
            state = WorldItemStackState.Stored,
            positionX = 2,
            positionY = 3,
            sourceStorageDestinationId = "warehouse:qa:combat-terminal",
            destinationPositionX = 7,
            destinationPositionY = 9,
            reservationRevision = 4L
        };
        string claim = source.SourceFingerprint;
        string ownership = new string('b', 64);
        string request = ProductionInputDestinationCustodyDrainFingerprint
            .CreateRequest(
                parentOperationId,
                stepOperationId,
                source.OwnerStableId,
                source.SourceId,
                source.FacilityId,
                source.InputDestinationId,
                7,
                9,
                claim,
                ownership,
                new[] { stack },
                Array.Empty<ProductionInputDestinationDrainOperationSaveData>(),
                Array.Empty<ProductionInputDestinationDrainActorSaveData>(),
                quantity,
                massGrams);
        string result = new string('d', 64);
        string commit = ProductionInputDestinationCustodyDrainFingerprint
            .CreateCommit(stepOperationId, request);
        ProductionInputDestinationCustodyDrainSaveData child = new()
        {
            parentOperationId = parentOperationId,
            stepOperationId = stepOperationId,
            ownerStableId = source.OwnerStableId,
            billId = source.SourceId,
            facilityId = source.FacilityId,
            sourceDestinationId = source.InputDestinationId,
            ownerGridX = 7,
            ownerGridY = 9,
            sourceClaimFingerprint = claim,
            sourceOwnershipFingerprint = ownership,
            requestFingerprint = request,
            phase = ProductionInputDestinationCustodyDrainPhase
                .EffectCommittedAwaitingBillAck,
            sourceStacks = new List<ProductionInputDestinationDrainStackSaveData>
            {
                stack
            },
            sourceOperations = new List<
                ProductionInputDestinationDrainOperationSaveData>(),
            sourceActors = new List<
                ProductionInputDestinationDrainActorSaveData>(),
            completedActorIds = new List<string>(),
            releasedOperationIds = new List<string>(),
            releasedStackIds = new List<string> { stack.stackId },
            inputQuantity = quantity,
            inputMassGrams = massGrams,
            releasedQuantity = quantity,
            releasedMassGrams = massGrams,
            resultFingerprint = result,
            commitId = commit,
            receiptFingerprint = ProductionInputDestinationCustodyDrainFingerprint
                .CreateReceipt(
                    request,
                    result,
                    quantity,
                    massGrams,
                    new[] { stack.stackId },
                    Array.Empty<string>())
        };
        Require(ProductionInputDestinationCustodyDrainContract.IsValidSave(child),
            "Fixture child receipt is invalid.");
        return child;
    }

    private static CombatEquipmentTerminalInputDispositionEvidence Evidence(
        ProductionInputDestinationCustodyDrainSaveData child) => child == null
        ? new CombatEquipmentTerminalInputDispositionEvidence(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0L)
        : new CombatEquipmentTerminalInputDispositionEvidence(
            child.stepOperationId,
            child.requestFingerprint,
            child.commitId,
            child.receiptFingerprint,
            child.releasedQuantity,
            child.releasedMassGrams);

    private static string Serialize(
        IEnumerable<CombatEquipmentTerminalDrainSaveData> records) =>
        string.Join("\n", (records
                ?? Array.Empty<CombatEquipmentTerminalDrainSaveData>())
            .OrderBy(value => value.stepOperationId, StringComparer.Ordinal)
            .Select(JsonUtility.ToJson));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class DrainCase
    {
        internal string ParentOperationId { get; set; }
        internal string StepOperationId { get; set; }
        internal string ChildStepOperationId { get; set; }
        internal CombatEquipmentTerminalFrozenSubject Source { get; set; }
        internal ProductionInputDestinationCustodyDrainSaveData ChildReceipt
        { get; set; }
        internal CombatEquipmentTerminalDrainRequest Request { get; set; }
    }

    private sealed class Fixture
    {
        internal Fixture()
            : this(new DungeonRuntimeAggregateRootStore(), new RecordingSource(),
                new RecordingChild())
        {
        }

        private Fixture(
            DungeonRuntimeAggregateRootStore root,
            RecordingSource source,
            RecordingChild child)
        {
            Root = root;
            Source = source;
            Child = child;
            Outbox = new CombatEquipmentTerminalDrainOutbox(
                Root,
                Source,
                Child);
        }

        internal DungeonRuntimeAggregateRootStore Root { get; }
        internal RecordingSource Source { get; }
        internal RecordingChild Child { get; }
        internal CombatEquipmentTerminalDrainOutbox Outbox { get; }

        internal DrainCase AddCraft(string suffix, bool withChild)
        {
            CombatEquipmentCraftOrderSaveData dto = new()
            {
                orderId = "craft:qa:" + suffix,
                definitionId = "weapon:qa:terminal",
                materialId = "material:iron",
                requiredWork = 20f,
                completedWork = 7f,
                materialDestinationId = "facility-input:combat-craft:" + suffix,
                facilityPersistentId = "facility:qa:combat-terminal",
                destinationX = 7,
                destinationY = 9,
                materialTransferOperationId = "transfer:qa:" + suffix,
                materialTransferCommitId = "commit:qa:" + suffix,
                materialTransferRequestFingerprint = new string('e', 64),
                materialTransferMassGrams = WipMassGrams,
                materialTransferAcknowledged = true
            };
            return Add(
                dto,
                withChild,
                suffix);
        }

        internal DrainCase AddRepair(string suffix, bool withChild)
        {
            CombatEquipmentRepairOrder dto = new()
            {
                orderId = "repair:qa:" + suffix,
                equipmentInstanceId = "equipment-instance:qa:" + suffix,
                originalOwnerCharacterId = "character:qa:smith",
                facilityBuildingId = "facility:qa:combat-terminal",
                materialItemId = "material:iron",
                requiredMaterialAmount = WipQuantity,
                requiredWork = 15f,
                completedWork = 4f,
                state = CombatEquipmentRepairOrderState.InProgress,
                materialsConsumed = true,
                materialTransferOperationId = "repair-transfer:qa:" + suffix,
                materialTransferCommitId = "repair-commit:qa:" + suffix,
                materialTransferRequestFingerprint = new string('f', 64),
                materialTransferMassGrams = WipMassGrams,
                materialTransferAcknowledged = true
            };
            CombatEquipmentTerminalMassAccounting mass = Mass(withChild);
            Require(CombatEquipmentTerminalFrozenSubject.TryCreateRepairOrder(
                    dto,
                    mass,
                    out CombatEquipmentTerminalFrozenSubject source,
                    out string failure),
                "Repair frozen source failed: " + failure);
            return Add(source, suffix, withChild);
        }

        internal Fixture CloneWithSameAuthorities() => new(
            new DungeonRuntimeAggregateRootStore(),
            Source.Clone(),
            Child.Clone());

        private DrainCase Add(
            CombatEquipmentCraftOrderSaveData dto,
            bool withChild,
            string suffix)
        {
            Require(CombatEquipmentTerminalFrozenSubject.TryCreateCraftOrder(
                    dto,
                    Mass(withChild),
                    out CombatEquipmentTerminalFrozenSubject source,
                    out string failure),
                "Craft frozen source failed: " + failure);
            return Add(source, suffix, withChild);
        }

        private DrainCase Add(
            CombatEquipmentTerminalFrozenSubject source,
            string suffix,
            bool withChild)
        {
            Source.AddLive(source);
            string parent = "drain:qa:combat:" + suffix;
            string step = parent + ":producer";
            string childStep = withChild ? parent + ":input-child" : string.Empty;
            ProductionInputDestinationCustodyDrainSaveData child = withChild
                ? CreateChildReceipt(
                    parent,
                    childStep,
                    source,
                    PendingQuantity,
                    PendingMassGrams)
                : null;
            CombatEquipmentTerminalDrainRequest request = CreateRequest(
                parent,
                step,
                source,
                child);
            return new DrainCase
            {
                ParentOperationId = parent,
                StepOperationId = step,
                ChildStepOperationId = childStep,
                Source = source,
                ChildReceipt = child,
                Request = request
            };
        }

        private static CombatEquipmentTerminalMassAccounting Mass(
            bool withChild) => new(
            withChild ? PendingQuantity : 0,
            withChild ? PendingMassGrams : 0L,
            WipQuantity,
            WipMassGrams,
            WipOutputMassGrams,
            WipLossMassGrams);
    }

    private sealed class RecordingSource :
        ICombatEquipmentTerminalSourceAuthority
    {
        private readonly Dictionary<string, CombatEquipmentTerminalFrozenSubject>
            live = new(StringComparer.Ordinal);
        private readonly Dictionary<string,
            CombatEquipmentTerminalWipLossReceiptSaveData> wip =
            new(StringComparer.Ordinal);
        private readonly Dictionary<string,
            CombatEquipmentTerminalSourceRemovalReceiptSaveData> removals =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> garbageCollected =
            new(StringComparer.Ordinal);

        internal int WipPublishCount { get; private set; }
        internal int RemovalCount { get; private set; }

        internal void AddLive(CombatEquipmentTerminalFrozenSubject source) =>
            live.Add(source.OwnerStableId, source);

        internal RecordingSource Clone()
        {
            RecordingSource clone = new();
            foreach (KeyValuePair<string, CombatEquipmentTerminalFrozenSubject>
                     pair in live)
                clone.live.Add(pair.Key, pair.Value);
            foreach (KeyValuePair<string,
                     CombatEquipmentTerminalWipLossReceiptSaveData> pair in wip)
                clone.wip.Add(pair.Key, pair.Value.Clone());
            foreach (KeyValuePair<string,
                     CombatEquipmentTerminalSourceRemovalReceiptSaveData> pair
                      in removals)
                clone.removals.Add(pair.Key, pair.Value.Clone());
            foreach (string fingerprint in garbageCollected)
                clone.garbageCollected.Add(fingerprint);
            clone.WipPublishCount = WipPublishCount;
            clone.RemovalCount = RemovalCount;
            return clone;
        }

        public bool TryCaptureLiveSource(
            string ownerStableId,
            out CombatEquipmentTerminalFrozenSubject source,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (live.TryGetValue(ownerStableId, out source))
                return true;
            failureReason = "fixture-combat-source-missing";
            return false;
        }

        public bool TryCaptureLiveSourceForPreparation(
            string ownerStableId,
            out CombatEquipmentTerminalPreparedSource prepared,
            out string failureReason)
        {
            prepared = null;
            if (!TryCaptureLiveSource(
                    ownerStableId,
                    out CombatEquipmentTerminalFrozenSubject source,
                    out failureReason))
            {
                return false;
            }
            ProductionInputDestinationDrainStackSaveData[] stacks =
                source.PendingInputQuantity == 0
                    ? Array.Empty<ProductionInputDestinationDrainStackSaveData>()
                    : new[]
                    {
                        new ProductionInputDestinationDrainStackSaveData
                        {
                            stackId = "stack:qa:" + source.SourceId,
                            itemId = "item:qa:combat-terminal",
                            componentFingerprint = new string('8', 64),
                            quantity = source.PendingInputQuantity,
                            massGrams = source.PendingInputMassGrams,
                            state = WorldItemStackState.Stored,
                            reservationRevision = 0L
                        }
                    };
            ProductionInputDestinationCustodySourceSnapshot custody =
                string.IsNullOrEmpty(source.InputDestinationId)
                    ? null
                    : new ProductionInputDestinationCustodySourceSnapshot(
                        source.InputDestinationId,
                        1L,
                        new string('9', 64),
                        stacks,
                        Array.Empty<
                            ProductionInputDestinationDrainOperationSaveData>(),
                        Array.Empty<
                            ProductionInputDestinationDrainActorSaveData>(),
                        source.PendingInputQuantity,
                        source.PendingInputMassGrams);
            return CombatEquipmentTerminalPreparedSource.TryCreate(
                source,
                custody,
                out prepared,
                out failureReason);
        }

        public bool TryCaptureWipLossReceipt(
            string commitId,
            out CombatEquipmentTerminalWipLossReceiptSaveData receipt)
        {
            receipt = null;
            if (!wip.TryGetValue(commitId, out var value))
                return false;
            receipt = value.Clone();
            return true;
        }

        public bool TryCaptureSourceRemovalReceipt(
            string commitId,
            out CombatEquipmentTerminalSourceRemovalReceiptSaveData receipt)
        {
            receipt = null;
            if (!removals.TryGetValue(commitId, out var value))
                return false;
            receipt = value.Clone();
            return true;
        }

        [GameplayInternalOnly(
            "Focused fixture publishes deterministic WIP receipt authority.",
            "CombatEquipmentTerminalDrainOutboxDebugScenarios only")]
        public CombatEquipmentTerminalEffectResult TryPublishWipLossReceipt(
            CombatEquipmentTerminalWipLossReceiptSaveData receipt,
            CombatEquipmentTerminalInputDispositionEvidence inputEvidence)
        {
            if (!CombatEquipmentTerminalDrainCanonical.IsValidWipLossReceipt(
                    receipt))
                return Conflict("fixture-wip-invalid");
            if (wip.TryGetValue(receipt.commitId, out var existing))
            {
                return CombatEquipmentTerminalDrainCanonical.WipReceiptEquals(
                        existing,
                        receipt)
                    ? Replay(receipt.receiptFingerprint)
                    : Conflict("fixture-wip-conflict");
            }
            if (!live.TryGetValue(
                    receipt.ownerStableId,
                    out CombatEquipmentTerminalFrozenSubject source)
                || !string.Equals(
                    source.SourceFingerprint,
                    receipt.sourceFingerprint,
                    StringComparison.Ordinal)
                || inputEvidence == null
                || !inputEvidence.IsValidFor(source))
                return Conflict("fixture-wip-source-missing");
            wip.Add(receipt.commitId, receipt.Clone());
            WipPublishCount++;
            return Applied(receipt.receiptFingerprint);
        }

        [GameplayInternalOnly(
            "Focused fixture removes one exact frozen source after WIP receipt.",
            "CombatEquipmentTerminalDrainOutboxDebugScenarios only")]
        public CombatEquipmentTerminalEffectResult TryRemoveExactSource(
            CombatEquipmentTerminalFrozenSubject source,
            CombatEquipmentTerminalSourceRemovalReceiptSaveData receipt,
            CombatEquipmentTerminalInputDispositionEvidence inputEvidence)
        {
            if (source == null
                || !CombatEquipmentTerminalDrainCanonical
                    .IsValidSourceRemovalReceipt(receipt)
                || inputEvidence == null
                || !inputEvidence.IsValidFor(source))
                return Conflict("fixture-removal-invalid");
            if (removals.TryGetValue(receipt.commitId, out var existing))
            {
                return CombatEquipmentTerminalDrainCanonical
                    .RemovalReceiptEquals(existing, receipt)
                    ? Replay(receipt.receiptFingerprint)
                    : Conflict("fixture-removal-conflict");
            }
            if (!live.TryGetValue(source.OwnerStableId, out var current)
                || !string.Equals(current.SourceFingerprint,
                    source.SourceFingerprint, StringComparison.Ordinal))
                return Conflict("fixture-removal-source-drift");
            CombatEquipmentTerminalWipLossReceiptSaveData expectedWip =
                CombatEquipmentTerminalDrainCanonical
                    .CreateWipLossReceipt(source);
            if (expectedWip != null
                && (!wip.TryGetValue(expectedWip.commitId, out var actualWip)
                    || !CombatEquipmentTerminalDrainCanonical.WipReceiptEquals(
                        actualWip,
                        expectedWip)))
                return Conflict("fixture-removal-wip-missing");
            live.Remove(source.OwnerStableId);
            removals.Add(receipt.commitId, receipt.Clone());
            RemovalCount++;
            return Applied(receipt.receiptFingerprint);
        }

        [GameplayInternalOnly(
            "Focused fixture garbage-collects one exact terminal receipt pair.",
            "CombatEquipmentTerminalDrainOutboxDebugScenarios only")]
        public CombatEquipmentTerminalEffectResult TryGarbageCollectReceipts(
            CombatEquipmentTerminalFrozenSubject source,
            string wipReceiptFingerprint,
            string removalReceiptFingerprint)
        {
            if (source == null
                || string.IsNullOrEmpty(removalReceiptFingerprint))
                return Conflict("fixture-terminal-gc-invalid");

            string gcFingerprint = source.SourceFingerprint + "|"
                + (wipReceiptFingerprint ?? string.Empty) + "|"
                + removalReceiptFingerprint;
            if (garbageCollected.Contains(gcFingerprint))
                return Replay(removalReceiptFingerprint);

            CombatEquipmentTerminalWipLossReceiptSaveData expectedWip =
                CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(source);
            CombatEquipmentTerminalSourceRemovalReceiptSaveData expectedRemoval =
                CombatEquipmentTerminalDrainCanonical
                    .CreateSourceRemovalReceipt(source);
            if ((expectedWip == null
                    ? !string.IsNullOrEmpty(wipReceiptFingerprint)
                    : !wip.TryGetValue(expectedWip.commitId, out var actualWip)
                        || !CombatEquipmentTerminalDrainCanonical.WipReceiptEquals(
                            actualWip,
                            expectedWip)
                        || !string.Equals(
                            actualWip.receiptFingerprint,
                            wipReceiptFingerprint,
                            StringComparison.Ordinal))
                || !removals.TryGetValue(
                    expectedRemoval.commitId,
                    out CombatEquipmentTerminalSourceRemovalReceiptSaveData
                        actualRemoval)
                || !CombatEquipmentTerminalDrainCanonical.RemovalReceiptEquals(
                    actualRemoval,
                    expectedRemoval)
                || !string.Equals(
                    actualRemoval.receiptFingerprint,
                    removalReceiptFingerprint,
                    StringComparison.Ordinal))
            {
                return Conflict("fixture-terminal-gc-receipt-conflict");
            }

            if (expectedWip != null)
                wip.Remove(expectedWip.commitId);
            removals.Remove(expectedRemoval.commitId);
            garbageCollected.Add(gcFingerprint);
            return Applied(removalReceiptFingerprint);
        }

        private static CombatEquipmentTerminalEffectResult Applied(string fp) =>
            new(CombatEquipmentTerminalEffectStatus.Applied, fp, string.Empty);
        private static CombatEquipmentTerminalEffectResult Replay(string fp) =>
            new(CombatEquipmentTerminalEffectStatus.Replay, fp, string.Empty);
        private static CombatEquipmentTerminalEffectResult Conflict(string why) =>
            new(CombatEquipmentTerminalEffectStatus.Conflict, string.Empty, why);
    }

    private sealed class RecordingChild :
        IProductionInputDestinationCustodyDrainOutbox
    {
        private readonly Dictionary<string,
            ProductionInputDestinationCustodyDrainSaveData> records =
            new(StringComparer.Ordinal);

        internal Action<string> OnGarbageCollected { get; set; }
        internal int GarbageCollectionCount { get; private set; }

        internal void PublishCommitted(
            ProductionInputDestinationCustodyDrainSaveData value) =>
            records[value.stepOperationId] = value.Clone();

        internal RecordingChild Clone()
        {
            RecordingChild clone = new();
            foreach (var pair in records)
                clone.records.Add(pair.Key, pair.Value.Clone());
            clone.GarbageCollectionCount = GarbageCollectionCount;
            return clone;
        }

        public bool TryCapture(
            string stepOperationId,
            out ProductionInputDestinationCustodyDrainSaveData record)
        {
            record = null;
            if (!records.TryGetValue(stepOperationId, out var value))
                return false;
            record = value.Clone();
            return true;
        }

        public ProductionInputDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            if (!records.TryGetValue(stepOperationId, out var value))
                return ChildConflict("fixture-child-missing");
            if (!string.Equals(value.receiptFingerprint, receiptFingerprint,
                    StringComparison.Ordinal))
                return ChildConflict("fixture-child-receipt-conflict");
            if (value.phase == ProductionInputDestinationCustodyDrainPhase
                    .BillAcknowledgedAwaitingCheckpointGc)
                return ChildResult(
                    value,
                    ProductionInputDestinationCustodyDrainStatus.Replay);
            if (value.phase != ProductionInputDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingBillAck)
                return ChildConflict("fixture-child-phase-conflict");
            value.phase = ProductionInputDestinationCustodyDrainPhase
                .BillAcknowledgedAwaitingCheckpointGc;
            records[stepOperationId] = value.Clone();
            return ChildResult(
                value,
                ProductionInputDestinationCustodyDrainStatus.Applied);
        }

        public ProductionInputDestinationCustodyDrainResult TryGarbageCollect(
            string stepOperationId,
            string receiptFingerprint)
        {
            if (!records.TryGetValue(stepOperationId, out var value))
                return new ProductionInputDestinationCustodyDrainResult(
                    ProductionInputDestinationCustodyDrainStatus.Replay,
                    string.Empty,
                    receiptFingerprint,
                    string.Empty);
            if (value.phase != ProductionInputDestinationCustodyDrainPhase
                    .BillAcknowledgedAwaitingCheckpointGc
                || !string.Equals(value.receiptFingerprint, receiptFingerprint,
                    StringComparison.Ordinal))
                return ChildConflict("fixture-child-gc-conflict");
            records.Remove(stepOperationId);
            GarbageCollectionCount++;
            OnGarbageCollected?.Invoke(stepOperationId);
            return ChildResult(
                value,
                ProductionInputDestinationCustodyDrainStatus.Applied);
        }

        public ProductionInputDestinationCustodyDrainResult TryPrepare(
            ProductionInputDestinationCustodyDrainRequest request) =>
            Unexpected(nameof(TryPrepare));
        public ProductionInputDestinationCustodyDrainResult TryBeginDraining(
            string stepOperationId,
            string requestFingerprint) => Unexpected(nameof(TryBeginDraining));
        public ProductionInputDestinationCustodyDrainResult TryRecordActorCompleted(
            string stepOperationId,
            string actorId) => Unexpected(nameof(TryRecordActorCompleted));
        public ProductionInputDestinationCustodyDrainResult
            TryBeginReleasingOperationAuthority(string stepOperationId) =>
            Unexpected(nameof(TryBeginReleasingOperationAuthority));
        public ProductionInputDestinationCustodyDrainResult
            TryRecordOperationReleased(string stepOperationId, string operationId) =>
            Unexpected(nameof(TryRecordOperationReleased));
        public ProductionInputDestinationCustodyDrainResult
            TryBeginReleasingDestination(string stepOperationId) =>
            Unexpected(nameof(TryBeginReleasingDestination));
        public ProductionInputDestinationCustodyDrainResult TryCommitEffect(
            string stepOperationId,
            IEnumerable<string> releasedStackIds,
            int releasedQuantity,
            long releasedMassGrams,
            string resultFingerprint) => Unexpected(nameof(TryCommitEffect));

        private static ProductionInputDestinationCustodyDrainResult ChildResult(
            ProductionInputDestinationCustodyDrainSaveData value,
            ProductionInputDestinationCustodyDrainStatus status) => new(
            status,
            value.commitId,
            value.receiptFingerprint,
            string.Empty);
        private static ProductionInputDestinationCustodyDrainResult ChildConflict(
            string reason) => new(
            ProductionInputDestinationCustodyDrainStatus.Conflict,
            string.Empty,
            string.Empty,
            reason);
        private static ProductionInputDestinationCustodyDrainResult Unexpected(
            string operation) => throw new InvalidOperationException(
            "Combat terminal fixture unexpectedly invoked child "
            + operation + ".");
    }
}
#endif
