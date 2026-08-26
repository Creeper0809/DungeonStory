#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class CombatEquipmentCraftTerminalAuthorityDebugScenarios
{
    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Combat Craft Terminal Authority")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_COMBAT_CRAFT_TERMINAL_AUTHORITY=PASS");
    }

    public static void RunAll()
    {
        VerifyZeroWipExactRemovalAndGc();
        VerifyWipRowPrecedesCrashAheadAcknowledgement();
        VerifyFrozenOrderDriftPreservesSource();
    }

    private static void VerifyZeroWipExactRemovalAndGc()
    {
        Fixture fixture = new();
        CombatEquipmentCraftOrderSaveData order = Order("zero");
        fixture.Add(order);
        string owner = Owner(order.orderId);
        Require(fixture.Authority.TryCaptureLiveSourceForPreparation(
                owner,
                out CombatEquipmentTerminalPreparedSource prepared,
                out string failure),
            "Zero-WIP source capture failed: " + failure);
        CombatEquipmentTerminalFrozenSubject source = prepared.Source;
        Require(source.WipInputMassGrams == 0L
            && source.PendingInputMassGrams == 0L
            && prepared.Custody == null,
            "Zero-WIP source captured unexpected custody or mass.");

        CombatEquipmentTerminalSourceRemovalReceiptSaveData removal =
            CombatEquipmentTerminalDrainCanonical.CreateSourceRemovalReceipt(
                source);
        CombatEquipmentTerminalEffectResult removed = fixture.Authority
            .TryRemoveExactSource(source, removal, EmptyEvidence());
        Require(removed.Status == CombatEquipmentTerminalEffectStatus.Applied
            && fixture.Orders().Length == 0
            && fixture.Effects().Length == 1
            && fixture.Effects()[0].phase ==
                CombatEquipmentCraftTerminalEffectPhase.SourceRemoved
            && !fixture.Authority.TryCaptureLiveSource(owner, out _, out _)
            && fixture.Authority.TryCaptureSourceRemovalReceipt(
                removal.commitId,
                out CombatEquipmentTerminalSourceRemovalReceiptSaveData actual)
            && CombatEquipmentTerminalDrainCanonical.RemovalReceiptEquals(
                actual,
                removal),
            "Zero-WIP exact removal did not publish same-aggregate authority.");

        Require(fixture.Authority.TryGarbageCollectReceipts(
                    source,
                    string.Empty,
                    removal.receiptFingerprint).Status ==
                CombatEquipmentTerminalEffectStatus.Applied
            && fixture.Effects().Length == 0,
            "Exact craft terminal receipt GC did not remove only the effect row.");
    }

    private static void VerifyWipRowPrecedesCrashAheadAcknowledgement()
    {
        Fixture fixture = new();
        CombatEquipmentCraftOrderSaveData order = Order("wip");
        order.materialsReady = true;
        order.materialTransferOperationId = CombatEquipmentCraftMaterialOutbox
            .FormatOperationId(order.orderId, order.qualityAttemptIndex);
        order.materialTransferInputs = new List<
            CombatEquipmentCraftMaterialTransferInput>
        {
            new()
            {
                itemId = "item:qa:iron",
                sourceStackId = "stack:qa:iron",
                quantity = 2
            }
        };
        order.materialTransferMassGrams = 2_000L;
        order.materialTransferRequestFingerprint =
            CombatEquipmentCraftMaterialOutbox.CreateRequestFingerprint(
                order.materialTransferInputs);
        order.materialTransferCommitId =
            $"physical-batch-disposition:"
            + $"{(int)PhysicalItemDispositionKind.Transfer}:"
            + $"{order.materialTransferOperationId}:2:2000";
        fixture.Add(order);

        Require(fixture.Authority.TryCaptureLiveSourceForPreparation(
                Owner(order.orderId),
                out CombatEquipmentTerminalPreparedSource prepared,
                out string failure),
            "WIP source capture failed: " + failure);
        CombatEquipmentTerminalFrozenSubject source = prepared.Source;
        CombatEquipmentTerminalWipLossReceiptSaveData wip =
            CombatEquipmentTerminalDrainCanonical.CreateWipLossReceipt(source);
        CombatEquipmentTerminalInputDispositionEvidence evidence = EmptyEvidence();
        Require(source.WipInputQuantity == 2
            && source.WipInputMassGrams == 2_000L
            && source.DeclaredLossMassGrams == 2_000L
            && wip != null
            && fixture.Authority.TryPublishWipLossReceipt(wip, evidence).Status ==
                CombatEquipmentTerminalEffectStatus.Applied
            && fixture.Orders().Length == 1
            && fixture.Effects().Length == 1
            && fixture.Effects()[0].phase ==
                CombatEquipmentCraftTerminalEffectPhase
                    .WipPreparedAwaitingInputDispositionAcknowledgement,
            "WIP row was not durable before source disposition acknowledgement.");

        CombatEquipmentCraftMaterialRestoreGuard.ValidateOwnerSet(
            fixture.Orders(),
            fixture.Effects(),
            Requirements,
            EmptyPhysicalCandidate.Instance);
        RequireThrows(() => CombatEquipmentCraftMaterialRestoreGuard
                .ValidateOwnerSet(
                    fixture.Orders(),
                    Requirements,
                    EmptyPhysicalCandidate.Instance),
            "A missing physical receipt without the terminal takeover row was accepted.");

        CombatEquipmentTerminalSourceRemovalReceiptSaveData removal =
            CombatEquipmentTerminalDrainCanonical.CreateSourceRemovalReceipt(
                source);
        Require(fixture.Authority.TryRemoveExactSource(
                    source,
                    removal,
                    evidence).Status == CombatEquipmentTerminalEffectStatus.Applied
            && fixture.Orders().Length == 0
            && fixture.Effects()[0].phase ==
                CombatEquipmentCraftTerminalEffectPhase.SourceRemoved,
            "Missing pending physical receipt was not recovered as exact WIP-row crash-ahead.");
    }

    private static void VerifyFrozenOrderDriftPreservesSource()
    {
        Fixture fixture = new();
        CombatEquipmentCraftOrderSaveData order = Order("drift");
        fixture.Add(order);
        Require(fixture.Authority.TryCaptureLiveSourceForPreparation(
                Owner(order.orderId),
                out CombatEquipmentTerminalPreparedSource prepared,
                out _),
            "Drift source capture failed.");
        CombatEquipmentCraftOrderSaveData changed = order.Clone();
        changed.completedWork = 1f;
        fixture.Replace(changed);
        CombatEquipmentTerminalFrozenSubject source = prepared.Source;
        CombatEquipmentTerminalSourceRemovalReceiptSaveData removal =
            CombatEquipmentTerminalDrainCanonical.CreateSourceRemovalReceipt(
                source);
        Require(fixture.Authority.TryRemoveExactSource(
                    source,
                    removal,
                    EmptyEvidence()).Status ==
                CombatEquipmentTerminalEffectStatus.Conflict
            && fixture.Orders().Length == 1
            && fixture.Effects().Length == 0,
            "A one-field frozen-order drift removed or mutated the live source.");
    }

    private static CombatEquipmentCraftOrderSaveData Order(string suffix) => new()
    {
        orderId = "craft:qa:terminal-authority:" + suffix,
        definitionId = "recipe:qa:terminal-authority",
        materialId = "material:qa:iron",
        requiredWork = 10f,
        completedWork = 0f,
        facilityPersistentId = "facility:qa:terminal-authority",
        materialDestinationId = string.Empty,
        destinationX = 4,
        destinationY = 6
    };

    private static string Owner(string orderId) =>
        ProductionFacilityDestructiveDrainOwnerStableIds
            .CombatCraftOrder(orderId);

    private static CombatEquipmentTerminalInputDispositionEvidence
        EmptyEvidence() => new(
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0L);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private static bool Requirements(
        CombatEquipmentCraftOrderSaveData order,
        out IReadOnlyDictionary<string, int> requirements)
    {
        requirements = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["item:qa:iron"] = 2
        };
        return true;
    }

    private sealed class Fixture
    {
        private readonly CombatEquipmentRuntimeStateStore store = new(
            new DungeonRuntimeAggregateRootStore());

        internal Fixture()
        {
            Authority = new CombatEquipmentCraftTerminalAuthority(
                store,
                new NoInputDrain(),
                UnavailableEquipmentPhysicalItemGateway.Instance,
                new DefinitionOnlyMass());
        }

        internal CombatEquipmentCraftTerminalAuthority Authority { get; }

        internal void Add(CombatEquipmentCraftOrderSaveData order) =>
            CombatEquipmentCraftTerminalAuthorityEditorAccess.AddOrder(
                store,
                order);

        internal void Replace(CombatEquipmentCraftOrderSaveData order) =>
            CombatEquipmentCraftTerminalAuthorityEditorAccess.ReplaceOrder(
                store,
                order);

        internal CombatEquipmentCraftOrderSaveData[] Orders() =>
            CombatEquipmentCraftTerminalAuthorityEditorAccess.CaptureOrders(store);

        internal CombatEquipmentCraftTerminalEffectSaveData[] Effects() =>
            CombatEquipmentCraftTerminalAuthorityEditorAccess.CaptureEffects(store);
    }

    private sealed class NoInputDrain :
        IProductionInputDestinationCustodyDrainService
    {
        public bool RequiresImmediateRecoveryBeforeGameplayTick => true;

        public bool TryCaptureSource(
            string sourceDestinationId,
            out ProductionInputDestinationCustodySourceSnapshot snapshot,
            out string failureReason)
        {
            snapshot = null;
            failureReason = "fixture-input-drain-unexpected";
            return false;
        }

        public bool TryBuildRequest(
            string parentOperationId,
            string stepOperationId,
            string ownerStableId,
            string billId,
            string facilityId,
            Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            ProductionInputDestinationCustodySourceSnapshot snapshot,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        {
            request = null;
            failureReason = "fixture-input-drain-unexpected";
            return false;
        }

        public bool TryCaptureRequest(
            string parentOperationId,
            string stepOperationId,
            string ownerStableId,
            string billId,
            string facilityId,
            string sourceDestinationId,
            Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        {
            request = null;
            failureReason = "fixture-input-drain-unexpected";
            return false;
        }

        public ProductionInputDestinationCustodyDrainResult TryPrepare(
            ProductionInputDestinationCustodyDrainRequest request) => Unexpected();
        public ProductionInputDestinationCustodyDrainResult TryCommit(
            string stepOperationId,
            string requestFingerprint) => Unexpected();
        public ProductionInputDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint) => Unexpected();
        public ProductionInputDestinationCustodyDrainResult TryGarbageCollect(
            string stepOperationId,
            string receiptFingerprint) => Unexpected();
        public bool TryCapture(
            string stepOperationId,
            out ProductionInputDestinationCustodyDrainSaveData record)
        {
            record = null;
            return false;
        }

        private static ProductionInputDestinationCustodyDrainResult Unexpected() =>
            new(
                ProductionInputDestinationCustodyDrainStatus.Conflict,
                string.Empty,
                string.Empty,
                "fixture-input-drain-unexpected");
    }

    private sealed class DefinitionOnlyMass : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 1L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(1L);
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => new(1L);
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => new(1L);
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            new(Math.Max(1, lot.Quantity));
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new(Math.Max(1, quantity));
    }

    private sealed class EmptyPhysicalCandidate :
        IPhysicalItemRestoreCandidateQuery
    {
        internal static readonly EmptyPhysicalCandidate Instance = new();
        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => Array.Empty<
                PhysicalItemRestoreCandidateDispositionSnapshot>();
        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = null;
            return false;
        }
    }
}
#endif
