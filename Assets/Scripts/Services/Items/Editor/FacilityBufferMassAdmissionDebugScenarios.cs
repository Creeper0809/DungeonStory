#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class FacilityBufferMassAdmissionDebugScenarios
{
    private const string DefaultProductionCapacitySourceDigest =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    public static void RunAll()
    {
        const string DestinationId = "power:building:qa:fuel-buffer";
        const string OwnerDomain = "infrastructure.electrical";
        Vector2Int dropPosition = new(7, 11);
        FacilityBufferDestinationClaimRegistry claims = new();
        FakeOccupancyQuery occupancy = new();
        FacilityBufferMassAdmissionService admission = new(claims, occupancy);
        FacilityBufferCapacityProfile profile = new(
            DestinationId,
            dropPosition,
            OwnerDomain,
            DestinationId,
            "building:qa:fuel-buffer",
            maxMass: new PhysicalMassGrams(8_000L),
            capacityRevision: 3L);

        Require(
            !admission.TryReplaceOwnedProfiles(
                OwnerDomain,
                new[] { profile },
                out FacilityBufferMassAdmissionFailureCode missingClaim,
                out _)
            && missingClaim
                == FacilityBufferMassAdmissionFailureCode.ClaimMissingOrMismatched,
            "Facility-buffer capacity published without exact owner claim.");
        Require(
            claims.TryClaim(
                new FacilityBufferDestinationClaim(
                    DestinationId,
                    dropPosition,
                    OwnerDomain,
                    DestinationId,
                    "building:qa:fuel-buffer",
                    FacilityBufferDestinationAnchorKind.LiveBuilding),
                out _,
                out _)
            && admission.TryReplaceOwnedProfiles(
                OwnerDomain,
                new[] { profile },
                out _,
                out _),
            "Exact facility-buffer capacity profile did not publish.");

        occupancy.NonCarriedMassGrams = 2_000L;
        FacilityBufferMassAdmissionRequest first = CreateRequest(
            "qa:facility-buffer-transfer:0001",
            profile,
            "stack:a",
            quantity: 2);
        Require(
            admission.TryReserveExactLot(first, out FacilityBufferMassAdmissionToken token, out _, out _)
            && admission.TryGetCapacity(
                DestinationId,
                dropPosition,
                out FacilityBufferMassCapacitySnapshot reserved)
            && reserved.ReservedMassGrams == 4_000L,
            "Facility-buffer exact lot did not reserve grams.");

        FacilityBufferMassAdmissionRequest overfill = CreateRequest(
            "qa:facility-buffer-transfer:0002",
            profile,
            "stack:b",
            quantity: 2);
        Require(
            !admission.TryReserveExactLot(
                overfill,
                out _,
                out FacilityBufferMassAdmissionFailureCode overfillFailure,
                out _)
            && overfillFailure
                == FacilityBufferMassAdmissionFailureCode.CapacityUnavailable,
            "Facility-buffer concurrent reserved grams did not reject overfill.");

        Require(
            admission.TryCommitRouted(
                token,
                token.ExactLot.Fingerprint,
                token.ReservedMassGrams,
                out FacilityBufferMassAdmissionReceipt receipt,
                out _,
                out _)
            && receipt.CommittedMassGrams == 4_000L
            && admission.TryCommitRouted(
                token,
                token.ExactLot.Fingerprint,
                token.ReservedMassGrams,
                out FacilityBufferMassAdmissionReceipt replay,
                out _,
                out _)
            && replay.TokenId == receipt.TokenId
            && admission.TryGetCapacity(
                DestinationId,
                dropPosition,
                out FacilityBufferMassCapacitySnapshot routed)
            && routed.ReservedMassGrams == 0L,
            "Facility-buffer routed receipt was not idempotent or retained double occupancy.");

        FacilityBufferMassAdmissionRequest releaseRequest = CreateRequest(
            "qa:facility-buffer-transfer:0003",
            profile,
            "stack:c",
            quantity: 1);
        occupancy.NonCarriedMassGrams = 6_000L;
        Require(
            admission.TryReserveExactLot(
                releaseRequest,
                out FacilityBufferMassAdmissionToken releaseToken,
                out _,
                out _)
            && admission.TryRelease(
                releaseToken,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _)
            && admission.TryGetCapacity(
                DestinationId,
                dropPosition,
                out FacilityBufferMassCapacitySnapshot released)
            && released.ReservedMassGrams == 0L,
            "Facility-buffer rollback did not release transient reserved grams.");

        VerifyLifecycleExceptionRollback();
        VerifyRestoreCandidateAtomicity();
        VerifyClaimRestorePublishRevisionPreflight();
        VerifyRestorePublishRevisionPreflight();
        VerifySemanticNoOpReplacementPreservesLiveRevisions();
        VerifyReserveRevalidatesClaim();
        VerifyPlannedOutputAdmission();
        VerifyProductionCapacitySourceBinding();
        VerifyPreparedOutputMaximumBranchOneGramBoundary();
        VerifyCustodyOwnedExactAdmission();
        VerifyPreparedOutputDestinationAdmissionArchitecture();
    }

    private static void VerifySemanticNoOpReplacementPreservesLiveRevisions()
    {
        const string destination = "production-output:building:qa:no-op";
        const string owner = ProductionOutputDestinationAuthorityRuntime.OwnerDomain;
        Vector2Int position = new(43, 19);
        FacilityBufferDestinationClaimRegistry claims = new();
        FakeOccupancyQuery occupancy = new();
        FacilityBufferMassAdmissionService admission = new(claims, occupancy);
        FacilityBufferDestinationClaim claim = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:no-op",
            FacilityBufferDestinationAnchorKind.LiveFacility);
        FacilityBufferCapacityProfile profile = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:no-op",
            new PhysicalMassGrams(8_000L),
            ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision);

        Require(claims.TryReplaceOwnedClaims(owner, new[] { claim }, out _, out _)
            && admission.TryReplaceOwnedProfiles(owner, new[] { profile }, out _, out _),
            "Semantic no-op fixture could not publish initial authority.");
        long claimRevision = claims.Revision;
        long profileRevision = admission.Revision;

        FacilityBufferDestinationClaim equivalentClaim = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:no-op",
            FacilityBufferDestinationAnchorKind.LiveFacility);
        FacilityBufferCapacityProfile equivalentProfile = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:no-op",
            new PhysicalMassGrams(8_000L),
            ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision);
        Require(claims.TryReplaceOwnedClaims(
                owner,
                new[] { equivalentClaim },
                out _,
                out _)
            && admission.TryReplaceOwnedProfiles(
                owner,
                new[] { equivalentProfile },
                out _,
                out _)
            && claims.Revision == claimRevision
            && admission.Revision == profileRevision,
            "Exact live authority replacement advanced a semantic revision.");

        claims.BeginRestoreCandidate();
        admission.BeginRestoreCandidate();
        Require(claims.TryReplaceOwnedClaims(owner, new[] { claim }, out _, out _)
            && admission.TryReplaceOwnedProfiles(owner, new[] { profile }, out _, out _),
            "Restore candidate did not accept an exact authority set.");
        claims.PublishRestoreCandidate();
        admission.PublishRestoreCandidate();
        claims.CompleteRestoreCandidate();
        admission.CompleteRestoreCandidate();
        Require(claims.TryGetClaim(destination, position, out _)
            && admission.TryGetCapacity(destination, position, out _),
            "Restore candidate no-op optimization skipped candidate publication.");
    }

    private static void VerifyProductionCapacitySourceBinding()
    {
        const string destination = "production-output:building:qa:source-binding";
        const string owner = ProductionOutputDestinationAuthorityRuntime.OwnerDomain;
        const string operation = "qa:production-output:capacity-source-binding";
        const string batch = "production-output-batch:qa:capacity-source-binding";
        const string outcome = "outcome:qa:capacity-source-binding";
        const string digestA = DefaultProductionCapacitySourceDigest;
        const string digestB =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        Vector2Int position = new(41, 17);
        FacilityBufferDestinationClaimRegistry claims = new();
        FakeOccupancyQuery occupancy = new();
        FakeMassQuery mass = new();
        FacilityBufferMassAdmissionService admission = new(
            claims,
            occupancy,
            mass);
        FacilityBufferDestinationClaim claim = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:source-binding",
            FacilityBufferDestinationAnchorKind.LiveFacility);
        FacilityBufferCapacityProfile profile = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:source-binding",
            new PhysicalMassGrams(10_000L),
            ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision);
        Require(claims.TryClaim(claim, out _, out _)
            && admission.TryReplaceOwnedProfiles(owner, new[] { profile }, out _, out _),
            "Production capacity-source fixture could not publish its profile.");

        FacilityBufferPlannedOutputRequest missingDigest = CreatePlannedRequest(
            profile,
            operation + ":missing",
            batch,
            outcome,
            "output:main",
            "qa:item:planned",
            quantity: 1,
            capacitySourceDigest: string.Empty,
            expectedMinimumCapacityGrams: 8_000L);
        Require(!admission.TryReservePlannedOutput(
                missingDigest,
                out _,
                out FacilityBufferMassAdmissionFailureCode missingFailure,
                out _)
            && missingFailure == FacilityBufferMassAdmissionFailureCode.InvalidRequest,
            "Production planned output admitted an empty capacity source digest.");

        FacilityBufferPlannedOutputRequest nonCanonicalDigest = CreatePlannedRequest(
            profile,
            operation + ":uppercase",
            batch,
            outcome,
            "output:main",
            "qa:item:planned",
            quantity: 1,
            capacitySourceDigest: digestA.ToUpperInvariant(),
            expectedMinimumCapacityGrams: 8_000L);
        Require(!admission.TryReservePlannedOutput(
                nonCanonicalDigest,
                out _,
                out FacilityBufferMassAdmissionFailureCode digestFailure,
                out _)
            && digestFailure == FacilityBufferMassAdmissionFailureCode.InvalidRequest,
            "Production planned output admitted a noncanonical capacity source digest.");

        FacilityBufferPlannedOutputRequest oversizedMinimum = CreatePlannedRequest(
            profile,
            operation + ":oversized",
            batch,
            outcome,
            "output:main",
            "qa:item:planned",
            quantity: 1,
            capacitySourceDigest: digestA,
            expectedMinimumCapacityGrams: 10_001L);
        Require(!admission.TryReservePlannedOutput(
                oversizedMinimum,
                out _,
                out FacilityBufferMassAdmissionFailureCode oversizedFailure,
                out _)
            && oversizedFailure == FacilityBufferMassAdmissionFailureCode.ProfileConflict,
            "Production planned output admitted a profile below its source-bound minimum.");

        FacilityBufferPlannedOutputRequest requestA = CreatePlannedRequest(
            profile,
            operation,
            batch,
            outcome,
            "output:main",
            "qa:item:planned",
            quantity: 1,
            capacitySourceDigest: digestA,
            expectedMinimumCapacityGrams: 8_000L);
        Require(admission.TryReservePlannedOutput(
                requestA,
                out FacilityBufferPlannedOutputToken tokenA,
                out _,
                out _)
            && admission.TryReleasePlannedOutput(
                tokenA,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _),
            "Production capacity source A did not reserve and release.");

        FacilityBufferPlannedOutputRequest requestB = CreatePlannedRequest(
            profile,
            operation,
            batch,
            outcome,
            "output:main",
            "qa:item:planned",
            quantity: 1,
            capacitySourceDigest: digestB,
            expectedMinimumCapacityGrams: 8_000L);
        FacilityBufferMassAdmissionService admissionB = new(
            claims,
            occupancy,
            mass);
        FacilityBufferPlannedOutputToken tokenB = default;
        Require(admissionB.TryReplaceOwnedProfiles(
                owner,
                new[] { profile },
                out _,
                out _)
            && admissionB.TryReservePlannedOutput(
                requestB,
                out tokenB,
                out _,
                out _),
            "Production capacity source B did not reserve.");
        Require(!string.Equals(
                    tokenA.PlannedOutput.Fingerprint,
                    tokenB.PlannedOutput.Fingerprint,
                    StringComparison.Ordinal),
            "Capacity source digest drift did not change the planned-output fingerprint: "
            + $"A={tokenA.PlannedOutput.Fingerprint}, "
            + $"B={tokenB.PlannedOutput.Fingerprint}, "
            + $"requestA={tokenA.Request.CapacitySourceDigest}, "
            + $"requestB={tokenB.Request.CapacitySourceDigest}.");
        Require(admissionB.TryReleasePlannedOutput(
                tokenB,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _),
            "Production capacity source B did not release.");

        FacilityBufferPlannedOutputRequest requestMinimum = CreatePlannedRequest(
            profile,
            operation,
            batch,
            outcome,
            "output:main",
            "qa:item:planned",
            quantity: 1,
            capacitySourceDigest: digestB,
            expectedMinimumCapacityGrams: 9_000L);
        FacilityBufferMassAdmissionService admissionMinimum = new(
            claims,
            occupancy,
            mass);
        FacilityBufferPlannedOutputToken minimumToken = default;
        Require(admissionMinimum.TryReplaceOwnedProfiles(
                owner,
                new[] { profile },
                out _,
                out _)
            && admissionMinimum.TryReservePlannedOutput(
                requestMinimum,
                out minimumToken,
                out _,
                out _),
            "Expected minimum capacity fixture did not reserve.");
        Require(!string.Equals(
                    tokenB.PlannedOutput.Fingerprint,
                    minimumToken.PlannedOutput.Fingerprint,
                    StringComparison.Ordinal)
            && admissionMinimum.TryReleasePlannedOutput(
                minimumToken,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _),
            "Expected minimum capacity drift did not bind the planned-output token.");
    }

    private static void VerifyPreparedOutputMaximumBranchOneGramBoundary()
    {
        const string destination = "production-output:building:qa:feedbench";
        const string owner = ProductionOutputDestinationAuthorityRuntime.OwnerDomain;
        Vector2Int position = new(19, 23);
        FacilityBufferDestinationClaimRegistry claims = new();
        FakeOccupancyQuery occupancy = new();
        FakeMassQuery mass = new();
        FacilityBufferMassAdmissionService admission = new(claims, occupancy, mass);
        FacilityBufferDestinationClaim claim = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:feedbench",
            FacilityBufferDestinationAnchorKind.LiveFacility);
        FacilityBufferCapacityProfile profile = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:feedbench",
            new PhysicalMassGrams(4_200L),
            ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision);
        Require(claims.TryClaim(claim, out _, out _)
            && admission.TryReplaceOwnedProfiles(owner, new[] { profile }, out _, out _),
            "Feedbench maximum-branch fixture could not publish 4,200g authority.");

        FacilityBufferPlannedOutputRequest dogFood = CreatePlannedRequest(
            profile,
            "qa:feedbench:max-branch",
            "production-output-batch:qa:feedbench:0001",
            "outcome:qa:feedbench:0001",
            "output:main",
            "feed:dog-food",
            quantity: 2);
        occupancy.NonCarriedMassGrams = 3_151L;
        Require(!admission.TryReservePlannedOutput(
                dogFood,
                out _,
                out FacilityBufferMassAdmissionFailureCode blocked,
                out _)
            && blocked == FacilityBufferMassAdmissionFailureCode.CapacityUnavailable,
            "A 1,050g maximum branch was admitted with only 1,049g free.");

        occupancy.NonCarriedMassGrams = 3_150L;
        Require(admission.TryReservePlannedOutput(
                dogFood,
                out FacilityBufferPlannedOutputToken exact,
                out _,
                out _)
            && exact.ReservedMassGrams == 1_050L,
            "A 1,050g maximum branch did not admit after exact 1g clearance.");
        Require(admission.TryReleasePlannedOutput(
                exact,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _),
            "The live-branch boundary fixture could not release its reservation.");

        FacilityBufferPlannedOutputRequest twoGrams = CreatePlannedRequest(
            profile,
            "qa:feedbench:raw-one-gram-boundary",
            "production-output-batch:qa:feedbench:0002",
            "outcome:qa:feedbench:0002",
            "output:boundary",
            "qa:item:two-grams",
            quantity: 1);
        occupancy.NonCarriedMassGrams = 4_199L;
        Require(!admission.TryReservePlannedOutput(
                twoGrams,
                out _,
                out FacilityBufferMassAdmissionFailureCode rawBlocked,
                out _)
            && rawBlocked == FacilityBufferMassAdmissionFailureCode.CapacityUnavailable,
            "A 2g pending output was admitted into only 1g free capacity.");
        occupancy.NonCarriedMassGrams = 4_198L;
        Require(admission.TryReservePlannedOutput(
                twoGrams,
                out FacilityBufferPlannedOutputToken rawExact,
                out _,
                out _)
            && rawExact.ReservedMassGrams == 2L,
            "The raw 1g clearance boundary did not admit an exact 2g output.");
    }

    private static void VerifyPreparedOutputDestinationAdmissionArchitecture()
    {
        string services = Path.Combine(
            Application.dataPath,
            "Scripts/Services/Items");
        string warehouse = File.ReadAllText(Path.Combine(
            services,
            "WorldItemWarehouseService.cs"));
        int prepareStart = warehouse.IndexOf(
            "internal bool TryPreparePreparedOutputAdmission(",
            StringComparison.Ordinal);
        int warehouseStart = prepareStart < 0
            ? -1
            : warehouse.IndexOf(
                "if (current.Kind == PreparedOutputExactDestinationTargetKind.Warehouse)",
                prepareStart,
                StringComparison.Ordinal);
        int facilityStart = warehouseStart < 0
            ? -1
            : warehouse.IndexOf(
                "FacilityBufferCapacityProfile profile =",
                warehouseStart,
                StringComparison.Ordinal);
        Require(prepareStart >= 0 && warehouseStart > prepareStart
            && facilityStart > warehouseStart,
            "Prepared-output destination admission source shape changed.");
        string warehousePreflight = warehouse.Substring(
            warehouseStart,
            facilityStart - warehouseStart);
        Require(warehousePreflight.Contains("RemainingMassGrams",
                    StringComparison.Ordinal)
            && !warehousePreflight.Contains("massAdmission.TryReserve(",
                StringComparison.Ordinal)
            && !warehousePreflight.Contains("WarehouseMassAdmissionToken",
                StringComparison.Ordinal),
            "Warehouse prepared-output admission is not preflight-only.");

        string participant = File.ReadAllText(Path.Combine(
            services,
            "PreparedOutputExactDestinationAdmission.cs"));
        int participantStart = participant.IndexOf(
            "public sealed class PreparedOutputExactDestinationAdmissionParticipant",
            StringComparison.Ordinal);
        int completeStart = participantStart < 0
            ? -1
            : participant.IndexOf(
                "public bool TryComplete(",
                participantStart,
                StringComparison.Ordinal);
        int requireStart = completeStart < 0
            ? -1
            : participant.IndexOf(
                "private bool TryRequire(",
                completeStart,
                StringComparison.Ordinal);
        Require(participant.Contains("HaulPlannerAdmissionRequired",
                    StringComparison.Ordinal)
            && participant.Contains("TryPublishPreparedOutputAdmission(",
                StringComparison.Ordinal)
            && participant.Contains("Phase == PreparedOutputExactDestinationAdmissionPhase.Prepared",
                StringComparison.Ordinal)
            && completeStart >= 0
            && requireStart > completeStart
            && !participant.Substring(completeStart, requireStart - completeStart)
                .Contains("destinations.", StringComparison.Ordinal),
            "Prepared-output candidate handoff/lifecycle contract drifted.");
    }

    private static void VerifyCustodyOwnedExactAdmission()
    {
        const string destination = "production-input:building:qa:custody";
        const string owner = "economy.production-input";
        Vector2Int position = new(41, 7);
        FacilityBufferDestinationClaimRegistry claims = new();
        FakeOccupancyQuery occupancy = new()
        {
            MassAuthorityRevision = 7L,
            CustodyMassGrams = 4_000L
        };
        FacilityBufferMassAdmissionService admission = new(claims, occupancy);
        FacilityBufferCapacityProfile profile = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:custody",
            new PhysicalMassGrams(4_000L),
            capacityRevision: 3L);
        Require(claims.TryClaim(new FacilityBufferDestinationClaim(
                    destination,
                    position,
                    owner,
                    destination,
                    "building:qa:custody",
                    FacilityBufferDestinationAnchorKind.LiveBuilding),
                out _, out _)
            && admission.TryReplaceOwnedProfiles(owner, new[] { profile },
                out _, out _)
            && admission.TryGetCapacityAuthorityFingerprint(
                destination, position, out string authorityFingerprint)
            && authorityFingerprint.Length == 64,
            "Custody admission fixture authority did not publish.");

        FacilityBufferCustodyOwnedAdmissionRequest request =
            CreateCustodyRequest(
                profile,
                "qa:custody-admission:0001",
                "stack:custody:a",
                4_000L,
                massRevision: 7L);
        Require(admission.TryReserveCustodyOwnedExactLot(
                request,
                out FacilityBufferMassAdmissionToken token,
                out _, out _)
            && token.ReservedMassGrams == 4_000L
            && admission.TryReserveCustodyOwnedExactLot(
                request,
                out FacilityBufferMassAdmissionToken replay,
                out _, out _)
            && replay.TokenId == token.TokenId,
            "Custody-owned exact admission was not idempotent.");

        FacilityBufferCustodyOwnedAdmissionRequest conflict =
            CreateCustodyRequest(
                profile,
                "qa:custody-admission:0001",
                "stack:custody:conflict",
                4_000L,
                massRevision: 7L);
        Require(!admission.TryReserveCustodyOwnedExactLot(
                conflict, out _,
                out FacilityBufferMassAdmissionFailureCode conflictFailure,
                out _)
            && conflictFailure == FacilityBufferMassAdmissionFailureCode.TokenMismatch,
            "Custody admission accepted same-operation conflicting range.");
        Require(admission.TryRelease(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _, out _)
            && admission.TryGetCapacity(destination, position, out var released)
            && released.ReservedMassGrams == 0L,
            "Custody admission rollback retained grams.");

        FacilityBufferCustodyOwnedAdmissionRequest routedRequest =
            CreateCustodyRequest(
                profile,
                "qa:custody-admission:routed",
                "stack:custody:routed",
                4_000L,
                massRevision: 7L);
        Require(admission.TryReserveCustodyOwnedExactLot(
                routedRequest,
                out FacilityBufferMassAdmissionToken routedToken,
                out _,
                out _)
            && !admission.TryCommitRouted(
                routedToken,
                "wrong-lot-fingerprint",
                4_000L,
                out _,
                out FacilityBufferMassAdmissionFailureCode faultFailure,
                out _)
            && faultFailure == FacilityBufferMassAdmissionFailureCode.TokenMismatch
            && admission.TryGetCapacity(destination, position, out var stillReserved)
            && stillReserved.ReservedMassGrams == 4_000L
            && admission.TryCommitRouted(
                routedToken,
                routedToken.ExactLot.Fingerprint,
                routedToken.ReservedMassGrams,
                out FacilityBufferMassAdmissionReceipt routedReceipt,
                out _,
                out _)
            && routedReceipt.CommittedMassGrams == 4_000L
            && admission.TryGetCapacity(destination, position, out var committed)
            && committed.ReservedMassGrams == 0L
            && admission.TryCommitRouted(
                routedToken,
                routedToken.ExactLot.Fingerprint,
                routedToken.ReservedMassGrams,
                out FacilityBufferMassAdmissionReceipt replayReceipt,
                out _,
                out _)
            && replayReceipt.TokenId == routedReceipt.TokenId
            && admission.TryRollbackRouted(
                routedToken,
                routedReceipt,
                out _,
                out _)
            && !admission.TryGetReceipt(routedToken.TokenId, out _),
            "Custody admission routed commit/fault/replay/rollback was not atomic.");

        FacilityBufferCustodyOwnedAdmissionRequest staleMass =
            CreateCustodyRequest(profile, "qa:custody-admission:stale-mass",
                "stack:custody:b", 4_000L, massRevision: 6L);
        Require(!admission.TryReserveCustodyOwnedExactLot(
                staleMass, out _, out _, out _),
            "Custody admission accepted stale mass revision.");
        FacilityBufferCapacityProfile staleProfile = new(
            destination, position, owner, destination, "building:qa:custody",
            new PhysicalMassGrams(4_000L), capacityRevision: 2L);
        Require(!admission.TryReserveCustodyOwnedExactLot(
                CreateCustodyRequest(staleProfile,
                    "qa:custody-admission:stale-profile",
                    "stack:custody:c", 4_000L, 7L),
                out _, out _, out _),
            "Custody admission accepted stale profile revision.");

        occupancy.CustodyMassGrams = 4_001L;
        Require(!admission.TryReserveCustodyOwnedExactLot(
                CreateCustodyRequest(profile,
                    "qa:custody-admission:one-gram-over",
                    "stack:custody:d", 4_001L, 7L),
                out _,
                out FacilityBufferMassAdmissionFailureCode capacityFailure,
                out _)
            && capacityFailure ==
                FacilityBufferMassAdmissionFailureCode.CapacityUnavailable,
            "Custody admission did not reject a 1g capacity shortage.");
    }

    private static FacilityBufferCustodyOwnedAdmissionRequest CreateCustodyRequest(
        FacilityBufferCapacityProfile profile,
        string operationId,
        string stackId,
        long exactMassGrams,
        long massRevision)
    {
        FacilityBufferMassAdmissionRequest exact = new(
            operationId,
            profile.DestinationId,
            profile.DropPosition,
            profile.OwnerDomain,
            profile.OwnerOperationId,
            profile.OwnerFacilityId,
            profile.CapacityRevision,
            new[]
            {
                new FacilityBufferMassLotSlice(
                    stackId,
                    1,
                    1L,
                    new string('a', 64),
                    exactMassGrams)
            });
        return new FacilityBufferCustodyOwnedAdmissionRequest(
            exact,
            "route:qa:custody",
            new string('b', 64),
            massRevision);
    }

    private static void VerifyPlannedOutputAdmission()
    {
        const string destination = "production-output:building:qa:planned";
        const string owner = "economy.production-output";
        Vector2Int position = new(31, 9);
        FacilityBufferDestinationClaimRegistry claims = new();
        FakeOccupancyQuery occupancy = new();
        FakeMassQuery mass = new();
        FacilityBufferMassAdmissionService admission = new(
            claims,
            occupancy,
            mass);
        FacilityBufferDestinationClaim claim = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:planned",
            FacilityBufferDestinationAnchorKind.LiveBuilding);
        FacilityBufferCapacityProfile profile = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:planned",
            new PhysicalMassGrams(8_000L),
            capacityRevision: 1L);
        Require(claims.TryClaim(claim, out _, out _)
            && admission.TryReplaceOwnedProfiles(
                owner,
                new[] { profile },
                out _,
                out _),
            "Planned-output fixture failed to publish its authority.");

        FacilityBufferMassAdmissionRequest sourceRequest = CreateRequest(
            "qa:facility-buffer-source-vs-planned:source",
            profile,
            "stack:source-vs-planned",
            quantity: 2);
        Require(admission.TryReserveExactLot(
                sourceRequest,
                out FacilityBufferMassAdmissionToken sourceToken,
                out _,
                out _)
            && sourceToken.ReservedMassGrams == 4_000L,
            "Source-lot side of shared capacity did not reserve 4kg.");

        FacilityBufferPlannedOutputRequest plannedRequest = CreatePlannedRequest(
            profile,
            "qa:facility-buffer-source-vs-planned:planned-a",
            "production-output-batch:qa:0001",
            "outcome:qa:0001",
            "output:main",
            "qa:item:planned",
            quantity: 2);
        Require(admission.TryReservePlannedOutput(
                plannedRequest,
                out FacilityBufferPlannedOutputToken plannedToken,
                out _,
                out _)
            && plannedToken.ReservedMassGrams == 4_000L
            && admission.TryGetCapacity(
                destination,
                position,
                out FacilityBufferMassCapacitySnapshot sharedFull)
            && sharedFull.ReservedMassGrams == 8_000L,
            "Source-lot and planned-output tokens did not share one destination ledger.");

        Require(admission.TryReleasePlannedOutput(
                plannedToken,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _)
            && admission.TryGetCapacity(
                destination,
                position,
                out FacilityBufferMassCapacitySnapshot afterRelease)
            && afterRelease.ReservedMassGrams == 4_000L,
            "Planned-output release did not return its exact shared capacity.");

        FacilityBufferPlannedOutputRequest oneGramOver = CreatePlannedRequest(
            profile,
            "qa:facility-buffer-source-vs-planned:one-gram-over",
            "production-output-batch:qa:0002",
            "outcome:qa:0002",
            "output:heavy",
            "qa:item:one-gram-over",
            quantity: 1);
        Require(!admission.TryReservePlannedOutput(
                oneGramOver,
                out _,
                out FacilityBufferMassAdmissionFailureCode overflowFailure,
                out _)
            && overflowFailure
                == FacilityBufferMassAdmissionFailureCode.CapacityUnavailable,
            "Planned-output admission did not reject an exact 1g overflow.");

        FacilityBufferPlannedOutputRequest commitRequest = CreatePlannedRequest(
            profile,
            "qa:facility-buffer-source-vs-planned:planned-commit",
            "production-output-batch:qa:0003",
            "outcome:qa:0003",
            "output:main",
            "qa:item:planned",
            quantity: 2);
        Require(admission.TryReservePlannedOutput(
                commitRequest,
                out FacilityBufferPlannedOutputToken commitToken,
                out _,
                out _),
            "Planned-output commit fixture did not reserve capacity.");
        FacilityBufferPlannedOutputPublicationReceipt tampered =
            CreatePublication(
                commitToken,
                "stack:planned-output:tampered",
                massGrams: commitToken.ReservedMassGrams - 1L);
        Require(!admission.TryCommitPlannedOutput(
                commitToken,
                tampered,
                out _,
                out FacilityBufferMassAdmissionFailureCode tamperedFailure,
                out _)
            && tamperedFailure == FacilityBufferMassAdmissionFailureCode.TokenMismatch,
            "Planned-output admission accepted a tampered physical receipt.");

        FacilityBufferPlannedOutputPublicationReceipt exact = CreatePublication(
            commitToken,
            "stack:planned-output:exact",
            commitToken.ReservedMassGrams);
        Require(admission.TryCommitPlannedOutput(
                commitToken,
                exact,
                out FacilityBufferPlannedOutputReceipt committed,
                out _,
                out _)
            && committed.CommittedMassGrams == 4_000L
            && committed.PublishedQuantity == 2
            && admission.TryCommitPlannedOutput(
                commitToken,
                exact,
                out FacilityBufferPlannedOutputReceipt replay,
                out _,
                out _)
            && replay.TokenId == committed.TokenId
            && admission.TryGetPlannedOutputReceipt(
                commitToken.TokenId,
                out FacilityBufferPlannedOutputReceipt queried)
            && queried.TokenId == committed.TokenId,
            "Planned-output exact commit was not idempotent.");

        FacilityBufferPlannedOutputPublicationReceipt conflictingReplay =
            CreatePublication(
                commitToken,
                "stack:planned-output:other",
                commitToken.ReservedMassGrams);
        Require(!admission.TryCommitPlannedOutput(
                commitToken,
                conflictingReplay,
                out _,
                out FacilityBufferMassAdmissionFailureCode replayFailure,
                out _)
            && replayFailure == FacilityBufferMassAdmissionFailureCode.TokenMismatch,
            "Planned-output idempotency accepted a conflicting replay receipt.");
        Require(admission.TryRelease(
                sourceToken,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _),
            "Source-lot fixture did not release after shared-capacity verification.");
        occupancy.NonCarriedMassGrams = committed.CommittedMassGrams;

        FacilityBufferPlannedOutputRequest staleRequest = CreatePlannedRequest(
            profile,
            "qa:facility-buffer-source-vs-planned:stale-profile",
            "production-output-batch:qa:0004",
            "outcome:qa:0004",
            "output:main",
            "qa:item:planned",
            quantity: 1);
        Require(admission.TryReservePlannedOutput(
                staleRequest,
                out FacilityBufferPlannedOutputToken staleToken,
                out _,
                out _),
            "Stale planned-output fixture did not reserve capacity.");
        FacilityBufferCapacityProfile revisedProfile = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:planned",
            new PhysicalMassGrams(8_000L),
            capacityRevision: 2L);
        Require(admission.TryReplaceOwnedProfiles(
                owner,
                new[] { revisedProfile },
                out _,
                out _)
            && !admission.TryCommitPlannedOutput(
                staleToken,
                CreatePublication(
                    staleToken,
                    "stack:planned-output:stale",
                    staleToken.ReservedMassGrams),
                out _,
                out FacilityBufferMassAdmissionFailureCode staleFailure,
                out _)
            && staleFailure == FacilityBufferMassAdmissionFailureCode.TokenMismatch
            && admission.TryReleasePlannedOutput(
                staleToken,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _),
            "Planned-output commit did not fail loudly after profile revision drift.");

        FacilityBufferPlannedOutputRequest massStaleRequest = CreatePlannedRequest(
            revisedProfile,
            "qa:facility-buffer-source-vs-planned:stale-mass",
            "production-output-batch:qa:0005",
            "outcome:qa:0005",
            "output:main",
            "qa:item:planned",
            quantity: 1);
        Require(admission.TryReservePlannedOutput(
                massStaleRequest,
                out FacilityBufferPlannedOutputToken massStaleToken,
                out _,
                out _),
            "Mass-revision planned-output fixture did not reserve capacity.");
        mass.AuthorityRevision++;
        Require(!admission.TryCommitPlannedOutput(
                massStaleToken,
                CreatePublication(
                    massStaleToken,
                    "stack:planned-output:mass-stale",
                    massStaleToken.ReservedMassGrams),
                out _,
                out FacilityBufferMassAdmissionFailureCode massStaleFailure,
                out _)
            && massStaleFailure == FacilityBufferMassAdmissionFailureCode.TokenMismatch
            && admission.TryReleasePlannedOutput(
                massStaleToken,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _)
            && admission.TryGetCapacity(
                destination,
                position,
                out FacilityBufferMassCapacitySnapshot final)
            && final.ReservedMassGrams == 0L,
            "Planned-output mass revision drift or shared ledger cleanup failed.");
    }

    private static void VerifyLifecycleExceptionRollback()
    {
        const string destination = "power:building:qa:lifecycle";
        const string owner = "infrastructure.electrical";
        Vector2Int position = new(13, 5);
        FacilityBufferDestinationClaimRegistry claims = new();
        FakeOccupancyQuery occupancy = new();
        FacilityBufferMassAdmissionService admission = new(claims, occupancy);
        FacilityBufferDestinationLifecycleService lifecycle = new(
            claims,
            claims,
            admission,
            admission);
        FacilityBufferDestinationClaim originalClaim = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:lifecycle",
            FacilityBufferDestinationAnchorKind.LiveBuilding);
        FacilityBufferCapacityProfile originalProfile = new(
            destination,
            position,
            owner,
            destination,
            "building:qa:lifecycle",
            new PhysicalMassGrams(8_000L),
            1L);
        Require(
            lifecycle.TryReplaceOwnedAuthorities(
                owner,
                new[] { originalClaim },
                new[] { originalProfile },
                out _),
            "Lifecycle fixture failed to publish original authority.");

        FacilityBufferDestinationClaim changedClaim = new(
            destination,
            position,
            owner,
            destination + ":replacement",
            "building:qa:lifecycle",
            FacilityBufferDestinationAnchorKind.LiveBuilding);
        FacilityBufferCapacityProfile changedProfile = new(
            destination,
            position,
            owner,
            destination + ":replacement",
            "building:qa:lifecycle",
            new PhysicalMassGrams(9_000L),
            2L);
        occupancy.ThrowOnNextCapture = true;
        Require(
            Throws(() => lifecycle.TryReplaceOwnedAuthorities(
                owner,
                new[] { changedClaim },
                new[] { changedProfile },
                out _))
            && claims.TryGetClaim(destination, position, out FacilityBufferDestinationClaim restoredClaim)
            && string.Equals(
                restoredClaim.OwnerOperationId,
                originalClaim.OwnerOperationId,
                StringComparison.Ordinal)
            && admission.TryGetCapacity(
                destination,
                position,
                out FacilityBufferMassCapacitySnapshot restoredProfile)
            && restoredProfile.Profile.CapacityRevision == 1L,
            "Lifecycle exception left a torn claim/profile authority.");
    }

    private static void VerifyRestoreCandidateAtomicity()
    {
        const string owner = "infrastructure.electrical";
        Vector2Int livePosition = new(3, 4);
        Vector2Int restoredPosition = new(9, 10);
        FacilityBufferDestinationClaimRegistry claims = new();
        FakeOccupancyQuery occupancy = new();
        FacilityBufferMassAdmissionService admission = new(claims, occupancy);
        FacilityBufferDestinationLifecycleService lifecycle = new(
            claims,
            claims,
            admission,
            admission);
        FacilityBufferDestinationClaim liveClaim = CreateClaim(
            "power:building:qa:live",
            livePosition,
            owner);
        FacilityBufferCapacityProfile liveProfile = CreateProfile(
            liveClaim,
            8_000L,
            1L);
        Require(lifecycle.TryReplaceOwnedAuthorities(
                owner,
                new[] { liveClaim },
                new[] { liveProfile },
                out _),
            "Restore fixture failed to publish live authority.");

        FacilityBufferDestinationClaim restoredClaim = CreateClaim(
            "power:building:qa:restored",
            restoredPosition,
            owner);
        FacilityBufferCapacityProfile restoredProfile = CreateProfile(
            restoredClaim,
            12_000L,
            2L);
        claims.BeginRestoreCandidate();
        admission.BeginRestoreCandidate();
        Require(lifecycle.TryReplaceOwnedAuthorities(
                owner,
                new[] { restoredClaim },
                new[] { restoredProfile },
                out _)
            && claims.TryGetClaim(liveClaim.DestinationId, livePosition, out _)
            && !claims.TryGetClaim(restoredClaim.DestinationId, restoredPosition, out _)
            && claims.TryGetAuthorityClaim(
                restoredClaim.DestinationId,
                restoredPosition,
                out _)
            && admission.CaptureAuthorityProfiles().Count == 1,
            "Restore staging leaked candidate authority into live queries.");
        claims.PublishRestoreCandidate();
        admission.PublishRestoreCandidate();
        Require(claims.TryGetClaim(
                restoredClaim.DestinationId,
                restoredPosition,
                out _)
            && admission.TryGetCapacity(
                restoredProfile.DestinationId,
                restoredPosition,
                out _),
            "Restore candidates did not publish in dependency order.");
        admission.RollbackPublishedRestoreCandidate();
        claims.RollbackPublishedRestoreCandidate();
        Require(claims.TryGetClaim(liveClaim.DestinationId, livePosition, out _)
            && admission.TryGetCapacity(
                liveProfile.DestinationId,
                livePosition,
                out _),
            "Restore rollback did not restore both live authorities.");
        Require(string.CompareOrdinal(claims.ParticipantId, admission.ParticipantId) < 0,
            "Restore participant ids do not publish claim before capacity.");
    }

    private static void VerifyReserveRevalidatesClaim()
    {
        const string owner = "infrastructure.electrical";
        Vector2Int position = new(21, 2);
        FacilityBufferDestinationClaimRegistry claims = new();
        FakeOccupancyQuery occupancy = new();
        FacilityBufferMassAdmissionService admission = new(claims, occupancy);
        FacilityBufferDestinationClaim claim = CreateClaim(
            "power:building:qa:stale-claim",
            position,
            owner);
        FacilityBufferCapacityProfile profile = CreateProfile(
            claim,
            8_000L,
            1L);
        Require(claims.TryClaim(claim, out _, out _)
            && admission.TryReplaceOwnedProfiles(owner, new[] { profile }, out _, out _)
            && claims.TryRevoke(claim, out _, out _),
            "Stale-claim fixture could not create a profile/claim split.");
        Require(!admission.TryReserveExactLot(
                CreateRequest(
                    "qa:facility-buffer-transfer:stale-claim",
                    profile,
                    "stack:stale",
                    1),
                out _,
                out FacilityBufferMassAdmissionFailureCode failure,
                out _)
            && failure
                == FacilityBufferMassAdmissionFailureCode.ClaimMissingOrMismatched,
            "Admission accepted a stale profile after its exact claim was revoked.");
    }

    private static void VerifyRestorePublishRevisionPreflight()
    {
        const string owner = "infrastructure.electrical";
        Vector2Int position = new(17, 6);
        FacilityBufferDestinationClaimRegistry claims = new();
        FakeOccupancyQuery occupancy = new();
        FacilityBufferMassAdmissionService admission = new(claims, occupancy);
        FacilityBufferDestinationClaim liveClaim = CreateClaim(
            "power:building:qa:revision-preflight",
            position,
            owner);
        FacilityBufferCapacityProfile liveProfile = CreateProfile(
            liveClaim,
            8_000L,
            1L);
        Require(claims.TryClaim(liveClaim, out _, out _)
            && admission.TryReplaceOwnedProfiles(
                owner,
                new[] { liveProfile },
                out _,
                out _),
            "Revision-preflight fixture failed to publish live authority.");

        SetPrivateRevision(admission, long.MaxValue - 1L);
        Require(ThrowsOverflow(admission.BeginRestoreCandidate)
            && admission.Revision == long.MaxValue - 1L
            && admission.TryGetCapacity(
                liveProfile.DestinationId,
                position,
                out _),
            "Restore Begin overflow mutated live capacity authority.");

        SetPrivateRevision(admission, 20L);
        admission.BeginRestoreCandidate();
        admission.PublishRestoreCandidate();
        Require(admission.Revision == 21L
            && !admission.TryGetCapacity(
                liveProfile.DestinationId,
                position,
                out _),
            "Restore publish did not use its precomputed revision/map.");
        admission.RollbackPublishedRestoreCandidate();
        Require(admission.Revision == 22L
            && admission.TryGetCapacity(
                liveProfile.DestinationId,
                position,
                out _),
            "Restore rollback did not use its precomputed revision/map.");

        admission.BeginRestoreCandidate();
        admission.PublishRestoreCandidate();
        long completedRevision = admission.Revision;
        admission.CompleteRestoreCandidate();
        Require(admission.Revision == completedRevision
            && !admission.TryGetCapacity(
                liveProfile.DestinationId,
                position,
                out _),
            "Restore completion changed the published revision or restored stale capacity.");
    }

    private static void VerifyClaimRestorePublishRevisionPreflight()
    {
        const string owner = "infrastructure.electrical";
        Vector2Int position = new(15, 6);
        FacilityBufferDestinationClaimRegistry claims = new();
        FacilityBufferDestinationClaim liveClaim = CreateClaim(
            "power:building:qa:claim-revision-preflight",
            position,
            owner);
        Require(claims.TryClaim(liveClaim, out _, out _),
            "Claim revision-preflight fixture failed to publish live authority.");

        SetPrivateRevision(claims, long.MaxValue - 1L);
        Require(ThrowsOverflow(claims.BeginRestoreCandidate)
            && claims.Revision == long.MaxValue - 1L
            && claims.TryGetClaim(liveClaim.DestinationId, position, out _),
            "Claim restore Begin overflow mutated live destination authority.");

        SetPrivateRevision(claims, 30L);
        claims.BeginRestoreCandidate();
        claims.PublishRestoreCandidate();
        Require(claims.Revision == 31L
            && !claims.TryGetClaim(liveClaim.DestinationId, position, out _),
            "Claim restore publish did not use its precomputed revision/map.");
        claims.RollbackPublishedRestoreCandidate();
        Require(claims.Revision == 32L
            && claims.TryGetClaim(liveClaim.DestinationId, position, out _),
            "Claim restore rollback did not use its precomputed revision/map.");

        claims.BeginRestoreCandidate();
        claims.PublishRestoreCandidate();
        long completedRevision = claims.Revision;
        claims.CompleteRestoreCandidate();
        Require(claims.Revision == completedRevision
            && !claims.TryGetClaim(liveClaim.DestinationId, position, out _),
            "Claim restore completion changed revision or restored stale authority.");
    }

    private static FacilityBufferDestinationClaim CreateClaim(
        string destination,
        Vector2Int position,
        string owner) => new(
        destination,
        position,
        owner,
        destination,
        destination.Substring("power:".Length),
        FacilityBufferDestinationAnchorKind.LiveBuilding);

    private static FacilityBufferCapacityProfile CreateProfile(
        FacilityBufferDestinationClaim claim,
        long maxMassGrams,
        long revision) => new(
        claim.DestinationId,
        claim.DropPosition,
        claim.OwnerDomain,
        claim.OwnerOperationId,
        claim.OwnerFacilityId,
        new PhysicalMassGrams(maxMassGrams),
        revision);

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private static bool ThrowsOverflow(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (OverflowException)
        {
            return true;
        }
    }

    private static void SetPrivateRevision(
        FacilityBufferMassAdmissionService admission,
        long value)
    {
        System.Reflection.FieldInfo field = typeof(
                FacilityBufferMassAdmissionService)
            .GetField(
                "revision",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException("Facility-buffer revision field is missing.");
        field.SetValue(admission, value);
    }

    private static void SetPrivateRevision(
        FacilityBufferDestinationClaimRegistry claims,
        long value)
    {
        System.Reflection.FieldInfo field = typeof(
                FacilityBufferDestinationClaimRegistry)
            .GetField(
                "revision",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic);
        if (field == null)
            throw new InvalidOperationException("Facility-buffer claim revision field is missing.");
        field.SetValue(claims, value);
    }

    private static FacilityBufferMassAdmissionRequest CreateRequest(
        string operationId,
        FacilityBufferCapacityProfile profile,
        string stackId,
        int quantity) => new(
        operationId,
        profile.DestinationId,
        profile.DropPosition,
        profile.OwnerDomain,
        profile.OwnerOperationId,
        profile.OwnerFacilityId,
        profile.CapacityRevision,
        new[]
        {
            new FacilityBufferMassLotSlice(
                stackId,
                quantity,
                expectedReservationRevision: 1L)
        });

    private static FacilityBufferPlannedOutputRequest CreatePlannedRequest(
        FacilityBufferCapacityProfile profile,
        string operationId,
        string batchCommitId,
        string outcomeFingerprint,
        string outputLineId,
        string itemId,
        int quantity,
        string capacitySourceDigest = DefaultProductionCapacitySourceDigest,
        long expectedMinimumCapacityGrams = 1L) => new(
        operationId,
        batchCommitId,
        outcomeFingerprint,
        profile.DestinationId,
        profile.DropPosition,
        profile.OwnerDomain,
        profile.OwnerOperationId,
        profile.OwnerFacilityId,
        profile.CapacityRevision,
        new[]
        {
            new FacilityBufferPlannedOutputSlice(
                outputLineId,
                PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)itemId),
                quantity)
        },
        capacitySourceDigest,
        expectedMinimumCapacityGrams);

    private static FacilityBufferPlannedOutputPublicationReceipt CreatePublication(
        FacilityBufferPlannedOutputToken token,
        string stackId,
        long massGrams)
    {
        FacilityBufferPlannedOutputSliceSnapshot line =
            token.PlannedOutput.Slices[0];
        return new FacilityBufferPlannedOutputPublicationReceipt(
            token.TokenId,
            token.Request.BatchCommitId,
            token.Request.OutcomeFingerprint,
            token.Request.DestinationId,
            token.Request.DropPosition,
            token.Request.ExpectedOwnerDomain,
            token.Request.ExpectedOwnerOperationId,
            token.Request.ExpectedOwnerFacilityId,
            token.Request.ExpectedCapacityRevision,
            token.PlannedOutput.Fingerprint,
            new[]
            {
                new FacilityBufferPublishedOutputStackReceipt(
                    stackId,
                    line.OutputLineId,
                    line.ItemDefinitionId,
                    line.Quantity,
                    new PhysicalMassGrams(massGrams))
            });
    }

    private sealed class FakeOccupancyQuery :
        IFacilityBufferCustodyOwnedPhysicalOccupancyQuery
    {
        public long NonCarriedMassGrams { get; set; }
        public bool ThrowOnNextCapture { get; set; }
        public long MassAuthorityRevision { get; set; } = 1L;
        public long CustodyMassGrams { get; set; } = 2_000L;

        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId)
        {
            if (ThrowOnNextCapture)
            {
                ThrowOnNextCapture = false;
                throw new InvalidOperationException("qa-occupancy-fault");
            }
            return new FacilityBufferPhysicalOccupancySnapshot(
                NonCarriedMassGrams,
                committedCarriedMassGrams: 0L);
        }

        public bool TryCaptureExactLot(
            System.Collections.Generic.IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            failureReason = string.Empty;
            int quantity = 0;
            foreach (FacilityBufferMassLotSlice slice in slices)
                quantity = checked(quantity + slice.Quantity);
            lot = new FacilityBufferExactLotSnapshot(
                "qa-lot:" + quantity,
                new PhysicalMassGrams(checked(quantity * 2_000L)));
            return quantity > 0;
        }

        public bool TryCaptureCustodyOwnedExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            string expectedDestinationId,
            string expectedRouteOperationId,
            string expectedPhysicalReceiptFingerprint,
            long expectedMassAuthorityRevision,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = string.Empty;
            if (expectedMassAuthorityRevision != MassAuthorityRevision
                || slices == null
                || slices.Count == 0)
            {
                failureReason = "qa-custody-stale";
                return false;
            }
            lot = new FacilityBufferExactLotSnapshot(
                $"qa-custody:{slices[0].StackId}:{CustodyMassGrams}",
                new PhysicalMassGrams(CustodyMassGrams));
            return true;
        }
    }

    private sealed class FakeMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision { get; set; } = 1L;

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(ResolveUnitMass(itemId));

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject)
        {
            if (subject == null)
                throw new ArgumentNullException(nameof(subject));
            return subject.HasPreparedUnitMass
                ? subject.PreparedUnitMass
                : GetDefinitionUnitMass(subject.ItemId);
        }

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject)
        {
            if (subject == null || !itemId.Equals(subject.ItemId))
                throw new InvalidOperationException("qa-mass-subject-mismatch");
            return subject.HasPreparedUnitMass
                ? subject.PreparedUnitMass
                : GetDefinitionUnitMass(itemId);
        }

        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            GetQuantityMass(lot.Subject.ItemId, lot.Subject, lot.Quantity);

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => GetStackUnitMass(itemId, subject).Multiply(quantity);

        private static long ResolveUnitMass(ItemDefinitionId itemId)
        {
            if (!itemId.IsValid)
                throw new InvalidOperationException("qa-mass-item-missing");
            return string.Equals(
                itemId.Value,
                "qa:item:one-gram-over",
                StringComparison.Ordinal)
                    ? 4_001L
                    : string.Equals(
                        itemId.Value,
                        "feed:dog-food",
                        StringComparison.Ordinal)
                        ? 525L
                    : string.Equals(
                        itemId.Value,
                        "qa:item:two-grams",
                        StringComparison.Ordinal)
                        ? 2L
                    : 2_000L;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
