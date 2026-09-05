using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CharacterConsumablesInputOwnerDebugScenarios
{
    private const string MealItemId = "food:qa-meal";
    private const string RecreationalItemId = "drink:qa-recreation";
    private const string FacilityId = "building:qa:consumables";

    [MenuItem("Tools/Dungeon Story/QA/Character Consumables Input Owner")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("CHARACTER_CONSUMABLES_INPUT_OWNER_PASS");
    }

    public static void RunAll()
    {
        VerifyCanonicalExactPositiveGramProjection();
        VerifyFailedCarriedReleaseRetainsPairedAuthority();
        VerifyCapabilityRemovalReleasesBeforePairedRevoke();
        VerifyCurrentFormatRestoreReplacementIsDeterministic();
        VerifyTypedPhysicalConsumePathRemainsAuthoritative();
    }

    private static void VerifyCanonicalExactPositiveGramProjection()
    {
        CharacterConsumablesInputOwnerDescriptor meal = Descriptor(
            CharacterConsumablesInputKind.Meal,
            MealItemId,
            new Vector2Int(4, 7));
        CharacterConsumablesInputOwnerDescriptor recreation = Descriptor(
            CharacterConsumablesInputKind.RecreationalSubstance,
            RecreationalItemId,
            new Vector2Int(4, 7));
        CharacterConsumablesInputOwnerProjection projection =
            CharacterConsumablesInputOwnerAuthority.BuildProjection(
                new[] { recreation, meal },
                MassQuery());
        FacilityBufferDestinationClaim[] claims = projection.Claims.ToArray();
        FacilityBufferCapacityProfile[] profiles = projection.Profiles.ToArray();
        Require(claims.Length == 2
            && profiles.Length == 2
            && claims.All(value => value.AnchorKind ==
                FacilityBufferDestinationAnchorKind.LiveFacility)
            && claims.All(value => value.AdmissionPolicy ==
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired)
            && claims.All(value => value.OwnerFacilityId == FacilityId)
            && profiles.Single(value => value.DestinationId == meal.DestinationId)
                .MaxMassGrams == 325L
            && profiles.Single(value => value.DestinationId == recreation.DestinationId)
                .MaxMassGrams == 575L
            && meal.DestinationId ==
                "facility-input:exact:survival.character-consumables:v1:meal:"
                + "building%3Aqa%3Aconsumables:food%3Aqa-meal"
            && recreation.DestinationId.Contains(
                ":recreation-substance:",
                StringComparison.Ordinal),
            "Character-consumables exact owner projection drifted.");
    }

    private static void VerifyFailedCarriedReleaseRetainsPairedAuthority()
    {
        AuthorityStore store = new();
        RecordingRelease release = new();
        CharacterConsumablesInputOwnerRuntime runtime = Runtime(store, release);
        CharacterConsumablesInputOwnerDescriptor descriptor = Descriptor(
            CharacterConsumablesInputKind.Meal,
            MealItemId,
            Vector2Int.one);
        Require(runtime.TryReconcileLive(
                new[] { descriptor },
                CharacterConsumablesInputOwnerAuthority
                    .CapabilityRemovedReleaseReasonCode,
                out string firstFailure),
            firstFailure);
        int publishes = store.ReplaceCalls;
        release.Fail = true;
        Require(!runtime.TryReconcileLive(
                Array.Empty<CharacterConsumablesInputOwnerDescriptor>(),
                CharacterConsumablesInputOwnerAuthority
                    .FacilityLostReleaseReasonCode,
                out string failureReason)
            && failureReason.StartsWith(
                "character-consumables-input-owner-terminal-release-failed:",
                StringComparison.Ordinal)
            && store.ReplaceCalls == publishes
            && store.Claims.Count == 1
            && store.Profiles.Count == 1,
            "Failed carried release retired character-consumables authority.");
    }

    private static void VerifyCapabilityRemovalReleasesBeforePairedRevoke()
    {
        AuthorityStore store = new();
        RecordingRelease release = new();
        CharacterConsumablesInputOwnerRuntime runtime = Runtime(store, release);
        CharacterConsumablesInputOwnerDescriptor descriptor = Descriptor(
            CharacterConsumablesInputKind.RecreationalSubstance,
            RecreationalItemId,
            new Vector2Int(9, 2));
        Require(runtime.TryReconcileLive(
                new[] { descriptor },
                CharacterConsumablesInputOwnerAuthority
                    .CapabilityRemovedReleaseReasonCode,
                out string firstFailure),
            firstFailure);
        Require(runtime.TryReconcileLive(
                Array.Empty<CharacterConsumablesInputOwnerDescriptor>(),
                CharacterConsumablesInputOwnerAuthority
                    .CapabilityRemovedReleaseReasonCode,
                out string secondFailure),
            secondFailure);
        Require(release.Destinations.SequenceEqual(
                new[] { descriptor.DestinationId },
                StringComparer.Ordinal)
            && store.Claims.Count == 0
            && store.Profiles.Count == 0,
            "Capability removal did not release custody before paired revoke.");
    }

    private static void VerifyCurrentFormatRestoreReplacementIsDeterministic()
    {
        AuthorityStore store = new();
        CharacterConsumablesInputOwnerRuntime runtime = Runtime(
            store,
            new RecordingRelease());
        CharacterConsumablesInputOwnerDescriptor first = Descriptor(
            CharacterConsumablesInputKind.Meal,
            MealItemId,
            new Vector2Int(1, 2));
        CharacterConsumablesInputOwnerDescriptor second = Descriptor(
            CharacterConsumablesInputKind.RecreationalSubstance,
            RecreationalItemId,
            new Vector2Int(1, 2));
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
            && store.Claims.Count == store.Profiles.Count
            && store.Profiles.All(value => value.MaxMassGrams > 0L),
            "Current-format owner restore depended on descriptor input order.");
    }

    private static void VerifyTypedPhysicalConsumePathRemainsAuthoritative()
    {
        string root = Directory.GetParent(Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        string adapters = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Services/Survival/CharacterConsumablesApplicationAdapters.cs"));
        string runtime = File.ReadAllText(Path.Combine(
            root,
            "Assets/Scripts/Models/Survival/Core/CharacterConsumablesRuntime.cs"));
        Require(adapters.Contains(
                "TryCommitReservedSinkPending(",
                StringComparison.Ordinal)
            && adapters.Contains(
                "TryCommitCarriedSinkPending(",
                StringComparison.Ordinal)
            && adapters.Contains(
                "TryPublishTareAndAcknowledge(",
                StringComparison.Ordinal)
            && runtime.Contains(
                "TryCommitReservedMealQuantityPending(",
                StringComparison.Ordinal)
            && runtime.Contains(
                "TryCommitSubstanceConsumptionPending(",
                StringComparison.Ordinal),
            "Typed meal/substance physical Sink authority was bypassed.");
    }

    private static CharacterConsumablesInputOwnerDescriptor Descriptor(
        CharacterConsumablesInputKind kind,
        string itemId,
        Vector2Int position) => new(
        kind,
        FacilityId,
        position,
        itemId);

    private static CharacterConsumablesInputOwnerRuntime Runtime(
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
            [MealItemId] = 325L,
            [RecreationalItemId] = 575L
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
            Require(ownerDomain ==
                CharacterConsumablesInputOwnerAuthority.OwnerDomain,
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
        internal List<string> Destinations { get; } = new();
        internal bool Fail { get; set; }

        public bool TryReleaseAtOwnerPosition(
            string destinationId,
            Vector2Int ownerPosition,
            string reasonCode,
            out int releasedQuantity,
            out string failureReason)
        {
            Destinations.Add(destinationId);
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
