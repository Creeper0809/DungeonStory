using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class StaffDiscontentDebugScenarios
{
    private static readonly HashSet<string> ScenarioStaffNames = new HashSet<string>
    {
        "Low Satisfaction Staff",
        "Efficiency Drop Staff",
        "Work Disruption Staff",
        "Moderate Low Mood Staff",
        "Departing Staff",
        "Rebel Staff",
        "Escalating Rebel Staff"
    };

    [MenuItem("DungeonStory/Debug/Character/Run P2 Staff Discontent Scenarios")]
    public static void RunFromMenu()
    {
        bool success = RunAll(true);
        if (!success)
        {
            Debug.LogError("P2 staff discontent scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        RunScenario("Moderate low mood does not permanently remove staff", VerifyModerateLowMoodDoesNotDepart, errors);

        RunScenario("만족도 낮음 상태", VerifyLowSatisfactionStage, errors);
        RunScenario("1단계 효율 저하", VerifyEfficiencyDropMultiplier, errors);
        RunScenario("2단계 태업 작업 차단", VerifyWorkDisruptionBlocksWork, errors);
        RunScenario("3단계 이탈 영구 손실", VerifyDeparturePermanentLoss, errors);
        RunScenario("4단계 국지 반란", VerifyLocalRebellionPermanentLoss, errors);
        RunScenario("반란 장기 방치 사장 위협", VerifyOwnerThreatEscalation, errors);
        RunScenario("Strict staff-discontent save boundary", VerifyStrictSaveBoundary, errors);

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
            Debug.Log("P2 staff discontent scenarios passed.");
        }

        return true;
    }

    public static bool RunStrictSaveBoundary() => VerifyStrictSaveBoundary();

    private static void RunScenario(string name, Func<bool> scenario, List<string> errors)
    {
        bool passed = false;
        try
        {
            passed = scenario();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
        finally
        {
            CleanupScenarioArtifacts();
        }

        if (!passed)
        {
            errors.Add(name);
        }
    }

    private static void CleanupScenarioArtifacts()
    {
        CharacterActor[] actors = Object.FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (CharacterActor actor in actors)
        {
            if (actor == null || !ScenarioStaffNames.Contains(actor.name))
            {
                continue;
            }

            if (actor.data != null && !AssetDatabase.Contains(actor.data))
            {
                Object.DestroyImmediate(actor.data);
            }

            Object.DestroyImmediate(actor.gameObject);
        }

        StaffDiscontentRuntime[] runtimes = Object.FindObjectsByType<StaffDiscontentRuntime>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        foreach (StaffDiscontentRuntime runtime in runtimes)
        {
            if (runtime != null && runtime.name == "StaffDiscontentRuntime_Test")
            {
                Object.DestroyImmediate(runtime.gameObject);
            }
        }
    }

    private static bool VerifyLowSatisfactionStage()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(201, "Low Satisfaction Staff", 45f);

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome outcome);
        bool valid = record != null
            && outcome == StaffDiscontentOutcome.Warning
            && record.Stage == StaffDiscontentStage.LowSatisfaction
            && !record.IsPermanentLoss;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyEfficiencyDropMultiplier()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(202, "Efficiency Drop Staff", 34f);

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome outcome);
        float multiplier = runtime.Runtime.GetWorkEfficiencyMultiplier(CharacterActor.From(staff));
        bool valid = record != null
            && outcome == StaffDiscontentOutcome.EfficiencyPenalty
            && record.Stage == StaffDiscontentStage.EfficiencyDrop
            && multiplier < 1f
            && multiplier > 0f;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyModerateLowMoodDoesNotDepart()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(206, "Moderate Low Mood Staff", 45f);
        StaffDiscontentRecord record = null;
        StaffDiscontentOutcome outcome = StaffDiscontentOutcome.None;
        for (int i = 0; i < 5; i++)
        {
            record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out outcome);
        }

        bool valid = record != null
            && record.Stage != StaffDiscontentStage.Departure
            && outcome != StaffDiscontentOutcome.PermanentDeparture
            && !record.IsPermanentLoss
            && staff.CurrentLifecycleState != CharacterLifecycleState.Despawned;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyWorkDisruptionBlocksWork()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(203, "Work Disruption Staff", 20f);
        AbilityWork work = staff.GetAbility<AbilityWork>();

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome outcome);
        bool canWork = work.CanStartWorkAction();
        bool valid = record != null
            && outcome == StaffDiscontentOutcome.WorkDisruption
            && record.Stage == StaffDiscontentStage.WorkDisruption
            && !canWork
            && work.IsOffDuty;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyDeparturePermanentLoss()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(204, "Departing Staff", 10f);

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome outcome);
        bool valid = record != null
            && outcome == StaffDiscontentOutcome.PermanentDeparture
            && record.Stage == StaffDiscontentStage.Departure
            && record.IsPermanentLoss
            && record.IsDeparted
            && staff.CurrentLifecycleState == CharacterLifecycleState.Despawned;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyLocalRebellionPermanentLoss()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(205, "Rebel Staff", 5f);

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome outcome);
        bool valid = record != null
            && outcome == StaffDiscontentOutcome.LocalRebellion
            && record.Stage == StaffDiscontentStage.LocalRebellion
            && record.IsPermanentLoss
            && record.IsInLocalRebellion
            && !record.IsDeparted;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyOwnerThreatEscalation()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(206, "Escalating Rebel Staff", 5f);

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome firstOutcome);
        record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome secondOutcome);
        bool valid = firstOutcome == StaffDiscontentOutcome.LocalRebellion
            && secondOutcome == StaffDiscontentOutcome.OwnerThreat
            && record != null
            && record.IsOwnerThreat;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyStrictSaveBoundary()
    {
        using ScenarioRuntime source = new ScenarioRuntime();
        using ScenarioRuntime target = new ScenarioRuntime();
        source.Runtime.RestoreSnapshots(new[]
        {
            new StaffDiscontentSnapshot(
                "staff-discontent-test:save-fixture",
                "Save Fixture Staff",
                StaffDiscontentStage.LocalRebellion,
                StaffDiscontentOutcome.None,
                5f,
                4,
                permanentLoss: true,
                departed: false,
                localRebellion: true,
                ownerThreat: false,
                isolated: true,
                suppressed: false)
        });

        CharacterSceneRuntimeReferences sourceReferences =
            new CharacterSceneRuntimeReferences(
                null,
                null,
                source.Runtime,
                null,
                null,
                null,
                null,
                null);
        StaffDiscontentSaveSection sourceSection =
            new StaffDiscontentSaveSection(sourceReferences);
        string canonicalJson = sourceSection.Capture();

        CharacterSceneRuntimeReferences targetReferences =
            new CharacterSceneRuntimeReferences(
                null,
                null,
                target.Runtime,
                null,
                null,
                null,
                null,
                null);
        StaffDiscontentSaveSection targetSection =
            new StaffDiscontentSaveSection(targetReferences);
        DungeonGameRestoreReport validReport =
            new DungeonGameRestoreReport();
        targetSection.Restore(
            canonicalJson,
            targetSection.SectionVersion,
            validReport);
        object sectionContract = targetSection;
        int validRecordCount = target.Runtime.State.Records.Count;
        bool validRoundTrip = string.Equals(
            targetSection.Capture(),
            canonicalJson,
            StringComparison.Ordinal);
        bool strictContract = sectionContract is IDungeonSaveSectionPreflight
            && sectionContract is IDungeonRollbackFreeSaveSection
            && sectionContract is not IOptionalDungeonSaveSection
            && sectionContract is not IDungeonStagedOptionalSaveSection;
        if (!validReport.Success
            || validRecordCount != 1
            || !validRoundTrip
            || !strictContract)
        {
            return FailStrictSaveBoundary(
                "valid-round-trip",
                $"report={validReport.Success}, "
                + $"errors={JoinErrors(validReport)}, "
                + $"records={validRecordCount}, "
                + $"canonical={validRoundTrip}, "
                + $"strictContract={strictContract}");
        }

        string beforeInvalid = targetSection.Capture();
        DungeonStaffDiscontentSaveData legacy =
            JsonUtility.FromJson<DungeonStaffDiscontentSaveData>(canonicalJson);
        legacy.version = DungeonStaffDiscontentSaveData.CurrentVersion - 1;
        if (!RejectsStaffPayloadWithoutMutation(
                targetSection,
                legacy,
                beforeInvalid))
        {
            return FailStrictSaveBoundary(
                "legacy-version-rejection",
                $"liveUnchanged={string.Equals(targetSection.Capture(), beforeInvalid, StringComparison.Ordinal)}");
        }

        string nullRecordsJson =
            $"{{\"version\":{DungeonStaffDiscontentSaveData.CurrentVersion},\"records\":null}}";
        if (!RejectsStaffPayloadJsonWithoutMutation(
                targetSection,
                nullRecordsJson,
                beforeInvalid))
        {
            return FailStrictSaveBoundary(
                "null-records-rejection",
                $"liveUnchanged={string.Equals(targetSection.Capture(), beforeInvalid, StringComparison.Ordinal)}");
        }

        string missingRecordsJson =
            $"{{\"version\":{DungeonStaffDiscontentSaveData.CurrentVersion}}}";
        if (!RejectsStaffPayloadJsonWithoutMutation(
                targetSection,
                missingRecordsJson,
                beforeInvalid))
        {
            return FailStrictSaveBoundary(
                "missing-records-rejection",
                $"liveUnchanged={string.Equals(targetSection.Capture(), beforeInvalid, StringComparison.Ordinal)}");
        }

        string recordsTextOnlyJson =
            $"{{\"version\":{DungeonStaffDiscontentSaveData.CurrentVersion},"
            + "\"note\":\"the token \\\"records\\\":[] is not a field\"}";
        if (!RejectsStaffPayloadJsonWithoutMutation(
                targetSection,
                recordsTextOnlyJson,
                beforeInvalid))
        {
            return FailStrictSaveBoundary(
                "records-text-spoof-rejection",
                $"liveUnchanged={string.Equals(targetSection.Capture(), beforeInvalid, StringComparison.Ordinal)}");
        }

        IDungeonSaveRestoreStage stagedForDiscard = targetSection.StageRestore(
            canonicalJson,
            targetSection.SectionVersion,
            new DungeonGameRestoreReport());
        if (stagedForDiscard is not IDungeonDiscardableSaveRestoreStage discardable)
        {
            return FailStrictSaveBoundary(
                "direct-candidate-discard",
                $"stageType={stagedForDiscard?.GetType().Name ?? "null"}, discardable=false");
        }
        discardable.Discard();
        if (!string.Equals(
                targetSection.Capture(),
                beforeInvalid,
                StringComparison.Ordinal))
        {
            return FailStrictSaveBoundary(
                "direct-candidate-discard",
                "discarded candidate changed the live capture");
        }

        DungeonStaffDiscontentSaveData invalid =
            JsonUtility.FromJson<DungeonStaffDiscontentSaveData>(
                canonicalJson);
        invalid.records[0].ownerThreat = true;
        DungeonGameRestoreReport invalidReport =
            new DungeonGameRestoreReport();
        bool invalidRejected = false;
        try
        {
            targetSection.Restore(
                JsonUtility.ToJson(invalid),
                targetSection.SectionVersion,
                invalidReport);
        }
        catch (InvalidOperationException)
        {
            invalidRejected = true;
        }
        if (!invalidRejected
            || target.Runtime.State.Records.Count != 1
            || !string.Equals(
                targetSection.Capture(),
                beforeInvalid,
                StringComparison.Ordinal))
        {
            return FailStrictSaveBoundary(
                "terminal-hierarchy-rejection",
                $"rejected={invalidRejected}, "
                + $"records={target.Runtime.State.Records.Count}, "
                + $"liveUnchanged={string.Equals(targetSection.Capture(), beforeInvalid, StringComparison.Ordinal)}, "
                + $"reportErrors={JoinErrors(invalidReport)}");
        }

        target.Runtime.RestoreSnapshots(
            Array.Empty<StaffDiscontentSnapshot>());
        string beforeLateFailure = targetSection.Capture();
        StaffDiscontentFailureSection lateFailure =
            new StaffDiscontentFailureSection();
        int revisionBefore = target.RootStore.PublishedRestoreRevision;
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[] { targetSection, lateFailure },
            target.RootStore);
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        foreach (DungeonSaveSectionEnvelope envelope in envelopes)
        {
            if (string.Equals(
                    envelope.sectionId,
                    StaffDiscontentSaveSection.Id,
                    StringComparison.Ordinal))
            {
                envelope.payloadJson = canonicalJson;
            }
        }
        DungeonGameRestoreReport restoreReport =
            new DungeonGameRestoreReport();
        bool restored = registry.RestoreAll(envelopes, restoreReport);
        bool valid = !restored
            && !restoreReport.Success
            && lateFailure.RemainingCommitFailures == 0
            && target.Runtime.State.Records.Count == 0
            && string.Equals(
                targetSection.Capture(),
                beforeLateFailure,
                StringComparison.Ordinal)
            && target.RootStore.PublishedRestoreRevision == revisionBefore;
        if (!valid)
        {
            return FailStrictSaveBoundary(
                "all-marker-late-failure",
                $"restored={restored}, "
                + $"reportSuccess={restoreReport.Success}, "
                + $"reportErrors={JoinErrors(restoreReport)}, "
                + $"remainingFailures={lateFailure.RemainingCommitFailures}, "
                + $"records={target.Runtime.State.Records.Count}, "
                + $"liveUnchanged={string.Equals(targetSection.Capture(), beforeLateFailure, StringComparison.Ordinal)}, "
                + $"revision={target.RootStore.PublishedRestoreRevision}, "
                + $"revisionBefore={revisionBefore}");
        }
        return true;
    }

    private static bool FailStrictSaveBoundary(string phase, string detail)
    {
        Debug.LogError(
            $"Staff-discontent strict save detail: phase={phase}; {detail}");
        return false;
    }

    private static string JoinErrors(DungeonGameRestoreReport report) =>
        report == null || report.Errors.Count == 0
            ? "none"
            : string.Join(" | ", report.Errors);

    private static bool RejectsStaffPayloadWithoutMutation(
        StaffDiscontentSaveSection section,
        DungeonStaffDiscontentSaveData payload,
        string expectedLiveJson) =>
        RejectsStaffPayloadJsonWithoutMutation(
            section,
            JsonUtility.ToJson(payload),
            expectedLiveJson);

    private static bool RejectsStaffPayloadJsonWithoutMutation(
        StaffDiscontentSaveSection section,
        string payloadJson,
        string expectedLiveJson)
    {
        try
        {
            section.Restore(
                payloadJson,
                section.SectionVersion,
                new DungeonGameRestoreReport());
            return false;
        }
        catch (InvalidOperationException)
        {
            return string.Equals(
                section.Capture(),
                expectedLiveJson,
                StringComparison.Ordinal);
        }
    }

    private static CharacterActor CreateStaff(int id, string name, float mood)
    {
        CharacterSO data = ScriptableObject.CreateInstance<CharacterSO>();
        data.id = id;
        data.characterType = CharacterType.NPC;
        data.role = CharacterRole.Regular;
        data.characterName = name;
        data.speciesTag = "Orc";
        data.defaultWorkPriorities = WorkPriorityProfile.CreateDefault();

        GameObject obj = new GameObject(name);
        obj.AddComponent<SpriteRenderer>();
        obj.AddComponent<CharacterActor>();
        obj.AddComponent<AbilityMove>();
        obj.AddComponent<AbilityShopping>();
        obj.AddComponent<AbilityWork>();
        AIBrain brain = obj.AddComponent<AIBrain>();
        brain.availableActions = AiDebugScenarioActionFactory.CreateStaffActions();
        CharacterAiEditorTestDependencies.Inject(obj);
        CharacterActor character = obj.GetComponent<CharacterActor>();
        typeof(CharacterActor)
            .GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.Invoke(character, null);
        character.RefreshAbilityCache();
        character.Initialization(data);
        character.Identity.SetPersistentId($"staff-discontent-test:{id}");
        character.SetLifecycleState(CharacterLifecycleState.Active);
        character.stats[CharacterCondition.SLEEP] = 50f;
        character.stats[CharacterCondition.HUNGER] = 50f;
        character.stats[CharacterCondition.FUN] = 50f;
        character.stats[CharacterCondition.EXCRETION] = 50f;
        character.stats[CharacterCondition.HYGIENE] = 50f;
        character.stats[CharacterCondition.MOOD] = mood;
        return character;
    }

    private static void DestroyStaff(CharacterActor staff)
    {
        if (staff == null) return;

        if (staff.data != null)
        {
            Object.DestroyImmediate(staff.data);
        }

        Object.DestroyImmediate(staff.gameObject);
    }

    private sealed class ScenarioRuntime : IDisposable
    {
        private readonly GameObject runtimeObject;

        public ScenarioRuntime()
        {
            runtimeObject = new GameObject("StaffDiscontentRuntime_Test");
            Runtime = runtimeObject.AddComponent<StaffDiscontentRuntime>();
            RootStore = new DungeonRuntimeAggregateRootStore();
            Runtime.Construct(
                CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.GameEvents,
                RootStore);
        }

        public StaffDiscontentRuntime Runtime { get; }
        public DungeonRuntimeAggregateRootStore RootStore { get; }

        public void Dispose()
        {
            Object.DestroyImmediate(runtimeObject);
        }
    }

    private sealed class StaffDiscontentFailureSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        public string SectionId => "staff-discontent.debug.late-failure";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Presentation;
        public IReadOnlyList<string> DependsOn =>
            new[] { StaffDiscontentSaveSection.Id };
        public int RemainingCommitFailures { get; set; } = 1;
        public string Capture() => "{}";
        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
        }
        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                commitReport =>
                {
                    if (RemainingCommitFailures <= 0)
                    {
                        return;
                    }

                    RemainingCommitFailures--;
                    commitReport.AddError(
                        "Injected late staff-discontent restore failure.");
                });
        }
    }
}
