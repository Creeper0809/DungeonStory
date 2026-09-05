#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class FluidFacilityInputOwnerDebugContract
{
    public static void Verify()
    {
        var descriptors = new[]
        {
            new FluidFacilityInputOwnerDescriptor(
                FluidFacilityInputOwnerProjectionAuthority.FluidOwnerDomain,
                "plumbing:manual-water:fixture:test",
                "manual-water-input-owner:fixture:test",
                "fixture:test",
                new Vector2Int(3, 4),
                FacilityBufferDestinationAnchorKind.LiveBuilding,
                2,
                500L,
                17L),
            new FluidFacilityInputOwnerDescriptor(
                FluidFacilityInputOwnerProjectionAuthority.ProcessFluidOwnerDomain,
                "plumbing:process-water:facility:test:work:craft",
                "process-fluid-input-owner:facility:test:work:craft",
                "facility:test",
                new Vector2Int(5, 6),
                FacilityBufferDestinationAnchorKind.LiveFacility,
                3,
                500L,
                17L)
        };
        FluidFacilityInputOwnerProjection fluid =
            FluidFacilityInputOwnerProjectionAuthority.Build(
                FluidFacilityInputOwnerProjectionAuthority.FluidOwnerDomain,
                descriptors);
        FluidFacilityInputOwnerProjection process =
            FluidFacilityInputOwnerProjectionAuthority.Build(
                FluidFacilityInputOwnerProjectionAuthority.ProcessFluidOwnerDomain,
                descriptors);
        Require(fluid.Claims.Count == 1
                && fluid.Claims[0].AnchorKind
                    == FacilityBufferDestinationAnchorKind.LiveBuilding
                && fluid.Claims[0].AdmissionPolicy
                    == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
                && fluid.Profiles[0].MaxMassGrams == 1000L
                && fluid.MassAuthorityRevision == 17L
                && fluid.Fingerprint.Length == 64,
            "fluid owner did not project exact positive-gram authority");
        Require(process.Claims.Count == 1
                && process.Claims[0].AnchorKind
                    == FacilityBufferDestinationAnchorKind.LiveFacility
                && process.Profiles[0].MaxMassGrams == 1500L
                && process.Fingerprint.Length == 64,
            "process-fluid owner did not project exact positive-gram authority");

        var events = new List<string>();
        var claimQuery = new ClaimQuery(fluid.Claims);
        var capacityQuery = new CapacityQuery(fluid.Profiles);
        var lifecycle = new Lifecycle(
            claimQuery, capacityQuery, events);
        var releases = new Releases(events);
        var owner = new FluidFacilityInputOwnerAuthority(
            new MassQuery(),
            claimQuery,
            capacityQuery,
            lifecycle,
            releases);
        Require(owner.TryReconcile(
                    new IndustrialTopologySnapshot(),
                    out string failureReason)
                && failureReason.Length == 0
                && events.SequenceEqual(new[]
                {
                    "release:plumbing:manual-water:fixture:test",
                    "replace:infrastructure.fluid",
                    "replace:infrastructure.process-fluid"
                }),
            "fluid owner did not release carried-aware custody before paired revoke");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class ClaimQuery :
        IFacilityBufferDestinationClaimAuthorityQuery
    {
        internal ClaimQuery(IEnumerable<FacilityBufferDestinationClaim> values) =>
            Values = values.ToList();
        internal List<FacilityBufferDestinationClaim> Values { get; }
        public bool TryGetAuthorityClaim(
            string destinationId,
            Vector2Int dropPosition,
            out FacilityBufferDestinationClaim claim)
        {
            claim = Values.SingleOrDefault(value =>
                value.DestinationId == destinationId
                && value.DropPosition == dropPosition);
            return claim != null;
        }
        public IReadOnlyList<FacilityBufferDestinationClaim>
            CaptureAuthorityClaims() => Values.ToArray();
    }

    private sealed class CapacityQuery :
        IFacilityBufferMassCapacityAuthorityQuery
    {
        internal CapacityQuery(IEnumerable<FacilityBufferCapacityProfile> values) =>
            Values = values.ToList();
        internal List<FacilityBufferCapacityProfile> Values { get; }
        public IReadOnlyList<FacilityBufferCapacityProfile>
            CaptureAuthorityProfiles() => Values.ToArray();
    }

    private sealed class Lifecycle :
        IFacilityBufferDestinationLifecycleCommand
    {
        private readonly ClaimQuery claims;
        private readonly CapacityQuery profiles;
        private readonly List<string> events;
        internal Lifecycle(
            ClaimQuery claims,
            CapacityQuery profiles,
            List<string> events)
        {
            this.claims = claims;
            this.profiles = profiles;
            this.events = events;
        }
        public bool TryReplaceOwnedAuthorities(
            string ownerDomain,
            IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out string failureReason)
        {
            events.Add("replace:" + ownerDomain);
            claims.Values.RemoveAll(value => value.OwnerDomain == ownerDomain);
            profiles.Values.RemoveAll(value => value.OwnerDomain == ownerDomain);
            claims.Values.AddRange(desiredClaims);
            profiles.Values.AddRange(desiredProfiles);
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class Releases : IFacilityBufferDestinationReleaseService
    {
        private readonly List<string> events;
        internal Releases(List<string> events) => this.events = events;
        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reason,
            out int releasedQuantity,
            out string failureReason)
        {
            events.Add("release:" + destinationId);
            releasedQuantity = 1;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class MassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 17L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(500L);
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => new(500L);
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => new(500L);
        public PhysicalMassGrams GetStackTotalMass(
            PhysicalItemLotSnapshot lot) => new(500L);
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new(checked(500L * quantity));
    }
}
#endif
