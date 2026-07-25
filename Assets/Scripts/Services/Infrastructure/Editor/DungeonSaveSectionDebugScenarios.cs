#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class DungeonSaveSectionDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Save/Run V15 Section Contracts")]
    public static void RunFromMenu()
    {
        if (!RunAll(true))
        {
            Debug.LogError("V15 save section contracts failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> failures = new List<string>();
        Verify("dependency order", VerifyDependencyOrder, failures);
        Verify("duplicate id rejected", VerifyDuplicateRejected, failures);
        Verify("missing dependency rejected", VerifyMissingDependencyRejected, failures);
        Verify("cycle rejected", VerifyCycleRejected, failures);
        Verify("capture and restore", VerifyCaptureRestore, failures);

        foreach (string failure in failures)
        {
            Debug.LogError(failure);
        }

        if (failures.Count == 0 && logSuccess)
        {
            Debug.Log("V15 save section contracts passed.");
        }

        return failures.Count == 0;
    }

    public static bool VerifyLiveCapture(out string details)
    {
        details = string.Empty;
        if (!Application.isPlaying)
        {
            details = "PlayMode required";
            return false;
        }

        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
        if (scope == null || scope.Container == null)
        {
            details = "gameplay scope missing";
            return false;
        }

        DungeonGameSaveData save = scope.Container.Resolve<IDungeonGameSaveService>().Capture();
        string[] required =
        {
            PhysicalItemsSaveSection.Id,
            WorkOrdersSaveSection.Id,
            WildlifeSaveSection.Id,
            SurvivalResourcesSaveSection.Id,
            DarkSurvivalSaveSection.Id,
            CharacterBodyHealthSaveSection.Id,
            CombatEquipmentSaveSection.Id,
            CharacterMedicalSaveSection.Id,
            DefenseTacticalSaveSection.Id,
            EquipmentMaintenanceSaveSection.Id,
            CharacterCombatCommandSaveSection.Id,
            ExteriorActivitySaveSection.Id,
            ExpeditionEquipmentSaveSection.Id,
            OffenseSaveSection.Id,
            InvasionSaveSection.Id,
            OperatingDaySettlementSaveSection.Id,
            EventAlertSaveSection.Id,
            RunFlowSaveSection.Id,
            DungeonDebugSaveSection.Id,
            BlueprintResearchSaveSection.Id,
            FacilityShopSaveSection.Id,
            RegularCustomerSaveSection.Id,
            StaffDiscontentSaveSection.Id,
            CodexSaveSection.Id,
            RunVariableSaveSection.Id,
            MetaProgressionSaveSection.Id,
            ModularFacilityWorldSaveSection.Id,
            CharacterWorldSaveSection.Id
        };
        string ids = string.Join(",", save.sections.Select(section => section.sectionId));
        details = $"version={save.version}; sections={save.sections.Count}; ids={ids}";
        return save.version == DungeonGameSaveData.CurrentVersion
            && required.All(id => save.sections.Any(section =>
                string.Equals(section.sectionId, id, StringComparison.Ordinal)));
    }

    public static bool VerifyLiveRoundTrip(out string details)
    {
        details = string.Empty;
        if (!Application.isPlaying)
        {
            details = "PlayMode required";
            return false;
        }

        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
        if (scope == null || scope.Container == null)
        {
            details = "gameplay scope missing";
            return false;
        }

        IDungeonGameSaveService saveService =
            scope.Container.Resolve<IDungeonGameSaveService>();
        IWorldItemStackRuntime itemRuntime =
            scope.Container.Resolve<IWorldItemStackRuntime>();
        DungeonGameSaveData baseline = saveService.Capture();
        try
        {
            if (!itemRuntime.SpawnStockAtDropoff(
                    StockCategory.General,
                    3,
                    "V15 왕복 검증",
                    out int spawned)
                || spawned != 3)
            {
                details = $"test item spawn failed: {spawned}";
                return false;
            }

            DungeonGameSaveData before = saveService.Capture();
            if (!saveService.TryRestore(before, out DungeonGameRestoreReport report)
                || report == null
                || !report.Success)
            {
                details = report == null
                    ? "restore report missing"
                    : string.Join(" | ", report.Errors);
                return false;
            }

            DungeonGameSaveData after = saveService.Capture();
            DungeonPhysicalItemSaveData baselineItems =
                DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(
                    baseline,
                    PhysicalItemsSaveSection.Id);
            DungeonPhysicalItemSaveData beforePhysicalItems =
                DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(
                    before,
                    PhysicalItemsSaveSection.Id);
            DungeonPhysicalItemSaveData afterPhysicalItems =
                DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(
                    after,
                    PhysicalItemsSaveSection.Id);
            string[] beforeIds = before.sections
                .Select(section => section.sectionId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            string[] afterIds = after.sections
                .Select(section => section.sectionId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            string beforeItems = BuildItemSignature(beforePhysicalItems);
            string afterItems = BuildItemSignature(afterPhysicalItems);
            details =
                $"version={before.version}->{after.version}; "
                + $"sections={string.Join(",", afterIds)}; "
                + $"itemStacks={beforePhysicalItems.stacks.Count}->{afterPhysicalItems.stacks.Count}; "
                + $"sequence={beforePhysicalItems.nextStackSequence}->{afterPhysicalItems.nextStackSequence}";
            return before.version == DungeonGameSaveData.CurrentVersion
                && after.version == DungeonGameSaveData.CurrentVersion
                && beforeIds.SequenceEqual(afterIds)
                && beforePhysicalItems.stacks.Count > baselineItems.stacks.Count
                && string.Equals(beforeItems, afterItems, StringComparison.Ordinal)
                && beforePhysicalItems.nextStackSequence == afterPhysicalItems.nextStackSequence;
        }
        finally
        {
            saveService.TryRestore(baseline, out _);
        }
    }

    private static string BuildItemSignature(DungeonPhysicalItemSaveData items)
    {
        return string.Join(
            "|",
            (items?.stacks ?? new List<WorldItemStackSaveData>())
                .OrderBy(stack => stack.stackId, StringComparer.Ordinal)
                .Select(stack =>
                    $"{stack.stackId}:{stack.itemId}:{stack.quantity}:{stack.state}:"
                    + $"{stack.gridX},{stack.gridY}:{stack.reservedByPersistentId}:"
                    + $"{stack.destinationId}:{stack.forbidden}"));
    }

    private static bool VerifyDependencyOrder()
    {
        FakeSection items = new FakeSection(
            "items",
            DungeonSaveRestorePhase.Items);
        FakeSection work = new FakeSection(
            "work",
            DungeonSaveRestorePhase.RuntimeState,
            "items");
        FakeSection survival = new FakeSection(
            "survival",
            DungeonSaveRestorePhase.LateRuntimeState,
            "work");
        DungeonSaveSectionRegistry registry =
            new DungeonSaveSectionRegistry(new IDungeonSaveSection[]
            {
                survival,
                work,
                items
            });

        return string.Join(",", registry.OrderedSections.Select(section => section.SectionId))
            == "items,work,survival";
    }

    private static bool VerifyDuplicateRejected()
    {
        return Throws(() => new DungeonSaveSectionRegistry(new IDungeonSaveSection[]
        {
            new FakeSection("same", DungeonSaveRestorePhase.Items),
            new FakeSection("same", DungeonSaveRestorePhase.Items)
        }));
    }

    private static bool VerifyMissingDependencyRejected()
    {
        return Throws(() => new DungeonSaveSectionRegistry(new IDungeonSaveSection[]
        {
            new FakeSection("work", DungeonSaveRestorePhase.RuntimeState, "items")
        }));
    }

    private static bool VerifyCycleRejected()
    {
        return Throws(() => new DungeonSaveSectionRegistry(new IDungeonSaveSection[]
        {
            new FakeSection("a", DungeonSaveRestorePhase.RuntimeState, "b"),
            new FakeSection("b", DungeonSaveRestorePhase.RuntimeState, "a")
        }));
    }

    private static bool VerifyCaptureRestore()
    {
        List<string> restored = new List<string>();
        FakeSection first = new FakeSection(
            "first",
            DungeonSaveRestorePhase.Items,
            restored: restored);
        FakeSection second = new FakeSection(
            "second",
            DungeonSaveRestorePhase.RuntimeState,
            new[] { "first" },
            restored);
        DungeonSaveSectionRegistry registry =
            new DungeonSaveSectionRegistry(new IDungeonSaveSection[] { second, first });
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();

        return envelopes.Count == 2
            && registry.RestoreAll(envelopes, report)
            && report.Success
            && string.Join(",", restored) == "first,second";
    }

    private static void Verify(
        string name,
        Func<bool> scenario,
        ICollection<string> failures)
    {
        try
        {
            if (!scenario())
            {
                failures.Add(name);
            }
        }
        catch (Exception exception)
        {
            failures.Add($"{name}: {exception.Message}");
        }
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class FakeSection : IDungeonSaveSection
    {
        private readonly IReadOnlyList<string> dependencies;
        private readonly ICollection<string> restored;

        public FakeSection(
            string id,
            DungeonSaveRestorePhase phase,
            string dependency)
            : this(id, phase, new[] { dependency }, null)
        {
        }

        public FakeSection(
            string id,
            DungeonSaveRestorePhase phase,
            IReadOnlyList<string> dependencies = null,
            ICollection<string> restored = null)
        {
            SectionId = id;
            RestorePhase = phase;
            this.dependencies = dependencies ?? Array.Empty<string>();
            this.restored = restored;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => dependencies;

        public string Capture()
        {
            return $"{{\"id\":\"{SectionId}\"}}";
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion)
            {
                report.AddError("version mismatch");
                return;
            }

            restored?.Add(SectionId);
        }
    }
}
#endif
