using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class RunInvasionDurableEquipmentDebugScenarios
{
    private static readonly BuildingInstanceId OfficeId =
        new("building:qa-administration-office");
    private static readonly BuildingInstanceId SignalPostId =
        new("building:qa-security-post");

    [MenuItem("Tools/Dungeon Story/QA/Run Invasion Durable Equipment")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("RUN_INVASION_DURABLE_EQUIPMENT_PASS");
    }

    public static void RunAll()
    {
        DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll();
        DurableFacilityEquipmentSlotRuntimeDebugScenarios.RunAll();
        DurableFacilityEquipmentSaveDebugScenarios.RunAll();
        VerifyExactPoliciesAndAuthoredMass();
        VerifyAdministrativeResolutionTransaction();
        VerifySignalHornRallyTransaction();
        VerifyLostOwnersCloseBothSlots();
    }

    public static void RunG11FromBatch()
    {
        string reportPath = RequireG11ReportPath();
        try
        {
            RunAll();
            ReproductionDurableEquipmentDebugScenarios.RunAll();
            ClimateDurableEquipmentDebugScenarios.RunAll();
            ReproductionProcessTransitionDebugScenarios
                .RunG11DurablePlanEvidence();
            WriteG11Report(
                reportPath,
                new G11Report
                {
                    passed = true,
                    commonCommittedContext = true,
                    wearSeparatedFromConsumption = true,
                    effectFailureRollback = true,
                    administrationOwnerResult = true,
                    signalOwnerResult = true,
                    reproductionOwnerResult = true,
                    climatePairOwnerResult = true,
                    secondToolFailureRollback = true,
                    drainSaveRestoreResume = true
                });
            Debug.Log(
                "[AuditRemediation][G11] PASS owners=administration,reproduction,signal,climate; drain-resume=1");
        }
        catch (Exception exception)
        {
            if (!File.Exists(reportPath))
            {
                WriteG11Report(
                    reportPath,
                    new G11Report
                    {
                        passed = false,
                        failure = exception.GetType().Name
                            + ": "
                            + exception.Message
                    });
            }
            Debug.LogException(exception);
            throw;
        }
    }

    private static void VerifyExactPoliciesAndAuthoredMass()
    {
        DurableFacilityEquipmentPolicyRegistry policies = CreatePolicies();
        VerifyPolicy(
            policies,
            RunAdministrativeSealDurableEquipmentPolicySource.PolicyId,
            RunAdministrativeSealDurableEquipmentPolicySource.LogicalOwnerDomain,
            RunAdministrativeSealDurableEquipmentPolicySource.RequirementId,
            DurableToolItemRules.AdministrativeSeal);
        VerifyPolicy(
            policies,
            InvasionSignalHornDurableEquipmentPolicySource.PolicyId,
            InvasionSignalHornDurableEquipmentPolicySource.LogicalOwnerDomain,
            InvasionSignalHornDurableEquipmentPolicySource.RequirementId,
            DurableToolItemRules.WatchSignalHorn);

        VerifyAuthoredMass(
            "Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I92_행정_인장.asset",
            DurableToolItemRules.AdministrativeSeal);
        VerifyAuthoredMass(
            "Assets/Resources/SO/Economy/Items/ResearchOverhaul/V3I108_경계_신호_나팔.asset",
            DurableToolItemRules.WatchSignalHorn);
    }

    private static void VerifyAdministrativeResolutionTransaction()
    {
        RecordingSlots slots = new();
        RecordingUse use = new(slots);
        RunAdministrativeSealDurableEquipmentRuntime runtime = new(
            CreatePolicies(),
            slots,
            use);
        int commits = 0;
        DurableFacilityEquipmentUseContext observed = null;
        DurableFacilityEquipmentUseEvidence evidence = null;
        Require(runtime.TryCommitResolution(
                OfficeId,
                new Vector2Int(11, 4),
                context =>
                {
                    observed = context;
                    evidence = new DurableFacilityEquipmentUseEvidence(context);
                    commits++;
                    return true;
                },
                out DurableFacilityEquipmentUseResult useResult,
                out string failure)
            && commits == 1
            && ReferenceEquals(observed, useResult.CommittedContext)
            && observed.Before.ItemId.Equals(
                (ItemDefinitionId)DurableToolItemRules.AdministrativeSeal)
            && failure.Length == 0,
            "Administrative seal resolution did not return its exact wear context.");
        VerifyEncodedOwnerResult(evidence, DurableToolItemRules.AdministrativeSeal);
        VerifyLastAssignment(
            slots,
            RunAdministrativeSealDurableEquipmentPolicySource.LogicalOwnerDomain,
            OfficeId,
            DurableToolItemRules.AdministrativeSeal);
        Require(use.LastRequirementId ==
                RunAdministrativeSealDurableEquipmentPolicySource.RequirementId
            && Math.Abs(use.LastWear
                - RunAdministrativeSealDurableEquipmentRuntime.WearPerResolution)
                <= 0.000001d,
            "Administrative seal wear contract drifted.");

        Require(!runtime.TryCommitResolution(
                OfficeId,
                new Vector2Int(11, 4),
                () => false,
                out failure)
            && failure == "administrative-seal-resolution-rejected",
            "A rejected administration effect was reported as committed.");
    }

    private static void VerifySignalHornRallyTransaction()
    {
        RecordingSlots slots = new();
        RecordingUse use = new(slots);
        InvasionSignalHornDurableEquipmentRuntime runtime = new(
            CreatePolicies(),
            slots,
            use);
        Vector2Int position = new(19, 5);
        Require(runtime.TryEnsureReady(SignalPostId, position, out _),
            "Signal-horn supply did not become ready.");
        int commits = 0;
        DurableFacilityEquipmentUseContext observed = null;
        DurableFacilityEquipmentUseEvidence evidence = null;
        Require(runtime.TryCommitRally(
                SignalPostId,
                position,
                context =>
                {
                    observed = context;
                    evidence = new DurableFacilityEquipmentUseEvidence(context);
                    commits++;
                    return true;
                },
                out DurableFacilityEquipmentUseResult useResult,
                out string failure)
            && commits == 1
            && ReferenceEquals(observed, useResult.CommittedContext)
            && observed.Before.ItemId.Equals(
                (ItemDefinitionId)DurableToolItemRules.WatchSignalHorn)
            && failure.Length == 0,
            "Signal-horn rally did not return its exact wear context.");
        VerifyEncodedOwnerResult(evidence, DurableToolItemRules.WatchSignalHorn);
        VerifyLastAssignment(
            slots,
            InvasionSignalHornDurableEquipmentPolicySource.LogicalOwnerDomain,
            SignalPostId,
            DurableToolItemRules.WatchSignalHorn);
        Require(use.LastRequirementId ==
                InvasionSignalHornDurableEquipmentPolicySource.RequirementId
            && Math.Abs(use.LastWear
                - InvasionSignalHornDurableEquipmentRuntime.WearPerRally)
                <= 0.000001d,
            "Signal-horn wear contract drifted.");

        Require(!runtime.TryCommitRally(
                SignalPostId,
                position,
                () => false,
                out failure)
            && failure == "invasion-signal-horn-rally-rejected",
            "A rejected signal-horn rally was reported as committed.");
    }

    private static void VerifyLostOwnersCloseBothSlots()
    {
        RecordingSlots slots = new();
        RunAdministrativeSealDurableEquipmentRuntime administration = new(
            CreatePolicies(),
            slots,
            new RecordingUse(slots));
        InvasionSignalHornDurableEquipmentRuntime invasion = new(
            CreatePolicies(),
            slots,
            new RecordingUse(slots));
        Require(administration.TryCommitResolution(
                OfficeId,
                new Vector2Int(11, 4),
                () => true,
                out _),
            "Lifecycle fixture could not create the administration slot.");
        Require(invasion.TryEnsureReady(
                SignalPostId,
                new Vector2Int(19, 5),
                out _),
            "Lifecycle fixture could not create the invasion slot.");

        RunInvasionDurableEquipmentLifecycleRuntime lifecycle = new(
            new EmptyBuildingWorld(),
            slots,
            slots);
        lifecycle.Start();
        Require(slots.CloseCalls == 2
            && slots.CaptureAll().Count == 0,
            "Lost event-tool facilities did not close both slots exactly once.");
        lifecycle.ValidateBeforeCapture();
    }

    private static DurableFacilityEquipmentPolicyRegistry CreatePolicies() =>
        new(new IDurableFacilityEquipmentPolicySource[]
        {
            new RunAdministrativeSealDurableEquipmentPolicySource(),
            new InvasionSignalHornDurableEquipmentPolicySource()
        });

    private static void VerifyPolicy(
        IDurableFacilityEquipmentPolicyQuery policies,
        string policyId,
        string ownerDomain,
        string requirementId,
        string itemId)
    {
        Require(policies.TryGetPolicy(
                policyId,
                out DurableFacilityEquipmentPolicy policy)
            && policy.LogicalOwnerDomain == ownerDomain
            && policy.Requirements.Count == 1
            && policy.Requirements[0].RequirementId == requirementId
            && policy.Requirements[0].ItemId.Equals((ItemDefinitionId)itemId)
            && policy.Requirements[0].RequiredQuantity == 1,
            "Event-tool durable-equipment policy lost exact identity: " + policyId);
    }

    private static void VerifyAuthoredMass(string path, string itemId)
    {
        ResourceItemDefinitionSO item =
            AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>(path);
        Require(item != null
            && item.StableId.Equals((ItemDefinitionId)itemId)
            && PhysicalMassGrams.FromCanonicalKilograms(item.UnitWeight).Value
                == 2_350L
            && item.MaxStack == 1,
            "Event-tool authored mass/max-stack drifted: " + itemId);
    }

    private static void VerifyLastAssignment(
        RecordingSlots slots,
        string ownerDomain,
        BuildingInstanceId facilityId,
        string itemId)
    {
        DurableFacilityEquipmentAssignment assignment = slots.LastAssignment;
        Require(assignment != null
            && assignment.Key.LogicalOwnerDomain == ownerDomain
            && assignment.Key.OwnerSubjectId == facilityId.Value
            && assignment.OwnerFacilityId.Equals(facilityId)
            && assignment.Requirements.Count == 1
            && assignment.Requirements[0].ItemId.Equals((ItemDefinitionId)itemId)
            && assignment.Requirements[0].RequiredQuantity == 1,
            "Event-tool assignment lost exact facility/item/quantity identity.");
    }

    private static void VerifyEncodedOwnerResult(
        DurableFacilityEquipmentUseEvidence evidence,
        string expectedItemId)
    {
        var operation = new MigratedProducerOutcomeSubject(
            MigratedProducerOutcomeIds.OperationKind,
            "operation:g11-owner-result",
            "operation:g11-owner-result",
            MigratedProducerOutcomeIds.OperationRole);
        MigratedProducerOutcomeDetails encoded =
            DurableFacilityEquipmentOutcomeEncoding.Append(
                new MigratedProducerOutcomeDetails(
                    new[] { operation },
                    Array.Empty<GameplayOutcomeMetric>(),
                    Array.Empty<GameplayOutcomeFact>(),
                    "G11 durable owner result"),
                evidence);
        Require(encoded.Subjects.Length == 2
            && encoded.Subjects[1].EntityId.Kind.Equals(
                MigratedProducerOutcomeIds.ItemStackKind)
            && encoded.Subjects[1].Role.Equals(
                MigratedProducerOutcomeIds.EquipmentRole)
            && encoded.Facts.Any(value => value.FactId.Equals(
                    MigratedProducerOutcomeIds.EquipmentDefinitionFact)
                && string.Equals(
                    value.Value,
                    expectedItemId,
                    StringComparison.Ordinal))
            && encoded.Facts.Any(value => value.FactId.Equals(
                    MigratedProducerOutcomeIds.DetailFact)
                && value.Value.StartsWith(
                    "semantics=wear;item=" + expectedItemId,
                    StringComparison.Ordinal)
                && value.Value.Contains(
                    ";quantity=1->1;",
                    StringComparison.Ordinal)),
            "A durable owner result lost exact wear evidence: "
            + expectedItemId);
    }

    private static string RequireG11ReportPath()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "G11 verification requires its isolated output directory.");
        }
        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "report.json");
        if (File.Exists(path))
            throw new InvalidOperationException("G11 report already exists: " + path);
        return path;
    }

    private static void WriteG11Report(string path, G11Report report)
    {
        using FileStream stream = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);
        using StreamWriter writer = new(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(JsonUtility.ToJson(report, prettyPrint: true));
    }

    [Serializable]
    private sealed class G11Report
    {
        public string rowId = "G11";
        public bool passed;
        public string failure = string.Empty;
        public bool commonCommittedContext;
        public bool wearSeparatedFromConsumption;
        public bool effectFailureRollback;
        public bool administrationOwnerResult;
        public bool signalOwnerResult;
        public bool reproductionOwnerResult;
        public bool climatePairOwnerResult;
        public bool secondToolFailureRollback;
        public bool drainSaveRestoreResume;
        public bool fullAuditExecuted;
        public bool humanApprovalRecorded;
        public bool trainingExecuted;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class EmptyBuildingWorld : IBuildingWorldQuery
    {
        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
    }

    private sealed class RecordingSlots :
        IDurableFacilityEquipmentSlotCommand,
        IDurableFacilityEquipmentSlotQuery
    {
        private readonly Dictionary<DurableFacilityEquipmentSlotKey,
            DurableFacilityEquipmentSlotSnapshot> snapshots = new();

        internal DurableFacilityEquipmentAssignment LastAssignment { get; private set; }
        internal int CloseCalls { get; private set; }

        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired)
        {
            LastAssignment = desired;
            DurableFacilityEquipmentSlotSnapshot snapshot = CreateSnapshot(desired);
            snapshots[desired.Key] = snapshot;
            return Success(DurableFacilityEquipmentSlotStatus.Applied, snapshot);
        }

        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key)
        {
            Require(snapshots.TryGetValue(key, out var snapshot),
                "Event-tool supply targeted a missing slot.");
            return Success(DurableFacilityEquipmentSlotStatus.Replay, snapshot);
        }

        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode)
        {
            Require(snapshots.TryGetValue(key, out var snapshot),
                "Event-tool close targeted a missing slot.");
            snapshots.Remove(key);
            CloseCalls++;
            return Success(DurableFacilityEquipmentSlotStatus.Applied, snapshot);
        }

        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() => Array.Empty<DurableFacilityEquipmentSlotResult>();

        public bool TryCapture(
            DurableFacilityEquipmentSlotKey key,
            out DurableFacilityEquipmentSlotSnapshot snapshot) =>
            snapshots.TryGetValue(key, out snapshot);

        public IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> CaptureAll() =>
            snapshots.Values.OrderBy(value => value.AssignmentSequence).ToArray();

        private static DurableFacilityEquipmentSlotResult Success(
            DurableFacilityEquipmentSlotStatus status,
            DurableFacilityEquipmentSlotSnapshot snapshot) =>
            new(status, snapshot, string.Empty);

        private static DurableFacilityEquipmentSlotSnapshot CreateSnapshot(
            DurableFacilityEquipmentAssignment assignment)
        {
            long sequence = assignment.Key.LogicalOwnerDomain ==
                RunAdministrativeSealDurableEquipmentPolicySource.LogicalOwnerDomain
                    ? 1L
                    : 2L;
            return new DurableFacilityEquipmentSlotSnapshot(
                assignment,
                sequence,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    assignment.Key,
                    sequence),
                DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                    assignment.Key,
                    sequence),
                DurableFacilityEquipmentFingerprint.CreateAssignment(assignment),
                new DurableFacilityEquipmentCapacityProjection(
                    DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                    new PhysicalMassGrams(2_350L),
                    1L,
                    new string('c', 64)),
                assignment.Requirements.Select(requirement =>
                    new DurableFacilityEquipmentRequirementStatus(
                        requirement,
                        pendingQuantity: 0,
                        bufferedUsableQuantity: 1)));
        }
    }

    private sealed class RecordingUse : IDurableFacilityEquipmentUseCommand
    {
        private readonly RecordingSlots slots;

        internal RecordingUse(RecordingSlots slots)
        {
            this.slots = slots;
        }

        internal string LastRequirementId { get; private set; } = string.Empty;
        internal double LastWear { get; private set; }

        public DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
            DurableFacilityEquipmentSlotKey key,
            string requirementId,
            double wearAmount,
            IDurableFacilityEquipmentEffectCommit effect)
        {
            Require(slots.TryCapture(key, out var slot),
                "Event-tool use targeted a missing slot.");
            DurableFacilityEquipmentRequirement requirement = slot.Assignment
                .Requirements.Single(value => value.RequirementId == requirementId);
            LastRequirementId = requirementId;
            LastWear = wearAmount;
            DurableFacilityEquipmentUseSubject before = new(
                "stack:" + requirementId,
                1L,
                requirement.ItemId,
                1,
                new[] { Durability(10d, 10d) });
            if (!effect.TryPreflight(
                    slot,
                    requirement,
                    before,
                    wearAmount,
                    out string failure))
            {
                return Failure(slot, failure);
            }
            DurableFacilityEquipmentUseSubject after = new(
                "stack:" + requirementId,
                2L,
                requirement.ItemId,
                1,
                new[] { Durability(10d - wearAmount, 10d) });
            DurableFacilityEquipmentUseContext context = new(
                slot,
                requirement,
                before,
                after,
                wearAmount);
            if (!effect.TryCommit(context, out failure))
            {
                return Failure(slot, failure);
            }
            return new DurableFacilityEquipmentUseResult(
                DurableFacilityEquipmentUseStatus.Applied,
                slot,
                before.StackId,
                string.Empty,
                committedContext: context);
        }

        private static DurableFacilityEquipmentUseResult Failure(
            DurableFacilityEquipmentSlotSnapshot slot,
            string reason) => new(
            DurableFacilityEquipmentUseStatus.Deferred,
            slot,
            string.Empty,
            reason);

        private static DurableFacilityEquipmentComponentSnapshot Durability(
            double current,
            double maximum) => new(
            ItemInstanceComponentIds.Durability,
            1,
            new[]
            {
                new DurableFacilityEquipmentComponentValueSnapshot(
                    "current",
                    ItemStateValueKind.Decimal,
                    string.Empty,
                    0L,
                    current,
                    false),
                new DurableFacilityEquipmentComponentValueSnapshot(
                    "maximum",
                    ItemStateValueKind.Decimal,
                    string.Empty,
                    0L,
                    maximum,
                    false)
            });
    }
}
