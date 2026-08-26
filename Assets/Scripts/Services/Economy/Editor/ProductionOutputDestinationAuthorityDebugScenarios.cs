#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionOutputDestinationAuthorityDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Economy/Run Production Output Destination Authority")]
    public static void RunAll()
    {
        VerifySharedFacilityExactAuthority();
        VerifyProjectedSetRetirementPreflight();
        VerifyPartialAuthorityFailsLoud();
        VerifyConflictingAuthorityFailsLoud();
        Debug.Log("V27_PRODUCTION_OUTPUT_DESTINATION_AUTHORITY=PASS");
    }

    private static void VerifyProjectedSetRetirementPreflight()
    {
        Fixture fixture = new();
        ProductionFacilityHandle feedbench = Facility(
            "building:qa:production-output:feedbench",
            new Vector2Int(23, 5));
        ProductionFacilityHandle other = Facility(
            "building:qa:production-output:other",
            new Vector2Int(25, 5));
        Require(fixture.Runtime.TryReplaceProjected(
                new[] { feedbench, other },
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    [feedbench.InstanceId.Value] = 4_200L,
                    [other.InstanceId.Value] = 6_000L
                },
                out string publishFailure),
            "Projected output authority set failed: " + publishFailure);
        RequireCounts(fixture, expected: 2);
        Require(fixture.Runtime.TryValidate(
                feedbench,
                out FacilityBufferCapacityProfile feedbenchProfile,
                out string validateFailure),
            "Projected feedbench authority did not validate: " + validateFailure);
        RequireProfile(feedbenchProfile, feedbench, 4_200L);

        fixture.Occupancy.SetMass(
            Destination(feedbench.InstanceId),
            1L);
        Require(!fixture.Runtime.TryReplaceProjected(
                new[] { other },
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    [other.InstanceId.Value] = 6_000L
                },
                out string blockedFailure)
            && blockedFailure.StartsWith(
                "production-output-projected-authority-publish-failed:",
                StringComparison.Ordinal),
            "Projected authority retired a destination with physical output.");
        RequireCounts(fixture, expected: 2);

        fixture.Occupancy.SetMass(
            Destination(feedbench.InstanceId),
            0L);
        Require(fixture.Runtime.TryReplaceProjected(
                new[] { other },
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    [other.InstanceId.Value] = 6_000L
                },
                out string retireFailure),
            "Drained projected authority did not retire: " + retireFailure);
        RequireCounts(fixture, expected: 1);
        Require(!fixture.Runtime.TryValidate(
                feedbench,
                out _,
                out string retiredFailure)
            && retiredFailure.StartsWith(
                "production-output-claim-invalid:",
                StringComparison.Ordinal),
            "Drained projected authority remained live after retirement.");
    }

    private static void VerifySharedFacilityExactAuthority()
    {
        Fixture fixture = new();
        ProductionFacilityHandle facilityA = Facility(
            "building:qa:production-output:a",
            new Vector2Int(4, 7));
        ProductionFacilityHandle facilityB = Facility(
            "building:qa:production-output:b",
            new Vector2Int(13, 9));

        Require(
            fixture.Runtime.TryEnsure(
                facilityA,
                8_000L,
                out FacilityBufferCapacityProfile firstBill,
                out string firstFailure),
            "Initial shared output authority failed: " + firstFailure);
        RequireProfile(firstBill, facilityA, 8_000L);
        RequireCounts(fixture, expected: 1);

        Require(
            fixture.Runtime.TryEnsure(
                facilityA,
                3_000L,
                out FacilityBufferCapacityProfile smallerBill,
                out string smallerFailure),
            "A smaller shared request could not retain the facility authority: " + smallerFailure);
        RequireProfile(smallerBill, facilityA, 8_000L);
        RequireCounts(fixture, expected: 1);

        Require(
            fixture.Runtime.TryEnsure(
                facilityA,
                14_000L,
                out FacilityBufferCapacityProfile largerBill,
                out string largerFailure),
            "A larger bill could not expand the shared authority: " + largerFailure);
        RequireProfile(largerBill, facilityA, 14_000L);
        RequireCounts(fixture, expected: 1);

        Require(
            fixture.Runtime.TryEnsure(
                facilityB,
                6_000L,
                out FacilityBufferCapacityProfile otherFacility,
                out string otherFailure),
            "Independent facility authority failed: " + otherFailure);
        RequireProfile(otherFacility, facilityB, 6_000L);
        RequireCounts(fixture, expected: 2);

        bool validA = fixture.Runtime.TryValidate(
            facilityA,
            out FacilityBufferCapacityProfile validatedA,
            out string validateAFailure);
        bool validB = fixture.Runtime.TryValidate(
            facilityB,
            out FacilityBufferCapacityProfile validatedB,
            out string validateBFailure);
        Require(
            validA && validB,
            "Exact authority validation failed: "
            + validateAFailure + " / " + validateBFailure);
        RequireProfile(validatedA, facilityA, 14_000L);
        RequireProfile(validatedB, facilityB, 6_000L);

        Require(
            fixture.Runtime.TryRevoke(
                facilityA.InstanceId,
                out string revokeFailure),
            "Facility-scoped authority revoke failed: " + revokeFailure);
        RequireCounts(fixture, expected: 1);
        Require(
            !fixture.Runtime.TryValidate(facilityA, out _, out string retiredReason)
            && retiredReason.StartsWith(
                "production-output-claim-invalid:",
                StringComparison.Ordinal),
            "Revoked facility retained output authority.");
        Require(
            fixture.Runtime.TryValidate(
                facilityB,
                out FacilityBufferCapacityProfile preserved,
                out string preservedFailure),
            "Revoking one facility damaged another: " + preservedFailure);
        RequireProfile(preserved, facilityB, 6_000L);
    }

    private static void VerifyPartialAuthorityFailsLoud()
    {
        Fixture fixture = new();
        ProductionFacilityHandle facility = Facility(
            "building:qa:production-output:partial",
            new Vector2Int(3, 11));
        FacilityBufferDestinationClaim claim = ExpectedClaim(facility);
        Require(
            fixture.Claims.TryReplaceOwnedClaims(
                ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                new[] { claim },
                out FacilityBufferDestinationClaimFailureCode claimFailure,
                out string claimReason),
            $"Partial fixture setup failed ({claimFailure}): {claimReason}");

        Require(
            !fixture.Runtime.TryEnsure(
                facility,
                5_000L,
                out _,
                out string failure)
            && failure.StartsWith(
                "production-output-authority-partial:",
                StringComparison.Ordinal)
            && fixture.Claims.CaptureClaims().Count == 1
            && fixture.Admission.CaptureProfiles().Count == 0,
            "Partial claim/profile authority was hidden or mutated: " + failure);
    }

    private static void VerifyConflictingAuthorityFailsLoud()
    {
        Fixture fixture = new();
        ProductionFacilityHandle facility = Facility(
            "building:qa:production-output:conflict",
            new Vector2Int(17, 2));
        string destination = Destination(facility.InstanceId);
        const string conflictingFacility =
            "building:qa:production-output:foreign-owner";
        FacilityBufferDestinationClaim claim = new(
            destination,
            facility.Position,
            ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
            destination,
            conflictingFacility,
            FacilityBufferDestinationAnchorKind.LiveFacility);
        FacilityBufferCapacityProfile profile = new(
            destination,
            facility.Position,
            ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
            destination,
            conflictingFacility,
            new PhysicalMassGrams(9_000L),
            ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision);
        Require(
            fixture.Lifecycle.TryReplaceOwnedAuthorities(
                ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                new[] { claim },
                new[] { profile },
                out string setupFailure),
            "Conflicting fixture setup failed: " + setupFailure);

        Require(
            !fixture.Runtime.TryEnsure(
                facility,
                12_000L,
                out _,
                out string failure)
            && failure.StartsWith(
                "production-output-authority-conflict:",
                StringComparison.Ordinal)
            && fixture.Admission.TryGetCapacity(
                destination,
                facility.Position,
                out FacilityBufferMassCapacitySnapshot unchanged)
            && unchanged.Profile.MaxMassGrams == 9_000L
            && string.Equals(
                unchanged.Profile.OwnerFacilityId,
                conflictingFacility,
                StringComparison.Ordinal),
            "Conflicting authority was hidden or overwritten: " + failure);
        Require(
            !fixture.Runtime.TryValidate(facility, out _, out string validateFailure)
            && validateFailure.StartsWith(
                "production-output-claim-invalid:",
                StringComparison.Ordinal),
            "Exact validation accepted conflicting facility ownership.");
    }

    private static ProductionFacilityHandle Facility(
        string instanceId,
        Vector2Int position) => new(
        new object(),
        new BuildingInstanceId(instanceId),
        position,
        isDestroyed: false,
        stockSensorInstallationItemId: string.Empty,
        allowsOverflowDump: false,
        overflowOffset: Vector2Int.zero,
        definitionId: "building-definition:qa-production-output",
        workstationTag: "workstation:qa-production-output",
        outputBufferCycleCapacity: 4);

    private static FacilityBufferDestinationClaim ExpectedClaim(
        ProductionFacilityHandle facility)
    {
        string destination = Destination(facility.InstanceId);
        return new FacilityBufferDestinationClaim(
            destination,
            facility.Position,
            ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
            destination,
            facility.InstanceId.Value,
            FacilityBufferDestinationAnchorKind.LiveFacility);
    }

    private static string Destination(BuildingInstanceId facilityId) =>
        ProductionBillRuntime.OutputDestinationPrefix + facilityId.Value;

    private static void RequireProfile(
        FacilityBufferCapacityProfile profile,
        ProductionFacilityHandle facility,
        long expectedMassGrams)
    {
        string destination = Destination(facility.InstanceId);
        Require(
            profile != null
            && profile.MaxMassGrams == expectedMassGrams
            && profile.CapacityRevision ==
                ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision
            && profile.DropPosition == facility.Position
            && string.Equals(profile.DestinationId, destination, StringComparison.Ordinal)
            && string.Equals(
                profile.OwnerDomain,
                ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                profile.OwnerOperationId,
                destination,
                StringComparison.Ordinal)
            && string.Equals(
                profile.OwnerFacilityId,
                facility.InstanceId.Value,
                StringComparison.Ordinal),
            $"Facility '{facility.InstanceId}' has a non-exact output profile.");
    }

    private static void RequireCounts(Fixture fixture, int expected)
    {
        FacilityBufferDestinationClaim[] claims = fixture.Claims.CaptureClaims()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] profiles = fixture.Admission.CaptureProfiles()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                StringComparison.Ordinal))
            .ToArray();
        Require(
            claims.Length == expected
            && profiles.Length == expected
            && claims.Select(value => value.DestinationId).SequenceEqual(
                profiles.Select(value => value.DestinationId),
                StringComparer.Ordinal),
            $"Expected {expected} exact output authority pairs, got "
            + $"{claims.Length}/{profiles.Length}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Fixture
    {
        internal Fixture()
        {
            Admission = new FacilityBufferMassAdmissionService(
                Claims,
                Occupancy);
            Lifecycle = new FacilityBufferDestinationLifecycleService(
                Claims,
                Claims,
                Admission,
                Admission);
            Runtime = new ProductionOutputDestinationAuthorityRuntime(
                Claims,
                Admission,
                Claims,
                Admission,
                Lifecycle);
        }

        internal FacilityBufferDestinationClaimRegistry Claims { get; } = new();
        internal MutableOccupancy Occupancy { get; } = new();
        internal FacilityBufferMassAdmissionService Admission { get; }
        internal FacilityBufferDestinationLifecycleService Lifecycle { get; }
        internal ProductionOutputDestinationAuthorityRuntime Runtime { get; }
    }

    private sealed class MutableOccupancy : IFacilityBufferPhysicalOccupancyQuery
    {
        private readonly Dictionary<string, long> massByDestination =
            new(StringComparer.Ordinal);

        internal void SetMass(string destinationId, long massGrams) =>
            massByDestination[destinationId] = massGrams;

        public FacilityBufferPhysicalOccupancySnapshot Capture(string destinationId) =>
            new(massByDestination.GetValueOrDefault(destinationId, 0L), 0L);

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "fixture-has-no-physical-lots";
            return false;
        }
    }
}
#endif
