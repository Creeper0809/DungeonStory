using System;
using UnityEditor;
using UnityEngine;

public static class ProductionOutputLifecycleRestoreCandidateIndexDebugScenarios
{
    [MenuItem(
        "DungeonStory/Debug/Economy/Run Production Output Restore Candidate Index Contracts")]
    public static void RunAll()
    {
        VerifyCommitOnlyProjectionHook();
        VerifyCompleteEightSlotLifecycle();
        VerifyInsertionOrderAndCloneIsolation();
        VerifyTerminalProducerProjectionLifecycle();
        VerifyFailLoudBoundariesAndCleanup();
        Debug.Log("Production output restore candidate index scenarios passed.");
    }

    private static void VerifyCommitOnlyProjectionHook()
    {
        ProjectionProbeSection section = new();
        string json = JsonUtility.ToJson(new ProjectionProbePayload
        {
            value = 7
        });
        DungeonGameRestoreReport report = new();

        section.ValidatePayload(json, 1, report);
        Require(section.ProjectionCount == 0,
            "ValidatePayload leaked a restore candidate projection.");

        IDungeonSaveRestoreStage staged = section.StageRestore(json, 1, report);
        Require(section.ProjectionCount == 0,
            "StageRestore build leaked a restore candidate projection.");
        ((IDungeonDiscardableSaveRestoreStage)staged).Discard();
        Require(section.ProjectionCount == 0,
            "Discarded stage published a restore candidate projection.");

        IDungeonSaveRestoreStage committed = section.StageRestore(json, 1, report);
        committed.Commit(report);
        Require(section.ProjectionCount == 1 && section.PublishedValue == 7,
            "Real commit did not publish the normalized projection exactly once.");
    }

    private static void VerifyCompleteEightSlotLifecycle()
    {
        RecordingDrainValidator validator = new();
        ProductionOutputLifecycleRestoreCandidateIndex index = new(validator);
        index.BeginRestoreCandidate();
        SetAllSources(index, reverse: false, out _);
        bool captured = index.TryCapture(
            out ProductionOutputLifecycleRestoreCandidateBundle bundle);
        Require(index.PublishedSourceCount == 8
                && captured
                && bundle != null
                && bundle.ManifestFingerprint.Length == 64,
            "Complete source candidate set was not capturable.");

        index.SetDrain(new DungeonProductionFacilityDestructiveDrainSaveData());
        Require(validator.CallCount == 1
                && string.Equals(
                    validator.LastManifest,
                    bundle.ManifestFingerprint,
                    StringComparison.Ordinal),
            "Drain candidate did not validate the exact eight-slot manifest.");
        index.PublishRestoreCandidate();
        index.CompleteRestoreCandidate();
        Require(!index.IsCandidateActive
                && index.PublishedSourceCount == 0
                && !index.TryCapture(out _),
            "Successful restore left stale normalized candidate references.");

        index.BeginRestoreCandidate();
        SetAllSources(index, reverse: false, out _);
        index.PublishRestoreCandidate();
        index.RollbackPublishedRestoreCandidate();
        Require(!index.IsCandidateActive && index.PublishedSourceCount == 0,
            "Published rollback did not erase the candidate index.");

        index.BeginRestoreCandidate();
        SetAllSources(index, reverse: false, out _);
        index.DiscardRestoreCandidate();
        Require(!index.IsCandidateActive && index.PublishedSourceCount == 0,
            "Unpublished discard did not erase the candidate index.");
    }

    private static void VerifyInsertionOrderAndCloneIsolation()
    {
        ProductionOutputLifecycleRestoreCandidateIndex forward =
            new(new RecordingDrainValidator());
        forward.BeginRestoreCandidate();
        SetAllSources(forward, reverse: false, out ModularFacilityWorldSaveData
            mutableWorld);
        Require(forward.TryCapture(
                out ProductionOutputLifecycleRestoreCandidateBundle first),
            "Forward candidate bundle was incomplete.");
        string frozenManifest = first.ManifestFingerprint;
        mutableWorld.gridWidth = 999;
        Require(forward.TryCapture(
                out ProductionOutputLifecycleRestoreCandidateBundle afterMutation)
                && string.Equals(
                    frozenManifest,
                    afterMutation.ManifestFingerprint,
                    StringComparison.Ordinal),
            "Candidate index retained a mutable DTO reference.");

        ProductionOutputLifecycleRestoreCandidateIndex reverse =
            new(new RecordingDrainValidator());
        reverse.BeginRestoreCandidate();
        SetAllSources(reverse, reverse: true, out _);
        Require(reverse.TryCapture(
                out ProductionOutputLifecycleRestoreCandidateBundle reversed)
                && string.Equals(
                    frozenManifest,
                    reversed.ManifestFingerprint,
                    StringComparison.Ordinal),
            "Candidate manifest depends on section publication order.");

        forward.DiscardRestoreCandidate();
        reverse.DiscardRestoreCandidate();
    }

    private static void VerifyFailLoudBoundariesAndCleanup()
    {
        ProductionOutputLifecycleRestoreCandidateIndex index =
            new(new RecordingDrainValidator());
        RequireThrows(
            () => index.SetWorld(new ModularFacilityWorldSaveData()),
            "outside the active transaction");

        index.BeginRestoreCandidate();
        index.SetWorld(new ModularFacilityWorldSaveData());
        RequireThrows(
            () => index.SetWorld(new ModularFacilityWorldSaveData()),
            "more than once");
        index.DiscardRestoreCandidate();

        index.BeginRestoreCandidate();
        index.SetWorld(new ModularFacilityWorldSaveData());
        RequireThrows(index.PublishRestoreCandidate, "incomplete");
        index.DiscardRestoreCandidate();

        index.BeginRestoreCandidate();
        SetAllSources(index, reverse: false, out _);
        index.DiscardRestoreCandidate();
        index.BeginRestoreCandidate();
        SetAllSources(index, reverse: false, out _);
        Require(index.TryCapture(out _),
            "A second restore inherited stale slot ownership.");
        index.DiscardRestoreCandidate();

        RecordingDrainValidator fault = new() { ThrowOnValidate = true };
        ProductionOutputLifecycleRestoreCandidateIndex faulted = new(fault);
        faulted.BeginRestoreCandidate();
        SetAllSources(faulted, reverse: false, out _);
        RequireThrows(
            () => faulted.SetDrain(
                new DungeonProductionFacilityDestructiveDrainSaveData()),
            "injected drain validation fault");
        faulted.DiscardRestoreCandidate();
        Require(!faulted.IsCandidateActive
                && faulted.PublishedSourceCount == 0,
            "Drain validation failure leaked candidate state.");
    }

    private static void VerifyTerminalProducerProjectionLifecycle()
    {
        ProductionOutputLifecycleRestoreCandidateIndex index =
            new(new RecordingDrainValidator());
        index.BeginRestoreCandidate();
        SetAllSources(index, reverse: false, out _);
        DungeonProductionGenericBillTerminalDrainSaveData producer = new()
        {
            version = DungeonProductionGenericBillTerminalDrainSaveData
                .CurrentVersion
        };
        index.SetGenericTerminalDrains(producer);
        DungeonCombatEquipmentTerminalDrainSaveData combatProducer = new()
        {
            version = DungeonCombatEquipmentTerminalDrainSaveData.CurrentVersion
        };
        DungeonProductionApparelOrderTerminalDrainSaveData apparelProducer =
            new()
            {
                version =
                    DungeonProductionApparelOrderTerminalDrainSaveData
                        .CurrentVersion
            };
        index.SetCombatTerminalDrains(combatProducer);
        index.SetApparelTerminalDrains(apparelProducer);
        producer.version = 0;
        combatProducer.version = 0;
        apparelProducer.version = 0;

        Require(index.IsGenericTerminalDrainCandidateAvailable
                && index.TryCaptureGenericTerminalDrains(
                    out DungeonProductionGenericBillTerminalDrainSaveData
                        captured)
                && captured.version ==
                    DungeonProductionGenericBillTerminalDrainSaveData
                        .CurrentVersion
                && captured.entries != null
                && captured.entries.Count == 0,
            "Generic producer candidate was not cloned and published exactly once.");
        Require(index.IsCombatTerminalDrainCandidateAvailable
                && index.TryCaptureCombatTerminalDrains(
                    out DungeonCombatEquipmentTerminalDrainSaveData
                        capturedCombat)
                && capturedCombat.version ==
                    DungeonCombatEquipmentTerminalDrainSaveData.CurrentVersion
                && capturedCombat.entries.Count == 0,
            "Combat producer candidate was not cloned and published exactly once.");
        Require(index.IsApparelTerminalDrainCandidateAvailable
                && index.TryCaptureApparelTerminalDrains(
                    out DungeonProductionApparelOrderTerminalDrainSaveData
                        capturedApparel)
                && capturedApparel.version ==
                    DungeonProductionApparelOrderTerminalDrainSaveData
                        .CurrentVersion
                && capturedApparel.entries.Count == 0,
            "Apparel producer candidate was not cloned and published exactly once.");
        RequireThrows(
            () => index.SetGenericTerminalDrains(new()
            {
                version = DungeonProductionGenericBillTerminalDrainSaveData
                    .CurrentVersion
            }),
            "more than once");
        RequireThrows(
            () => index.SetCombatTerminalDrains(new()
            {
                version = DungeonCombatEquipmentTerminalDrainSaveData
                    .CurrentVersion
            }),
            "more than once");
        RequireThrows(
            () => index.SetApparelTerminalDrains(new()
            {
                version = DungeonProductionApparelOrderTerminalDrainSaveData
                    .CurrentVersion
            }),
            "more than once");

        index.PublishRestoreCandidate();
        index.CompleteRestoreCandidate();
        Require(!index.IsGenericTerminalDrainCandidateAvailable
                && !index.IsCombatTerminalDrainCandidateAvailable
                && !index.IsApparelTerminalDrainCandidateAvailable
                && !index.TryCaptureGenericTerminalDrains(out _)
                && !index.TryCaptureCombatTerminalDrains(out _)
                && !index.TryCaptureApparelTerminalDrains(out _),
            "Terminal producer candidate leaked after restore completion.");
    }

    private static void SetAllSources(
        IProductionOutputLifecycleRestoreCandidatePublisher publisher,
        bool reverse,
        out ModularFacilityWorldSaveData world)
    {
        world = new ModularFacilityWorldSaveData
        {
            gridWidth = 27,
            gridHeight = 3
        };
        DungeonCharacterWorldSaveData characters = new();
        DungeonPhysicalItemSaveData items = new();
        DungeonProductionBillSaveData production = new();
        ProductionPreparedOutputRoutingSaveData routing = new();
        DungeonCombatEquipmentSaveData combat = new();
        CombatEquipmentMaintenanceSaveData maintenance = new();
        DungeonCharacterEnvironmentSaveData environment = new()
        {
            exposures = Array.Empty<CharacterEnvironmentExposure>(),
            equippedWorkwear = Array.Empty<EnvironmentalWorkwearSaveData>(),
            equippedApparel = Array.Empty<EquippedApparelSaveData>(),
            apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>()
        };

        if (!reverse)
        {
            publisher.SetWorld(world);
            publisher.SetCharacters(characters);
            publisher.SetPhysicalItems(items);
            publisher.SetProduction(production);
            publisher.SetRouting(routing);
            publisher.SetCombat(combat);
            publisher.SetMaintenance(maintenance);
            publisher.SetEnvironment(environment);
            return;
        }

        publisher.SetEnvironment(environment);
        publisher.SetMaintenance(maintenance);
        publisher.SetCombat(combat);
        publisher.SetRouting(routing);
        publisher.SetProduction(production);
        publisher.SetPhysicalItems(items);
        publisher.SetCharacters(characters);
        publisher.SetWorld(world);
    }

    private static void RequireThrows(Action action, string token)
    {
        bool threw = false;
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            threw = exception.Message.Contains(token, StringComparison.Ordinal);
        }

        Require(threw, "Expected fail-loud token was not observed: " + token);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class RecordingDrainValidator :
        IProductionFacilityDestructiveDrainCandidateValidator
    {
        internal int CallCount { get; private set; }
        internal string LastManifest { get; private set; } = string.Empty;
        internal bool ThrowOnValidate { get; set; }

        public void Validate(
            ProductionOutputLifecycleRestoreCandidateBundle bundle,
            DungeonProductionGenericBillTerminalDrainSaveData genericTerminalDrains,
            DungeonCombatEquipmentTerminalDrainSaveData combatTerminalDrains,
            DungeonProductionApparelOrderTerminalDrainSaveData apparelTerminalDrains,
            DungeonProductionFacilityDestructiveDrainSaveData drain)
        {
            CallCount++;
            LastManifest = bundle?.ManifestFingerprint ?? string.Empty;
            if (ThrowOnValidate)
                throw new InvalidOperationException(
                    "injected drain validation fault");
        }
    }

    [Serializable]
    private sealed class ProjectionProbePayload
    {
        public int value;
    }

    private sealed class ProjectionProbeCandidate
    {
        internal ProjectionProbeCandidate(int value)
        {
            Value = value;
        }

        internal int Value { get; }
    }

    private sealed class ProjectionProbeSection :
        DungeonStrictJsonSaveSection<
            ProjectionProbePayload,
            ProjectionProbeCandidate>,
        IDungeonRollbackFreeSaveSection
    {
        internal int ProjectionCount { get; private set; }
        internal int PublishedValue { get; private set; }

        public override string SectionId => "qa.projection-probe";
        public override int SectionVersion => 1;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;

        protected override ProjectionProbePayload CapturePayload() => new();

        protected override ProjectionProbeCandidate BuildRestoreCandidate(
            ProjectionProbePayload payload) => new(payload.value);

        protected override void PublishRestoreCandidateProjection(
            ProjectionProbePayload payload,
            ProjectionProbeCandidate candidate)
        {
            ProjectionCount++;
            PublishedValue = payload.value;
        }

        protected override void PublishRestoreCandidate(
            ProjectionProbeCandidate candidate)
        {
            if (candidate.Value != PublishedValue)
                throw new InvalidOperationException(
                    "Projection and candidate values diverged.");
        }
    }
}
