#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class ExteriorActivityDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Exterior/Run Exterior Activity Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("Exterior activity scenarios failed.");
        }
    }

    [MenuItem("DungeonStory/Debug/Exterior/Run Exterior Activity PlayMode Snapshot")]
    public static void RunPlayModeSnapshotFromMenu()
    {
        bool success = RunPlayModeRuntimeSnapshot(true);
        if (!success)
        {
            Debug.LogError("Exterior activity PlayMode snapshot failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("save V19 contains exterior snapshot", VerifySaveV19ContainsExteriorSnapshot, errors);
        RunScenario("reception work type is registered", VerifyReceptionWorkTypeIsRegistered, errors);
        RunScenario("exterior runtime state is not a ScriptableObject", VerifyRuntimeStateIsNotScriptableObject, errors);
        RunScenario("exterior ability contracts expose expected work types", VerifyExteriorAbilityContracts, errors);
        RunScenario(
            "incident Aggregate owns handler time and stage transitions",
            VerifyIncidentAggregateAuthorityAgreement,
            errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Exterior activity scenarios passed.");
        }

        return true;
    }

    public static bool RunPlayModeRuntimeSnapshot(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("playmode runtime services and zones", VerifyPlayModeRuntimeServicesAndZones, errors);
        RunScenario("playmode reception work candidate", VerifyPlayModeReceptionWorkCandidate, errors);
        RunScenario("playmode incident and save capture", VerifyPlayModeIncidentAndSaveCapture, errors);
        RunScenario(
            "playmode incident handler transition agrees with query capture restore",
            VerifyIncidentHandlerTransitionAgreement,
            errors);
        RunScenario(
            "playmode invalid exterior preflight preserves live zones",
            VerifyInvalidExteriorPreflightPreservesLiveZones,
            errors);
        RunScenario(
            "playmode exterior roundtrip publishes detached zones",
            VerifyExteriorRoundtripPublishesDetachedZones,
            errors);
        RunScenario(
            "playmode later failure discards exterior candidate",
            VerifyLaterFailureDiscardsExteriorCandidate,
            errors);

        if (errors.Count > 0)
        {
            foreach (string error in errors)
            {
                Debug.LogError(error);
            }

            return false;
        }

        if (logSuccess)
        {
            Debug.Log("Exterior activity PlayMode snapshot passed.");
        }

        return true;
    }

    private static bool VerifySaveV19ContainsExteriorSnapshot()
    {
        DungeonGameSaveData save = new DungeonGameSaveData();
        DungeonSaveSectionPayload.Write(
            save,
            ExteriorActivitySaveSection.Id,
            DungeonExteriorActivitySaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            new DungeonExteriorActivitySaveData());
        DungeonExteriorActivitySaveData exterior =
            DungeonSaveSectionPayload.ReadOrNew<DungeonExteriorActivitySaveData>(
                save,
                ExteriorActivitySaveSection.Id);
        return DungeonGameSaveData.CurrentVersion == 23
            && save.version == DungeonGameSaveData.CurrentVersion
            && exterior.version == DungeonExteriorActivitySaveData.CurrentVersion;
    }

    private static bool VerifyReceptionWorkTypeIsRegistered()
    {
        bool resolved = WorkTypeCatalog.TryGet(BuiltInWorkTypeIds.Reception, out WorkTypeDefinition task);
        WorkPriorityProfile priorities = WorkPriorityProfile.CreateDefault();
        return resolved
            && task != null
            && task.WorkTypeId == BuiltInWorkTypeIds.Reception
            && priorities.GetPriority(BuiltInWorkTypeIds.Reception) == WorkPriorityLevel.Priority2;
    }

    private static bool VerifyRuntimeStateIsNotScriptableObject()
    {
        return !typeof(ExteriorActivityRuntime).IsSubclassOf(typeof(ScriptableObject))
            && !typeof(ExteriorZoneMarker).IsSubclassOf(typeof(ScriptableObject))
            && typeof(MonoBehaviour).IsAssignableFrom(typeof(ExteriorZoneMarker));
    }

    private static bool VerifyExteriorAbilityContracts()
    {
        IBuildingExteriorWorkRuntimeAbility reception = new BuildingReceptionAbility();
        IBuildingExteriorWorkRuntimeAbility patrol = new BuildingPatrolPostAbility();
        IBuildingExteriorWorkRuntimeAbility rest = new BuildingOutdoorRestAbility();
        IBuildingExteriorWorkRuntimeAbility maintenance = new BuildingExteriorMaintenanceAbility();
        return reception.SupportsExteriorWork(BuiltInWorkTypeIds.Reception)
            && patrol.SupportsExteriorWork(BuiltInWorkTypeIds.Guard)
            && rest.SupportsExteriorWork(BuiltInWorkTypeIds.Rest)
            && maintenance.SupportsExteriorWork(BuiltInWorkTypeIds.Clean)
            && maintenance.SupportsExteriorWork(BuiltInWorkTypeIds.Repair)
            && !maintenance.SupportsExteriorWork(BuiltInWorkTypeIds.Reception);
    }

    private static bool VerifyIncidentAggregateAuthorityAgreement()
    {
        ExteriorIncidentRuntimeState state = new ExteriorIncidentRuntimeState
        {
            incidentId = "incident:Thief:1",
            kind = ExteriorIncidentKind.Thief,
            zoneId = "exterior:DropZone:1:1",
            stage = ExteriorIncidentStage.Active,
            durationSeconds = 10f,
            remainingSeconds = 10f
        };
        DungeonStory.Exterior.ExteriorIncidentAggregate<
            ExteriorIncidentRuntimeState> aggregate = CreateIncidentAggregate();
        aggregate.Add(state);
        DungeonStory.Exterior.ExteriorIncidentTransition<
            ExteriorIncidentRuntimeState> transition = aggregate.Tick(
                2f,
                (current, _) =>
                {
                    current.remainingSeconds = 5f;
                    current.stage = ExteriorIncidentStage.Interacting;
                }).Single();

        ExteriorIncidentRuntimeState queried = aggregate.States.Single();
        DungeonExteriorActivitySaveData captured =
            new DungeonExteriorActivitySaveData
            {
                incidentStates = aggregate.States
                    .Select(current => current.Clone())
                    .ToList()
            };
        DungeonStory.Exterior.ExteriorIncidentAggregate<
            ExteriorIncidentRuntimeState> restoredAggregate =
            CreateIncidentAggregate();
        restoredAggregate.ReplaceAll(
            captured.incidentStates.Select(current => current.Clone()));
        ExteriorIncidentRuntimeState restored =
            restoredAggregate.States.Single();
        bool restoredMatchesCapturedState =
            restored.stage == queried.stage
            && Mathf.Approximately(
                restored.remainingSeconds,
                queried.remainingSeconds);
        DungeonStory.Exterior.ExteriorIncidentTransition<
            ExteriorIncidentRuntimeState> resolved = restoredAggregate.Mutate(
                restored,
                current =>
                {
                    current.remainingSeconds = 0f;
                    current.stage = ExteriorIncidentStage.Resolved;
                });

        return Mathf.Approximately(transition.RemainingSeconds, 5f)
            && queried.stage == ExteriorIncidentStage.Interacting
            && Mathf.Approximately(queried.remainingSeconds, 5f)
            && restoredMatchesCapturedState
            && resolved.IsTerminal
            && restoredAggregate.ActiveCount == 0
            && typeof(ExteriorZoneMarker).GetMethod(
                "TickIncident",
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic) == null;
    }

    private static DungeonStory.Exterior.ExteriorIncidentAggregate<
        ExteriorIncidentRuntimeState> CreateIncidentAggregate()
    {
        return new DungeonStory.Exterior.ExteriorIncidentAggregate<
            ExteriorIncidentRuntimeState>(
                current => current.IsTerminal,
                current => current.remainingSeconds,
                (current, remainingSeconds) =>
                    current.remainingSeconds = remainingSeconds);
    }

    private static bool VerifyPlayModeRuntimeServicesAndZones()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        IExteriorZoneQuery query = scope.Container.Resolve<IExteriorZoneQuery>();
        ExteriorActivityOverviewSnapshot overview = query.GetOverview();
        HashSet<ExteriorZoneType> types = query.Zones.Select(zone => zone.ZoneType).ToHashSet();
        return overview.ZoneCount >= 7
            && types.Contains(ExteriorZoneType.DropZone)
            && types.Contains(ExteriorZoneType.ReceptionPoint)
            && types.Contains(ExteriorZoneType.GuardPost)
            && types.Contains(ExteriorZoneType.PatrolPoint)
            && types.Contains(ExteriorZoneType.OutdoorRestSpot)
            && types.Contains(ExteriorZoneType.ExpeditionStaging)
            && types.Contains(ExteriorZoneType.IncidentPoint);
    }

    private static bool VerifyPlayModeIncidentAndSaveCapture()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        IExteriorIncidentRuntime incidentRuntime = scope.Container.Resolve<IExteriorIncidentRuntime>();
        IExteriorActivityRuntime exteriorRuntime = scope.Container.Resolve<IExteriorActivityRuntime>();
        IExperiencePacingRuntime pacing = scope.Container.Resolve<IExperiencePacingRuntime>();
        IDungeonGameSaveService saveService = scope.Container.Resolve<IDungeonGameSaveService>();
        DungeonGameSaveData baseline = saveService.Capture();
        bool scenarioPassed = false;
        bool baselineRestored = false;
        try
        {
            pacing.AdvanceToDay(Math.Max(31, pacing.CurrentDay));
            bool started = incidentRuntime.TryStartIncident(
                ExteriorIncidentKind.Thief,
                "수상한 그림자가 하차장 근처를 맴돕니다.");
            DungeonExteriorActivitySaveData exterior = exteriorRuntime.Capture();
            DungeonGameSaveData save = saveService.Capture();
            DungeonExteriorActivitySaveData savedExterior =
                DungeonSaveSectionPayload.ReadOrNew<DungeonExteriorActivitySaveData>(
                    save,
                    ExteriorActivitySaveSection.Id);
            scenarioPassed = started
                && exterior.zones.Count >= 7
                && exterior.incidentStates.Count >= 1
                && save.version == DungeonGameSaveData.CurrentVersion
                && savedExterior.zones.Count >= 7
                && savedExterior.incidentStates.Count >= 1;
        }
        finally
        {
            baselineRestored = saveService.TryRestore(
                    baseline,
                    out DungeonGameRestoreReport restoreReport)
                && restoreReport.Success;
        }

        return scenarioPassed && baselineRestored;
    }

    private static bool VerifyIncidentHandlerTransitionAgreement()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope?.Container == null)
        {
            return false;
        }

        ExteriorActivityRuntime runtime =
            scope.Container.Resolve<ExteriorActivityRuntime>();
        ExteriorIncidentHandlerRegistry registry =
            scope.Container.Resolve<ExteriorIncidentHandlerRegistry>();
        IDungeonGameSaveService saveService =
            scope.Container.Resolve<IDungeonGameSaveService>();
        IExperiencePacingRuntime pacing =
            scope.Container.Resolve<IExperiencePacingRuntime>();
        FieldInfo handlersField = typeof(ExteriorIncidentHandlerRegistry)
            .GetField(
                "handlers",
                BindingFlags.Instance | BindingFlags.NonPublic);
        Dictionary<ExteriorIncidentKind, IExteriorIncidentHandler> handlers =
            handlersField?.GetValue(registry)
                as Dictionary<ExteriorIncidentKind, IExteriorIncidentHandler>;
        if (handlers == null
            || !handlers.TryGetValue(
                ExteriorIncidentKind.Thief,
                out IExteriorIncidentHandler originalHandler))
        {
            return false;
        }

        DungeonGameSaveData baseline = saveService.Capture();
        bool baselineRestored = false;
        try
        {
            handlers[ExteriorIncidentKind.Thief] =
                new IncidentAuthorityProbeHandler();
            pacing.AdvanceToDay(Math.Max(31, pacing.CurrentDay));
            if (!runtime.TryStartIncident(
                    ExteriorIncidentKind.Thief,
                    "incident authority probe"))
            {
                return false;
            }

            ExteriorIncidentRuntimeState started = runtime.IncidentStates
                .Single(state => !state.IsTerminal
                    && state.kind == ExteriorIncidentKind.Thief);
            typeof(ExteriorActivityRuntime)
                .GetMethod(
                    "TickIncidentStates",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(runtime, new object[] { 2f });

            ExteriorIncidentRuntimeState queried = runtime.IncidentStates
                .Single(state => state.incidentId == started.incidentId);
            ExteriorIncidentSaveData active = runtime.ActiveIncidents
                .Single(state => state.incidentId == started.incidentId);
            ExteriorIncidentRuntimeState captured = runtime.Capture()
                .incidentStates
                .Single(state => state.incidentId == started.incidentId);
            ExteriorZoneMarker marker = runtime.Zones
                .Single(zone => zone.ZoneId == queried.zoneId);
            bool transitionAgrees =
                queried.stage == ExteriorIncidentStage.Interacting
                && Mathf.Approximately(queried.remainingSeconds, 37f)
                && Mathf.Approximately(active.remainingSeconds, 37f)
                && captured.stage == queried.stage
                && Mathf.Approximately(
                    captured.remainingSeconds,
                    queried.remainingSeconds)
                && marker.ActiveIncidentId == queried.incidentId
                && Mathf.Approximately(
                    marker.IncidentRemainingSeconds,
                    queried.remainingSeconds);
            if (!transitionAgrees)
            {
                return false;
            }

            DungeonGameSaveData snapshot = saveService.Capture();
            if (!saveService.TryRestore(
                    snapshot,
                    out DungeonGameRestoreReport restoreReport)
                || !restoreReport.Success)
            {
                return false;
            }

            ExteriorIncidentRuntimeState restored = runtime.IncidentStates
                .Single(state => state.incidentId == queried.incidentId);
            ExteriorIncidentRuntimeState recaptured = runtime.Capture()
                .incidentStates
                .Single(state => state.incidentId == queried.incidentId);
            ExteriorZoneMarker restoredMarker = runtime.Zones
                .Single(zone => zone.ZoneId == restored.zoneId);
            return restored.stage == ExteriorIncidentStage.Interacting
                && Mathf.Approximately(restored.remainingSeconds, 37f)
                && recaptured.stage == restored.stage
                && Mathf.Approximately(
                    recaptured.remainingSeconds,
                    restored.remainingSeconds)
                && restoredMarker.ActiveIncidentId == restored.incidentId
                && Mathf.Approximately(
                    restoredMarker.IncidentRemainingSeconds,
                    restored.remainingSeconds);
        }
        finally
        {
            handlers[ExteriorIncidentKind.Thief] = originalHandler;
            baselineRestored = saveService.TryRestore(
                    baseline,
                    out DungeonGameRestoreReport restoreReport)
                && restoreReport.Success;
            if (!baselineRestored)
            {
                Debug.LogError(
                    "Exterior incident authority probe could not restore its baseline.");
            }
        }
    }

    private static bool VerifyInvalidExteriorPreflightPreservesLiveZones()
    {
        if (!TryResolveSaveScenario(
                out ExteriorActivityRuntime exteriorRuntime,
                out IDungeonGameSaveService saveService,
                out _))
        {
            return false;
        }

        ExteriorZoneMarker[] before = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        if (before.Length == 0)
        {
            return false;
        }

        DungeonGameSaveData invalid = saveService.Capture();
        if (!DungeonSaveSectionPayload.TryRead(
                invalid,
                ExteriorActivitySaveSection.Id,
                out DungeonExteriorActivitySaveData payload)
            || payload.zones.Count == 0)
        {
            return false;
        }

        payload.zones[0].buildingInstanceId =
            "invalid-exterior-building-id";
        DungeonSaveSectionPayload.Write(
            invalid,
            ExteriorActivitySaveSection.Id,
            DungeonExteriorActivitySaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState,
            payload);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);

        bool restored = saveService.TryRestore(
            invalid,
            out DungeonGameRestoreReport report);
        ExteriorZoneMarker[] after = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        return !restored
            && !report.Success
            && before.SequenceEqual(after)
            && after.All(zone => zone.gameObject.activeSelf
                && !zone.IsDetachedRestoreCandidate);
    }

    private static bool VerifyExteriorRoundtripPublishesDetachedZones()
    {
        if (!TryResolveSaveScenario(
                out ExteriorActivityRuntime exteriorRuntime,
                out IDungeonGameSaveService saveService,
                out DungeonRuntimeLifetimeScope scope))
        {
            return false;
        }

        ExteriorZoneMarker[] before = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        string[] expectedIds = before
            .Select(zone => zone.ZoneId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (expectedIds.Length == 0)
        {
            return false;
        }

        DungeonGameSaveData snapshot = saveService.Capture();
        if (!DungeonSaveSectionPayload.TryRead(
                snapshot,
                ExteriorActivitySaveSection.Id,
                out DungeonExteriorActivitySaveData capturedExterior))
        {
            return false;
        }

        string expectedJson = JsonUtility.ToJson(capturedExterior);
        if (!saveService.TryRestore(
                snapshot,
                out DungeonGameRestoreReport report)
            || !report.Success)
        {
            return false;
        }

        ExteriorZoneMarker[] after = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        string[] restoredIds = after
            .Select(zone => zone.ZoneId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        string restoredJson = JsonUtility.ToJson(exteriorRuntime.Capture());
        IRestoreWorldCandidateQuery candidates =
            scope.Container.Resolve<IRestoreWorldCandidateQuery>();
        return expectedIds.SequenceEqual(restoredIds)
            && string.Equals(
                expectedJson,
                restoredJson,
                StringComparison.Ordinal)
            && before.All(previous => after.All(current =>
                !ReferenceEquals(previous, current)))
            && after.All(zone => zone.gameObject.activeSelf
                && !zone.IsDetachedRestoreCandidate)
            && !candidates.TryGetExteriorZones(out _)
            && report.Warnings.Count == 0;
    }

    private static bool VerifyLaterFailureDiscardsExteriorCandidate()
    {
        if (!TryResolveSaveScenario(
                out ExteriorActivityRuntime exteriorRuntime,
                out _,
                out DungeonRuntimeLifetimeScope scope))
        {
            return false;
        }

        ExteriorZoneMarker[] before = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        IReadOnlyList<ExteriorZoneMarker> previousView = exteriorRuntime.Zones;
        object previousZonesRoot = GetPrivateField<object>(
            exteriorRuntime,
            "zones");
        object previousIncidentRoot = GetPrivateField<object>(
            exteriorRuntime,
            "incidentAggregate");
        int previousIncidentSequence = GetPrivateField<int>(
            exteriorRuntime,
            "incidentSequence");
        float previousConditionTick = GetPrivateField<float>(
            exteriorRuntime,
            "nextConditionTick");
        float previousIncidentCheck = GetPrivateField<float>(
            exteriorRuntime,
            "nextIncidentCheck");
        string previousCapture = JsonUtility.ToJson(exteriorRuntime.Capture());
        string[] expectedIds = before
            .Select(zone => zone.ZoneId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (expectedIds.Length == 0)
        {
            return false;
        }

        IDungeonSaveSectionRegistry liveRegistry =
            scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        IDungeonSaveSection facilitySection = liveRegistry.OrderedSections
            .Single(section =>
                section.SectionId == ModularFacilityWorldSaveSection.Id);
        IDungeonSaveSection exteriorSection = liveRegistry.OrderedSections
            .Single(section =>
                section.SectionId == ExteriorActivitySaveSection.Id);
        MarkerDependencySection runDependency =
            new MarkerDependencySection(RunVariableSaveSection.Id);
        MarkerDependencySection metaDependency =
            new MarkerDependencySection(MetaProgressionSaveSection.Id);
        MarkerDependencySection itemDependency =
            new MarkerDependencySection(PhysicalItemsSaveSection.Id);
        MarkerDependencySection workDependency =
            new MarkerDependencySection(WorkOrdersSaveSection.Id);
        MarkerDependencySection characterDependency =
            new MarkerDependencySection(CharacterWorldSaveSection.Id);
        MarkerDependencySection wildlifeDependency =
            new MarkerDependencySection(WildlifeSaveSection.Id);
        DungeonRuntimeAggregateRootStore isolatedRoot =
            new DungeonRuntimeAggregateRootStore();
        IDungeonRestoreTransactionParticipant facilityParticipant =
            scope.Container.Resolve<IModularFacilityWorldSaveService>()
                as IDungeonRestoreTransactionParticipant;
        if (facilityParticipant == null)
        {
            return false;
        }
        LateExteriorParticipantFaultProbe lateParticipant = new(
            exteriorRuntime,
            before);

        DungeonSaveSectionRegistry testRegistry =
            new DungeonSaveSectionRegistry(
                new IDungeonSaveSection[]
                {
                    runDependency,
                    metaDependency,
                    itemDependency,
                    facilitySection,
                    workDependency,
                    characterDependency,
                    wildlifeDependency,
                    exteriorSection
                },
                isolatedRoot,
                new IDungeonRestoreTransactionParticipant[]
                {
                    facilityParticipant,
                    exteriorRuntime,
                    lateParticipant
                });

        List<DungeonSaveSectionEnvelope> snapshot =
            testRegistry.CaptureAll();
        DungeonSaveSectionEnvelope exteriorEnvelope = snapshot.Single(
            envelope => envelope.sectionId == ExteriorActivitySaveSection.Id);
        DungeonExteriorActivitySaveData exteriorPayload =
            JsonUtility.FromJson<DungeonExteriorActivitySaveData>(
                exteriorEnvelope.payloadJson);
        exteriorPayload.incidentStates.Clear();
        exteriorEnvelope.payloadJson = JsonUtility.ToJson(exteriorPayload);
        IRestoreWorldCandidateQuery candidateIndex =
            scope.Container.Resolve<IRestoreWorldCandidateQuery>();
        IGridSystemProvider gridProvider =
            scope.Container.Resolve<IGridSystemProvider>();
        if (!gridProvider.TryGetGrid(out Grid liveGrid))
        {
            return false;
        }

        int detachedBefore = CountDetachedExteriorCandidates();
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        bool restored = testRegistry.RestoreAll(snapshot, report);
        ExteriorZoneMarker[] after = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        string[] restoredIds = after
            .Select(zone => zone.ZoneId)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        bool sameGrid = gridProvider.TryGetGrid(out Grid afterGrid)
            && ReferenceEquals(liveGrid, afterGrid);
        bool candidateIndexClear = !candidateIndex.TryGetGrid(out _)
            && !candidateIndex.TryGetBuildings(out _)
            && !candidateIndex.TryGetExteriorZones(out _);
        int detachedAfter = CountDetachedExteriorCandidates();
        bool exactRollback = !restored
            && !report.Success
            && report.Errors.Any(error => error.Contains(
                LateExteriorParticipantFaultProbe.FailureMessage,
                StringComparison.Ordinal))
            && lateParticipant.PublishCount == 1
            && lateParticipant.RollbackCount == 1
            && lateParticipant.CompleteCount == 0
            && lateParticipant.ObservedOldActiveAndCandidateHidden
            && before.SequenceEqual(after)
            && ReferenceEquals(previousView, exteriorRuntime.Zones)
            && ReferenceEquals(
                previousZonesRoot,
                GetPrivateField<object>(exteriorRuntime, "zones"))
            && ReferenceEquals(
                previousIncidentRoot,
                GetPrivateField<object>(exteriorRuntime, "incidentAggregate"))
            && previousIncidentSequence == GetPrivateField<int>(
                exteriorRuntime,
                "incidentSequence")
            && Mathf.Approximately(
                previousConditionTick,
                GetPrivateField<float>(exteriorRuntime, "nextConditionTick"))
            && Mathf.Approximately(
                previousIncidentCheck,
                GetPrivateField<float>(exteriorRuntime, "nextIncidentCheck"))
            && string.Equals(
                previousCapture,
                JsonUtility.ToJson(exteriorRuntime.Capture()),
                StringComparison.Ordinal)
            && expectedIds.SequenceEqual(restoredIds)
            && sameGrid
            && candidateIndexClear
            && !isolatedRoot.IsRestoreStaging
            && isolatedRoot.PublishedRestoreRevision == 0
            && detachedAfter == detachedBefore
            && after.All(zone => zone.gameObject.activeSelf
                && !zone.IsDetachedRestoreCandidate);
        if (!exactRollback)
        {
            return false;
        }

        DungeonGameRestoreReport successReport = new DungeonGameRestoreReport();
        bool completed = testRegistry.RestoreAll(snapshot, successReport);
        ExteriorZoneMarker[] completedZones = exteriorRuntime.Zones
            .Where(zone => zone != null)
            .ToArray();
        bool successCandidateIndexClear = !candidateIndex.TryGetGrid(out _)
            && !candidateIndex.TryGetBuildings(out _)
            && !candidateIndex.TryGetExteriorZones(out _);
        return completed
            && successReport.Success
            && lateParticipant.PublishCount == 2
            && lateParticipant.RollbackCount == 1
            && lateParticipant.CompleteCount == 1
            && completedZones.Length == before.Length
            && completedZones.All(zone => before.All(old =>
                !ReferenceEquals(zone, old)))
            && completedZones.All(zone => zone.gameObject.activeSelf
                && !zone.IsDetachedRestoreCandidate)
            && before.All(zone => zone == null || !zone.gameObject.activeSelf)
            && successCandidateIndexClear
            && isolatedRoot.PublishedRestoreRevision == 1;
    }

    private static T GetPrivateField<T>(object target, string fieldName)
    {
        FieldInfo field = target?.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (field == null)
        {
            throw new InvalidOperationException(
                $"Missing private field '{fieldName}' on {target?.GetType().Name ?? "null"}.");
        }

        return (T)field.GetValue(target);
    }

    private static bool TryResolveSaveScenario(
        out ExteriorActivityRuntime exteriorRuntime,
        out IDungeonGameSaveService saveService,
        out DungeonRuntimeLifetimeScope scope)
    {
        exteriorRuntime = null;
        saveService = null;
        scope = null;
        if (!Application.isPlaying)
        {
            return false;
        }

        scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        exteriorRuntime = scope.Container.Resolve<ExteriorActivityRuntime>();
        saveService = scope.Container.Resolve<IDungeonGameSaveService>();
        return exteriorRuntime.Zones.Any(zone => zone != null);
    }

    private static bool VerifyPlayModeReceptionWorkCandidate()
    {
        if (!Application.isPlaying)
        {
            return false;
        }

        DungeonRuntimeLifetimeScope scope = FindScope();
        if (scope == null || scope.Container == null)
        {
            return false;
        }

        IExteriorZoneQuery zoneQuery = scope.Container.Resolve<IExteriorZoneQuery>();
        ExteriorZoneMarker reception = zoneQuery.GetZones(ExteriorZoneType.ReceptionPoint).FirstOrDefault();
        if (reception == null || !reception.CanRunReceptionWork)
        {
            Debug.LogWarning($"Reception marker unavailable. marker={reception != null}, canRun={reception != null && reception.CanRunReceptionWork}");
            return false;
        }

        AbilityWork[] workers = UnityEngine.Object.FindObjectsByType<AbilityWork>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .Where(work => work != null
                && work.WorkerActor != null)
            .ToArray();
        GameObject temporaryWorkerObject = null;
        if (workers.Length == 0
            && TryCreateTemporaryReceptionWorker(zoneQuery, reception, out AbilityWork temporaryWorker, out temporaryWorkerObject))
        {
            workers = new[] { temporaryWorker };
        }

        Debug.Log($"Reception candidate workers={workers.Length}");
        try
        {
            foreach (AbilityWork work in workers)
            {
                work.EnsureWorkReferences();
                Grid grid = work.CachedGrid;
                if (grid == null)
                {
                    Debug.LogWarning($"Reception candidate skipped actor={work.WorkerActor.name} reason=no-grid");
                    continue;
                }

                GridPathSearchResult search = grid.SearchPath(work.WorkerActor.GetNowXY());
                work.SetWorkPriority(BuiltInWorkTypeIds.Reception, WorkPriorityLevel.Priority1, search);
                bool canStart = work.CanStartWorkAction(BuiltInWorkTypeIds.Reception, search);
                bool found = work.TryGetBestWorkCandidate(
                    BuiltInWorkTypeIds.Reception,
                    search,
                    out WorkTargetCandidate candidate);
                ExteriorZoneMarker candidateZone =
                    WorkTargetCandidateRuntimeAdapter
                        .ResolveBuilding<ExteriorZoneMarker>(candidate);
                BuildableObject candidateBuilding =
                    WorkTargetCandidateRuntimeAdapter.ResolveBuilding(candidate);
                bool isRegisteredReceptionZone = candidateZone != null
                    && candidateZone.CanRunReceptionWork
                    && zoneQuery.Zones.Contains(candidateZone);
                bool positionReachable = search.GetReachablePositions().Contains(reception.GridPosition);
                bool buildingReachable = search.GetAllReachableBuilding().Contains(reception);
                Debug.Log(
                    $"Reception candidate probe actor={work.WorkerActor.name} "
                    + $"pos={work.WorkerActor.GetNowXY()} "
                    + $"hasExteriorQuery={work.HasExteriorZoneQuery} "
                    + $"receptionPos={reception.GridPosition} "
                    + $"positionReachable={positionReachable} "
                    + $"buildingReachable={buildingReachable} "
                    + $"canStart={canStart} found={found} "
                    + $"candidateValid={candidate.IsValid} "
                    + $"candidateType={candidate.WorkTypeId} "
                    + $"candidateBuilding={(candidateBuilding != null ? candidateBuilding.name : "null")} "
                    + $"sameReception={candidateBuilding == reception} "
                    + $"registeredReceptionZone={isRegisteredReceptionZone} "
                    + $"lastRejected={work.LastRejectedWorkCandidate.FailureKind}");
                if (canStart
                    && found
                    && candidate.IsValid
                    && candidate.WorkTypeId == BuiltInWorkTypeIds.Reception
                    && isRegisteredReceptionZone)
                {
                    return true;
                }
            }
        }
        finally
        {
            if (temporaryWorkerObject != null)
            {
                UnityEngine.Object.DestroyImmediate(temporaryWorkerObject);
            }
        }

        return false;
    }

    private static bool TryCreateTemporaryReceptionWorker(
        IExteriorZoneQuery zoneQuery,
        ExteriorZoneMarker reception,
        out AbilityWork work,
        out GameObject workerObject)
    {
        work = null;
        workerObject = null;
        if (zoneQuery == null || reception == null)
        {
            return false;
        }

        GridSystemManager manager = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        Grid grid = manager != null ? manager.grid : null;
        if (grid == null || !grid.TryFindNearestWalkablePosition(reception.GridPosition, out Vector2Int spawnPosition))
        {
            return false;
        }

        CharacterSO data = AssetDatabase.LoadAssetAtPath<CharacterSO>(
            "Assets/Resources/SO/Character/Owners/Owner_Slime.asset");
        if (data == null)
        {
            return false;
        }

        workerObject = new GameObject("ExteriorReceptionCandidateWorker");
        workerObject.AddComponent<SpriteRenderer>();
        workerObject.AddComponent<CharacterActor>();
        workerObject.AddComponent<AbilityMove>();
        work = workerObject.AddComponent<AbilityWork>();
        workerObject.AddComponent<AIBrain>();
        workerObject.transform.position = grid.GetWorldPos(spawnPosition);

        CharacterAiEditorTestDependencies.Inject(workerObject);
        CharacterActor character = workerObject.GetComponent<CharacterActor>();
        typeof(CharacterActor)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(character, null);
        character.RefreshAbilityCache();
        character.Initialization(data);
        character.SetLifecycleState(CharacterLifecycleState.Active);
        work = workerObject.GetComponent<AbilityWork>();
        typeof(AbilityWork)
            .GetField("exteriorZoneQuery", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(work, zoneQuery);
        work.EnsureWorkReferences();
        return work != null;
    }

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private static int CountDetachedExteriorCandidates()
    {
        return Resources.FindObjectsOfTypeAll<ExteriorZoneMarker>()
            .Count(zone => zone != null && zone.IsDetachedRestoreCandidate);
    }

    private sealed class MarkerDependencySection :
        DungeonDebugStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly string id;

        public MarkerDependencySection(string id)
        {
            this.id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public override string SectionId => id;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Foundation;

        protected override void CommitMarker(
            DungeonGameRestoreReport report)
        {
        }
    }

    private sealed class FailOnceAfterExteriorSaveSection :
        IDungeonSaveSection,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        public const string Id = "exterior.debug.fail-after-candidate";

        public int CommitCount { get; private set; }
        public string SectionId => Id;
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.LateRuntimeState;
        public IReadOnlyList<string> DependsOn =>
            new[] { ExteriorActivitySaveSection.Id };

        public string Capture() => "{}";

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report)
                .Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion)
            {
                throw new InvalidOperationException(
                    $"Unexpected debug section version {sectionVersion}.");
            }

            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                _ =>
                {
                    CommitCount++;
                    if (CommitCount == 1)
                    {
                        throw new InvalidOperationException(
                            "Intentional failure after exterior candidate staging.");
                    }
                });
        }
    }

    private sealed class LateExteriorParticipantFaultProbe :
        IDungeonRestoreTransactionParticipant
    {
        internal const string FailureMessage =
            "Intentional later participant failure after exterior publication.";

        private readonly ExteriorActivityRuntime runtime;
        private readonly IReadOnlyList<ExteriorZoneMarker> previousZones;
        private bool failNextPublish = true;

        internal LateExteriorParticipantFaultProbe(
            ExteriorActivityRuntime runtime,
            IReadOnlyList<ExteriorZoneMarker> previousZones)
        {
            this.runtime = runtime
                ?? throw new ArgumentNullException(nameof(runtime));
            this.previousZones = previousZones
                ?? throw new ArgumentNullException(nameof(previousZones));
        }

        public string ParticipantId => "999.debug.exterior-late-participant";
        internal int PublishCount { get; private set; }
        internal int RollbackCount { get; private set; }
        internal int CompleteCount { get; private set; }
        internal bool ObservedOldActiveAndCandidateHidden { get; private set; }

        public void BeginRestoreCandidate()
        {
        }

        public void PublishRestoreCandidate()
        {
            PublishCount++;
            ObservedOldActiveAndCandidateHidden =
                previousZones.All(zone => zone != null
                    && zone.gameObject.activeSelf
                    && !zone.IsDetachedRestoreCandidate)
                && runtime.Zones.Count > 0
                && runtime.Zones.All(zone => zone != null
                    && !zone.gameObject.activeSelf
                    && zone.IsDetachedRestoreCandidate);
            if (!failNextPublish)
            {
                return;
            }

            failNextPublish = false;
            throw new InvalidOperationException(FailureMessage);
        }

        public void RollbackPublishedRestoreCandidate()
        {
            RollbackCount++;
        }

        public void CompleteRestoreCandidate()
        {
            CompleteCount++;
        }

        public void DiscardRestoreCandidate()
        {
        }
    }

    private sealed class IncidentAuthorityProbeHandler :
        IExteriorIncidentHandler
    {
        public ExteriorIncidentKind Kind => ExteriorIncidentKind.Thief;
        public string DefaultText => "incident authority probe";
        public float DurationSeconds => 90f;

        public bool TryBegin(
            ExteriorIncidentRuntimeState state,
            ExteriorZoneMarker zone,
            out string failureReason)
        {
            state.stage = ExteriorIncidentStage.Active;
            failureReason = string.Empty;
            return zone != null;
        }

        public void Tick(
            ExteriorIncidentRuntimeState state,
            ExteriorZoneMarker zone,
            float deltaTime)
        {
            state.remainingSeconds = 37f;
            state.stage = ExteriorIncidentStage.Interacting;
        }

        public void Restore(
            ExteriorIncidentRuntimeState state,
            ExteriorZoneMarker zone)
        {
        }

        public bool TryExecutePrimaryAction(
            ExteriorIncidentRuntimeState state,
            ExteriorZoneMarker zone,
            out string message)
        {
            state.remainingSeconds = 0f;
            state.stage = ExteriorIncidentStage.Resolved;
            message = "resolved";
            return true;
        }
    }

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        try
        {
            if (scenario())
            {
                return;
            }
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        errors.Add(name);
    }
}
#endif
