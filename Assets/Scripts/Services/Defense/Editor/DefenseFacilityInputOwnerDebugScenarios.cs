using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class DefenseFacilityInputOwnerDebugScenarios
{
    private const string FacilityId = "building:qa-defense-facility";
    private const string SupplyItemId = "ammo:bolt-iron";

    [MenuItem("Tools/Dungeon Story/QA/Defense Facility Input Owner")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("DEFENSE_FACILITY_INPUT_OWNER_PASS");
    }

    public static void RunAll()
    {
        VerifyExactProjectionAndStableIdentity();
        VerifyCapacityExpansionRetainsPhysicalCustody();
        VerifyRetirementReleasesBeforeAuthorityReplacement();
        VerifyFailedReleaseRetainsExistingAuthority();
        VerifyRestoreStateJoinRejectsOrphanPendingCommit();
    }

    private static void VerifyExactProjectionAndStableIdentity()
    {
        FixedMassQuery mass = CreateMassQuery();
        DefenseFacilityInputOwnerProjection projection =
            DefenseFacilityInputOwnerAuthority.BuildProjection(
                new[] { Descriptor(capacity: 8) },
                mass);

        Require(projection.Claims.Count == 2
            && projection.Profiles.Count == 2,
            "Defense owner did not project the supply/maintenance pair.");
        Require(projection.Claims.All(value =>
                value.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
                && value.AdmissionPolicy ==
                    FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
                && value.OwnerDomain ==
                    DefenseFacilityInputOwnerAuthority.OwnerDomain
                && value.OwnerFacilityId == FacilityId),
            "Defense owner claim lost exact live-facility identity.");

        FacilityBufferCapacityProfile supply = projection.Profiles.Single(
            value => value.DestinationId ==
                DefenseFacilityInputOwnerAuthority.BuildSupplyDestinationId(
                    FacilityId));
        FacilityBufferCapacityProfile maintenance = projection.Profiles.Single(
            value => value.DestinationId ==
                DefenseFacilityInputOwnerAuthority
                    .BuildMaintenanceDestinationId(FacilityId));
        Require(supply.MaxMassGrams == 4_800L
            && maintenance.MaxMassGrams == 700L,
            "Defense exact gram capacities drifted from current item masses.");
    }

    private static void VerifyCapacityExpansionRetainsPhysicalCustody()
    {
        MutableSource source = new(Descriptor(capacity: 8));
        AuthorityStore authorities = new();
        RecordingRelease releases = new();
        DefenseFacilityInputOwnerRuntime runtime = CreateRuntime(
            source,
            authorities,
            releases);
        Require(runtime.TryReconcileLive(out string firstFailure), firstFailure);

        source.Set(Descriptor(capacity: 9));
        Require(runtime.TryReconcileLive(out string secondFailure), secondFailure);
        Require(releases.Calls.Count == 0,
            "A monotonic defense capacity expansion released valid custody.");
        FacilityBufferCapacityProfile supply = authorities.Profiles.Single(
            value => value.DestinationId ==
                DefenseFacilityInputOwnerAuthority.BuildSupplyDestinationId(
                    FacilityId));
        Require(supply.MaxMassGrams == 5_400L,
            "Expanded defense capacity was not republished exactly.");
    }

    private static void VerifyRetirementReleasesBeforeAuthorityReplacement()
    {
        MutableSource source = new(Descriptor(capacity: 8));
        AuthorityStore authorities = new();
        RecordingRelease releases = new();
        DefenseFacilityInputOwnerRuntime runtime = CreateRuntime(
            source,
            authorities,
            releases);
        Require(runtime.TryReconcileLive(out string firstFailure), firstFailure);
        int replaceCount = authorities.ReplaceCalls;

        source.Set();
        Require(runtime.TryReconcileLive(out string terminalFailure),
            terminalFailure);
        Require(releases.Calls.SequenceEqual(new[]
            {
                DefenseFacilityInputOwnerAuthority
                    .BuildMaintenanceDestinationId(FacilityId),
                DefenseFacilityInputOwnerAuthority
                    .BuildSupplyDestinationId(FacilityId)
            }, StringComparer.Ordinal)
            && authorities.ReplaceCalls == replaceCount + 1
            && authorities.Claims.Count == 0
            && authorities.Profiles.Count == 0,
            "Defense terminal retirement did not release both destinations before revoke.");
    }

    private static void VerifyFailedReleaseRetainsExistingAuthority()
    {
        MutableSource source = new(Descriptor(capacity: 8));
        AuthorityStore authorities = new();
        RecordingRelease releases = new();
        DefenseFacilityInputOwnerRuntime runtime = CreateRuntime(
            source,
            authorities,
            releases);
        Require(runtime.TryReconcileLive(out string firstFailure), firstFailure);
        int replaceCount = authorities.ReplaceCalls;
        releases.Fail = true;
        source.Set();

        Require(!runtime.TryReconcileLive(out string failureReason)
            && failureReason.StartsWith(
                "defense-input-owner-terminal-release-failed:",
                StringComparison.Ordinal)
            && authorities.ReplaceCalls == replaceCount
            && authorities.Claims.Count == 2
            && authorities.Profiles.Count == 2,
            "Failed defense release retired or mutated paired authority.");
    }

    private static void VerifyRestoreStateJoinRejectsOrphanPendingCommit()
    {
        MutableSource source = new();
        AuthorityStore authorities = new();
        DefenseFacilityInputOwnerRuntime runtime = CreateRuntime(
            source,
            authorities,
            new RecordingRelease());
        DefenseFacilityState orphan = new()
        {
            facilityPersistentId = FacilityId,
            buildingId = 1701,
            gridX = 6,
            gridY = 4,
            pendingSupply = new DefenseFacilityPhysicalCommitSaveData
            {
                phase = DefenseFacilityPhysicalCommitPhase.IntentRecorded
            }
        };
        Require(!runtime.TryReconcileRestore(
                new[] { orphan },
                out string failureReason)
            && failureReason ==
                "defense-input-owner-pending-state-facility-missing:"
                + FacilityId
            && authorities.ReplaceCalls == 0,
            "Restore accepted a pending defense receipt without a live owner.");
    }

    private static DefenseFacilityInputOwnerRuntime CreateRuntime(
        MutableSource source,
        AuthorityStore authorities,
        RecordingRelease releases) =>
        new(
            source,
            CreateMassQuery(),
            authorities,
            authorities,
            authorities,
            releases);

    private static DefenseFacilityInputOwnerDescriptor Descriptor(int capacity) =>
        new(
            FacilityId,
            buildingId: 1701,
            new Vector2Int(6, 4),
            usesPhysicalSupply: true,
            SupplyItemId,
            StockCategory.Ammunition,
            capacity,
            capacityLevel: 0);

    private static FixedMassQuery CreateMassQuery() => new(new Dictionary<string, long>
    {
        [SupplyItemId] = 600L,
        [DefenseFacilityInputOwnerAuthority.MixedDefenseAmmunitionBoxItemId] =
            4_800L,
        [DefenseFacilityPhysicalTransactionOutbox.MaintenanceItemId] = 700L
    });

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class MutableSource : IDefenseFacilityInputOwnerSource
    {
        private DefenseFacilityInputOwnerDescriptor[] values;

        internal MutableSource(params DefenseFacilityInputOwnerDescriptor[] values)
        {
            this.values = values ?? Array.Empty<DefenseFacilityInputOwnerDescriptor>();
        }

        public long Revision { get; private set; } = 1L;
        public IReadOnlyList<DefenseFacilityInputOwnerDescriptor> Capture() =>
            values;

        internal void Set(params DefenseFacilityInputOwnerDescriptor[] next)
        {
            values = next ?? Array.Empty<DefenseFacilityInputOwnerDescriptor>();
            Revision++;
        }
    }

    private sealed class AuthorityStore :
        IFacilityBufferDestinationClaimAuthorityQuery,
        IFacilityBufferMassCapacityAuthorityQuery,
        IFacilityBufferDestinationLifecycleCommand
    {
        internal IReadOnlyList<FacilityBufferDestinationClaim> Claims { get; private set; }
            = Array.Empty<FacilityBufferDestinationClaim>();
        internal IReadOnlyList<FacilityBufferCapacityProfile> Profiles { get; private set; }
            = Array.Empty<FacilityBufferCapacityProfile>();
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
            Require(ownerDomain == DefenseFacilityInputOwnerAuthority.OwnerDomain,
                "Fixture received another owner domain.");
            Claims = desiredClaims.ToArray();
            Profiles = desiredProfiles.ToArray();
            ReplaceCalls++;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class RecordingRelease : IFacilityBufferDestinationReleaseService
    {
        internal List<string> Calls { get; } = new();
        internal bool Fail { get; set; }

        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            Calls.Add(destinationId);
            releasedQuantity = 0;
            failureReason = Fail ? "qa-release-rejected" : string.Empty;
            return !Fail;
        }
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly IReadOnlyDictionary<string, long> masses;

        internal FixedMassQuery(IReadOnlyDictionary<string, long> masses)
        {
            this.masses = masses;
        }

        public long AuthorityRevision => 1L;

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(masses[itemId.Value]);

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            throw new NotSupportedException();

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(itemId);

        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            throw new NotSupportedException();

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) =>
            new(checked(GetDefinitionUnitMass(itemId).Value * quantity));
    }
}
