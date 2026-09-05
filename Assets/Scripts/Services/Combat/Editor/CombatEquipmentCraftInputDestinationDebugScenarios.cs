#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CombatEquipmentCraftInputDestinationDebugScenarios
{
    [MenuItem("Tools/Dungeon Story/QA/Run Combat Craft Input Destination Contracts")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("[PASS] combat equipment craft input destination contracts");
    }

    public static void RunAll()
    {
        AuthorityFixture authority = new();
        FixedMassQuery mass = new(new Dictionary<string, long>(
            StringComparer.Ordinal)
        {
            ["material:lumber"] = 300L,
            ["material:iron-ingot"] = 500L
        });
        RecordingGateway gateway = new();
        RecordingRelease release = new();
        FacilityBufferDestinationLifecycleService lifecycle = new(
            authority,
            authority,
            authority,
            authority);
        CombatEquipmentCraftInputDestinationRuntime runtime = new(
            mass,
            gateway,
            authority,
            authority,
            lifecycle,
            release);
        GameObject ownerObject = new("CombatCraftInputOwnerFixture");
        try
        {
            Facility facility = ownerObject.AddComponent<Facility>();
            facility.RestorePersistentIdentity(
                (BuildingInstanceId)"building:qa:combat-craft-input");
            IReadOnlyDictionary<string, int> requirements =
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    ["material:lumber"] = 2,
                    ["material:iron-ingot"] = 1
                };
            CombatEquipmentCraftOrderSaveData order = new()
            {
                orderId = "combat-craft:00000042",
                definitionId = "weapon:qa",
                materialDestinationId =
                    ReservedTargetDestinationIdentity.ExactFacilityInputPrefix
                    + "combat.equipment-crafting:combat-craft:00000042",
                facilityPersistentId =
                    facility.RequirePersistentInstanceId().Value,
                destinationX = facility.centerPos.x,
                destinationY = facility.centerPos.y,
                requiredWork = 1f,
                craftWorkPerAttempt = 1f
            };

            Require(runtime.TryOpen(
                    order,
                    facility,
                    requirements,
                    out string openFailure),
                openFailure);
            Require(order.materialBufferCapacityGrams == 1_100L,
                "exact batch capacity was not frozen");
            Require(order.materialMassAuthorityRevision == mass.AuthorityRevision,
                "mass authority revision was not frozen");
            Require(!string.IsNullOrWhiteSpace(
                    order.materialCapacityFingerprint),
                "capacity fingerprint was not frozen");
            Require(authority.Claims.Single().AdmissionPolicy ==
                    FacilityBufferDestinationAdmissionPolicy.ExactGramRequired,
                "exact-gram claim was not published");
            Require(authority.Profiles.Single().MaxMassGrams == 1_100L,
                "positive gram profile was not published");
            Require(runtime.TryRequest(order, requirements, out string requestFailure),
                requestFailure);
            Require(gateway.Requests.SequenceEqual(new[]
                {
                    "material:iron-ingot:1",
                    "material:lumber:2"
                }, StringComparer.Ordinal),
                "delivery requests were not stable-ordered and exact");

            CombatEquipmentCraftOrderSaveData restored = order.Clone();
            Require(runtime.TryReplace(new[]
                {
                    new CombatEquipmentCraftInputDestinationProjection(
                        restored,
                        requirements)
                }, out string replaceFailure), replaceFailure);
            CombatEquipmentCraftOrderSaveData tampered = restored.Clone();
            tampered.materialBufferCapacityGrams++;
            Require(!runtime.TryReplace(new[]
                {
                    new CombatEquipmentCraftInputDestinationProjection(
                        tampered,
                        requirements)
                }, out _), "one-gram restore drift was accepted");

            Require(runtime.TryClose(
                    order,
                    "fixture-terminal-close",
                    out string closeFailure),
                closeFailure);
            Require(release.CallCount == 1
                    && authority.Claims.Count == 0
                    && authority.Profiles.Count == 0,
                "carried-aware close did not retire the authority pair");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ownerObject);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly IReadOnlyDictionary<string, long> grams;
        internal FixedMassQuery(IReadOnlyDictionary<string, long> grams) =>
            this.grams = grams;
        public long AuthorityRevision => 7L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(grams[itemId.Value]);
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(subject.ItemId);
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => GetDefinitionUnitMass(itemId);
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            GetQuantityMass(lot.Subject.ItemId, lot.Subject, lot.Quantity);
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new(checked(grams[itemId.Value] * quantity));
    }

    private sealed class AuthorityFixture :
        IFacilityBufferDestinationClaimAuthorityQuery,
        IFacilityBufferDestinationClaimCommand,
        IFacilityBufferMassCapacityAuthorityQuery,
        IFacilityBufferMassCapacityCommand
    {
        internal List<FacilityBufferDestinationClaim> Claims { get; } = new();
        internal List<FacilityBufferCapacityProfile> Profiles { get; } = new();
        public bool TryGetAuthorityClaim(
            string destinationId,
            Vector2Int dropPosition,
            out FacilityBufferDestinationClaim claim)
        {
            claim = Claims.SingleOrDefault(value => string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal) && value.DropPosition == dropPosition);
            return claim != null;
        }
        public IReadOnlyList<FacilityBufferDestinationClaim>
            CaptureAuthorityClaims() => Claims.ToArray();
        public bool TryClaim(
            FacilityBufferDestinationClaim claim,
            out FacilityBufferDestinationClaimFailureCode failureCode,
            out string failureReason) => TryReplaceOwnedClaims(
            claim.OwnerDomain,
            new[] { claim },
            out failureCode,
            out failureReason);
        public bool TryRevoke(
            FacilityBufferDestinationClaim expectedClaim,
            out FacilityBufferDestinationClaimFailureCode failureCode,
            out string failureReason)
        {
            Claims.Remove(expectedClaim);
            failureCode = FacilityBufferDestinationClaimFailureCode.None;
            failureReason = string.Empty;
            return true;
        }
        public bool TryReplaceOwnedClaims(
            string ownerDomain,
            IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            out FacilityBufferDestinationClaimFailureCode failureCode,
            out string failureReason)
        {
            Claims.RemoveAll(value => string.Equals(
                value.OwnerDomain, ownerDomain, StringComparison.Ordinal));
            Claims.AddRange(desiredClaims);
            failureCode = FacilityBufferDestinationClaimFailureCode.None;
            failureReason = string.Empty;
            return true;
        }
        public IReadOnlyList<FacilityBufferCapacityProfile>
            CaptureAuthorityProfiles() => Profiles.ToArray();
        public bool TryReplaceOwnedProfiles(
            string ownerDomain,
            IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out FacilityBufferMassAdmissionFailureCode failureCode,
            out string failureReason)
        {
            Profiles.RemoveAll(value => string.Equals(
                value.OwnerDomain, ownerDomain, StringComparison.Ordinal));
            Profiles.AddRange(desiredProfiles);
            failureCode = FacilityBufferMassAdmissionFailureCode.None;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class RecordingRelease :
        IFacilityBufferDestinationReleaseService
    {
        internal int CallCount { get; private set; }
        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            CallCount++;
            releasedQuantity = 0;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class RecordingGateway : IEquipmentPhysicalItemGateway
    {
        internal List<string> Requests { get; } = new();
        public bool TryRequestItemDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            Requests.Add(itemId + ":" + amount);
            requested = amount;
            failureReason = string.Empty;
            return true;
        }
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
            Array.Empty<WorldItemStackSnapshot>();
        public bool SpawnItemAt(string itemId, int amount, Vector2Int position,
            WorldItemStackState state, string destinationId, out int spawned)
        { spawned = 0; return false; }
        public bool SpawnItemAtWithComponents(string itemId, int amount,
            Vector2Int position, WorldItemStackState state, string destinationId,
            IReadOnlyList<ItemInstanceComponentSaveData> components, out int spawned)
        { spawned = 0; return false; }
        public bool SpawnExistingUniqueItemAt(string itemId,
            ItemInstanceId itemInstanceId, Vector2Int position,
            WorldItemStackState state, string destinationId, out string stackId)
        { stackId = string.Empty; return false; }
        public bool TryAbsorbUniqueItemStack(string stackId,
            ItemInstanceId expectedInstanceId) => false;
        public bool TryConsumeFacilityItemBuffer(string destinationId,
            IReadOnlyDictionary<string, int> costs, out string failureReason)
        { failureReason = string.Empty; return false; }
        public bool DeleteStack(string stackId) => false;
        public bool TryConsumeStackQuantity(string stackId, int quantity,
            out WorldItemStackSnapshot consumed)
        { consumed = null; return false; }
        public bool TryCommitBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind, string operationId,
            string reasonCode, out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        { receipt = default; failureReason = string.Empty; return false; }
        public bool TryCommitPendingBatchPhysicalDisposition(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind, string operationId,
            string reasonCode, out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason)
        { receipt = default; failureReason = string.Empty; return false; }
        public bool TryGetPendingBatchPhysicalDisposition(string operationId,
            out PhysicalItemBatchDispositionReceipt receipt)
        { receipt = default; return false; }
        public bool AcknowledgeBatchPhysicalDisposition(string commitId,
            out string failureReason)
        { failureReason = string.Empty; return false; }
        public bool TrySetInstanceComponent(string stackId,
            ItemInstanceComponentSaveData component) => false;
        public bool TryRemoveInstanceComponent(string stackId,
            string componentTypeId) => false;
        public int ReleaseStacksByDestination(string destinationId,
            Vector2Int releasePosition) => 0;
    }
}
#endif
