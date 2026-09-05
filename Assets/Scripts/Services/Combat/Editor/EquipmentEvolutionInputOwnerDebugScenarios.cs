#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class EquipmentEvolutionInputOwnerDebugScenarios
{
    private const string OrderId = "equipment-reforge:qa-0001";
    private const string DestinationId = "facility-reforge:" + OrderId;
    private const string FacilityId = "building:qa:equipment-reforge";
    private const string EquipmentItemId = "equipment:qa-blade";
    private const string EquipmentInstanceId = "equipment-instance:qa-blade-0001";
    private const string EquipmentStackId = "world-item:qa-blade-0001";

    [MenuItem("Tools/Dungeon Story/QA/Equipment Evolution Input Owner")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("EQUIPMENT_EVOLUTION_INPUT_OWNER_PASS");
    }

    public static void RunAll()
    {
        AuthorityFixture authority = new();
        RecordingDelivery delivery = new(CreateEquipmentStack(Component("baseline")));
        RecordingRelease release = new();
        MutableBuildingWorld buildings = new();
        Facility facility = CreateFacility(buildings);
        try
        {
            EquipmentEvolutionInputOwnerRuntime runtime = new(
                new FixedMassQuery(new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    [EquipmentItemId] = 2_500L,
                    ["material:iron-ingot"] = 500L,
                    ["resource:dark-resin"] = 300L
                }),
                delivery,
                buildings,
                authority,
                authority,
                new FacilityBufferDestinationLifecycleService(
                    authority,
                    authority,
                    authority,
                    authority),
                release);
            EquipmentEvolutionInputOwnerDescriptor opening = Descriptor(
                facility,
                Component("baseline"),
                storedCapacity: 0L,
                revision: 0L,
                fingerprint: string.Empty);
            Require(runtime.TryOpen(
                    opening,
                    out EquipmentEvolutionInputOwnerProjection projection,
                    out string openFailure),
                openFailure);
            Require(projection.CapacityGrams == 3_800L
                && projection.MassAuthorityRevision == 17L
                && !string.IsNullOrWhiteSpace(projection.CapacityFingerprint),
                "Owner did not freeze the exact positive equipment-plus-material grams.");
            Require(authority.Claims.Single().AnchorKind ==
                    FacilityBufferDestinationAnchorKind.LiveFacility
                && authority.Claims.Single().AdmissionPolicy ==
                    FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
                && authority.Claims.Single().OwnerFacilityId == FacilityId
                && authority.Profiles.Single().MaxMassGrams == 3_800L,
                "Owner did not publish an exact LiveFacility claim/profile pair.");

            EquipmentEvolutionInputOwnerDescriptor stored = Descriptor(
                facility,
                Component("baseline"),
                projection.CapacityGrams,
                projection.MassAuthorityRevision,
                projection.CapacityFingerprint);
            Require(runtime.TryRequest(stored, out string requestFailure),
                requestFailure);
            Require(delivery.Requests.SequenceEqual(new[]
                {
                    "item:material:iron-ingot:2",
                    "item:resource:dark-resin:1",
                    "stack:" + EquipmentStackId + ":1"
                }, StringComparer.Ordinal),
                "Material and unique-stack custody requests were not stable and exact.");
            Require(runtime.TryValidateAuthority(stored, out string validateFailure),
                validateFailure);
            Require(runtime.TryReplaceForRestore(
                    new[] { stored },
                    out string restoreFailure),
                restoreFailure);

            EquipmentEvolutionInputOwnerDescriptor oneGramDrift = Descriptor(
                facility,
                Component("baseline"),
                projection.CapacityGrams + 1L,
                projection.MassAuthorityRevision,
                projection.CapacityFingerprint);
            Require(!runtime.TryReplaceForRestore(
                    new[] { oneGramDrift },
                    out _)
                && authority.Claims.Count == 1
                && authority.Profiles.Count == 1,
                "Current-format restore accepted a one-gram projection drift.");

            delivery.Set(CreateEquipmentStack(Component("mutated")));
            Require(!runtime.TryValidateAuthority(stored, out _),
                "Unique equipment component custody drift was accepted.");
            delivery.Set(CreateEquipmentStack(Component("baseline")));

            release.Fail = true;
            Require(!runtime.TryClose(stored, "qa-terminal", out _)
                && authority.Claims.Count == 1
                && authority.Profiles.Count == 1,
                "Failed carried-aware release revoked the authority pair.");
            release.Fail = false;
            Require(runtime.TryClose(stored, "qa-terminal", out string closeFailure),
                closeFailure);
            Require(release.Calls == 2
                && authority.Claims.Count == 0
                && authority.Profiles.Count == 0,
                "Terminal close did not release before paired authority revoke.");

            facility.BuildingData.AbilityModules
                .Remove<BuildingEquipmentCraftingAbility>();
            Require(!runtime.TryOpen(opening, out _, out _),
                "A live building without equipment-crafting capability became an anchor.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facility.BuildingData);
            UnityEngine.Object.DestroyImmediate(facility.gameObject);
        }
    }

    private static Facility CreateFacility(MutableBuildingWorld buildings)
    {
        BuildingSO definition = ScriptableObject.CreateInstance<BuildingSO>();
        definition.AbilityModules.Add(new BuildingEquipmentCraftingAbility());
        GameObject owner = new("EquipmentEvolutionOwnerFacilityFixture");
        Facility facility = owner.AddComponent<Facility>();
        PropertyInfo buildingData = typeof(BuildableObject).GetProperty(
            nameof(BuildableObject.BuildingData),
            BindingFlags.Instance | BindingFlags.Public);
        buildingData?.SetValue(facility, definition);
        facility.RestorePersistentIdentity((BuildingInstanceId)FacilityId);
        facility.SetRuntimeGridPosition(new Vector2Int(8, 13));
        buildings.Values = new BuildableObject[] { facility };
        return facility;
    }

    private static EquipmentEvolutionInputOwnerDescriptor Descriptor(
        Facility facility,
        ItemInstanceComponentSaveData component,
        long storedCapacity,
        long revision,
        string fingerprint) => new(
        OrderId,
        DestinationId,
        FacilityId,
        facility.centerPos,
        EquipmentInstanceId,
        EquipmentItemId,
        EquipmentStackId,
        new[] { component },
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["resource:dark-resin"] = 1,
            ["material:iron-ingot"] = 2
        },
        storedCapacity,
        revision,
        fingerprint);

    private static ItemInstanceComponentSaveData Component(string value) => new()
    {
        componentTypeId = ItemInstanceComponentIds.Equipment,
        schemaVersion = 1,
        affectsStacking = true,
        values = new List<ItemStateValueSaveData>
        {
            new()
            {
                key = "qa-state",
                kind = ItemStateValueKind.String,
                stringValue = value
            }
        }
    };

    private static WorldItemStackSnapshot CreateEquipmentStack(
        ItemInstanceComponentSaveData component) => new()
    {
        StackId = EquipmentStackId,
        ItemId = EquipmentItemId,
        ItemInstanceId = EquipmentInstanceId,
        Quantity = 1,
        State = WorldItemStackState.Stored,
        Components = new[] { component }
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class MutableBuildingWorld : IBuildingWorldQuery
    {
        internal IReadOnlyList<BuildableObject> Values { get; set; } =
            Array.Empty<BuildableObject>();
        public IReadOnlyList<BuildableObject> Buildings => Values;
        public int BuildingVersion => 1;
    }

    private sealed class RecordingDelivery : IEquipmentEvolutionInputDeliveryGateway
    {
        private IReadOnlyList<WorldItemStackSnapshot> stacks;
        internal RecordingDelivery(params WorldItemStackSnapshot[] values) =>
            stacks = values;
        internal List<string> Requests { get; } = new();
        internal void Set(params WorldItemStackSnapshot[] values) => stacks = values;
        public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() => stacks;
        public bool TryRequestItemDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            Requests.Add("item:" + itemId + ":" + amount);
            requested = amount;
            failureReason = string.Empty;
            return true;
        }
        public bool TryRequestStackDelivery(
            string stackId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason)
        {
            Requests.Add("stack:" + stackId + ":" + amount);
            requested = amount;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly IReadOnlyDictionary<string, long> grams;
        internal FixedMassQuery(IReadOnlyDictionary<string, long> grams) =>
            this.grams = grams;
        public long AuthorityRevision => 17L;
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
        public IReadOnlyList<FacilityBufferDestinationClaim>
            CaptureAuthorityClaims() => Claims.ToArray();
        public bool TryGetAuthorityClaim(
            string destinationId,
            Vector2Int dropPosition,
            out FacilityBufferDestinationClaim claim)
        {
            claim = Claims.SingleOrDefault(value =>
                value.DestinationId == destinationId
                && value.DropPosition == dropPosition);
            return claim != null;
        }
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
            Claims.RemoveAll(value => value.OwnerDomain == ownerDomain);
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
            Profiles.RemoveAll(value => value.OwnerDomain == ownerDomain);
            Profiles.AddRange(desiredProfiles);
            failureCode = FacilityBufferMassAdmissionFailureCode.None;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class RecordingRelease :
        IFacilityBufferDestinationReleaseService
    {
        internal bool Fail { get; set; }
        internal int Calls { get; private set; }
        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            Calls++;
            releasedQuantity = 0;
            failureReason = Fail ? "qa-carried-release-rejected" : string.Empty;
            return !Fail;
        }
    }
}
#endif
