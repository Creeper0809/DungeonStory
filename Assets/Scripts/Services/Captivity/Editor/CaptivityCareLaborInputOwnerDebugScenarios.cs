using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CaptivityCareLaborInputOwnerDebugScenarios
{
    private const string PassToken =
        "CAPTIVITY_CARE_LABOR_INPUT_OWNER_FOCUSED_PASS";

    [MenuItem(
        "DungeonStory/V27/Captivity/Run Care Labor Input Owner Contracts")]
    public static void RunAll()
    {
        VerifyExactPositiveGramProjection();
        VerifyTerminalReleasePrecedesAuthorityRetirement();
        VerifyFailedCarriedReleasePreservesPair();
        VerifyCurrentFormatRestoreJoinOrdering();
        VerifyPerformerDelegatesSharedCareOwnership();
        Debug.Log("[CaptivityCareLaborInputOwner] " + PassToken);
    }

    private static void VerifyExactPositiveGramProjection()
    {
        CaptivityCareLaborInputOwnerProjection projection =
            CaptivityCareLaborInputOwnerAuthority.BuildProjection(new[]
            {
                Descriptor(CaptivityCareLaborInputKind.Care, 800L),
                Descriptor(CaptivityCareLaborInputKind.LaborTool, 2_350L)
            });
        Require(projection.Claims.Count == 2
            && projection.Profiles.Count == 2,
            "Care/labor owner did not create two exact pairs.");
        Require(projection.Claims.All(value =>
                value.OwnerDomain ==
                    CaptivityCareLaborInputOwnerAuthority.OwnerDomain
                && value.OwnerFacilityId == "housing:alpha"
                && value.AnchorKind ==
                    FacilityBufferDestinationAnchorKind.LiveFacility
                && value.AdmissionPolicy ==
                    FacilityBufferDestinationAdmissionPolicy
                        .ExactGramRequired),
            "Care/labor claim lost its canonical domain or live housing anchor.");
        Require(projection.Claims.Select(value => value.DestinationId)
                .SequenceEqual(new[]
                {
                    "captive-care:captive:alpha",
                    "captive-labor-tool:captive:alpha"
                }, StringComparer.Ordinal)
            && projection.Profiles.Select(value => value.MaxMassGrams)
                .SequenceEqual(new[] { 800L, 2_350L }),
            "Care/labor destination identity or positive gram profile drifted.");
    }

    private static void VerifyTerminalReleasePrecedesAuthorityRetirement()
    {
        AuthorityStore store = StoreWithProjection();
        CaptivityCareLaborInputOwnerRuntime runtime = Runtime(store);
        int replacementsBefore = store.ReplaceCalls;

        Require(runtime.TryReconcileLive(
                Array.Empty<CaptiveState>(),
                out string failureReason),
            failureReason);
        Require(store.ReleaseCalls.SequenceEqual(new[]
                {
                    "captive-care:captive:alpha",
                    "captive-labor-tool:captive:alpha"
                }, StringComparer.Ordinal)
            && store.ReplaceCalls == replacementsBefore + 1
            && store.Claims.Count == 0
            && store.Profiles.Count == 0,
            "Terminal retirement did not drain both destinations before paired revoke.");
    }

    private static void VerifyFailedCarriedReleasePreservesPair()
    {
        AuthorityStore store = StoreWithProjection();
        CaptivityCareLaborInputOwnerRuntime runtime = Runtime(store);
        int replacementsBefore = store.ReplaceCalls;
        store.FailRelease = true;

        Require(!runtime.TryReconcileLive(
                Array.Empty<CaptiveState>(),
                out string failureReason)
            && failureReason.StartsWith(
                "captivity-care-labor-terminal-release-failed:",
                StringComparison.Ordinal)
            && store.ReplaceCalls == replacementsBefore
            && store.Claims.Count == 2
            && store.Profiles.Count == 2,
            "Failed carried-aware release retired or tore owner authority.");
    }

    private static void VerifyCurrentFormatRestoreJoinOrdering()
    {
        RecordingOwner owner = new();
        CaptivityCareLaborInputOwnerRestoreParticipant participant = new(
            new EmptyCaptivityRuntime(),
            owner);
        participant.BeginRestoreCandidate();
        participant.PublishRestoreCandidate();
        participant.CompleteRestoreCandidate();
        Require(owner.RestoreCalls == 1
            && participant.ParticipantId.CompareTo(
                "220.world.facility-buffer-destinations") < 0,
            "Current-format care/labor projection was not joined before shared claim/profile publication.");

        participant.BeginRestoreCandidate();
        participant.DiscardRestoreCandidate();
    }

    private static void VerifyPerformerDelegatesSharedCareOwnership()
    {
        string root = Directory.GetCurrentDirectory();
        string performer = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Models/Captivity/Core/CaptivityPerformerRuntime.cs"));
        string runtime = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Services/Captivity/CaptivityRuntime.cs"));
        Require(!performer.Contains("TryRequestFacilityDelivery(",
                StringComparison.Ordinal)
            && !performer.Contains("captive-care:",
                StringComparison.Ordinal)
            && runtime.Contains(
                "CaptivityCareLaborInputOwnerAuthority",
                StringComparison.Ordinal)
            && runtime.Contains("FormatCareDestinationId(",
                StringComparison.Ordinal),
            "Performer still double-owns shared captive care delivery.");
    }

    private static CaptivityCareLaborInputOwnerDescriptor Descriptor(
        CaptivityCareLaborInputKind kind,
        long capacityGrams) => new(
        kind,
        "captive:alpha",
        "housing:alpha",
        new Vector2Int(7, 9),
        capacityGrams);

    private static AuthorityStore StoreWithProjection()
    {
        AuthorityStore store = new();
        CaptivityCareLaborInputOwnerProjection projection =
            CaptivityCareLaborInputOwnerAuthority.BuildProjection(new[]
            {
                Descriptor(CaptivityCareLaborInputKind.Care, 800L),
                Descriptor(CaptivityCareLaborInputKind.LaborTool, 2_350L)
            });
        Require(store.TryReplaceOwnedAuthorities(
                CaptivityCareLaborInputOwnerAuthority.OwnerDomain,
                projection.Claims,
                projection.Profiles,
                out string failureReason),
            failureReason);
        return store;
    }

    private static CaptivityCareLaborInputOwnerRuntime Runtime(
        AuthorityStore store) => new(
        new EmptyBuildingWorld(),
        EmptyResourceEconomyContentCatalog.Instance,
        new FixedMassQuery(),
        store,
        store,
        store,
        store);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class EmptyBuildingWorld : IBuildingWorldQuery
    {
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 1L;

        public PhysicalMassGrams GetDefinitionUnitMass(
            ItemDefinitionId itemId) => new(2_350L);

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            throw new NotSupportedException();

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => GetDefinitionUnitMass(itemId);

        public PhysicalMassGrams GetStackTotalMass(
            PhysicalItemLotSnapshot lot) =>
            throw new NotSupportedException();

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new(checked(2_350L * quantity));
    }

    private sealed class AuthorityStore :
        IFacilityBufferDestinationClaimAuthorityQuery,
        IFacilityBufferMassCapacityAuthorityQuery,
        IFacilityBufferDestinationLifecycleCommand,
        IFacilityBufferDestinationReleaseService
    {
        internal List<FacilityBufferDestinationClaim> Claims { get; } = new();
        internal List<FacilityBufferCapacityProfile> Profiles { get; } = new();
        internal List<string> ReleaseCalls { get; } = new();
        internal int ReplaceCalls { get; private set; }
        internal bool FailRelease { get; set; }

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
            if (!string.Equals(
                    ownerDomain,
                    CaptivityCareLaborInputOwnerAuthority.OwnerDomain,
                    StringComparison.Ordinal)
                || desiredClaims.Count != desiredProfiles.Count)
            {
                failureReason = "fixture-owner-pair-invalid";
                return false;
            }
            Claims.RemoveAll(value => value.OwnerDomain == ownerDomain);
            Profiles.RemoveAll(value => value.OwnerDomain == ownerDomain);
            Claims.AddRange(desiredClaims);
            Profiles.AddRange(desiredProfiles);
            ReplaceCalls++;
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
            ReleaseCalls.Add(destinationId);
            releasedQuantity = 0;
            failureReason = FailRelease
                ? "fixture-carried-release-blocked"
                : string.Empty;
            return !FailRelease;
        }
    }

    private sealed class RecordingOwner :
        ICaptivityCareLaborInputOwnerRuntime
    {
        internal int RestoreCalls { get; private set; }

        public bool TryReconcileLive(
            IReadOnlyList<CaptiveState> states,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryReconcileRestore(
            IReadOnlyList<CaptiveState> states,
            out string failureReason)
        {
            RestoreCalls++;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class EmptyCaptivityRuntime : ICaptivityRuntime
    {
        public IReadOnlyList<CaptiveState> Captives =>
            Array.Empty<CaptiveState>();
        public IReadOnlyList<CaptivePolicyData> Policies =>
            Array.Empty<CaptivePolicyData>();

        public bool TryGetCaptive(string captiveId, out CaptiveState captive)
        {
            captive = null;
            return false;
        }

        public bool TryGetActor(string captiveId, out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetHousing(string captiveId, out BuildableObject housing)
        {
            housing = null;
            return false;
        }

        public bool TryGetRehabilitationFacility(
            string captiveId,
            out BuildableObject facility)
        {
            facility = null;
            return false;
        }

        public bool IsCaptive(string persistentId) => false;

        public bool HasSecureHousing(
            CharacterActor captive,
            out BuildableObject housing,
            out string reason)
        {
            housing = null;
            reason = string.Empty;
            return false;
        }
    }
}
