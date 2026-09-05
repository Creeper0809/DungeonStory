#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class DurableFacilityEquipmentCapacityProjectionDebugScenarios
{
    private const string ArcaneIndexId = "record:arcane-index";
    private const string PositiveDurabilityPolicyKind =
        "component-durability-positive";
    private static readonly DurableFacilityEquipmentSlotKey ArcaneSlotKey =
        new(
            "research.arcane-index",
            "building:qa-arcane-index");
    private static readonly BuildingInstanceId ArcaneFacilityId =
        (BuildingInstanceId)"building:qa-arcane-index";
    private static readonly Vector2Int ArcaneDropPosition = new(9, 4);

    [MenuItem(
        "DungeonStory/Debug/V27/Run Durable Facility Equipment Capacity Contracts")]
    public static void RunAll()
    {
        VerifyCanonicalContractValidation();
        VerifyRequirementSortingAndIdentityAreDeterministic();
        VerifyArcaneIndexDefinitionMassIsExactly1300Grams();
        VerifyNonPositiveAndCheckedOverflowFailClosed();
        VerifyUnregisteredAndDuplicateProjectorsFailLoudly();
        VerifySyntheticStateMassProjectorRegistersWithoutCoreBranch();
        VerifyRegistrationDrivenPolicyAndUsabilityCanary();

        Debug.Log(
            "[V27][PASS] Durable facility-equipment canonical contracts, "
            + "deterministic identity, exact definition mass, overflow gates, "
            + "and extension-closed capacity projector registration are exact.");
    }

    private static void VerifyCanonicalContractValidation()
    {
        Require(
            Throws<ArgumentException>(() =>
                _ = new DurableFacilityEquipmentSlotKey(
                    " research.arcane-index",
                    "building:qa-arcane-index"))
            && Throws<ArgumentException>(() =>
                _ = new DurableFacilityEquipmentSlotKey(
                    "research.arcane-index",
                    string.Empty)),
            "Non-canonical durable equipment slot keys were accepted.");

        Require(
            Throws<ArgumentException>(() =>
                _ = new DurableFacilityEquipmentRequirement(
                    " arcane-index",
                    (ItemDefinitionId)ArcaneIndexId,
                    1))
            && Throws<ArgumentException>(() =>
                _ = new DurableFacilityEquipmentRequirement(
                    "arcane-index",
                    default,
                    1))
            && Throws<ArgumentException>(() =>
                _ = new DurableFacilityEquipmentRequirement(
                    "arcane-index",
                    (ItemDefinitionId)ArcaneIndexId,
                    0)),
            "Invalid durable equipment requirements were accepted.");

        DurableFacilityEquipmentRequirement valid =
            CreateRequirement("arcane-index", ArcaneIndexId, 1);
        Require(
            Throws<ArgumentException>(() => CreateAssignment(
                DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                new[] { valid },
                policyId: " policy:research.arcane-index"))
            && Throws<ArgumentException>(() => CreateAssignment(
                DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                new[] { valid },
                policyRevision: 0L))
            && Throws<ArgumentException>(() => CreateAssignment(
                " definition-mass",
                new[] { valid }))
            && Throws<ArgumentException>(() => CreateAssignment(
                DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                new[] { valid, valid })),
            "Invalid or duplicate durable equipment assignments were accepted.");

        DurableFacilityEquipmentAssignment assignment = CreateAssignment(
            DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
            new[] { valid });
        DurableFacilityEquipmentRequirementStatus status = new(
            valid,
            pendingQuantity: 1,
            bufferedUsableQuantity: 0);
        Require(
            !status.IsReady
            && Throws<ArgumentException>(() =>
                _ = new DurableFacilityEquipmentRequirementStatus(
                    valid,
                    pendingQuantity: -1,
                    bufferedUsableQuantity: 0)),
            "Requirement status accepted negative quantities or misreported readiness.");

        string fingerprint = new('a', 64);
        DurableFacilityEquipmentCapacityProjection projection = new(
            assignment.CapacityPolicyKind,
            new PhysicalMassGrams(1_300L),
            sourceAuthorityRevision: 13L,
            sourceAuthorityFingerprint: new string('b', 64));
        DurableFacilityEquipmentSlotSnapshot snapshot = new(
            assignment,
            assignmentSequence: 1L,
            DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                assignment.Key,
                1L),
            DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                assignment.Key,
                1L),
            DurableFacilityEquipmentFingerprint.CreateAssignment(assignment),
            projection,
            new[] { status });
        Require(
            snapshot.Capacity.Value == 1_300L
            && Throws<ArgumentException>(() =>
                _ = new DurableFacilityEquipmentSlotSnapshot(
                    assignment,
                    assignmentSequence: 0L,
                    "destination:qa",
                    "operation:qa",
                    fingerprint,
                    projection,
                    new[] { status }))
            && Throws<ArgumentException>(() =>
                _ = new DurableFacilityEquipmentSlotSnapshot(
                    assignment,
                    assignmentSequence: 1L,
                    DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                        assignment.Key,
                        1L),
                    DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                        assignment.Key,
                        1L),
                    fingerprint.ToUpperInvariant(),
                    projection,
                    new[] { status })),
            "Slot snapshot accepted invalid sequence or fingerprint evidence.");
    }

    private static void VerifyRequirementSortingAndIdentityAreDeterministic()
    {
        DurableFacilityEquipmentRequirement alpha = CreateRequirement(
            "alpha-index",
            ArcaneIndexId,
            1);
        DurableFacilityEquipmentRequirement zeta = CreateRequirement(
            "zeta-observer",
            "tool:weather-observation-kit",
            2);
        DurableFacilityEquipmentAssignment forward = CreateAssignment(
            DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
            new[] { alpha, zeta });
        DurableFacilityEquipmentAssignment reversed = CreateAssignment(
            DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
            new[] { zeta, alpha });

        Require(
            forward.Requirements.Select(value => value.RequirementId)
                .SequenceEqual(
                    new[] { "alpha-index", "zeta-observer" },
                    StringComparer.Ordinal)
            && reversed.Requirements.Select(value => value.RequirementId)
                .SequenceEqual(
                    forward.Requirements.Select(value => value.RequirementId),
                    StringComparer.Ordinal),
            "Requirement order depended on authoring enumeration order.");
        Require(
            ArcaneSlotKey.Equals(new DurableFacilityEquipmentSlotKey(
                "research.arcane-index",
                "building:qa-arcane-index"))
            && ArcaneSlotKey.GetHashCode()
                == new DurableFacilityEquipmentSlotKey(
                    "research.arcane-index",
                    "building:qa-arcane-index").GetHashCode()
            && string.Equals(
                ArcaneSlotKey.ToString(),
                "research.arcane-index:building:qa-arcane-index",
                StringComparison.Ordinal),
            "Slot-key equality, hash, or canonical text identity drifted.");

        const long sequence = 7L;
        Require(
            string.Equals(
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    ArcaneSlotKey,
                    sequence),
                "facility-input:exact:durable-equipment:research.arcane-index:"
                + "building:qa-arcane-index:00000007",
                StringComparison.Ordinal)
            && string.Equals(
                DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                    ArcaneSlotKey,
                    sequence),
                "durable-equipment-slot:research.arcane-index:"
                + "building:qa-arcane-index:00000007",
                StringComparison.Ordinal)
            && string.Equals(
                DurableFacilityEquipmentSlotIdentity.BuildDrainParentOperationId(
                    ArcaneSlotKey,
                    sequence),
                "durable-equipment-slot-drain:research.arcane-index:"
                + "building:qa-arcane-index:00000007",
                StringComparison.Ordinal)
            && string.Equals(
                DurableFacilityEquipmentSlotIdentity.BuildDrainStepOperationId(
                    ArcaneSlotKey,
                    sequence),
                "durable-equipment-slot-drain:research.arcane-index:"
                + "building:qa-arcane-index:00000007:custody",
                StringComparison.Ordinal),
            "Durable equipment identity formatting is not exact and deterministic.");
        Require(
            Throws<ArgumentException>(() =>
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    ArcaneSlotKey,
                    sequence: 0L))
            && Throws<ArgumentException>(() =>
                _ = new DurableFacilityEquipmentSlotKey(
                    "research:arcane-index",
                    "building:qa-arcane-index")),
            "Durable equipment identity accepted an ambiguous domain or non-positive sequence.");
    }

    private static void VerifyArcaneIndexDefinitionMassIsExactly1300Grams()
    {
        RecordingMassQuery mass = new(new Dictionary<string, long>(
            StringComparer.Ordinal)
        {
            [ArcaneIndexId] = 1_300L
        });
        DefinitionMassDurableFacilityEquipmentCapacityProjector definition =
            new(mass);
        DurableFacilityEquipmentCapacityProjectionRegistry registry = new(
            new IDurableFacilityEquipmentCapacityProjector[] { definition });
        DurableFacilityEquipmentAssignment assignment = CreateAssignment(
            DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
            new[] { CreateRequirement("arcane-index", ArcaneIndexId, 1) });

        Require(
            registry.TryProjectMaximumMass(
                assignment,
                out DurableFacilityEquipmentCapacityProjection projection,
                out string failureReason)
            && projection.MaximumMass.Value == 1_300L
            && projection.SourceAuthorityRevision == mass.AuthorityRevision
            && DurableFacilityEquipmentFingerprint.IsFingerprint(
                projection.SourceAuthorityFingerprint)
            && mass.DefinitionQueryCalls == 1
            && string.IsNullOrEmpty(failureReason),
            "Arcane index definition-mass capacity was not exactly 1,300g: "
            + failureReason);
    }

    private static void VerifyNonPositiveAndCheckedOverflowFailClosed()
    {
        DurableFacilityEquipmentAssignment assignment = CreateAssignment(
            DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
            new[] { CreateRequirement("arcane-index", ArcaneIndexId, 2) });

        DefinitionMassDurableFacilityEquipmentCapacityProjector nonPositive =
            new(new RecordingMassQuery(new Dictionary<string, long>(
                StringComparer.Ordinal)
            {
                [ArcaneIndexId] = 0L
            }));
        Require(
            !nonPositive.TryProjectMaximumMass(
                assignment,
                out DurableFacilityEquipmentCapacityProjection
                    nonPositiveProjection,
                out string nonPositiveFailure)
            && nonPositiveProjection == null
            && string.Equals(
                nonPositiveFailure,
                "durable-equipment-definition-mass-nonpositive:"
                + ArcaneIndexId,
                StringComparison.Ordinal),
            "A non-positive definition mass did not fail with typed evidence.");

        DefinitionMassDurableFacilityEquipmentCapacityProjector overflow = new(
            new RecordingMassQuery(new Dictionary<string, long>(
                StringComparer.Ordinal)
            {
                [ArcaneIndexId] = long.MaxValue
            }));
        Require(
            !overflow.TryProjectMaximumMass(
                assignment,
                out DurableFacilityEquipmentCapacityProjection
                    overflowProjection,
                out string overflowFailure)
            && overflowProjection == null
            && overflowFailure.StartsWith(
                "durable-equipment-definition-mass-failed:OverflowException:",
                StringComparison.Ordinal),
            "Checked definition-mass overflow did not fail closed.");
    }

    private static void VerifyUnregisteredAndDuplicateProjectorsFailLoudly()
    {
        DefinitionMassDurableFacilityEquipmentCapacityProjector definition =
            new(new RecordingMassQuery(new Dictionary<string, long>(
                StringComparer.Ordinal)
            {
                [ArcaneIndexId] = 1_300L
            }));
        DurableFacilityEquipmentCapacityProjectionRegistry registry = new(
            new IDurableFacilityEquipmentCapacityProjector[] { definition });
        DurableFacilityEquipmentAssignment unregistered = CreateAssignment(
            "qa.synthetic-state-mass",
            new[] { CreateRequirement("arcane-index", ArcaneIndexId, 1) });

        Require(
            !registry.TryProjectMaximumMass(
                unregistered,
                out DurableFacilityEquipmentCapacityProjection projection,
                out string failureReason)
            && projection == null
            && string.Equals(
                failureReason,
                "durable-equipment-capacity-policy-unregistered:"
                + "qa.synthetic-state-mass",
                StringComparison.Ordinal),
            "An unregistered capacity policy did not fail loudly.");

        Require(
            Throws<InvalidOperationException>(() =>
                _ = new DurableFacilityEquipmentCapacityProjectionRegistry(
                    new IDurableFacilityEquipmentCapacityProjector[]
                    {
                        new SyntheticStateMassProjector(
                            "qa.synthetic-state-mass",
                            250L),
                        new SyntheticStateMassProjector(
                            "qa.synthetic-state-mass",
                            500L)
                    }))
            && Throws<InvalidOperationException>(() =>
                _ = new DurableFacilityEquipmentCapacityProjectionRegistry(
                    new IDurableFacilityEquipmentCapacityProjector[]
                    {
                        new SyntheticStateMassProjector(
                            " qa.noncanonical",
                            250L)
                    }))
            && Throws<InvalidOperationException>(() =>
                _ = new DurableFacilityEquipmentCapacityProjectionRegistry(
                    Array.Empty<IDurableFacilityEquipmentCapacityProjector>())),
            "Duplicate, non-canonical, or empty projector registration was accepted.");
    }

    private static void VerifySyntheticStateMassProjectorRegistersWithoutCoreBranch()
    {
        RecordingMassQuery mass = new(new Dictionary<string, long>(
            StringComparer.Ordinal)
        {
            [ArcaneIndexId] = 1_300L
        });
        DefinitionMassDurableFacilityEquipmentCapacityProjector definition =
            new(mass);
        SyntheticStateMassProjector stateMass = new(
            "qa.synthetic-state-mass",
            stateSurchargeGramsPerUnit: 275L);
        DurableFacilityEquipmentCapacityProjectionRegistry forward = new(
            new IDurableFacilityEquipmentCapacityProjector[]
            {
                definition,
                stateMass
            });
        DurableFacilityEquipmentCapacityProjectionRegistry reversed = new(
            new IDurableFacilityEquipmentCapacityProjector[]
            {
                stateMass,
                definition
            });
        DurableFacilityEquipmentAssignment assignment = CreateAssignment(
            stateMass.PolicyKind,
            new[] { CreateRequirement("arcane-index", ArcaneIndexId, 2) });

        bool firstSucceeded = forward.TryProjectMaximumMass(
            assignment,
            out DurableFacilityEquipmentCapacityProjection first,
            out string firstFailure);
        bool secondSucceeded = reversed.TryProjectMaximumMass(
            assignment,
            out DurableFacilityEquipmentCapacityProjection second,
            out string secondFailure);
        Require(
            firstSucceeded
            && secondSucceeded
            && first.MaximumMass.Value == 550L
            && second.MaximumMass.Value == first.MaximumMass.Value
            && first.SourceAuthorityFingerprint
                == second.SourceAuthorityFingerprint
            && stateMass.InvocationCount == 2
            && mass.DefinitionQueryCalls == 0
            && string.IsNullOrEmpty(firstFailure)
            && string.IsNullOrEmpty(secondFailure),
            "Synthetic state-mass policy was not dispatched extension-closed: "
            + firstFailure + secondFailure);
    }

    private static void VerifyRegistrationDrivenPolicyAndUsabilityCanary()
    {
        DurableFacilityEquipmentRequirement arcane = CreateRequirement(
            "arcane-index",
            ArcaneIndexId,
            1);
        DurableFacilityEquipmentRequirement synthetic = CreateRequirement(
            "synthetic-tool",
            "tool:qa-synthetic-durable",
            1);
        DurableFacilityEquipmentPolicy researchPolicy = new(
            "policy:research.arcane-index",
            revision: 1L,
            logicalOwnerDomain: "research.arcane-index",
            DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
            DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent,
            new[] { arcane });
        DurableFacilityEquipmentPolicy syntheticPolicy = new(
            "policy:qa.synthetic-durable",
            revision: 3L,
            logicalOwnerDomain: "qa.synthetic-durable",
            DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
            DurableFacilityEquipmentPolicyKinds.PositiveDurabilityComponent,
            new[] { synthetic });
        StaticPolicySource alpha = new(
            "qa.source.alpha",
            5L,
            new[] { researchPolicy });
        StaticPolicySource zeta = new(
            "qa.source.zeta",
            7L,
            new[] { syntheticPolicy });
        DurableFacilityEquipmentPolicyRegistry forward = new(
            new IDurableFacilityEquipmentPolicySource[] { alpha, zeta });
        DurableFacilityEquipmentPolicyRegistry reversed = new(
            new IDurableFacilityEquipmentPolicySource[] { zeta, alpha });
        Require(
            forward.Revision == reversed.Revision
            && forward.CapturePolicies().Select(value => value.PolicyId)
                .SequenceEqual(
                    reversed.CapturePolicies().Select(value => value.PolicyId),
                    StringComparer.Ordinal)
            && forward.TryGetPolicy(
                syntheticPolicy.PolicyId,
                out DurableFacilityEquipmentPolicy captured)
            && captured.CreateAssignment(
                    "building:qa-synthetic",
                    (BuildingInstanceId)"building:qa-synthetic",
                    new Vector2Int(4, 2))
                .UsabilityPolicyKind
                == DurableFacilityEquipmentPolicyKinds
                    .PositiveDurabilityComponent,
            "Policy discovery depended on registration order or a core content branch.");

        DurableFacilityEquipmentUsabilityRegistry usability = new(
            new IDurableFacilityEquipmentUsabilityPolicy[]
            {
                new PositiveDurabilityComponentUsabilityPolicy()
            });
        DurableFacilityEquipmentUseSubject usable =
            DurableFacilityEquipmentUseSubjectCapture.Capture(
                CreateStack(ArcaneIndexId, current: 80d, maximum: 160d));
        DurableFacilityEquipmentUseSubject exhausted =
            DurableFacilityEquipmentUseSubjectCapture.Capture(
                CreateStack(ArcaneIndexId, current: 0d, maximum: 160d));
        bool usableOk = usability.TryEvaluate(
            researchPolicy.UsabilityPolicyKind,
            arcane,
            usable,
            out DurableFacilityEquipmentUsabilityResult usableResult,
            out string usableFailure);
        bool exhaustedOk = usability.TryEvaluate(
            researchPolicy.UsabilityPolicyKind,
            arcane,
            exhausted,
            out DurableFacilityEquipmentUsabilityResult exhaustedResult,
            out string exhaustedFailure);
        bool unknownRejected = !usability.TryEvaluate(
            "qa.unknown-usability",
            arcane,
            usable,
            out _,
            out string unknownFailure);
        Require(
            usableOk
            && usableResult.IsUsable
            && exhaustedOk
            && exhaustedResult.Disposition ==
                DurableFacilityEquipmentUsabilityDisposition.Exhausted
            && unknownRejected
            && unknownFailure.StartsWith(
                "durable-equipment-usability-policy-unregistered:",
                StringComparison.Ordinal)
            && string.IsNullOrEmpty(usableFailure)
            && string.IsNullOrEmpty(exhaustedFailure),
            "Component-driven usability or unknown-policy fail-loud behavior drifted.");

        Require(
            Throws<InvalidOperationException>(() =>
                _ = new DurableFacilityEquipmentPolicyRegistry(
                    new IDurableFacilityEquipmentPolicySource[]
                    {
                        alpha,
                        new StaticPolicySource(
                            "qa.source.duplicate-policy",
                            1L,
                            new[] { researchPolicy })
                    }))
            && Throws<InvalidOperationException>(() =>
                _ = new DurableFacilityEquipmentUsabilityRegistry(
                    new IDurableFacilityEquipmentUsabilityPolicy[]
                    {
                        new PositiveDurabilityComponentUsabilityPolicy(),
                        new PositiveDurabilityComponentUsabilityPolicy()
                    })),
            "Duplicate policy or usability registration was accepted.");
    }

    private static WorldItemStackSnapshot CreateStack(
        string itemId,
        double current,
        double maximum) => new()
    {
        StackId = "stack:qa-durable",
        ContentRevision = 11L,
        ItemId = itemId,
        Quantity = 1,
        Components = new[]
        {
            new ItemInstanceComponentSaveData
            {
                componentTypeId = ItemInstanceComponentIds.Durability,
                schemaVersion = 1,
                values = new List<ItemStateValueSaveData>
                {
                    new()
                    {
                        key = "current",
                        kind = ItemStateValueKind.Decimal,
                        decimalValue = current
                    },
                    new()
                    {
                        key = "maximum",
                        kind = ItemStateValueKind.Decimal,
                        decimalValue = maximum
                    }
                }
            }
        }
    };

    private static DurableFacilityEquipmentRequirement CreateRequirement(
        string requirementId,
        string itemId,
        int quantity) => new(
        requirementId,
        (ItemDefinitionId)itemId,
        quantity);

    private static DurableFacilityEquipmentAssignment CreateAssignment(
        string capacityPolicyKind,
        IEnumerable<DurableFacilityEquipmentRequirement> requirements,
        string policyId = "policy:research.arcane-index",
        long policyRevision = 1L) => new(
        ArcaneSlotKey,
        policyId,
        policyRevision,
        capacityPolicyKind,
        PositiveDurabilityPolicyKind,
        ArcaneFacilityId,
        ArcaneDropPosition,
        requirements);

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class SyntheticStateMassProjector :
        IDurableFacilityEquipmentCapacityProjector
    {
        private readonly long stateSurchargeGramsPerUnit;

        internal SyntheticStateMassProjector(
            string policyKind,
            long stateSurchargeGramsPerUnit)
        {
            PolicyKind = policyKind;
            this.stateSurchargeGramsPerUnit = stateSurchargeGramsPerUnit;
        }

        public string PolicyKind { get; }
        internal int InvocationCount { get; private set; }

        public bool TryProjectMaximumMass(
            DurableFacilityEquipmentAssignment assignment,
            out DurableFacilityEquipmentCapacityProjection projection,
            out string failureReason)
        {
            InvocationCount++;
            projection = null;
            failureReason = string.Empty;
            if (assignment == null
                || !string.Equals(
                    assignment.CapacityPolicyKind,
                    PolicyKind,
                    StringComparison.Ordinal))
            {
                failureReason = "qa-synthetic-state-mass-assignment-invalid";
                return false;
            }
            try
            {
                long total = 0L;
                foreach (DurableFacilityEquipmentRequirement requirement in
                         assignment.Requirements)
                {
                    total = checked(total + checked(
                        stateSurchargeGramsPerUnit
                        * requirement.RequiredQuantity));
                }
                PhysicalMassGrams maximum = new(total);
                const long sourceRevision = 29L;
                string fingerprint =
                    DurableFacilityEquipmentFingerprint.CreateProjectionSource(
                        DurableFacilityEquipmentFingerprint.CreateAssignment(
                            assignment),
                        PolicyKind,
                        sourceRevision,
                        maximum,
                        new[]
                        {
                            stateSurchargeGramsPerUnit.ToString(
                                System.Globalization.CultureInfo.InvariantCulture)
                        });
                projection = new DurableFacilityEquipmentCapacityProjection(
                    PolicyKind,
                    maximum,
                    sourceRevision,
                    fingerprint);
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException or OverflowException)
            {
                failureReason = "qa-synthetic-state-mass-failed:"
                    + exception.GetType().Name;
                return false;
            }
        }
    }

    private sealed class RecordingMassQuery : IPhysicalItemMassQuery
    {
        private readonly IReadOnlyDictionary<string, long> masses;

        internal RecordingMassQuery(IReadOnlyDictionary<string, long> masses)
        {
            this.masses = masses;
        }

        public long AuthorityRevision => 13L;
        internal int DefinitionQueryCalls { get; private set; }

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId)
        {
            DefinitionQueryCalls++;
            if (!itemId.IsValid
                || masses == null
                || !masses.TryGetValue(itemId.Value, out long grams))
            {
                throw new InvalidOperationException(
                    "qa-definition-mass-missing:" + itemId.Value);
            }
            return grams > 0L ? new PhysicalMassGrams(grams) : default;
        }

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
            int quantity) =>
            GetDefinitionUnitMass(itemId).Multiply(quantity);
    }

    private sealed class StaticPolicySource :
        IDurableFacilityEquipmentPolicySource
    {
        private readonly IReadOnlyList<DurableFacilityEquipmentPolicy> policies;

        internal StaticPolicySource(
            string sourceId,
            long revision,
            IEnumerable<DurableFacilityEquipmentPolicy> policies)
        {
            SourceId = sourceId;
            Revision = revision;
            this.policies = Array.AsReadOnly((policies
                    ?? Array.Empty<DurableFacilityEquipmentPolicy>())
                .ToArray());
        }

        public string SourceId { get; }
        public long Revision { get; }

        public IReadOnlyList<DurableFacilityEquipmentPolicy>
            CapturePolicies() => policies;
    }
}
#endif
