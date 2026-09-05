#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class DurableFacilityEquipmentSaveDebugScenarios
{
    private const string ItemId = "record:qa-durable-index";
    private const string PolicyId = "policy:qa.durable-save";
    private const string OwnerDomain = "qa.durable-save";
    private const string OwnerSubjectId = "slot:qa-durable-save";
    private const string FacilityId = "building:qa-durable-save";
    private const string ZeroDigest =
        "0000000000000000000000000000000000000000000000000000000000000000";
    private const string OtherDigest =
        "1111111111111111111111111111111111111111111111111111111111111111";
    private static readonly Vector2Int DropPosition = new(14, 9);

    [MenuItem(
        "DungeonStory/Debug/V27/Run Durable Facility Equipment Save Contracts")]
    public static void RunAll()
    {
        VerifyActiveCaptureJsonProjectionRoundTrip();
        VerifyExactUpperLowerJoin();
        VerifyRegistryEnvelopeJoin();
        VerifyMissingJoinSidesFailLoud();
        VerifyExactJoinTamperingFailsLoud();
        VerifyMissingActiveFacilityFailsLoud();
        VerifyDuplicateNonClosedAndSequenceFailLoud();

        Debug.Log(
            "[V27][PASS] Durable facility-equipment save contracts preserve "
            + "active current-format capture/JSON/projection, exact upper/lower "
            + "custody joins, facility ownership, unique active keys and "
            + "assignment sequences, with all tested tampering rejected.");
    }

    private static void VerifyActiveCaptureJsonProjectionRoundTrip()
    {
        Fixture fixture = new();
        DurableFacilityEquipmentSlotSnapshot active =
            fixture.CreateSnapshot(sequence: 1L);
        DungeonDurableFacilityEquipmentSaveData captured = new()
        {
            version = DungeonDurableFacilityEquipmentSaveData.CurrentVersion,
            nextAssignmentSequence = 2L,
            revision = 7L,
            slots = new List<DurableFacilityEquipmentSlotSaveData>
            {
                DurableFacilityEquipmentRestoreProjection.Capture(active)
            }
        };
        CandidateDrainQuery childCandidate = new(Array.Empty<
            FacilityBufferDestinationCustodyDrainSnapshot>());
        DurableFacilityEquipmentRestoreProjection projection = new(
            fixture.Policies,
            fixture.Capacity,
            childCandidate);
        DurableFacilityEquipmentSaveSection section = new(
            new CapturedPersistence(captured),
            projection);

        string json = section.Capture();
        DungeonDurableFacilityEquipmentSaveData decoded =
            JsonUtility.FromJson<DungeonDurableFacilityEquipmentSaveData>(json);
        DurableFacilityEquipmentRestoreCandidate restored =
            projection.Prepare(decoded);
        DungeonDurableFacilityEquipmentSaveData recaptured = new()
        {
            version = DungeonDurableFacilityEquipmentSaveData.CurrentVersion,
            nextAssignmentSequence = restored.NextAssignmentSequence,
            revision = restored.Revision,
            slots = restored.Slots
                .Select(DurableFacilityEquipmentRestoreProjection.Capture)
                .ToList()
        };

        Require(
            decoded != null
            && restored.Slots.Count == 1
            && restored.Slots[0].LifecyclePhase ==
                DurableFacilityEquipmentSlotLifecyclePhase.Active
            && restored.Slots[0].Capacity.Value == 1300L
            && string.Equals(
                json,
                JsonUtility.ToJson(recaptured),
                StringComparison.Ordinal),
            "Active durable-equipment capture did not round-trip through current JSON and projection exactly.");
    }

    private static void VerifyExactUpperLowerJoin()
    {
        ScenarioPayload payload = ScenarioPayload.CreateDraining();
        DungeonGameRestoreReport report = Validate(payload);
        Require(
            report.Success,
            "An exact durable-equipment upper/lower join was rejected: "
            + string.Join(" | ", report.Errors));
    }

    private static void VerifyRegistryEnvelopeJoin()
    {
        ScenarioPayload payload = ScenarioPayload.CreateDraining();
        Dictionary<string, DungeonSaveSectionEnvelope> envelopes = new(
            StringComparer.Ordinal)
        {
            [DurableFacilityEquipmentSaveSection.Id] = Envelope(
                DurableFacilityEquipmentSaveSection.Id,
                DungeonDurableFacilityEquipmentSaveData.CurrentVersion,
                payload.Upper),
            [PhysicalItemsSaveSection.Id] = Envelope(
                PhysicalItemsSaveSection.Id,
                DungeonPhysicalItemSaveData.CurrentVersion,
                payload.Physical),
            [ModularFacilityWorldSaveSection.Id] = Envelope(
                ModularFacilityWorldSaveSection.Id,
                ModularFacilityWorldSaveSection.CurrentSectionVersion,
                payload.Facilities)
        };
        DungeonGameRestoreReport report = new();
        new DurableFacilityEquipmentCrossAggregateSaveValidation()
            .Validate(envelopes, report);
        Require(
            report.Success,
            "An exact current-format registry envelope join was rejected: "
            + string.Join(" | ", report.Errors));
    }

    private static DungeonSaveSectionEnvelope Envelope<T>(
        string sectionId,
        int sectionVersion,
        T payload) => new()
    {
        sectionId = sectionId,
        sectionVersion = sectionVersion,
        payloadJson = JsonUtility.ToJson(payload)
    };

    private static void VerifyMissingJoinSidesFailLoud()
    {
        AssertFails(
            payload => payload.Upper.slots.Clear(),
            "A durable lower custody row without its upper owner was accepted.");
        AssertFails(
            payload => payload.Physical
                .pendingProductionInputDestinationDrains.Clear(),
            "A draining durable upper without its lower custody row was accepted.");
    }

    private static void VerifyExactJoinTamperingFailsLoud()
    {
        AssertFails(
            payload => payload.Upper.slots[0].drainParentOperationId =
                "durable-equipment-slot-drain:tampered",
            "A tampered durable drain parent operation was accepted.");
        AssertFails(
            payload => payload.Upper.slots[0].drainStepOperationId =
                "durable-equipment-slot-drain:tampered:custody",
            "A tampered durable drain step operation was accepted.");
        AssertFails(
            payload => payload.Upper.slots[0]
                .drainSourceAuthorityFingerprint = OtherDigest,
            "A tampered durable drain source fingerprint was accepted.");
        AssertFails(
            payload => payload.Upper.slots[0].drainInputMassGrams += 1L,
            "A tampered durable drain input mass was accepted.");
        AssertFails(
            payload => payload.Upper.slots[0].drainPhase =
                FacilityBufferDestinationCustodyDrainPhase.ReleasingActors,
            "A tampered durable drain phase was accepted.");
    }

    private static void VerifyMissingActiveFacilityFailsLoud()
    {
        Fixture fixture = new();
        DungeonDurableFacilityEquipmentSaveData upper = new()
        {
            nextAssignmentSequence = 2L,
            revision = 1L,
            slots = new List<DurableFacilityEquipmentSlotSaveData>
            {
                DurableFacilityEquipmentRestoreProjection.Capture(
                    fixture.CreateSnapshot(sequence: 1L))
            }
        };
        ScenarioPayload payload = new(
            upper,
            CreatePhysical(Array.Empty<
                ProductionInputDestinationCustodyDrainSaveData>()),
            CreateFacilities(Array.Empty<string>()));
        Require(
            !Validate(payload).Success,
            "An active durable-equipment row referencing a missing facility was accepted.");
    }

    private static void VerifyDuplicateNonClosedAndSequenceFailLoud()
    {
        Fixture fixture = new();
        DurableFacilityEquipmentSlotSaveData first =
            DurableFacilityEquipmentRestoreProjection.Capture(
                fixture.CreateSnapshot(sequence: 1L));
        DurableFacilityEquipmentSlotSaveData second =
            DurableFacilityEquipmentRestoreProjection.Capture(
                fixture.CreateSnapshot(sequence: 2L));
        DungeonDurableFacilityEquipmentSaveData duplicateActive = new()
        {
            nextAssignmentSequence = 3L,
            revision = 1L,
            slots = new List<DurableFacilityEquipmentSlotSaveData>
            {
                first,
                second
            }
        };
        ScenarioPayload activePayload = new(
            duplicateActive,
            CreatePhysical(Array.Empty<
                ProductionInputDestinationCustodyDrainSaveData>()),
            CreateFacilities(new[] { FacilityId }));
        Require(
            !Validate(activePayload).Success,
            "Two non-closed durable-equipment rows for one key were accepted.");

        DungeonDurableFacilityEquipmentSaveData duplicateSequence =
            Clone(duplicateActive);
        duplicateSequence.slots[1].logicalOwnerDomain =
            "qa.durable-save-secondary";
        duplicateSequence.slots[1].assignmentSequence =
            duplicateSequence.slots[0].assignmentSequence;
        ScenarioPayload sequencePayload = new(
            duplicateSequence,
            CreatePhysical(Array.Empty<
                ProductionInputDestinationCustodyDrainSaveData>()),
            CreateFacilities(new[] { FacilityId }));
        Require(
            !Validate(sequencePayload).Success,
            "A duplicate durable-equipment assignment sequence was accepted.");
    }

    private static void AssertFails(
        Action<ScenarioPayload> mutate,
        string message)
    {
        ScenarioPayload payload = ScenarioPayload.CreateDraining();
        mutate(payload);
        Require(!Validate(payload).Success, message);
    }

    private static DungeonGameRestoreReport Validate(ScenarioPayload payload)
    {
        DungeonGameSaveData save = new();
        DungeonSaveSectionPayload.Write(
            save,
            DurableFacilityEquipmentSaveSection.Id,
            DungeonDurableFacilityEquipmentSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            payload.Upper);
        DungeonSaveSectionPayload.Write(
            save,
            PhysicalItemsSaveSection.Id,
            DungeonPhysicalItemSaveData.CurrentVersion,
            DungeonSaveRestorePhase.Items,
            payload.Physical);
        DungeonSaveSectionPayload.Write(
            save,
            ModularFacilityWorldSaveSection.Id,
            1,
            DungeonSaveRestorePhase.World,
            payload.Facilities);
        DungeonGameRestoreReport report = new();
        new DurableFacilityEquipmentCrossAggregateSaveValidation()
            .Validate(save, report);
        return report;
    }

    private static DungeonPhysicalItemSaveData CreatePhysical(
        IEnumerable<ProductionInputDestinationCustodyDrainSaveData> drains) =>
        new()
        {
            pendingProductionInputDestinationDrains = drains
                .Select(value => value?.Clone())
                .ToList()
        };

    private static ModularFacilityWorldSaveData CreateFacilities(
        IEnumerable<string> facilityIds) => new()
    {
        version = ModularFacilityWorldSaveService.CurrentVersion,
        buildings = facilityIds.Select(value =>
            new ModularFacilityBuildingSaveData
            {
                persistentInstanceId = value,
                centerX = DropPosition.x,
                centerY = DropPosition.y,
                width = 1,
                height = 1
            }).ToList()
    };

    private static ProductionInputDestinationCustodyDrainSaveData
        CreatePreparedDrain(
            Fixture fixture,
            long sequence)
    {
        DurableFacilityEquipmentSlotKey key = fixture.Assignment.Key;
        string parent = DurableFacilityEquipmentSlotIdentity
            .BuildDrainParentOperationId(key, sequence);
        string step = DurableFacilityEquipmentSlotIdentity
            .BuildDrainStepOperationId(key, sequence);
        string owner = DurableFacilityEquipmentSlotIdentity
            .BuildOwnerStableId(key, sequence);
        string destination = DurableFacilityEquipmentSlotIdentity
            .BuildDestinationId(key, sequence);
        ProductionInputDestinationDrainStackSaveData stack = new()
        {
            stackId = "stack:qa-durable-save",
            itemId = ItemId,
            componentFingerprint = ZeroDigest,
            quantity = 1,
            massGrams = 1300L,
            state = WorldItemStackState.FacilityBuffer,
            positionX = DropPosition.x,
            positionY = DropPosition.y,
            destinationPositionX = DropPosition.x,
            destinationPositionY = DropPosition.y
        };
        ProductionInputDestinationCustodyDrainSaveData row = new()
        {
            parentOperationId = parent,
            stepOperationId = step,
            ownerStableId = owner,
            billId = OwnerSubjectId,
            facilityId = FacilityId,
            sourceDestinationId = destination,
            ownerGridX = DropPosition.x,
            ownerGridY = DropPosition.y,
            sourceClaimFingerprint =
                fixture.CapacityProjection.SourceAuthorityFingerprint,
            sourceOwnershipFingerprint = ZeroDigest,
            phase = ProductionInputDestinationCustodyDrainPhase.Prepared,
            sourceStacks = new List<
                ProductionInputDestinationDrainStackSaveData> { stack },
            inputQuantity = 1,
            inputMassGrams = 1300L
        };
        row.requestFingerprint =
            ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                row.parentOperationId,
                row.stepOperationId,
                row.ownerStableId,
                row.billId,
                row.facilityId,
                row.sourceDestinationId,
                row.ownerGridX,
                row.ownerGridY,
                row.sourceClaimFingerprint,
                row.sourceOwnershipFingerprint,
                row.sourceStacks,
                row.sourceOperations,
                row.sourceActors,
                row.inputQuantity,
                row.inputMassGrams);
        Require(
            ProductionInputDestinationCustodyDrainContract.IsValidSave(row),
            "The focused fixture generated an invalid custody drain row.");
        return row;
    }

    private static T Clone<T>(T value) where T : class =>
        JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class ScenarioPayload
    {
        internal ScenarioPayload(
            DungeonDurableFacilityEquipmentSaveData upper,
            DungeonPhysicalItemSaveData physical,
            ModularFacilityWorldSaveData facilities)
        {
            Upper = upper;
            Physical = physical;
            Facilities = facilities;
        }

        internal DungeonDurableFacilityEquipmentSaveData Upper { get; }
        internal DungeonPhysicalItemSaveData Physical { get; }
        internal ModularFacilityWorldSaveData Facilities { get; }

        internal static ScenarioPayload CreateDraining()
        {
            Fixture fixture = new();
            ProductionInputDestinationCustodyDrainSaveData child =
                CreatePreparedDrain(fixture, sequence: 1L);
            FacilityBufferDestinationCustodyDrainSnapshot projected =
                FacilityBufferDestinationCustodyDrainProjection
                    .ProjectValidated(child);
            DurableFacilityEquipmentSlotSnapshot draining =
                fixture.CreateSnapshot(
                    sequence: 1L,
                    phase: DurableFacilityEquipmentSlotLifecyclePhase.Draining,
                    closeReason: "qa-close",
                    drain: projected);
            return new ScenarioPayload(
                new DungeonDurableFacilityEquipmentSaveData
                {
                    nextAssignmentSequence = 2L,
                    revision = 2L,
                    slots = new List<DurableFacilityEquipmentSlotSaveData>
                    {
                        DurableFacilityEquipmentRestoreProjection.Capture(
                            draining)
                    }
                },
                CreatePhysical(new[] { child }),
                CreateFacilities(new[] { FacilityId }));
        }
    }

    private sealed class Fixture
    {
        internal Fixture()
        {
            Requirement = new DurableFacilityEquipmentRequirement(
                "arcane-index",
                (ItemDefinitionId)ItemId,
                1);
            DurableFacilityEquipmentPolicy policy = new(
                PolicyId,
                revision: 1L,
                OwnerDomain,
                DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                DurableFacilityEquipmentPolicyKinds
                    .PositiveDurabilityComponent,
                new[] { Requirement });
            Policies = new DurableFacilityEquipmentPolicyRegistry(
                new IDurableFacilityEquipmentPolicySource[]
                {
                    new StaticPolicySource(policy)
                });
            Capacity = new DurableFacilityEquipmentCapacityProjectionRegistry(
                new IDurableFacilityEquipmentCapacityProjector[]
                {
                    new DefinitionMassDurableFacilityEquipmentCapacityProjector(
                        new FixedMassQuery())
                });
            Assignment = policy.CreateAssignment(
                OwnerSubjectId,
                (BuildingInstanceId)FacilityId,
                DropPosition);
            Require(
                Capacity.TryProjectMaximumMass(
                    Assignment,
                    out DurableFacilityEquipmentCapacityProjection projection,
                    out string failureReason),
                "The focused fixture could not project capacity: "
                + failureReason);
            CapacityProjection = projection;
        }

        internal DurableFacilityEquipmentRequirement Requirement { get; }
        internal DurableFacilityEquipmentPolicyRegistry Policies { get; }
        internal DurableFacilityEquipmentCapacityProjectionRegistry Capacity
        {
            get;
        }
        internal DurableFacilityEquipmentAssignment Assignment { get; }
        internal DurableFacilityEquipmentCapacityProjection CapacityProjection
        {
            get;
        }

        internal DurableFacilityEquipmentSlotSnapshot CreateSnapshot(
            long sequence,
            DurableFacilityEquipmentSlotLifecyclePhase phase =
                DurableFacilityEquipmentSlotLifecyclePhase.Active,
            string closeReason = "",
            FacilityBufferDestinationCustodyDrainSnapshot drain = null) => new(
            Assignment,
            sequence,
            DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                Assignment.Key,
                sequence),
            DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                Assignment.Key,
                sequence),
            DurableFacilityEquipmentFingerprint.CreateAssignment(Assignment),
            CapacityProjection,
            new[]
            {
                new DurableFacilityEquipmentRequirementStatus(
                    Requirement,
                    pendingQuantity: 0,
                    bufferedUsableQuantity: 0)
            },
            phase,
            closeReason,
            drain,
            authoritiesRevoked: false);
    }

    private sealed class StaticPolicySource :
        IDurableFacilityEquipmentPolicySource
    {
        private readonly IReadOnlyList<DurableFacilityEquipmentPolicy> policies;

        internal StaticPolicySource(DurableFacilityEquipmentPolicy policy)
        {
            policies = Array.AsReadOnly(new[] { policy });
        }

        public string SourceId => "qa.durable-save-policy-source";
        public long Revision => 1L;
        public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
            policies;
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 11L;

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId)
        {
            if (!string.Equals(itemId.Value, ItemId, StringComparison.Ordinal))
                throw new InvalidOperationException("Unknown focused-fixture item.");
            return new PhysicalMassGrams(1300L);
        }

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(subject.ItemId);

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(itemId);

        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            GetDefinitionUnitMass(lot.Subject.ItemId).Multiply(lot.Quantity);

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => GetDefinitionUnitMass(itemId).Multiply(quantity);
    }

    private sealed class CandidateDrainQuery :
        IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            FacilityBufferDestinationCustodyDrainSnapshot> drains;

        internal CandidateDrainQuery(
            IEnumerable<FacilityBufferDestinationCustodyDrainSnapshot> drains)
        {
            this.drains = Array.AsReadOnly((drains
                    ?? Array.Empty<
                        FacilityBufferDestinationCustodyDrainSnapshot>())
                .OrderBy(value => value.StepOperationId, StringComparer.Ordinal)
                .ToArray());
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot>
            Drains => drains;

        public bool TryGetDrain(
            string stepOperationId,
            out FacilityBufferDestinationCustodyDrainSnapshot snapshot)
        {
            snapshot = drains.FirstOrDefault(value => string.Equals(
                value.StepOperationId,
                stepOperationId,
                StringComparison.Ordinal));
            return snapshot != null;
        }
    }

    private sealed class CapturedPersistence :
        IDurableFacilityEquipmentSlotPersistence
    {
        private readonly DungeonDurableFacilityEquipmentSaveData captured;

        internal CapturedPersistence(
            DungeonDurableFacilityEquipmentSaveData captured)
        {
            this.captured = captured;
        }

        public DungeonDurableFacilityEquipmentSaveData CaptureSaveData() =>
            Clone(captured);

        public void PublishRestoreCandidate(
            DurableFacilityEquipmentRestoreCandidate candidate) =>
            throw new InvalidOperationException(
                "The capture-only fixture must not publish live state.");
    }
}
#endif
