using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class OffenseUrgentMitigationInputOwnerDebugScenarios
{
    private const string OrderId = "threat-mitigation:qa-site:7";
    private const string FacilityId = "building:qa-urgent-mitigation";
    private const string ItemId = "material:lumber";
    private static readonly Vector2Int Position = new(5, 3);

    [MenuItem("Tools/Dungeon Story/QA/V27/Offense Urgent Mitigation Input Owner")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("OFFENSE_URGENT_MITIGATION_INPUT_OWNER_PASS");
    }

    public static void RunAll()
    {
        OffenseUrgentSiteDefinitionSO definition = CreateDefinition();
        GameObject facilityObject = new("QaUrgentMitigationOwnerFacility");
        try
        {
            BuildableObject facility = facilityObject.AddComponent<BuildableObject>();
            facility.RestorePersistentIdentity((BuildingInstanceId)FacilityId);
            SetPosition(facility, Position);

            AuthorityStore authorities = new();
            RecordingRelease releases = new();
            OffenseUrgentMitigationInputOwnerRuntime owner = new(
                new FixedContentCatalog(definition),
                new FixedMassQuery(ItemId, 1_200L),
                authorities,
                authorities,
                authorities,
                releases);
            OffenseUrgentMitigationOrderStateData order = CreateOrder();

            Require(owner.TryEnsure(order, facility, out string ensureFailure),
                ensureFailure);
            Require(order.inputBufferCapacityGrams == 3_600L
                    && order.inputMassAuthorityRevision == 17L
                    && order.inputCapacityFingerprint?.Length == 64,
                "Urgent mitigation order did not store its exact mass projection.");
            FacilityBufferDestinationClaim claim = authorities.Claims.Single();
            FacilityBufferCapacityProfile profile = authorities.Profiles.Single();
            Require(claim.AnchorKind
                        == FacilityBufferDestinationAnchorKind.LiveFacility
                    && claim.AdmissionPolicy
                        == FacilityBufferDestinationAdmissionPolicy
                            .ExactGramRequired
                    && claim.OwnerFacilityId == FacilityId
                    && claim.DropPosition == Position
                    && profile.MaxMassGrams == 3_600L
                    && profile.CapacityRevision
                        == OffenseUrgentMitigationInputOwnerAuthority
                            .CapacitySchemaRevision,
                "Urgent mitigation input lost LiveFacility/exact-gram authority.");
            Require(owner.TryValidateForCapture(
                    new[] { order },
                    new[] { facility },
                    out string captureFailure),
                captureFailure);

            string fingerprint = order.inputCapacityFingerprint;
            order.inputCapacityFingerprint = new string('b', 64);
            int replacementCount = authorities.ReplaceCalls;
            Require(!owner.TryReplaceForRestore(
                    new[] { order },
                    out string restoreFailure)
                    && restoreFailure.StartsWith(
                        "urgent-mitigation-input-owner-stored-projection-invalid:",
                        StringComparison.Ordinal)
                    && authorities.ReplaceCalls == replacementCount,
                "Restore accepted a stale input-capacity fingerprint.");
            order.inputCapacityFingerprint = fingerprint;
            Require(owner.TryReplaceForRestore(
                    new[] { order },
                    out restoreFailure),
                restoreFailure);

            releases.Fail = true;
            Require(!owner.TryRetire(
                    order,
                    OffenseUrgentMitigationInputOwnerAuthority
                        .FacilityLostReleaseReasonCode,
                    out string retireFailure)
                    && retireFailure.StartsWith(
                        "urgent-mitigation-input-owner-terminal-release-failed:",
                        StringComparison.Ordinal)
                    && authorities.Claims.Count == 1
                    && authorities.Profiles.Count == 1
                    && order.inputBufferCapacityGrams == 3_600L,
                "Failed carried-aware release revoked live ownership.");

            releases.Fail = false;
            Require(owner.TryRetire(
                    order,
                    OffenseUrgentMitigationInputOwnerAuthority
                        .FacilityLostReleaseReasonCode,
                    out retireFailure),
                retireFailure);
            Require(authorities.Claims.Count == 0
                    && authorities.Profiles.Count == 0
                    && OffenseUrgentMitigationInputOwnerAuthority
                        .StoredProjectionIsEmpty(order)
                    && releases.Calls == 2
                    && releases.LastDestinationId
                        == OffenseUrgentMitigationInputOwnerAuthority
                            .BuildDestinationId(OrderId)
                    && releases.LastPosition == Position,
                "Terminal close did not release before paired-authority revoke.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(facilityObject);
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static OffenseUrgentMitigationOrderStateData CreateOrder() => new()
    {
        orderId = OrderId,
        siteId = "qa-site",
        definitionId = "qa-urgent-definition",
        facilityPersistentId = FacilityId,
        facilityX = Position.x,
        facilityY = Position.y,
        destinationId = OffenseUrgentMitigationInputOwnerAuthority
            .BuildDestinationId(OrderId),
        requiredWork = 100f,
        status = OffenseUrgentMitigationOrderStatus.WaitingForMaterials
    };

    private static OffenseUrgentSiteDefinitionSO CreateDefinition()
    {
        OffenseUrgentSiteDefinitionSO definition =
            ScriptableObject.CreateInstance<OffenseUrgentSiteDefinitionSO>();
        definition.urgentSiteId = "qa-urgent-definition";
        definition.mitigationItemId = ItemId;
        definition.mitigationItemAmount = 3;
        definition.mitigationWork = 100f;
        definition.maximumMitigation = 0.2f;
        return definition;
    }

    private static void SetPosition(
        BuildableObject facility,
        Vector2Int position)
    {
        PropertyInfo property = typeof(BuildableObject).GetProperty(
            nameof(BuildableObject.centerPos),
            BindingFlags.Instance | BindingFlags.Public);
        MethodInfo setter = property?.GetSetMethod(nonPublic: true);
        if (setter == null)
            throw new InvalidOperationException("BuildableObject center setter missing.");
        setter.Invoke(facility, new object[] { position });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixedContentCatalog : IOffenseContentCatalog
    {
        internal FixedContentCatalog(OffenseUrgentSiteDefinitionSO definition)
        {
            UrgentSites = new[] { definition };
        }

        public IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes =>
            Array.Empty<OffenseSiteArchetypeSO>();
        public IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites { get; }
        public IReadOnlyList<OffenseDecisionCardSO> DecisionCards =>
            Array.Empty<OffenseDecisionCardSO>();
        public IReadOnlyList<OffenseEncounterSO> Encounters =>
            Array.Empty<OffenseEncounterSO>();
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly string itemId;
        private readonly long grams;

        internal FixedMassQuery(string itemId, long grams)
        {
            this.itemId = itemId;
            this.grams = grams;
        }

        public long AuthorityRevision => 17L;

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId id) =>
            id.Value == itemId
                ? new PhysicalMassGrams(grams)
                : throw new InvalidOperationException("Unknown fixture item.");

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            throw new NotSupportedException();

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId id,
            PhysicalItemMassSubject subject) => GetDefinitionUnitMass(id);

        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            throw new NotSupportedException();

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId id,
            PhysicalItemMassSubject subject,
            int quantity) => new(checked(
            GetDefinitionUnitMass(id).Value * quantity));
    }

    private sealed class AuthorityStore :
        IFacilityBufferDestinationClaimAuthorityQuery,
        IFacilityBufferMassCapacityAuthorityQuery,
        IFacilityBufferDestinationLifecycleCommand
    {
        internal IReadOnlyList<FacilityBufferDestinationClaim> Claims
            { get; private set; } = Array.Empty<FacilityBufferDestinationClaim>();
        internal IReadOnlyList<FacilityBufferCapacityProfile> Profiles
            { get; private set; } = Array.Empty<FacilityBufferCapacityProfile>();
        internal int ReplaceCalls { get; private set; }

        public IReadOnlyList<FacilityBufferDestinationClaim>
            CaptureAuthorityClaims() => Claims;

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

        public IReadOnlyList<FacilityBufferCapacityProfile>
            CaptureAuthorityProfiles() => Profiles;

        public bool TryReplaceOwnedAuthorities(
            string ownerDomain,
            IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out string failureReason)
        {
            if (ownerDomain
                != OffenseUrgentMitigationInputOwnerAuthority.OwnerDomain)
            {
                failureReason = "qa-owner-domain-invalid";
                return false;
            }
            Claims = desiredClaims.ToArray();
            Profiles = desiredProfiles.ToArray();
            ReplaceCalls++;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class RecordingRelease :
        IFacilityBufferDestinationReleaseService
    {
        internal bool Fail { get; set; }
        internal int Calls { get; private set; }
        internal string LastDestinationId { get; private set; } = string.Empty;
        internal Vector2Int LastPosition { get; private set; }

        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            Calls++;
            LastDestinationId = destinationId;
            LastPosition = ownerPosition;
            releasedQuantity = 0;
            failureReason = Fail ? "qa-carried-release-failed" : string.Empty;
            return !Fail;
        }
    }
}
