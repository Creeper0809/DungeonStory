using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WildlifeCareInputOwnerDebugScenarios
{
    private const string PassToken =
        "WILDLIFE_CARE_INPUT_OWNER_FOCUSED_PASS";

    [MenuItem(
        "DungeonStory/V27/Captivity/Run Wildlife Care Input Owner Contracts")]
    public static void RunAll()
    {
        VerifyExactProjection();
        VerifyCapacityExpansionRetainsPhysicalCustody();
        VerifyShrinkAndTerminalCloseAreReleaseFirst();
        VerifyRestoreJoinAndParticipantLifecycle();
        Debug.Log("[WildlifeCareInputOwner] " + PassToken);
    }

    private static void VerifyExactProjection()
    {
        WildlifeCareInputOwnerProjection projection =
            WildlifeCareInputOwnerAuthority.BuildProjection(new[]
            {
                Descriptor(animalCount: 2)
            });
        Require(projection.Claims.Count == 1
            && projection.Profiles.Count == 1,
            "Exact wildlife-care projection did not create one pair.");
        FacilityBufferDestinationClaim claim = projection.Claims[0];
        FacilityBufferCapacityProfile profile = projection.Profiles[0];
        Require(claim.DestinationId ==
                "facility-input:exact:captivity.wildlife-care:pen:alpha"
            && claim.DropPosition == new Vector2Int(7, 11)
            && claim.OwnerDomain ==
                WildlifeCareInputOwnerAuthority.OwnerDomain
            && claim.OwnerFacilityId == "pen:alpha"
            && claim.AnchorKind ==
                FacilityBufferDestinationAnchorKind.LiveFacility
            && claim.AdmissionPolicy ==
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
            && profile.MaxMassGrams == 4_000L
            && profile.CapacityRevision ==
                WildlifeCareInputOwnerAuthority.CapacitySchemaRevision
            && WildlifeCareInputOwnerAuthority.ProfilesMatch(
                profile,
                projection.Profiles.Single()),
            "Wildlife-care pair lost exact identity, gram capacity, or anchor policy.");
    }

    private static void VerifyCapacityExpansionRetainsPhysicalCustody()
    {
        MutableSource source = new(Descriptor(animalCount: 1));
        AuthorityStore store = new();
        WildlifeCareInputOwnerRuntime owner = new(
            source,
            store,
            store,
            store,
            store);
        Require(owner.TryReconcileLive(out string firstFailure), firstFailure);
        source.Set(Descriptor(animalCount: 3));
        Require(owner.TryReconcileLive(out string expandFailure),
            expandFailure);
        Require(store.ReleaseCalls == 0
            && store.Profiles.Single().MaxMassGrams == 6_000L,
            "Capacity expansion released valid physical custody or projected the wrong gram limit.");
    }

    private static void VerifyShrinkAndTerminalCloseAreReleaseFirst()
    {
        MutableSource source = new(Descriptor(animalCount: 3));
        AuthorityStore store = new();
        WildlifeCareInputOwnerRuntime owner = new(
            source,
            store,
            store,
            store,
            store);
        Require(owner.TryReconcileLive(out string firstFailure), firstFailure);

        source.Set(Descriptor(animalCount: 1));
        Require(owner.TryReconcileLive(out string shrinkFailure),
            shrinkFailure);
        Require(store.ReleaseCalls == 1
            && store.Profiles.Single().MaxMassGrams == 2_000L,
            "Capacity shrink did not release carried/deposited custody before replacement.");

        source.Clear();
        store.FailRelease = true;
        Require(!owner.TryReconcileLive(out string blockedFailure)
            && blockedFailure.Contains(
                "wildlife-care-input-terminal-release-failed",
                StringComparison.Ordinal)
            && store.Claims.Count == 1
            && store.Profiles.Count == 1,
            "Failed terminal release retired or tore wildlife-care authority.");

        store.FailRelease = false;
        Require(owner.TryReconcileLive(out string closeFailure), closeFailure);
        Require(store.Claims.Count == 0
            && store.Profiles.Count == 0
            && store.LastReleaseReason ==
                WildlifeCareInputOwnerAuthority.TerminalReleaseReasonCode,
            "Successful terminal release did not retire the exact pair.");
    }

    private static void VerifyRestoreJoinAndParticipantLifecycle()
    {
        MutableSource source = new(Descriptor(animalCount: 2));
        AuthorityStore store = new();
        WildlifeCareInputOwnerRuntime owner = new(
            source,
            store,
            store,
            store,
            store);
        Require(owner.TryReconcileRestore(out string restoreFailure),
            restoreFailure);
        Require(store.ReleaseCalls == 0
            && store.Claims.Single().OwnerFacilityId == "pen:alpha"
            && store.Profiles.Single().MaxMassGrams == 4_000L,
            "Current-format restore did not rebuild the exact pen/profile join.");

        WildlifeCareInputOwnerRestoreParticipant participant = new(owner);
        participant.BeginRestoreCandidate();
        participant.PublishRestoreCandidate();
        participant.CompleteRestoreCandidate();
        Require(participant.ParticipantId.CompareTo(
                "220.world.facility-buffer-destinations") < 0,
            "Wildlife-care restore join must publish before claim/profile authorities.");

        participant.BeginRestoreCandidate();
        participant.DiscardRestoreCandidate();
    }

    private static WildlifeCareInputOwnerDescriptor Descriptor(
        int animalCount) => new(
        "pen:alpha",
        new Vector2Int(7, 11),
        animalCount,
        foodUnitsPerAnimal: 2,
        waterUnitsPerAnimal: 1,
        maximumFeedUnitMassGrams: 500L,
        maximumWaterUnitMassGrams: 1_000L,
        massAuthorityRevision: 17L);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class MutableSource : IWildlifeCareInputOwnerSource
    {
        private IReadOnlyList<WildlifeCareInputOwnerDescriptor> values;

        internal MutableSource(WildlifeCareInputOwnerDescriptor value) =>
            Set(value);

        internal void Set(WildlifeCareInputOwnerDescriptor value) =>
            values = new[] { value };

        internal void Clear() =>
            values = Array.Empty<WildlifeCareInputOwnerDescriptor>();

        public IReadOnlyList<WildlifeCareInputOwnerDescriptor> Capture() =>
            values;
    }

    private sealed class AuthorityStore :
        IFacilityBufferDestinationClaimAuthorityQuery,
        IFacilityBufferMassCapacityAuthorityQuery,
        IFacilityBufferDestinationLifecycleCommand,
        IFacilityBufferDestinationReleaseService
    {
        internal List<FacilityBufferDestinationClaim> Claims { get; } = new();
        internal List<FacilityBufferCapacityProfile> Profiles { get; } = new();
        internal int ReleaseCalls { get; private set; }
        internal bool FailRelease { get; set; }
        internal string LastReleaseReason { get; private set; } = string.Empty;

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

        public IReadOnlyList<FacilityBufferDestinationClaim>
            CaptureAuthorityClaims() => Claims.ToArray();

        public IReadOnlyList<FacilityBufferCapacityProfile>
            CaptureAuthorityProfiles() => Profiles.ToArray();

        public bool TryReplaceOwnedAuthorities(
            string ownerDomain,
            IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out string failureReason)
        {
            if (desiredClaims.Count != desiredProfiles.Count
                || desiredClaims.Any(value => value.OwnerDomain != ownerDomain)
                || desiredProfiles.Any(value => value.OwnerDomain != ownerDomain))
            {
                failureReason = "fixture-pair-invalid";
                return false;
            }
            Claims.RemoveAll(value => value.OwnerDomain == ownerDomain);
            Profiles.RemoveAll(value => value.OwnerDomain == ownerDomain);
            Claims.AddRange(desiredClaims);
            Profiles.AddRange(desiredProfiles);
            failureReason = string.Empty;
            return true;
        }

        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            ReleaseCalls++;
            LastReleaseReason = reasonCode;
            releasedQuantity = 0;
            failureReason = FailRelease
                ? "fixture-carried-release-blocked"
                : string.Empty;
            return !FailRelease;
        }
    }
}
