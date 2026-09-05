using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CertifiedSeedInputOwnerDebugScenarios
{
    private const string OrderId = "certified-seed-order:00000003";
    private const string FacilityId = "building:greenhouse:qa";
    private const string CropId = "crop:twilight-grain";
    private const string SeedItemId = "seed:twilight-grain";

    [MenuItem("Tools/Dungeon Story/QA/Certified Seed Input Owner")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("CERTIFIED_SEED_INPUT_OWNER_PASS");
    }

    public static void RunAll()
    {
        VerifyExactProjection();
        VerifyFailedCarriedReleaseRetainsAuthority();
        VerifySuccessfulRetirementReleasesBeforeRevoke();
        VerifyRestoreReplacementIsDeterministic();
    }

    private static void VerifyExactProjection()
    {
        CertifiedSeedInputOwnerProjection projection =
            CertifiedSeedInputOwnerAuthority.BuildProjection(
                new[] { Descriptor(OrderId, FacilityId, CropId, 3) },
                MassQuery());
        FacilityBufferDestinationClaim claim = projection.Claims.Single();
        FacilityBufferCapacityProfile profile = projection.Profiles.Single();
        Require(claim.AdmissionPolicy ==
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
            && claim.AnchorKind == FacilityBufferDestinationAnchorKind.LiveFacility
            && claim.OwnerDomain == CertifiedSeedInputOwnerAuthority.OwnerDomain
            && claim.OwnerFacilityId == FacilityId
            && profile.MaxMassGrams == 650L
            && profile.DestinationId == claim.DestinationId
            && profile.OwnerOperationId == claim.OwnerOperationId,
            "Certified-seed exact seed+kit gram projection drifted.");
    }

    private static void VerifyFailedCarriedReleaseRetainsAuthority()
    {
        AuthorityStore store = new();
        RecordingRelease release = new();
        CertifiedSeedInputOwnerRuntime runtime = Runtime(store, release);
        CertifiedSeedInputOwnerDescriptor descriptor = Descriptor(
            OrderId,
            FacilityId,
            CropId,
            3);
        Require(runtime.TryEnsure(descriptor, out string ensureFailure),
            ensureFailure);
        int replaceCount = store.ReplaceCalls;
        release.Fail = true;
        Require(!runtime.TryRetire(
                descriptor,
                CertifiedSeedInputOwnerAuthority.FacilityLostReleaseReasonCode,
                out string failureReason)
            && failureReason.StartsWith(
                "certified-seed-input-owner-terminal-release-failed:",
                StringComparison.Ordinal)
            && store.ReplaceCalls == replaceCount
            && store.Claims.Count == 1
            && store.Profiles.Count == 1,
            "Failed carried release retired certified-seed authority.");
    }

    private static void VerifySuccessfulRetirementReleasesBeforeRevoke()
    {
        AuthorityStore store = new();
        RecordingRelease release = new();
        CertifiedSeedInputOwnerRuntime runtime = Runtime(store, release);
        CertifiedSeedInputOwnerDescriptor descriptor = Descriptor(
            OrderId,
            FacilityId,
            CropId,
            3);
        Require(runtime.TryEnsure(descriptor, out string ensureFailure),
            ensureFailure);
        int replaceCount = store.ReplaceCalls;
        Require(runtime.TryRetire(
                descriptor,
                CertifiedSeedInputOwnerAuthority.CompletionReleaseReasonCode,
                out string retireFailure),
            retireFailure);
        Require(release.Calls.SequenceEqual(
                new[] { descriptor.DestinationId },
                StringComparer.Ordinal)
            && store.ReplaceCalls == replaceCount + 1
            && store.Claims.Count == 0
            && store.Profiles.Count == 0,
            "Certified-seed retirement did not release before paired revoke.");
    }

    private static void VerifyRestoreReplacementIsDeterministic()
    {
        AuthorityStore store = new();
        CertifiedSeedInputOwnerRuntime runtime = Runtime(
            store,
            new RecordingRelease());
        CertifiedSeedInputOwnerDescriptor second = Descriptor(
            "certified-seed-order:00000004",
            "building:greenhouse:qb",
            "crop:sun-grain",
            4,
            "seed:sun-grain");
        CertifiedSeedInputOwnerDescriptor first = Descriptor(
            OrderId,
            FacilityId,
            CropId,
            3);
        Require(runtime.TryReplaceForRestore(
                new[] { second, first },
                out string firstFailure),
            firstFailure);
        string[] ordered = store.Claims.Select(value => value.DestinationId)
            .ToArray();
        int replaceCount = store.ReplaceCalls;
        Require(runtime.TryReplaceForRestore(
                new[] { first, second },
                out string secondFailure),
            secondFailure);
        Require(ordered.SequenceEqual(
                store.Claims.Select(value => value.DestinationId),
                StringComparer.Ordinal)
            && store.ReplaceCalls == replaceCount + 1
            && store.Claims.Count == 2
            && store.Profiles.Count == 2,
            "Certified-seed restore replacement depended on input order.");
    }

    private static CertifiedSeedInputOwnerRuntime Runtime(
        AuthorityStore store,
        RecordingRelease release) => new(
        MassQuery(),
        store,
        store,
        store,
        release);

    private static FixedMassQuery MassQuery() => new(new Dictionary<string, long>
    {
        [SeedItemId] = 50L,
        ["seed:sun-grain"] = 40L,
        [CertifiedSeedPhysicalTransformAuthority.CertificationKitItemId] = 600L
    });

    private static CertifiedSeedInputOwnerDescriptor Descriptor(
        string orderId,
        string facilityId,
        string cropId,
        int sequence,
        string seedItemId = SeedItemId) => new(
        orderId,
        facilityId,
        new Vector2Int(sequence, 4),
        CertifiedSeedInputOwnerAuthority.BuildDestinationId(
            facilityId,
            cropId,
            sequence),
        seedItemId);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class AuthorityStore :
        IFacilityBufferDestinationClaimAuthorityQuery,
        IFacilityBufferMassCapacityAuthorityQuery,
        IFacilityBufferDestinationLifecycleCommand
    {
        internal IReadOnlyList<FacilityBufferDestinationClaim> Claims {
            get;
            private set;
        } = Array.Empty<FacilityBufferDestinationClaim>();
        internal IReadOnlyList<FacilityBufferCapacityProfile> Profiles {
            get;
            private set;
        } = Array.Empty<FacilityBufferCapacityProfile>();
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
            Require(ownerDomain == CertifiedSeedInputOwnerAuthority.OwnerDomain,
                "Fixture received another owner domain.");
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
            failureReason = Fail ? "qa-carried-release-rejected" : string.Empty;
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

        public PhysicalMassGrams GetStackTotalMass(
            PhysicalItemLotSnapshot lot) =>
            throw new NotSupportedException();

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) =>
            new(checked(GetDefinitionUnitMass(itemId).Value * quantity));
    }
}
