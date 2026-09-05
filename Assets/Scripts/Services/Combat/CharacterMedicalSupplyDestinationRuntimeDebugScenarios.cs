#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CharacterMedicalSupplyDestinationRuntimeDebugScenarios
{
    private const string FacilityId = "building:qa:character-medical-supply";
    private const string LightMedicineId = "medicine:qa:light-treatment";
    private const string HeavyMedicineId = "medicine:qa:capability-canary";
    private const string UnsupportedMedicineId = "medicine:qa:unsupported";
    private const long AuthorityRevision = 17L;

    [MenuItem(
        "DungeonStory/Debug/V27/Run Character Medical Supply Destination Runtime Contracts")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log(
            "[V27][PASS] Character medical supply destination capability projection, exact authority pair, sequence identity, replay, revoke, restore replacement, and tamper rejection are exact.");
    }

    public static void RunAll()
    {
        using EditorVerificationSceneFixtureScope fixtureScene = new(
            "qa:character-medical-supply-destination-runtime");
        VerifyCapabilityDerivedMaximumIncludesMedicineAndExtractedBlood();
        VerifySequenceScopedIdentityDuplicateAndNoOpEnsure();
        VerifyRevokeClearsOnlyTheExactAuthorityPair();
        VerifyRestoreStyleReplacementIsDeterministic();
        VerifyProjectionAndFingerprintTamperFailClosed();
    }

    private static void
        VerifyCapabilityDerivedMaximumIncludesMedicineAndExtractedBlood()
    {
        using (Fixture bloodHeaviest = new(
                   lightMedicineMass: 450L,
                   heavyMedicineMass: 1_250L,
                   extractedBloodMass: 1_900L,
                   unsupportedMedicineMass: 8_000L))
        {
            CharacterMedicalOrder order = bloodHeaviest.CreateOrder(
                "medical:1",
                destinationSequence: 1);
            Require(
                bloodHeaviest.Runtime.TryEnsure(
                    order,
                    bloodHeaviest.Facility,
                    out string failureReason),
                "Extracted-blood maximum projection failed: " + failureReason);
            AssertCanonicalPair(
                bloodHeaviest,
                order,
                expectedCapacityGrams: 1_900L);
        }

        using (Fixture medicineHeaviest = new(
                   lightMedicineMass: 450L,
                   heavyMedicineMass: 2_300L,
                   extractedBloodMass: 1_900L,
                   unsupportedMedicineMass: 8_000L))
        {
            CharacterMedicalOrder order = medicineHeaviest.CreateOrder(
                "medical:1",
                destinationSequence: 1);
            Require(
                medicineHeaviest.Runtime.TryEnsure(
                    order,
                    medicineHeaviest.Facility,
                    out string failureReason),
                "Capability-derived medicine maximum projection failed: "
                + failureReason);
            AssertCanonicalPair(
                medicineHeaviest,
                order,
                expectedCapacityGrams: 2_300L);
        }
    }

    private static void VerifySequenceScopedIdentityDuplicateAndNoOpEnsure()
    {
        using Fixture fixture = new();
        CharacterMedicalOrder first = fixture.CreateOrder(
            "medical:1",
            destinationSequence: 1);
        CharacterMedicalOrder second = fixture.CreateOrder(
            "medical:2",
            destinationSequence: 7);

        Require(
            fixture.Runtime.TryEnsure(
                first,
                fixture.Facility,
                out string firstFailure),
            "First medical destination ensure failed: " + firstFailure);
        Require(
            fixture.Runtime.TryEnsure(
                second,
                fixture.Facility,
                out string secondFailure),
            "Second medical destination ensure failed: " + secondFailure);
        Require(
            first.treatmentMaterialDestinationId
                == CharacterMedicalSupplyDestinationAuthority
                    .FormatDestinationId("medical:1", 1)
            && second.treatmentMaterialDestinationId
                == CharacterMedicalSupplyDestinationAuthority
                    .FormatDestinationId("medical:2", 7)
            && first.treatmentMaterialDestinationId
                != second.treatmentMaterialDestinationId,
            "Medical destinations are not scoped by order and sequence.");

        FacilityBufferDestinationClaim firstClaim = fixture.RequireClaim(first);
        FacilityBufferDestinationClaim secondClaim = fixture.RequireClaim(second);
        Require(
            firstClaim.OwnerOperationId
                == CharacterMedicalSupplyDestinationAuthority
                    .FormatOwnerOperationId("medical:1", 1)
            && secondClaim.OwnerOperationId
                == CharacterMedicalSupplyDestinationAuthority
                    .FormatOwnerOperationId("medical:2", 7)
            && firstClaim.OwnerOperationId != secondClaim.OwnerOperationId,
            "Medical owner operation IDs are not sequence-scoped.");

        long claimRevision = fixture.Claims.Revision;
        long capacityRevision = fixture.Capacities.Revision;
        Require(
            fixture.Runtime.TryEnsure(
                first,
                fixture.Facility,
                out string replayFailure),
            "Canonical repeated ensure was rejected: " + replayFailure);
        Require(
            fixture.Claims.Revision == claimRevision
            && fixture.Capacities.Revision == capacityRevision
            && fixture.OwnedClaims.Count == 2
            && fixture.OwnedProfiles.Count == 2,
            "Canonical repeated ensure mutated the authority registries.");

        CharacterMedicalOrder duplicate = fixture.CreateOrder(
            "medical:1",
            destinationSequence: 1);
        Require(
            !fixture.Runtime.TryEnsure(
                duplicate,
                fixture.Facility,
                out string duplicateFailure)
            && duplicateFailure.Contains(
                "authority-destination-duplicate",
                StringComparison.Ordinal)
            && duplicate.treatmentBufferCapacityGrams == 0L
            && duplicate.treatmentMassAuthorityRevision == 0L
            && duplicate.treatmentCapacityFingerprint.Length == 0
            && fixture.OwnedClaims.Count == 2
            && fixture.OwnedProfiles.Count == 2,
            "Duplicate medical destination was not rejected atomically: "
            + duplicateFailure);
    }

    private static void VerifyRevokeClearsOnlyTheExactAuthorityPair()
    {
        using Fixture fixture = new();
        CharacterMedicalOrder first = fixture.CreateOrder("medical:1", 1);
        CharacterMedicalOrder second = fixture.CreateOrder("medical:2", 1);
        Ensure(fixture, first);
        Ensure(fixture, second);

        Require(
            fixture.Runtime.TryRevoke(first, out string failureReason),
            "Exact medical destination revoke failed: " + failureReason);
        Require(
            !fixture.HasClaim(first)
            && !fixture.HasProfile(first)
            && fixture.HasClaim(second)
            && fixture.HasProfile(second)
            && fixture.OwnedClaims.Count == 1
            && fixture.OwnedProfiles.Count == 1,
            "Medical destination revoke did not remove exactly one authority pair.");
        bool revokedRejected = !fixture.Runtime.TryValidate(first, out _);
        bool survivorAccepted = fixture.Runtime.TryValidate(
            second,
            out string validationFailure);
        Require(
            revokedRejected && survivorAccepted,
            "Medical destination validation disagreed with exact revoke: "
            + validationFailure);
    }

    private static void VerifyRestoreStyleReplacementIsDeterministic()
    {
        using Fixture fixture = new();
        CharacterMedicalOrder first = fixture.CreateOrder("medical:1", 3);
        CharacterMedicalOrder second = fixture.CreateOrder("medical:2", 9);
        Ensure(fixture, first);
        Ensure(fixture, second);

        Require(
            fixture.Lifecycle.TryReplaceOwnedAuthorities(
                CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
                Array.Empty<FacilityBufferDestinationClaim>(),
                Array.Empty<FacilityBufferCapacityProfile>(),
                out string clearFailure),
            "Restore fixture could not clear live medical authority: "
            + clearFailure);
        Require(
            fixture.OwnedClaims.Count == 0
            && fixture.OwnedProfiles.Count == 0,
            "Restore fixture retained medical authority after exact clear.");

        Dictionary<string, Vector2Int> positions = new(StringComparer.Ordinal)
        {
            [FacilityId] = fixture.Position
        };
        Require(
            fixture.Runtime.TryReplace(
                new[] { second, first },
                positions,
                out string replaceFailure),
            "Restore-style medical destination replacement failed: "
            + replaceFailure);
        string[] expectedOrder = new[]
            {
                first.treatmentMaterialDestinationId,
                second.treatmentMaterialDestinationId
            }
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Require(
            fixture.OwnedClaims.Select(value => value.DestinationId)
                .SequenceEqual(expectedOrder, StringComparer.Ordinal)
            && fixture.OwnedProfiles.Select(value => value.DestinationId)
                .SequenceEqual(expectedOrder, StringComparer.Ordinal)
            && fixture.Runtime.TryValidate(first, out _)
            && fixture.Runtime.TryValidate(second, out _),
            "Restore-style replacement was not deterministic or exactly joined.");

        long claimRevision = fixture.Claims.Revision;
        long capacityRevision = fixture.Capacities.Revision;
        Require(
            fixture.Runtime.TryReplace(
                new[] { first, second },
                positions,
                out string replayFailure),
            "Restore-style replay was rejected: " + replayFailure);
        Require(
            fixture.Claims.Revision == claimRevision
            && fixture.Capacities.Revision == capacityRevision,
            "Byte-equivalent restore-style replay mutated authority revisions.");
    }

    private static void VerifyProjectionAndFingerprintTamperFailClosed()
    {
        using Fixture fixture = new();
        CharacterMedicalOrder order = fixture.CreateOrder("medical:1", 4);
        Ensure(fixture, order);
        long canonicalCapacity = order.treatmentBufferCapacityGrams;
        long canonicalRevision = order.treatmentMassAuthorityRevision;
        string canonicalFingerprint = order.treatmentCapacityFingerprint;
        long claimRevision = fixture.Claims.Revision;
        long capacityRevision = fixture.Capacities.Revision;

        order.treatmentBufferCapacityGrams = checked(canonicalCapacity + 1L);
        Require(
            !fixture.Runtime.TryEnsure(
                order,
                fixture.Facility,
                out string projectionFailure)
            && projectionFailure.Contains(
                "stored-projection-invalid",
                StringComparison.Ordinal)
            && fixture.Claims.Revision == claimRevision
            && fixture.Capacities.Revision == capacityRevision,
            "Tampered medical capacity projection did not fail closed: "
            + projectionFailure);

        order.treatmentBufferCapacityGrams = canonicalCapacity;
        order.treatmentMassAuthorityRevision = canonicalRevision;
        order.treatmentCapacityFingerprint = new string('f', 64);
        Require(
            !fixture.Runtime.TryReplace(
                new[] { order },
                new Dictionary<string, Vector2Int>(StringComparer.Ordinal)
                {
                    [FacilityId] = fixture.Position
                },
                out string fingerprintFailure)
            && fingerprintFailure.Contains(
                "stored-projection-invalid",
                StringComparison.Ordinal)
            && fixture.Claims.Revision == claimRevision
            && fixture.Capacities.Revision == capacityRevision,
            "Tampered medical fingerprint did not fail restore atomically: "
            + fingerprintFailure);

        order.treatmentCapacityFingerprint = canonicalFingerprint;
        Require(
            fixture.Runtime.TryValidate(order, out string validationFailure),
            "Canonical authority pair did not survive tamper rejection: "
            + validationFailure);
    }

    private static void AssertCanonicalPair(
        Fixture fixture,
        CharacterMedicalOrder order,
        long expectedCapacityGrams)
    {
        FacilityBufferDestinationClaim claim = fixture.RequireClaim(order);
        FacilityBufferCapacityProfile profile = fixture.RequireProfile(order);
        Require(
            expectedCapacityGrams > 0L
            && order.treatmentBufferCapacityGrams == expectedCapacityGrams
            && profile.MaxMassGrams == expectedCapacityGrams,
            "Capability-derived medical capacity did not use the positive maximum gram value.");
        Require(
            order.treatmentMassAuthorityRevision == AuthorityRevision
            && order.treatmentCapacityFingerprint.Length == 64
            && claim.AdmissionPolicy
                == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
            && claim.AnchorKind
                == FacilityBufferDestinationAnchorKind.LiveFacility
            && claim.OwnerDomain
                == CharacterMedicalSupplyDestinationAuthority.OwnerDomain
            && profile.OwnerDomain
                == CharacterMedicalSupplyDestinationAuthority.OwnerDomain
            && claim.OwnerOperationId == profile.OwnerOperationId
            && claim.OwnerFacilityId == FacilityId
            && profile.OwnerFacilityId == FacilityId
            && claim.DropPosition == fixture.Position
            && profile.DropPosition == fixture.Position
            && profile.CapacityRevision
                == CharacterMedicalSupplyDestinationAuthority
                    .CapacitySchemaRevision,
            "Medical claim/profile pair did not preserve exact policy and ownership.");
    }

    private static void Ensure(Fixture fixture, CharacterMedicalOrder order)
    {
        Require(
            fixture.Runtime.TryEnsure(
                order,
                fixture.Facility,
                out string failureReason),
            $"Medical destination ensure failed for '{order.orderId}': "
            + failureReason);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ResourceItemDefinitionSO[] definitions;
        private readonly GameObject facilityObject;

        internal Fixture(
            long lightMedicineMass = 450L,
            long heavyMedicineMass = 1_250L,
            long extractedBloodMass = 1_900L,
            long unsupportedMedicineMass = 8_000L)
        {
            definitions = new[]
            {
                CreateMedicine(LightMedicineId, supportsTreatment: true),
                CreateMedicine(HeavyMedicineId, supportsTreatment: true),
                CreateMedicine(UnsupportedMedicineId, supportsTreatment: false)
            };
            facilityObject = null;
            try
            {
                FixedResourceCatalog content = new(definitions);
                FixedMassQuery mass = new(new Dictionary<string, long>(
                    StringComparer.Ordinal)
                {
                    [LightMedicineId] = lightMedicineMass,
                    [HeavyMedicineId] = heavyMedicineMass,
                    [UnsupportedMedicineId] = unsupportedMedicineMass,
                    [CharacterMedicalSupplyCoordinator.ExtractedBloodItemId] =
                        extractedBloodMass
                });
                Claims = new FacilityBufferDestinationClaimRegistry();
                Capacities = new FacilityBufferMassAdmissionService(
                    Claims,
                    EmptyOccupancy.Instance,
                    mass);
                Lifecycle = new FacilityBufferDestinationLifecycleService(
                    Claims,
                    Claims,
                    Capacities,
                    Capacities);
                Runtime = new CharacterMedicalSupplyDestinationRuntime(
                    content,
                    mass,
                    Claims,
                    Capacities,
                    Lifecycle);

                facilityObject = new GameObject(
                    "QA Character Medical Supply Destination Facility");
                facilityObject.SetActive(false);
                Facility = facilityObject.AddComponent<BuildableObject>();
                typeof(BuildableObject).GetField(
                        "facilityCandidateCache",
                        System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(Facility, NoopFacilityStateChange.Instance);
                Facility.RestorePersistentIdentity((BuildingInstanceId)FacilityId);
                Facility.SetRuntimeGridPosition(Position);
            }
            catch
            {
                if (facilityObject != null)
                    UnityEngine.Object.DestroyImmediate(facilityObject);
                foreach (ResourceItemDefinitionSO definition in definitions)
                {
                    if (definition != null)
                        UnityEngine.Object.DestroyImmediate(definition);
                }
                throw;
            }
        }

        internal Vector2Int Position { get; } = new(8, 5);
        internal BuildableObject Facility { get; }
        internal CharacterMedicalSupplyDestinationRuntime Runtime { get; }
        internal FacilityBufferDestinationClaimRegistry Claims { get; }
        internal FacilityBufferMassAdmissionService Capacities { get; }
        internal FacilityBufferDestinationLifecycleService Lifecycle { get; }

        internal IReadOnlyList<FacilityBufferDestinationClaim> OwnedClaims =>
            Claims.CaptureAuthorityClaims()
                .Where(value => value != null
                    && value.OwnerDomain
                        == CharacterMedicalSupplyDestinationAuthority.OwnerDomain)
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray();

        internal IReadOnlyList<FacilityBufferCapacityProfile> OwnedProfiles =>
            Capacities.CaptureAuthorityProfiles()
                .Where(value => value != null
                    && value.OwnerDomain
                        == CharacterMedicalSupplyDestinationAuthority.OwnerDomain)
                .OrderBy(value => value.DestinationId, StringComparer.Ordinal)
                .ToArray();

        internal CharacterMedicalOrder CreateOrder(
            string orderId,
            int destinationSequence)
        {
            return new CharacterMedicalOrder
            {
                orderId = orderId,
                patientId = "character:qa:patient",
                treatmentFacilityId = FacilityId,
                state = CharacterMedicalOrderState.Treating,
                treatmentMaterialDestinationId =
                    CharacterMedicalSupplyDestinationAuthority
                        .FormatDestinationId(orderId, destinationSequence),
                treatmentDestinationSequence = destinationSequence,
                nextTreatmentMaterialDestinationSequence = checked(
                    destinationSequence + 1)
            };
        }

        internal FacilityBufferDestinationClaim RequireClaim(
            CharacterMedicalOrder order) => OwnedClaims.Single(value =>
            value.DestinationId == order.treatmentMaterialDestinationId);

        internal FacilityBufferCapacityProfile RequireProfile(
            CharacterMedicalOrder order) => OwnedProfiles.Single(value =>
            value.DestinationId == order.treatmentMaterialDestinationId);

        internal bool HasClaim(CharacterMedicalOrder order) => OwnedClaims.Any(
            value => value.DestinationId
                == order.treatmentMaterialDestinationId);

        internal bool HasProfile(CharacterMedicalOrder order) =>
            OwnedProfiles.Any(value => value.DestinationId
                == order.treatmentMaterialDestinationId);

        public void Dispose()
        {
            if (facilityObject != null)
            {
                UnityEngine.Object.DestroyImmediate(facilityObject);
            }
            foreach (ResourceItemDefinitionSO definition in definitions)
            {
                if (definition != null)
                {
                    UnityEngine.Object.DestroyImmediate(definition);
                }
            }
        }
    }

    private sealed class NoopFacilityStateChange :
        IBuildingFacilityStateChangePort
    {
        internal static readonly NoopFacilityStateChange Instance = new();

        public void MarkDynamicStateDirty()
        {
        }
    }

    private static ResourceItemDefinitionSO CreateMedicine(
        string itemId,
        bool supportsTreatment)
    {
        ResourceItemDefinitionSO medicine =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        medicine.hideFlags = HideFlags.HideAndDontSave;
        medicine.Configure(
            itemId,
            itemId,
            "Character medical supply destination QA fixture.",
            StockCategory.Medicine,
            ResourceItemKind.Medicine,
            ResourceIngredientTag.None,
            1,
            0.1f,
            1,
            string.Empty);
        medicine.ConfigureMedicine(
            supportsTreatment,
            1f,
            0f,
            0f,
            0f);
        return medicine;
    }

    private sealed class FixedResourceCatalog :
        IResourceEconomyContentCatalog
    {
        private readonly IReadOnlyList<ResourceItemDefinitionSO> items;

        internal FixedResourceCatalog(
            IReadOnlyList<ResourceItemDefinitionSO> items)
        {
            this.items = items
                ?? throw new ArgumentNullException(nameof(items));
        }

        public IReadOnlyList<ResourceItemDefinitionSO> Items => items;
        public IReadOnlyList<ProductionRecipeSO> Recipes =>
            Array.Empty<ProductionRecipeSO>();
        public IReadOnlyList<CropDefinitionSO> Crops =>
            Array.Empty<CropDefinitionSO>();
        public IReadOnlyList<CraftMaterialDefinitionSO> Materials =>
            Array.Empty<CraftMaterialDefinitionSO>();
        public IReadOnlyList<SubstanceDefinitionView> Substances =>
            Array.Empty<SubstanceDefinitionView>();

        public bool TryGetItem(
            string itemId,
            out ResourceItemDefinitionSO definition)
        {
            definition = items.SingleOrDefault(value => value != null
                && string.Equals(
                    value.ItemId,
                    itemId,
                    StringComparison.Ordinal));
            return definition != null;
        }

        public bool TryGetRecipe(
            string recipeId,
            out ProductionRecipeSO definition)
        {
            definition = null;
            return false;
        }

        public bool TryGetCrop(
            string cropId,
            out CropDefinitionSO definition)
        {
            definition = null;
            return false;
        }

        public bool TryGetMaterial(
            string materialId,
            out CraftMaterialDefinitionSO definition)
        {
            definition = null;
            return false;
        }

        public bool TryGetSubstance(
            string substanceId,
            out SubstanceDefinitionView definition)
        {
            definition = default;
            return false;
        }
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly IReadOnlyDictionary<string, long> unitMassByItem;

        internal FixedMassQuery(
            IReadOnlyDictionary<string, long> unitMassByItem)
        {
            this.unitMassByItem = unitMassByItem
                ?? throw new ArgumentNullException(nameof(unitMassByItem));
        }

        public long AuthorityRevision =>
            CharacterMedicalSupplyDestinationRuntimeDebugScenarios
                .AuthorityRevision;

        public PhysicalMassGrams GetDefinitionUnitMass(
            ItemDefinitionId itemId) => new(RequireUnitMass(itemId));

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(subject.ItemId);

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(itemId);

        public PhysicalMassGrams GetStackTotalMass(
            PhysicalItemLotSnapshot lot) =>
            GetQuantityMass(
                lot.Subject.ItemId,
                lot.Subject,
                lot.Quantity);

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new(checked(RequireUnitMass(itemId) * quantity));

        private long RequireUnitMass(ItemDefinitionId itemId) =>
            itemId.IsValid
            && unitMassByItem.TryGetValue(itemId.Value, out long grams)
            && grams > 0L
                ? grams
                : throw new InvalidOperationException(
                    "Unknown or non-positive QA item mass: " + itemId.Value);
    }

    private sealed class EmptyOccupancy :
        IFacilityBufferPhysicalOccupancyQuery
    {
        internal static readonly EmptyOccupancy Instance = new();

        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(0L, 0L);

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "qa-no-physical-lot";
            return false;
        }
    }
}
#endif
