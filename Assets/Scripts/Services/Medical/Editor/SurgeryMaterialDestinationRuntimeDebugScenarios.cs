#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class SurgeryMaterialDestinationRuntimeDebugScenarios
{
    private const long AuthorityRevision = 27L;
    private const long ExpectedCapacityGrams = 8_800L;

    [MenuItem(
        "DungeonStory/Debug/Medical/Verify Surgery Material Destination Runtime")]
    public static void RunFromMenu() => RunAll(log: true);

    public static bool RunAll(bool log = true)
    {
        using EditorVerificationSceneFixtureScope fixtureScene = new(
            "qa:surgery-material-destination-runtime");
        List<string> errors = new();
        Run(
            "generic and exact mass with atomic claim",
            VerifyCapacityAndAtomicClaim,
            errors);
        Run(
            "current-format restore, invalid projection, and revoke",
            VerifyRestoreValidationAndRevoke,
            errors);
        Run(
            "exact-gram policy rejects missing profile without count fallback",
            VerifyExactGramPolicyRejectsMissingProfile,
            errors);

        if (errors.Count > 0)
        {
            if (log)
            {
                Debug.LogError(
                    "Surgery material destination scenarios failed:\n"
                    + string.Join("\n", errors));
            }
            return false;
        }

        if (log)
        {
            Debug.Log(
                "Surgery material destination scenarios passed: exact generic, "
                + "corpse, and selected-part grams; claim/profile atomicity; "
                + "current-format candidate restore; strict stored projection; "
                + "paired revoke.");
        }
        return true;
    }

    private static void VerifyCapacityAndAtomicClaim()
    {
        using Fixture fixture = Fixture.Create();
        SurgeryOrder rejected = fixture.CreateOrder("surgery:101");
        SurgeryMaterialDestinationRuntime rejectingRuntime = fixture.CreateRuntime(
            new RejectingLifecycle("qa-injected-lifecycle-rejection"));

        Require(
            !rejectingRuntime.TryClaim(
                rejected,
                fixture.Facility,
                out string rejectReason)
            && rejectReason.Contains(
                "qa-injected-lifecycle-rejection",
                StringComparison.Ordinal),
            "Injected lifecycle rejection was not reported.");
        Require(
            rejected.materialBufferCapacityGrams == 0L
            && rejected.materialMassAuthorityRevision == 0L
            && string.IsNullOrEmpty(rejected.materialCapacityFingerprint),
            "Failed claim retained projected mass authority on the order.");
        RequirePairCount(fixture, expected: 0);

        SurgeryOrder order = fixture.CreateOrder("surgery:102");
        Require(
            fixture.Runtime.TryClaim(
                order,
                fixture.Facility,
                out string failureReason),
            "Exact surgery destination claim failed: " + failureReason);
        Require(
            order.materialBufferCapacityGrams == ExpectedCapacityGrams,
            $"Expected {ExpectedCapacityGrams}g, got "
            + $"{order.materialBufferCapacityGrams}g.");
        Require(
            order.materialMassAuthorityRevision == AuthorityRevision,
            "Surgery order did not capture the mass authority revision.");
        Require(
            IsLowercaseSha256(order.materialCapacityFingerprint),
            "Surgery order did not capture a canonical capacity fingerprint.");
        RequirePair(fixture, order, fixture.Position, ExpectedCapacityGrams);
        Require(
            fixture.Runtime.TryValidate(order, out failureReason),
            "Published claim/profile pair did not validate: " + failureReason);
    }

    private static void VerifyRestoreValidationAndRevoke()
    {
        using Fixture fixture = Fixture.Create();
        SurgeryOrder liveOrder = fixture.CreateOrder("surgery:201");
        Require(
            fixture.Runtime.TryClaim(
                liveOrder,
                fixture.Facility,
                out string failureReason),
            "Restore fixture could not publish its live pair: " + failureReason);
        liveOrder.materials = new List<SurgicalMaterialRequirement>
        {
            new() { itemId = "medical:bandage", quantity = 5 },
            new() { itemId = "medical:anesthetic", quantity = 2 },
            new()
            {
                itemId = "medical:optional-tonic",
                quantity = 99,
                optional = true
            }
        };

        DungeonSurgerySaveData current = new()
        {
            version = DungeonSurgerySaveData.CurrentVersion,
            orderSequence = 201,
            partSequence = 1,
            orders = new List<SurgeryOrder> { liveOrder },
            parts = new List<SurgicalPartInstance> { fixture.SelectedPart }
        };
        DungeonSurgerySaveData restored = JsonUtility.FromJson<DungeonSurgerySaveData>(
            JsonUtility.ToJson(current));
        Require(
            restored != null
            && restored.version == DungeonSurgerySaveData.CurrentVersion
            && restored.orders?.Count == 1,
            "Current-format surgery payload did not round-trip.");
        Require(
            ValidateCurrentFormat(restored, out string validationDetail),
            "Current-format surgery payload failed strict validation: "
            + validationDetail);

        SurgeryOrder restoredOrder = restored.orders[0];
        Vector2Int restoredPosition = new(17, 9);
        Dictionary<string, Vector2Int> restoredFacilities = new(
            StringComparer.Ordinal)
        {
            [restoredOrder.facilityId] = restoredPosition
        };

        fixture.Claims.BeginRestoreCandidate();
        fixture.Admission.BeginRestoreCandidate();
        try
        {
            Require(
                fixture.Runtime.TryReplace(
                    restored.orders,
                    restoredFacilities,
                    out failureReason),
                "Current-format claim/profile restore staging failed: "
                + failureReason);
            Require(
                fixture.Claims.TryGetClaim(
                    restoredOrder.materialDestinationId,
                    fixture.Position,
                    out _)
                && !fixture.Claims.TryGetClaim(
                    restoredOrder.materialDestinationId,
                    restoredPosition,
                    out _),
                "Restore staging leaked a candidate claim into the live query.");
            Require(
                fixture.Claims.TryGetAuthorityClaim(
                    restoredOrder.materialDestinationId,
                    restoredPosition,
                    out _)
                && fixture.Admission.CaptureAuthorityProfiles().Single()
                    .DropPosition == restoredPosition,
                "Restore authority view did not expose the staged pair.");

            fixture.Claims.PublishRestoreCandidate();
            fixture.Admission.PublishRestoreCandidate();
            fixture.Admission.CompleteRestoreCandidate();
            fixture.Claims.CompleteRestoreCandidate();
        }
        catch
        {
            fixture.Admission.DiscardRestoreCandidate();
            fixture.Claims.DiscardRestoreCandidate();
            throw;
        }

        RequirePair(
            fixture,
            restoredOrder,
            restoredPosition,
            ExpectedCapacityGrams);
        VerifyInvalidProjectionRejected(
            fixture,
            restoredOrder,
            restoredFacilities,
            order => order.materialBufferCapacityGrams = 0L,
            "non-positive capacity");
        VerifyInvalidProjectionRejected(
            fixture,
            restoredOrder,
            restoredFacilities,
            order => order.materialMassAuthorityRevision = AuthorityRevision + 1L,
            "stale mass revision");
        VerifyInvalidProjectionRejected(
            fixture,
            restoredOrder,
            restoredFacilities,
            order => order.materialCapacityFingerprint = new string('b', 64),
            "mismatched fingerprint");

        Require(
            fixture.Runtime.TryRevoke(restoredOrder, out failureReason),
            "Paired surgery authority revoke failed: " + failureReason);
        RequirePairCount(fixture, expected: 0);
        Require(
            !fixture.Runtime.TryValidate(restoredOrder, out failureReason)
            && failureReason.Contains(
                "pair-cardinality:0:0",
                StringComparison.Ordinal),
            "Revoked destination still validated as a live pair.");
    }

    private static void VerifyExactGramPolicyRejectsMissingProfile()
    {
        const string orderId = "surgery:301";
        const string facilityId = "building:qa:surgery-missing-profile";
        string destinationId = "surgery-materials:" + orderId;
        Vector2Int sourcePosition = new(2, 3);
        Vector2Int destinationPosition = new(8, 6);
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        string stackId = repository.AddEditorTestStack(
            "medical:bandage",
            quantity: 1,
            stackState: WorldItemStackState.Loose,
            position: sourcePosition);
        FacilityBufferDestinationClaimRegistry claims = new();
        Require(
            claims.TryClaim(
                new FacilityBufferDestinationClaim(
                    destinationId,
                    destinationPosition,
                    SurgeryMaterialDestinationAuthority.OwnerDomain,
                    orderId,
                    facilityId,
                    FacilityBufferDestinationAnchorKind.LiveFacility,
                    FacilityBufferDestinationAdmissionPolicy.ExactGramRequired),
                out _,
                out string claimFailureReason),
            "Missing-profile fixture could not publish its exact claim: "
            + claimFailureReason);
        FixedMassQuery mass = new(new Dictionary<string, long>(
            StringComparer.Ordinal)
        {
            ["medical:bandage"] = 100L
        });
        FacilityBufferMassAdmissionService admission = new(
            claims,
            new EmptyOccupancy(),
            mass);
        WorldItemWarehouseService deliveries = new(
            CreateProxy<IDungeonItemCatalogProvider>(null),
            repository,
            CreateProxy<ICharacterAiWorldRegistry>(null),
            CreateProxy<IWorldItemSpawner>(null),
            CreateProxy<IItemMarkerPresenter>(null),
            CreateProxy<IGridSystemProvider>(null),
            CreateProxy<ICharacterIdRegistry>(null),
            CreateProxy<IItemReservationService>(null),
            facilityBufferMassAdmission: admission,
            facilityBufferDestinationClaims: claims);

        Require(
            !deliveries.TryRequestStackDelivery(
                stackId,
                amount: 1,
                destinationPosition: destinationPosition,
                destinationId: destinationId,
                requested: out int requested,
                failureReason: out string failureReason)
            && requested == 0
            && string.Equals(
                failureReason,
                "items.delivery.facility_buffer_mass_profile_missing",
                StringComparison.Ordinal),
            "Exact-gram surgery delivery did not fail on its missing profile: "
            + failureReason);
        Require(
            repository.GetEditorTestQuantity(stackId) == 1
            && deliveries.TryRequestStackDelivery(
                stackId,
                amount: 1,
                destinationPosition: new Vector2Int(9, 6),
                destinationId: "qa:count-compatible-destination",
                requested: out requested,
                failureReason: out failureReason)
            && requested == 1,
            "Rejected exact delivery mutated its source or silently consumed it: "
            + failureReason);
    }

    private static void VerifyInvalidProjectionRejected(
        Fixture fixture,
        SurgeryOrder valid,
        IReadOnlyDictionary<string, Vector2Int> positions,
        Action<SurgeryOrder> corrupt,
        string caseName)
    {
        SurgeryOrder invalid = JsonUtility.FromJson<SurgeryOrder>(
            JsonUtility.ToJson(valid));
        corrupt(invalid);
        DungeonSurgerySaveData invalidSave = new()
        {
            version = DungeonSurgerySaveData.CurrentVersion,
            orderSequence = int.MaxValue,
            partSequence = int.MaxValue,
            orders = new List<SurgeryOrder> { invalid },
            parts = new List<SurgicalPartInstance>
            {
                JsonUtility.FromJson<SurgicalPartInstance>(
                    JsonUtility.ToJson(fixture.SelectedPart))
            }
        };
        Require(
            !ValidateCurrentFormat(invalidSave, out string validationDetail),
            $"Strict current-format validation accepted {caseName}: "
            + validationDetail);
        Require(
            !fixture.Runtime.TryReplace(
                new[] { invalid },
                positions,
                out string failureReason)
            && failureReason.Contains(
                "stored-projection-invalid",
                StringComparison.Ordinal),
            $"Surgery restore accepted {caseName}: {failureReason}");
        RequirePair(
            fixture,
            valid,
            positions[valid.facilityId],
            ExpectedCapacityGrams);
    }

    private static bool ValidateCurrentFormat(
        DungeonSurgerySaveData save,
        out string detail)
    {
        ISurgicalProcedureCatalog procedures =
            CreateProxy<ISurgicalProcedureCatalog>((method, arguments) =>
            {
                if (method.Name == "TryGet")
                {
                    arguments[1] = null;
                    return true;
                }
                if (method.Name == "get_Procedures")
                    return Array.Empty<SurgicalProcedureSO>();
                if (method.Name == "Validate")
                    return Array.Empty<string>();
                return DefaultValue(method.ReturnType);
            });
        IAnatomyProfileCatalog anatomy =
            CreateProxy<IAnatomyProfileCatalog>((method, arguments) =>
            {
                if (method.Name == "get_Profiles")
                    return Array.Empty<AnatomyProfileDefinition>();
                if (method.Name == "Validate")
                    return Array.Empty<string>();
                return DefaultValue(method.ReturnType);
            });
        DungeonGameRestoreReport report = new();
        SurgerySaveValidation.Validate(save, procedures, anatomy, report);
        detail = string.Join(" | ", report.Errors);
        return report.Success;
    }

    private static void RequirePair(
        Fixture fixture,
        SurgeryOrder order,
        Vector2Int position,
        long expectedCapacityGrams)
    {
        bool hasClaim = fixture.Claims.TryGetClaim(
            order.materialDestinationId,
            position,
            out FacilityBufferDestinationClaim claim);
        bool hasCapacity = fixture.Admission.TryGetCapacity(
            order.materialDestinationId,
            position,
            out FacilityBufferMassCapacitySnapshot capacity);
        Require(
            hasClaim && hasCapacity,
            "Expected claim/profile pair was not live.");
        Require(
            string.Equals(
                claim.OwnerDomain,
                SurgeryMaterialDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                claim.OwnerOperationId,
                order.orderId,
                StringComparison.Ordinal)
            && string.Equals(
                claim.OwnerFacilityId,
                order.facilityId,
                StringComparison.Ordinal)
            && claim.AdmissionPolicy
                == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
            && capacity.Profile.MaxMassGrams == expectedCapacityGrams
            && capacity.Profile.CapacityRevision
                == SurgeryMaterialDestinationAuthority
                    .InputBufferCapacitySchemaRevision,
            "Live surgery authority pair did not match its exact owner/capacity.");
        RequirePairCount(fixture, expected: 1);
    }

    private static void RequirePairCount(Fixture fixture, int expected)
    {
        int claimCount = fixture.Claims.CaptureClaims().Count(value =>
            string.Equals(
                value.OwnerDomain,
                SurgeryMaterialDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal));
        int profileCount = fixture.Admission.CaptureProfiles().Count(value =>
            string.Equals(
                value.OwnerDomain,
                SurgeryMaterialDestinationAuthority.OwnerDomain,
                StringComparison.Ordinal));
        Require(
            claimCount == expected && profileCount == expected,
            $"Expected {expected} surgery authority pairs, got "
            + $"{claimCount}/{profileCount}.");
    }

    private static bool IsLowercaseSha256(string value) =>
        value?.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');

    private static void Run(
        string name,
        Action test,
        ICollection<string> errors)
    {
        try
        {
            test();
        }
        catch (Exception exception)
        {
            errors.Add(name + ": " + exception.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IWorldItemStackRuntime items;
        private readonly ISurgicalPartRuntime parts;

        private Fixture()
        {
            Mass = new FixedMassQuery(new Dictionary<string, long>(
                StringComparer.Ordinal)
            {
                ["medical:bandage"] = 100L,
                ["medical:anesthetic"] = 250L,
                ["medical:optional-tonic"] = 900L,
                ["corpse:humanoid"] = 7_000L,
                ["medical:test-implant"] = 800L
            });
            SelectedPart = new SurgicalPartInstance
            {
                partInstanceId = "surgical-part:1",
                kind = SurgicalPartKind.Prosthetic,
                nodeId = "heart",
                displayName = "QA implant",
                worldStackId = "stack:qa:selected-part"
            };
            WorldItemStackSnapshot[] stacks =
            {
                new()
                {
                    StackId = "stack:qa:corpse",
                    ItemId = "corpse:humanoid",
                    Quantity = 1,
                    Components = Array.Empty<ItemInstanceComponentSaveData>()
                },
                new()
                {
                    StackId = SelectedPart.worldStackId,
                    ItemId = "medical:test-implant",
                    Quantity = 1,
                    Components = Array.Empty<ItemInstanceComponentSaveData>()
                }
            };
            items = CreateProxy<IWorldItemStackRuntime>((method, arguments) =>
                method.Name switch
                {
                    "get_MassQuery" => Mass,
                    "GetAllStacks" => stacks,
                    _ => DefaultValue(method.ReturnType)
                });
            parts = CreateProxy<ISurgicalPartRuntime>((method, arguments) =>
            {
                if (method.Name == "get_Parts")
                    return new[] { SelectedPart };
                if (method.Name == "TryGet")
                {
                    bool found = string.Equals(
                        arguments[0] as string,
                        SelectedPart.partInstanceId,
                        StringComparison.Ordinal);
                    arguments[1] = found ? SelectedPart : null;
                    return found;
                }
                return DefaultValue(method.ReturnType);
            });

            Admission = new FacilityBufferMassAdmissionService(
                Claims,
                new EmptyOccupancy(),
                Mass);
            Lifecycle = new FacilityBufferDestinationLifecycleService(
                Claims,
                Claims,
                Admission,
                Admission);
            Runtime = CreateRuntime(Lifecycle);
            GameObject facilityObject = null;
            try
            {
                facilityObject = new GameObject(
                    "QA Surgery Material Destination Facility");
                facilityObject.SetActive(false);
                FacilityObject = facilityObject;
                Facility = FacilityObject.AddComponent<BuildableObject>();
                CharacterAiEditorTestDependencies.Inject(Facility);
                Facility.SetRuntimeGridPosition(Position);
            }
            catch
            {
                if (facilityObject != null)
                    UnityEngine.Object.DestroyImmediate(facilityObject);
                throw;
            }
        }

        internal static Fixture Create() => new();

        internal FixedMassQuery Mass { get; }
        internal FacilityBufferDestinationClaimRegistry Claims { get; } = new();
        internal FacilityBufferMassAdmissionService Admission { get; }
        internal FacilityBufferDestinationLifecycleService Lifecycle { get; }
        internal SurgeryMaterialDestinationRuntime Runtime { get; }
        internal GameObject FacilityObject { get; }
        internal BuildableObject Facility { get; }
        internal SurgicalPartInstance SelectedPart { get; }
        internal Vector2Int Position { get; } = new(6, 4);

        internal SurgeryMaterialDestinationRuntime CreateRuntime(
            IFacilityBufferDestinationLifecycleCommand lifecycle) => new(
            parts,
            items,
            Mass,
            Claims,
            Admission,
            lifecycle);

        internal SurgeryOrder CreateOrder(string orderId) => new()
        {
            orderId = orderId,
            procedureId = "procedure:qa-mass-contract",
            subject = new SurgicalSubjectRef
            {
                kind = SurgicalSubjectKind.HumanoidCorpse,
                subjectId = "stack:qa:corpse",
                displayName = "QA corpse"
            },
            selectedPartInstanceId = SelectedPart.partInstanceId,
            facilityId = "building:qa:surgery",
            materialDestinationId = "surgery-materials:" + orderId,
            state = SurgeryOrderState.MaterialsWaiting,
            requiredWork = 1f,
            materials = new List<SurgicalMaterialRequirement>
            {
                new() { itemId = "medical:bandage", quantity = 2 },
                new() { itemId = "medical:bandage", quantity = 3 },
                new() { itemId = "medical:anesthetic", quantity = 2 },
                new()
                {
                    itemId = "medical:optional-tonic",
                    quantity = 99,
                    optional = true
                }
            }
        };

        public void Dispose()
        {
            if (FacilityObject != null)
                UnityEngine.Object.DestroyImmediate(FacilityObject);
        }
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        private readonly IReadOnlyDictionary<string, long> unitMassByItem;

        internal FixedMassQuery(IReadOnlyDictionary<string, long> unitMassByItem)
        {
            this.unitMassByItem = unitMassByItem;
        }

        public long AuthorityRevision =>
            SurgeryMaterialDestinationRuntimeDebugScenarios.AuthorityRevision;

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(RequireUnitMass(itemId));

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(subject.ItemId);

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(itemId);

        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            GetQuantityMass(lot.Subject.ItemId, lot.Subject, lot.Quantity);

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new(checked(RequireUnitMass(itemId) * quantity));

        private long RequireUnitMass(ItemDefinitionId itemId) =>
            itemId.IsValid
            && unitMassByItem.TryGetValue(itemId.Value, out long grams)
                ? grams
                : throw new InvalidOperationException(
                    "Unknown QA item mass: " + itemId.Value);
    }

    private sealed class EmptyOccupancy : IFacilityBufferPhysicalOccupancyQuery
    {
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

    private sealed class RejectingLifecycle :
        IFacilityBufferDestinationLifecycleCommand
    {
        private readonly string reason;

        internal RejectingLifecycle(string reason)
        {
            this.reason = reason;
        }

        public bool TryReplaceOwnedAuthorities(
            string ownerDomain,
            IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            IReadOnlyList<FacilityBufferCapacityProfile> desiredProfiles,
            out string failureReason)
        {
            failureReason = reason;
            return false;
        }
    }

    private static T CreateProxy<T>(
        Func<MethodInfo, object[], object> handler)
        where T : class
    {
        T proxy = DispatchProxy.Create<T, ConfigurableDispatchProxy>();
        ((ConfigurableDispatchProxy)(object)proxy).Handler = handler;
        return proxy;
    }

    public class ConfigurableDispatchProxy : DispatchProxy
    {
        public Func<MethodInfo, object[], object> Handler { get; set; }

        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].ParameterType.IsByRef)
                {
                    args[index] = DefaultValue(
                        parameters[index].ParameterType.GetElementType());
                }
            }
            return Handler?.Invoke(targetMethod, args)
                ?? DefaultValue(targetMethod.ReturnType);
        }
    }

    private static object DefaultValue(Type type) =>
        type == typeof(void)
            ? null
            : type != null && type.IsValueType
                ? Activator.CreateInstance(type)
                : null;
}
#endif
