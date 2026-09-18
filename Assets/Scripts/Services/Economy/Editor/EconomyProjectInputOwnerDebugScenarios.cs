using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class EconomyProjectInputOwnerDebugScenarios
{
    [MenuItem("Tools/Dungeon Story/QA/Economy Project Input Owners")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("ECONOMY_PROJECT_INPUT_OWNER_PASS");
    }

    public static void RunAll()
    {
        AuthorityStore store = new();
        RecordingRelease release = new();
        EconomyProjectInputOwnerRuntime runtime = new(
            new FixedMassQuery(new Dictionary<string, long>
            {
                ["material:stone"] = 100L,
                ["material:metal"] = 250L,
                ["material:lumber"] = 80L
            }),
            store,
            store,
            store,
            release);

        EconomyProjectInputOwnerDescriptor grand = Descriptor(
            EconomyProjectInputOwnerAuthority.GrandProjectDomain,
            "grand-project:qa",
            new Vector2Int(3, 5),
            FacilityBufferDestinationAnchorKind.LiveFacility,
            "building:grand-project-office:qa",
            new Dictionary<string, int> { ["material:stone"] = 2 });
        EconomyProjectInputOwnerDescriptor regional = Descriptor(
            EconomyProjectInputOwnerAuthority.RegionalContractDomain,
            "regional-contract:qa",
            new Vector2Int(7, 9),
            FacilityBufferDestinationAnchorKind.ReservedTarget,
            string.Empty,
            new Dictionary<string, int> { ["material:metal"] = 3 });
        EconomyProjectInputOwnerDescriptor stock = Descriptor(
            EconomyProjectInputOwnerAuthority.StockPolicyDomain,
            "material:lumber",
            new Vector2Int(11, 13),
            FacilityBufferDestinationAnchorKind.ReservedTarget,
            string.Empty,
            new Dictionary<string, int> { ["material:lumber"] = 20 });
        EconomyProjectInputOwnerDescriptor faction = Descriptor(
            EconomyProjectInputOwnerAuthority.FactionContractDomain,
            "faction-contract:qa",
            new Vector2Int(13, 15),
            FacilityBufferDestinationAnchorKind.ReservedTarget,
            string.Empty,
            new Dictionary<string, int> { ["material:stone"] = 4 });

        VerifyProjection(runtime, store, grand, 200L);
        VerifyProjection(runtime, store, regional, 750L);
        VerifyProjection(runtime, store, stock, 1_600L);
        VerifyProjection(runtime, store, faction, 400L);
        VerifyCanonicalFailLoud(grand);
        VerifyCarriedAwareRetirement(
            runtime,
            store,
            release,
            regional,
            EconomyProjectInputOwnerAuthority.RegionalContractTerminalReason);
        VerifyCarriedAwareRetirement(
            runtime,
            store,
            release,
            faction,
            EconomyProjectInputOwnerAuthority.FactionContractTerminalReason);
        VerifyCurrentFormatRestore(runtime, store, grand, stock, faction);
    }

    private static void VerifyProjection(
        EconomyProjectInputOwnerRuntime runtime,
        AuthorityStore store,
        EconomyProjectInputOwnerDescriptor descriptor,
        long expectedGrams)
    {
        Require(runtime.TryEnsure(
                descriptor,
                out EconomyProjectInputOwnerProjection projection,
                out string failureReason),
            failureReason);
        FacilityBufferDestinationClaim claim = store.Claims.Single(value =>
            value.DestinationId == descriptor.DestinationId);
        FacilityBufferCapacityProfile profile = store.Profiles.Single(value =>
            value.DestinationId == descriptor.DestinationId);
        Require(projection.CapacityGrams == expectedGrams
            && projection.MassAuthorityRevision == 17L
            && projection.Fingerprint.Length == 64
            && claim.AdmissionPolicy ==
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
            && claim.AnchorKind == descriptor.AnchorKind
            && profile.MaxMassGrams == expectedGrams,
            "Economy project input-owner exact gram projection drifted.");
        Require(runtime.TryValidate(
                Frozen(descriptor, projection),
                out string validateFailure),
            validateFailure);
    }

    private static void VerifyCanonicalFailLoud(
        EconomyProjectInputOwnerDescriptor descriptor)
    {
        bool rejected = false;
        try
        {
            _ = new EconomyProjectInputOwnerDescriptor(
                descriptor.OwnerDomain,
                descriptor.OwnerOperationId,
                descriptor.DestinationId,
                descriptor.Position,
                FacilityBufferDestinationAnchorKind.LiveFacility,
                " " + descriptor.OwnerFacilityId,
                descriptor.Requirements);
        }
        catch (ArgumentException)
        {
            rejected = true;
        }
        Require(rejected, "Economy input owner silently trimmed a saved owner ID.");
    }

    private static void VerifyCarriedAwareRetirement(
        EconomyProjectInputOwnerRuntime runtime,
        AuthorityStore store,
        RecordingRelease release,
        EconomyProjectInputOwnerDescriptor descriptor,
        string terminalReason)
    {
        store.Events.Clear();
        release.Events = store.Events;
        release.Fail = true;
        Require(!runtime.TryRetireDestination(
                descriptor.OwnerDomain,
                descriptor.DestinationId,
                terminalReason,
                out _)
            && store.Claims.Any(value =>
                value.DestinationId == descriptor.DestinationId),
            "Failed carried release revoked economy input authority.");
        release.Fail = false;
        Require(runtime.TryRetireDestination(
                descriptor.OwnerDomain,
                descriptor.DestinationId,
                terminalReason,
                out string failureReason),
            failureReason);
        Require(store.Events.SequenceEqual(new[] { "release", "release", "replace" }),
            "Economy input retirement did not release before paired revoke.");
    }

    private static void VerifyCurrentFormatRestore(
        EconomyProjectInputOwnerRuntime runtime,
        AuthorityStore store,
        EconomyProjectInputOwnerDescriptor grand,
        EconomyProjectInputOwnerDescriptor stock,
        EconomyProjectInputOwnerDescriptor faction)
    {
        EconomyProjectInputOwnerDescriptor frozenGrand = Freeze(runtime, grand);
        EconomyProjectInputOwnerDescriptor frozenStock = Freeze(runtime, stock);
        EconomyProjectInputOwnerDescriptor frozenFaction = Freeze(
            runtime,
            faction);
        Require(runtime.TryReplaceForRestore(
                grand.OwnerDomain,
                new[] { frozenGrand },
                out string grandFailure),
            grandFailure);
        Require(runtime.TryReplaceForRestore(
                stock.OwnerDomain,
                new[] { frozenStock },
                out string stockFailure),
            stockFailure);
        Require(runtime.TryReplaceForRestore(
                faction.OwnerDomain,
                new[] { frozenFaction },
                out string factionFailure),
            factionFailure);
        Require(store.Claims.Count == 3
            && store.Profiles.Count == 3
            && store.Profiles.All(value => value.MaxMassGrams > 0L),
            "Economy input-owner current-format restore join drifted.");

        string authorityBeforeReject = AuthoritySignature(store);
        EconomyProjectInputOwnerDescriptor driftedFaction = new(
            frozenFaction.OwnerDomain,
            frozenFaction.OwnerOperationId,
            frozenFaction.DestinationId,
            frozenFaction.Position,
            frozenFaction.AnchorKind,
            frozenFaction.OwnerFacilityId,
            frozenFaction.Requirements,
            frozenFaction.StoredCapacityGrams + 1L,
            frozenFaction.StoredMassAuthorityRevision,
            frozenFaction.StoredCapacityFingerprint);
        Require(!runtime.TryValidateForRestore(
                faction.OwnerDomain,
                new[] { driftedFaction },
                out _)
            && !runtime.TryReplaceForRestore(
                faction.OwnerDomain,
                new[] { driftedFaction },
                out _)
            && string.Equals(
                authorityBeforeReject,
                AuthoritySignature(store),
                StringComparison.Ordinal),
            "Rejected faction input-owner restore mutated live authority.");
    }

    private static string AuthoritySignature(AuthorityStore store) =>
        string.Join(
            "|",
            store.Claims.Select(value =>
                $"C:{value.OwnerDomain}:{value.DestinationId}"))
        + "||"
        + string.Join(
            "|",
            store.Profiles.Select(value =>
                $"P:{value.OwnerDomain}:{value.DestinationId}:"
                + value.MaxMassGrams));

    private static EconomyProjectInputOwnerDescriptor Freeze(
        EconomyProjectInputOwnerRuntime runtime,
        EconomyProjectInputOwnerDescriptor descriptor)
    {
        Require(runtime.TryEnsure(
                descriptor,
                out EconomyProjectInputOwnerProjection projection,
                out string failureReason),
            failureReason);
        return Frozen(descriptor, projection);
    }

    private static EconomyProjectInputOwnerDescriptor Frozen(
        EconomyProjectInputOwnerDescriptor descriptor,
        EconomyProjectInputOwnerProjection projection) => new(
        descriptor.OwnerDomain,
        descriptor.OwnerOperationId,
        descriptor.DestinationId,
        descriptor.Position,
        descriptor.AnchorKind,
        descriptor.OwnerFacilityId,
        descriptor.Requirements,
        projection.CapacityGrams,
        projection.MassAuthorityRevision,
        projection.Fingerprint);

    private static EconomyProjectInputOwnerDescriptor Descriptor(
        string domain,
        string operationId,
        Vector2Int position,
        FacilityBufferDestinationAnchorKind anchor,
        string facilityId,
        IReadOnlyDictionary<string, int> requirements) => new(
        domain,
        operationId,
        EconomyProjectInputOwnerAuthority.BuildDestinationId(
            domain,
            operationId),
        position,
        anchor,
        facilityId,
        requirements);

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
        internal List<FacilityBufferDestinationClaim> Claims { get; } = new();
        internal List<FacilityBufferCapacityProfile> Profiles { get; } = new();
        internal List<string> Events { get; } = new();

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
            Claims.RemoveAll(value => value.OwnerDomain == ownerDomain);
            Profiles.RemoveAll(value => value.OwnerDomain == ownerDomain);
            Claims.AddRange(desiredClaims);
            Profiles.AddRange(desiredProfiles);
            Claims.Sort((left, right) => string.CompareOrdinal(
                left.DestinationId,
                right.DestinationId));
            Profiles.Sort((left, right) => string.CompareOrdinal(
                left.DestinationId,
                right.DestinationId));
            Events.Add("replace");
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class RecordingRelease :
        IFacilityBufferDestinationReleaseService
    {
        internal List<string> Events { get; set; }
        internal bool Fail { get; set; }

        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            Events?.Add("release");
            releasedQuantity = 0;
            failureReason = Fail ? "qa-carried-release-rejected" : string.Empty;
            return !Fail;
        }
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly IReadOnlyDictionary<string, long> masses;

        internal FixedMassQuery(IReadOnlyDictionary<string, long> masses) =>
            this.masses = masses;

        public long AuthorityRevision => 17L;

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(masses[itemId.Value]);

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => throw new NotSupportedException();

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => GetDefinitionUnitMass(itemId);

        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            throw new NotSupportedException();

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new(checked(
            GetDefinitionUnitMass(itemId).Value * quantity));
    }
}
