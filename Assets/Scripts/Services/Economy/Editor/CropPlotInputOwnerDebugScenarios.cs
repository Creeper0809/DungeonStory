using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CropPlotInputOwnerDebugScenarios
{
    private const string PlotId = "building:crop-plot:qa";
    private const string SeedItemId = "seed:twilight-grain";
    private const string WaterItemId = "resource:clean-water";
    private const string TreatmentItemId = "supply:botanical-pesticide";

    [MenuItem("Tools/Dungeon Story/QA/Crop Plot Input Owner")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("CROP_PLOT_INPUT_OWNER_PASS");
    }

    public static void RunAll()
    {
        VerifyExactSowAndTreatmentProjection();
        VerifyFailedCarriedReleaseRetainsAuthority();
        VerifySuccessfulRetirementReleasesBeforeRevoke();
        VerifyCapacityExpansionRetainsPhysicalCustody();
        VerifyRestoreReplacementIsDeterministic();
    }

    private static void VerifyExactSowAndTreatmentProjection()
    {
        CropPlotInputOwnerDescriptor sow = SowDescriptor(
            PlotId,
            2,
            waterQuantity: 3);
        CropPlotInputOwnerDescriptor treatment = TreatmentDescriptor(
            PlotId,
            4);
        CropPlotInputOwnerProjection projection =
            CropPlotInputOwnerAuthority.BuildProjection(
                new[] { treatment, sow },
                MassQuery());
        Require(projection.Claims.Count == 2
            && projection.Profiles.Count == 2
            && projection.Claims.All(value =>
                value.AnchorKind
                    == FacilityBufferDestinationAnchorKind.LiveFacility
                && value.AdmissionPolicy
                    == FacilityBufferDestinationAdmissionPolicy
                        .ExactGramRequired
                && value.OwnerDomain
                    == CropPlotInputOwnerAuthority.OwnerDomain
                && value.OwnerFacilityId == PlotId),
            "Crop-plot input claim policy drifted.");
        FacilityBufferCapacityProfile sowProfile = projection.Profiles.Single(
            value => value.DestinationId == sow.DestinationId);
        FacilityBufferCapacityProfile treatmentProfile =
            projection.Profiles.Single(
                value => value.DestinationId == treatment.DestinationId);
        Require(sowProfile.MaxMassGrams == 650L
            && treatmentProfile.MaxMassGrams == 700L
            && projection.Claims.Select(value => value.DestinationId)
                .SequenceEqual(
                    projection.Claims.Select(value => value.DestinationId)
                        .OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal),
            "Crop-plot exact gram projection or stable ordering drifted.");
    }

    private static void VerifyFailedCarriedReleaseRetainsAuthority()
    {
        AuthorityStore store = new();
        RecordingRelease release = new();
        CropPlotInputOwnerRuntime runtime = Runtime(store, release);
        CropPlotInputOwnerDescriptor descriptor = SowDescriptor(
            PlotId,
            1,
            waterQuantity: 2);
        Require(runtime.TryEnsure(descriptor, out string ensureFailure),
            ensureFailure);
        int replaceCalls = store.ReplaceCalls;
        release.Fail = true;
        Require(!runtime.TryRetireDestination(
                descriptor.DestinationId,
                descriptor.Position,
                CropPlotInputOwnerAuthority.PlotLostReleaseReasonCode,
                out string retireFailure)
            && retireFailure.StartsWith(
                "crop-plot-input-owner-terminal-release-failed:",
                StringComparison.Ordinal)
            && store.ReplaceCalls == replaceCalls
            && store.Claims.Count == 1
            && store.Profiles.Count == 1,
            "Failed carried release retired crop-plot input authority.");
    }

    private static void VerifySuccessfulRetirementReleasesBeforeRevoke()
    {
        AuthorityStore store = new();
        RecordingRelease release = new();
        CropPlotInputOwnerRuntime runtime = Runtime(store, release);
        CropPlotInputOwnerDescriptor descriptor = TreatmentDescriptor(
            PlotId,
            3);
        Require(runtime.TryEnsure(descriptor, out string ensureFailure),
            ensureFailure);
        int replaceCalls = store.ReplaceCalls;
        Require(runtime.TryRetireDestination(
                descriptor.DestinationId,
                descriptor.Position,
                CropPlotInputOwnerAuthority
                    .TreatmentCompletedReleaseReasonCode,
                out string retireFailure),
            retireFailure);
        Require(release.Calls.SequenceEqual(
                new[] { descriptor.DestinationId },
                StringComparer.Ordinal)
            && store.ReplaceCalls == replaceCalls + 1
            && store.Claims.Count == 0
            && store.Profiles.Count == 0,
            "Crop-plot retirement did not release before paired revoke.");
    }

    private static void VerifyCapacityExpansionRetainsPhysicalCustody()
    {
        AuthorityStore store = new();
        RecordingRelease release = new();
        CropPlotInputOwnerRuntime runtime = Runtime(store, release);
        CropPlotInputOwnerDescriptor small = SowDescriptor(
            PlotId,
            5,
            waterQuantity: 1);
        CropPlotInputOwnerDescriptor expanded = SowDescriptor(
            PlotId,
            5,
            waterQuantity: 4);
        Require(runtime.TryEnsure(small, out string smallFailure),
            smallFailure);
        Require(runtime.TryEnsure(expanded, out string expansionFailure),
            expansionFailure);
        Require(release.Calls.Count == 0
            && store.Profiles.Single().MaxMassGrams == 850L,
            "Positive crop-plot capacity expansion released physical custody.");
    }

    private static void VerifyRestoreReplacementIsDeterministic()
    {
        AuthorityStore store = new();
        CropPlotInputOwnerRuntime runtime = Runtime(
            store,
            new RecordingRelease());
        CropPlotInputOwnerDescriptor first = SowDescriptor(
            PlotId,
            0,
            waterQuantity: 2);
        CropPlotInputOwnerDescriptor second = TreatmentDescriptor(
            "building:crop-plot:qb",
            2);
        Require(runtime.TryReplaceForRestore(
                new[] { second, first },
                out string firstFailure),
            firstFailure);
        string[] ordered = store.Claims.Select(value => value.DestinationId)
            .ToArray();
        Require(runtime.TryReplaceForRestore(
                new[] { first, second },
                out string secondFailure),
            secondFailure);
        Require(ordered.SequenceEqual(
                store.Claims.Select(value => value.DestinationId),
                StringComparer.Ordinal)
            && store.Claims.Count == 2
            && store.Profiles.Count == 2,
            "Crop-plot restore replacement depended on input order.");
    }

    private static CropPlotInputOwnerRuntime Runtime(
        AuthorityStore store,
        RecordingRelease release) => new(
        MassQuery(),
        store,
        store,
        store,
        release);

    private static FixedMassQuery MassQuery() => new(
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            [SeedItemId] = 50L,
            [WaterItemId] = 200L,
            [TreatmentItemId] = 700L
        });

    private static CropPlotInputOwnerDescriptor SowDescriptor(
        string plotId,
        int sequence,
        int waterQuantity) => new(
        plotId,
        new Vector2Int(sequence, 7),
        CropPlotInputOwnerAuthority.BuildSowDestinationId(plotId, sequence),
        CropPhysicalTransactionOutbox.FormatSowOperationId(plotId, sequence),
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [SeedItemId] = 1,
            [WaterItemId] = waterQuantity
        });

    private static CropPlotInputOwnerDescriptor TreatmentDescriptor(
        string plotId,
        int sequence) => new(
        plotId,
        new Vector2Int(sequence, 9),
        CropPlotInputOwnerAuthority.BuildTreatmentDestinationId(
            plotId,
            sequence),
        CropTreatmentPhysicalOutbox.FormatOperationId(plotId, sequence),
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [TreatmentItemId] = 1
        });

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
            Require(ownerDomain == CropPlotInputOwnerAuthority.OwnerDomain,
                "Fixture received another owner domain.");
            Claims = desiredClaims
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray();
            Profiles = desiredProfiles
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray();
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
