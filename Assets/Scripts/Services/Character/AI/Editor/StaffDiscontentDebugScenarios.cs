using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class StaffDiscontentDebugScenarios
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
        RunScenario("동일 일자 중복 처리와 무변경 원장 억제", VerifySameDayAndNoChangeSuppression, errors);
        RunScenario("전이 원장 거절 시 상태·이탈 롤백", VerifyTransitionCommitRejectionRollsBack, errors);
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

    public static void RunBatch()
    {
        if (!RunAll(true))
        {
            throw new InvalidOperationException(
                "G10 staff-discontent transition scenarios failed.");
        }
    }

    public static void RunG10FromBatch()
    {
        string reportPath = RequireG10ReportPath();
        try
        {
            if (!RunAll(true))
            {
                throw new InvalidOperationException(
                    "G10 daily transition scenarios failed.");
            }
            if (!StaffRebellionResponseDebugScenarios.RunAll(true))
            {
                throw new InvalidOperationException(
                    "G10 response-cause scenarios failed.");
            }
            if (!StaffRebellionResponseDebugScenarios.RunCausalOwnerFamily(
                    log: true))
            {
                throw new InvalidOperationException(
                    "G10 response save/restore scenario failed.");
            }

            G10Report report = VerifyG10DepartureSaveAndQuery();
            report.passed = true;
            report.dailyTransitionsVerified = true;
            report.responseCausesVerified = true;
            report.responseSaveRestoreVerified = true;
            WriteG10Report(reportPath, report);
            Debug.Log(
                "[AuditRemediation][G10] PASS staff="
                + report.departureStaffId
                + ", day="
                + report.departureAbsoluteDay);
        }
        catch (Exception exception)
        {
            if (!File.Exists(reportPath))
            {
                WriteG10Report(
                    reportPath,
                    new G10Report
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

    public static bool RunStrictSaveBoundary() => VerifyStrictSaveBoundary();
    public static bool RunStrictSaveBoundary(ICharacterAiWorldRegistry world, ICharacterSettlementStandingQuery standings)
        => VerifyStrictSaveBoundary(world ?? throw new ArgumentNullException(nameof(world)),
            standings ?? throw new ArgumentNullException(nameof(standings)));

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
            && !record.IsPermanentLoss
            && runtime.CountTransitions(StaffDiscontentOutcome.Warning) == 1;

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
            && multiplier > 0f
            && runtime.CountTransitions(
                StaffDiscontentOutcome.EfficiencyPenalty) == 1;

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
            runtime.Clock.SetAbsoluteDay(i + 1);
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
            && work.IsOffDuty
            && runtime.CountTransitions(
                StaffDiscontentOutcome.WorkDisruption) == 1;

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
            && staff.CurrentLifecycleState == CharacterLifecycleState.Despawned
            && runtime.CountTransitions(
                StaffDiscontentOutcome.PermanentDeparture) == 1;

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
            && !record.IsDeparted
            && runtime.CountTransitions(
                StaffDiscontentOutcome.LocalRebellion) == 1;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyOwnerThreatEscalation()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(206, "Escalating Rebel Staff", 5f);

        StaffDiscontentRecord record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome firstOutcome);
        runtime.Clock.SetAbsoluteDay(2);
        record = runtime.Runtime.ProcessStaff(CharacterActor.From(staff), out StaffDiscontentOutcome secondOutcome);
        bool valid = firstOutcome == StaffDiscontentOutcome.LocalRebellion
            && secondOutcome == StaffDiscontentOutcome.OwnerThreat
            && record != null
            && record.IsOwnerThreat
            && runtime.CountTransitions(
                StaffDiscontentOutcome.LocalRebellion) == 1
            && runtime.CountTransitions(
                StaffDiscontentOutcome.OwnerThreat) == 1;

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifySameDayAndNoChangeSuppression()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(207, "Efficiency Drop Staff", 34f);

        runtime.Clock.SetAbsoluteDay(4);
        StaffDiscontentRecord first = runtime.Runtime.ProcessStaff(
            CharacterActor.From(staff),
            out StaffDiscontentOutcome firstOutcome);
        StaffDiscontentRecord duplicate = runtime.Runtime.ProcessStaff(
            CharacterActor.From(staff),
            out StaffDiscontentOutcome duplicateOutcome);
        int lowMoodDaysAfterDuplicate = duplicate?.LowMoodDays ?? -1;
        long revisionAfterDuplicate = runtime.Runtime.TransitionRevision;

        runtime.Clock.SetAbsoluteDay(5);
        StaffDiscontentRecord unchanged = runtime.Runtime.ProcessStaff(
            CharacterActor.From(staff),
            out StaffDiscontentOutcome unchangedOutcome);
        bool valid = first != null
            && duplicate != null
            && unchanged != null
            && firstOutcome == StaffDiscontentOutcome.EfficiencyPenalty
            && duplicateOutcome == StaffDiscontentOutcome.None
            && unchangedOutcome == StaffDiscontentOutcome.None
            && lowMoodDaysAfterDuplicate == 1
            && unchanged.LowMoodDays == 2
            && unchanged.LastProcessedAbsoluteDay == 5
            && revisionAfterDuplicate == 1L
            && runtime.Runtime.TransitionRevision == 1L
            && runtime.CountAllTransitions() == 1;

        if (!valid)
        {
            Debug.LogError(
                "G10 same-day detail: "
                + $"first={(first != null)}, duplicate={(duplicate != null)}, "
                + $"unchanged={(unchanged != null)}, "
                + $"outcomes={firstOutcome}/{duplicateOutcome}/{unchangedOutcome}, "
                + $"duplicateLowDays={lowMoodDaysAfterDuplicate}, "
                + $"unchangedLowDays={unchanged?.LowMoodDays}, "
                + $"lastDay={unchanged?.LastProcessedAbsoluteDay}, "
                + $"duplicateRevision={revisionAfterDuplicate}, "
                + $"revision={runtime.Runtime.TransitionRevision}, "
                + $"ledger={runtime.CountAllTransitions()}");
        }

        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyTransitionCommitRejectionRollsBack()
    {
        using ScenarioRuntime runtime = new ScenarioRuntime(
            rejectTransitionCommit: true);
        CharacterActor staff = CreateStaff(208, "Departing Staff", 10f);
        CharacterLifecycleState lifecycleBefore = staff.CurrentLifecycleState;
        bool rejected = false;
        try
        {
            runtime.Runtime.ProcessStaff(
                CharacterActor.From(staff),
                out _);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        bool valid = rejected
            && runtime.Runtime.State.Records.Count == 0
            && runtime.Runtime.OutcomeRevision == 0L
            && runtime.Runtime.TransitionRevision == 0L
            && staff.CurrentLifecycleState == lifecycleBefore
            && runtime.CountAllTransitions() == 0
            && runtime.Ledger.GetOutboxSnapshot().Count == 0;
        DestroyStaff(staff);
        return valid;
    }

    private static bool VerifyStrictSaveBoundary()
        => VerifyStrictSaveBoundary(CharacterAiEditorTestDependencies.WorldRegistry,
            CharacterAiEditorTestDependencies.SettlementStandings);

    private static bool VerifyStrictSaveBoundary(ICharacterAiWorldRegistry world, ICharacterSettlementStandingQuery standings)
    {
        using ScenarioRuntime source = new ScenarioRuntime(world, standings);
        using ScenarioRuntime target = new ScenarioRuntime(world, standings);
        const string staffId =
            "character:staff-discontent-test:save-fixture";
        const string displayName = "Save Fixture Staff";
        StaffDiscontentSnapshot transitionBefore = new(
            staffId,
            displayName,
            StaffDiscontentStage.WorkDisruption,
            StaffDiscontentOutcome.None,
            5f,
            3,
            permanentLoss: false,
            departed: false,
            localRebellion: false,
            ownerThreat: false,
            isolated: false,
            suppressed: false,
            lastProcessedAbsoluteDay: 11);
        StaffDiscontentSnapshot transitionAfter = new(
            staffId,
            displayName,
            StaffDiscontentStage.LocalRebellion,
            StaffDiscontentOutcome.LocalRebellion,
            5f,
            4,
            permanentLoss: true,
            departed: false,
            localRebellion: true,
            ownerThreat: false,
            isolated: false,
            suppressed: false,
            lastProcessedAbsoluteDay: 12,
            localRebellionDays: 1);
        StaffDiscontentSnapshot responseBefore = new(
            staffId,
            displayName,
            transitionAfter.stage,
            StaffDiscontentOutcome.None,
            transitionAfter.mood,
            transitionAfter.lowMoodDays,
            transitionAfter.permanentLoss,
            transitionAfter.departed,
            transitionAfter.localRebellion,
            transitionAfter.ownerThreat,
            isolated: false,
            suppressed: transitionAfter.suppressed,
            lastProcessedAbsoluteDay:
                transitionAfter.lastProcessedAbsoluteDay,
            localRebellionDays: transitionAfter.localRebellionDays);
        StaffDiscontentSnapshot restoredSnapshot = new(
            staffId,
            displayName,
            transitionAfter.stage,
            StaffDiscontentOutcome.None,
            transitionAfter.mood,
            transitionAfter.lowMoodDays,
            transitionAfter.permanentLoss,
            transitionAfter.departed,
            transitionAfter.localRebellion,
            transitionAfter.ownerThreat,
            isolated: true,
            suppressed: transitionAfter.suppressed,
            lastProcessedAbsoluteDay:
                transitionAfter.lastProcessedAbsoluteDay,
            localRebellionDays: transitionAfter.localRebellionDays);
        StaffDiscontentObservationData transitionObservation = new()
        {
            fact = new StaffDiscontentFactData
            {
                kind = StaffDiscontentFactKind.DailyTransition,
                revision = 11L,
                absoluteDay = 12,
                transition = StaffDiscontentOutcome.LocalRebellion,
                before = StaffDiscontentFactValues.Capture(transitionBefore),
                after = StaffDiscontentFactValues.Capture(transitionAfter)
            },
            publicationKey = StaffDiscontentTransitionOutcomeIds
                .ResultKey(11L)
                .ToString()
        };
        StaffDiscontentObservationData responseObservation = new()
        {
            fact = new StaffDiscontentFactData
            {
                kind = StaffDiscontentFactKind.Response,
                revision = 7L,
                absoluteDay = 12,
                responseType = StaffRebellionResponseType.Isolate,
                before = StaffDiscontentFactValues.Capture(responseBefore),
                after = StaffDiscontentFactValues.Capture(restoredSnapshot)
            },
            publicationKey = StaffDiscontentOutcomeIds
                .ResultKey(7L)
                .ToString()
        };
        source.Runtime.RestoreSnapshots(new[]
        {
            restoredSnapshot
        }, 7L, 11L, responseObservation, transitionObservation);

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
        long validOutcomeRevision = target.Runtime.OutcomeRevision;
        long validTransitionRevision = target.Runtime.TransitionRevision;
        int validLastProcessedDay = target.Runtime.State.Records
            .Single()
            .LastProcessedAbsoluteDay;
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
            || validOutcomeRevision != 7L
            || validTransitionRevision != 11L
            || validLastProcessedDay != 12
            || !validRoundTrip
            || !strictContract)
        {
            return FailStrictSaveBoundary(
                "valid-round-trip",
                $"report={validReport.Success}, "
                + $"errors={JoinErrors(validReport)}, "
                + $"records={validRecordCount}, "
                + $"outcomeRevision={validOutcomeRevision}, "
                + $"transitionRevision={validTransitionRevision}, "
                + $"lastProcessedDay={validLastProcessedDay}, "
                + $"canonical={validRoundTrip}, "
                + $"strictContract={strictContract}");
        }

        string beforeInvalid = targetSection.Capture();
        DungeonStaffDiscontentSaveData negativeRevision =
            JsonUtility.FromJson<DungeonStaffDiscontentSaveData>(canonicalJson);
        negativeRevision.outcomeRevision = -1L;
        if (!RejectsStaffPayloadWithoutMutation(
                targetSection,
                negativeRevision,
                beforeInvalid))
        {
            return FailStrictSaveBoundary(
                "negative-outcome-revision-rejection",
                $"liveUnchanged={string.Equals(targetSection.Capture(), beforeInvalid, StringComparison.Ordinal)}");
        }

        DungeonStaffDiscontentSaveData negativeTransitionRevision =
            JsonUtility.FromJson<DungeonStaffDiscontentSaveData>(canonicalJson);
        negativeTransitionRevision.transitionRevision = -1L;
        if (!RejectsStaffPayloadWithoutMutation(
                targetSection,
                negativeTransitionRevision,
                beforeInvalid))
        {
            return FailStrictSaveBoundary(
                "negative-transition-revision-rejection",
                $"liveUnchanged={string.Equals(targetSection.Capture(), beforeInvalid, StringComparison.Ordinal)}");
        }

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
        CharacterSO data = CharacterAiEditorTestDependencies.CreateCharacterFixtureData(
            CharacterType.NPC,
            name,
            "Orc");
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
        character.Identity.SetPersistentId($"character:staff-discontent-test:{id}");
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

    private static G10Report VerifyG10DepartureSaveAndQuery()
    {
        using ScenarioRuntime source = new ScenarioRuntime();
        using ScenarioRuntime target = new ScenarioRuntime();
        CharacterActor staff = CreateStaff(
            210,
            "G10 Departing Staff",
            10f);
        try
        {
            source.Clock.SetAbsoluteDay(17);
            RequireG10(
                CharacterPersistentIdentity.TryGet(staff, out CharacterId staffId),
                "G10 departure staff has no persistent identity.");

            StaffDiscontentRecord record = source.Runtime.ProcessStaff(
                CharacterActor.From(staff),
                out StaffDiscontentOutcome outcome);
            RequireG10(
                record != null
                && outcome == StaffDiscontentOutcome.PermanentDeparture
                && record.IsDeparted
                && record.IsPermanentLoss
                && record.LastProcessedAbsoluteDay == 17
                && staff.CurrentLifecycleState == CharacterLifecycleState.Despawned
                && source.Runtime.TransitionRevision == 1L,
                "Actual staff departure did not reach its terminal owner state.");

            GameplayOutcomeSnapshot sourceOutcome =
                RequireG10DepartureOutcome(source, staffId, 17);
            StaffDiscontentSaveSection sourceSection =
                CreateG10SaveSection(source);
            string ownerJson = sourceSection.Capture();
            string ledgerJson = JsonUtility.ToJson(
                source.Ledger.CaptureGameplayOutcomes());

            StaffDiscontentSaveSection targetSection =
                CreateG10SaveSection(target);
            DungeonGameRestoreReport ownerReport =
                new DungeonGameRestoreReport();
            targetSection.Restore(
                ownerJson,
                targetSection.SectionVersion,
                ownerReport);
            RequireG10(
                ownerReport.Success,
                "Staff-discontent owner restore failed: "
                + JoinErrors(ownerReport));

            GameplayOutcomeLedgerSaveData ledgerSave =
                JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
                    ledgerJson);
            GameplayOutcomeLedgerRestoreCandidate ledgerCandidate =
                target.Ledger.PrepareGameplayOutcomeRestore(ledgerSave);
            target.Ledger.BeginRestoreCandidate();
            target.Ledger.PublishGameplayOutcomeRestore(ledgerCandidate);
            target.Ledger.PublishRestoreCandidate();
            target.Ledger.CompleteRestoreCandidate();

            StaffDiscontentSnapshot restored = target.Runtime
                .CaptureSnapshots()
                .Single();
            GameplayOutcomeSnapshot restoredOutcome =
                RequireG10DepartureOutcome(target, staffId, 17);
            bool ownerRoundTrip = string.Equals(
                targetSection.Capture(),
                ownerJson,
                StringComparison.Ordinal);
            bool outcomeIdentityRoundTrip = string.Equals(
                    restoredOutcome.runId,
                    sourceOutcome.runId,
                    StringComparison.Ordinal)
                && restoredOutcome.sequence == sourceOutcome.sequence
                && string.Equals(
                    restoredOutcome.immutablePayloadHash,
                    sourceOutcome.immutablePayloadHash,
                    StringComparison.Ordinal);
            RequireG10(
                target.Runtime.TransitionRevision == 1L
                && restored.lastProcessedAbsoluteDay == 17
                && restored.departed
                && restored.permanentLoss
                && ownerRoundTrip
                && outcomeIdentityRoundTrip,
                "Departure owner or exact outcome did not survive save/restore.");

            long revisionBeforeReplay = source.Runtime.TransitionRevision;
            int outcomeCountBeforeReplay = source.CountAllTransitions();
            source.Clock.SetAbsoluteDay(18);
            StaffDiscontentRecord replayRecord = source.Runtime.ProcessStaff(
                CharacterActor.From(staff),
                out StaffDiscontentOutcome replayOutcome);
            RequireG10(
                replayRecord != null
                && replayOutcome == StaffDiscontentOutcome.None
                && source.Runtime.TransitionRevision == revisionBeforeReplay
                && source.CountAllTransitions() == outcomeCountBeforeReplay,
                "Terminal departure replay produced a duplicate result.");

            return new G10Report
            {
                departureStaffId = staffId.Value,
                departureAbsoluteDay = sourceOutcome.absoluteDay,
                transitionRevision = source.Runtime.TransitionRevision,
                exactQueryCount = source.CountAllTransitions(),
                restoredExactQueryCount = target.CountAllTransitions(),
                ownerSaveRoundTrip = ownerRoundTrip,
                exactOutcomeSaveRoundTrip = outcomeIdentityRoundTrip,
                sameDayDuplicateCount = 0,
                terminalReplayCount = source.CountAllTransitions()
                    - outcomeCountBeforeReplay,
                fullAuditExecuted = false,
                humanApprovalRecorded = false,
                trainingExecuted = false
            };
        }
        finally
        {
            DestroyStaff(staff);
        }
    }

    private static GameplayOutcomeSnapshot RequireG10DepartureOutcome(
        ScenarioRuntime runtime,
        CharacterId staffId,
        int expectedDay)
    {
        GameplayEntityId staffEntity = new(
            StaffDiscontentTransitionOutcomeIds.CharacterKind,
            staffId.Value);
        GameplayOutcomeQueryPage subject = runtime.Ledger.GetForEntity(
            staffEntity,
            OutcomeCursor.FirstPage(8),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage operation = runtime.Ledger.GetForOperation(
            new GameplayOperationId("staff-discontent:1"));
        RequireG10(
            subject.Items.Count == 1
            && operation.Items.Count == 1
            && subject.Items[0].Exact != null
            && operation.Items[0].Exact != null,
            "Departure outcome is not exactly queryable by staff and operation.");

        GameplayOutcomeSnapshot snapshot = subject.Items[0].Exact;
        GameplayOutcomeParticipantSnapshot participant =
            snapshot.participants.Single();
        RequireG10(
            string.Equals(
                snapshot.producerId,
                StaffDiscontentTransitionOutcomeIds.ProducerId,
                StringComparison.Ordinal)
            && string.Equals(
                snapshot.outcomeTypeId,
                StaffDiscontentTransitionOutcomeIds.Transitioned.Value,
                StringComparison.Ordinal)
            && string.Equals(
                snapshot.operationId,
                "staff-discontent:1",
                StringComparison.Ordinal)
            && snapshot.ownerRevision == 1L
            && snapshot.commitRevision == 1L
            && snapshot.absoluteDay == expectedDay
            && snapshot.status == GameplayOutcomeStatus.Succeeded
            && string.Equals(
                participant.entityKindId,
                StaffDiscontentTransitionOutcomeIds.CharacterKind.Value,
                StringComparison.Ordinal)
            && string.Equals(
                participant.entityId,
                staffId.Value,
                StringComparison.Ordinal)
            && string.Equals(
                participant.roleId,
                StaffDiscontentTransitionOutcomeIds.StaffRole.Value,
                StringComparison.Ordinal)
            && Metric(
                    snapshot,
                    StaffDiscontentTransitionOutcomeIds.OutcomeMetric)
                == (int)StaffDiscontentOutcome.PermanentDeparture
            && Metric(
                    snapshot,
                    StaffDiscontentTransitionOutcomeIds.PermanentLossMetric)
                == 1d
            && Metric(
                    snapshot,
                    StaffDiscontentTransitionOutcomeIds.DepartedMetric)
                == 1d
            && Metric(
                    snapshot,
                    StaffDiscontentTransitionOutcomeIds.LocalRebellionMetric)
                == 0d,
            "Departure outcome lost its staff identity, day, or terminal facts.");
        return snapshot;
    }

    private static double Metric(
        GameplayOutcomeSnapshot snapshot,
        GameplayMetricId metricId) => snapshot.metrics
        .Single(metric => string.Equals(
            metric.metricId,
            metricId.Value,
            StringComparison.Ordinal))
        .value;

    private static StaffDiscontentSaveSection CreateG10SaveSection(
        ScenarioRuntime runtime) => new(
        new CharacterSceneRuntimeReferences(
            null,
            null,
            runtime.Runtime,
            null,
            null,
            null,
            null,
            null));

    private static string RequireG10ReportPath()
    {
        string output = Environment.GetEnvironmentVariable(
            "DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException(
                "G10 verification requires its isolated output directory.");
        }

        string directory = Path.GetFullPath(output);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "report.json");
        if (File.Exists(path))
        {
            throw new InvalidOperationException(
                "G10 report already exists: " + path);
        }
        return path;
    }

    private static void WriteG10Report(string path, G10Report report)
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
    private sealed class G10Report
    {
        public string rowId = "G10";
        public bool passed;
        public string failure = string.Empty;
        public bool dailyTransitionsVerified;
        public bool responseCausesVerified;
        public bool responseSaveRestoreVerified;
        public string departureStaffId = string.Empty;
        public int departureAbsoluteDay;
        public long transitionRevision;
        public int exactQueryCount;
        public int restoredExactQueryCount;
        public bool ownerSaveRoundTrip;
        public bool exactOutcomeSaveRoundTrip;
        public int sameDayDuplicateCount;
        public int terminalReplayCount;
        public bool fullAuditExecuted;
        public bool humanApprovalRecorded;
        public bool trainingExecuted;
    }

    private static void RequireG10(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class ScenarioRuntime : IDisposable
    {
        private readonly GameObject runtimeObject;
        private readonly IGameEventBus events;
        private readonly DomainFactDispatcher facts;
        private readonly IDisposable transitionObservation;
        private readonly StaffDiscontentPresentationObserver presentation;

        public ScenarioRuntime(bool rejectTransitionCommit = false)
            : this(CharacterAiEditorTestDependencies.WorldRegistry,
                CharacterAiEditorTestDependencies.SettlementStandings, rejectTransitionCommit) { }

        public ScenarioRuntime(ICharacterAiWorldRegistry world, ICharacterSettlementStandingQuery standings,
            bool rejectTransitionCommit = false)
        {
            runtimeObject = new GameObject("StaffDiscontentRuntime_Test");
            Runtime = runtimeObject.AddComponent<StaffDiscontentRuntime>();
            RootStore = new DungeonRuntimeAggregateRootStore();
            Clock = new MutableClock();
            events = new GameEventBus();
            presentation = new StaffDiscontentPresentationObserver(
                world,
                events);
            presentation.Initialize();
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new StaffDiscontentTransitionOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new StaffDiscontentTransitionOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(
                    smallPageCount: 8,
                    largePageCount: 0,
                    knownResultKeyCapacity: 128),
                new GameplayOutcomeRunId(
                    "run:staff-discontent-transition-test"),
                1L);
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                registry,
                events);
            facts = new DomainFactDispatcher();
            transitionObservation = facts.Subscribe(
                "staff-discontent-transition:debug",
                new GameplayDomainFactObserver<
                    StaffDiscontentTransitionedFact,
                    StaffDiscontentTransitionOutcomeReceipt>(
                    StaffDiscontentFactMapping.Capture,
                    registry,
                    Ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, Ledger)));
            IDomainFactStagingPublisher publisher = rejectTransitionCommit
                ? new RejectingTransitionPublisher(facts)
                : facts;
            Runtime.Construct(
                new DungeonStory.Foundation.UnityUiClock(),
                world,
                events,
                RootStore,
                standings,
                publisher,
                facts,
                Clock);
        }

        public StaffDiscontentRuntime Runtime { get; }
        public DungeonRuntimeAggregateRootStore RootStore { get; }
        public GameplayOutcomeLedger Ledger { get; }
        public MutableClock Clock { get; }

        public int CountAllTransitions() => Ledger.GetGlobal(
            OutcomeCursor.FirstPage(32),
            OutcomeFilter.All).Items.Count;

        public int CountTransitions(StaffDiscontentOutcome outcome) =>
            Ledger.GetGlobal(
                    OutcomeCursor.FirstPage(32),
                    OutcomeFilter.All)
                .Items
                .Count(item => item.Exact != null
                    && item.Exact.metrics.Any(metric =>
                        metric.metricId
                            == StaffDiscontentTransitionOutcomeIds
                                .OutcomeMetric.Value
                        && metric.value == (int)outcome));

        public void Dispose()
        {
            presentation.Dispose();
            transitionObservation.Dispose();
            facts.Dispose();
            Object.DestroyImmediate(runtimeObject);
        }
    }

    private sealed class MutableClock : IGameCalendar
    {
        private int day = 1;
        private int hour;

        public int Day => day;
        public int Hour => hour;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => hour
            / (float)GameCalendarRules.HoursPerDay
            * GameCalendarRules.SecondsPerDay;
        public TimeOfDay TimeOfDay => TimeOfDay.Noon;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(day, hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(day, hour, utcOffsetHours);

        public void Start() => IsRunning = true;

        public void SetAbsoluteDay(int nextDay) => SetDateTime(nextDay, hour);

        public void SetDateTime(int nextDay, int nextHour)
        {
            if (nextDay < 1 || nextHour < 0 || nextHour >= GameCalendarRules.HoursPerDay)
                throw new ArgumentOutOfRangeException("Staff fixture requires a valid one-based calendar date.");
            day = nextDay;
            hour = nextHour;
        }
    }

    private sealed class RejectingTransitionPublisher :
        IDomainFactStagingPublisher
    {
        private readonly IDomainFactStagingPublisher inner;

        public RejectingTransitionPublisher(
            IDomainFactStagingPublisher inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failureReason)
        {
            if (!inner.TryStage(fact, out stage, out failureReason))
                return false;
            if (fact is StaffDiscontentTransitionedFact)
                stage = new RejectingTransitionStage(stage);
            return true;
        }
    }

    private sealed class RejectingTransitionStage : IDomainFactStage
    {
        private readonly IDomainFactStage inner;

        public RejectingTransitionStage(IDomainFactStage inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public string OperationKey => inner.OperationKey;
        public long OwnerRevision => inner.OwnerRevision;
        public bool IsReplay => inner.IsReplay;
        public bool IsActive => inner.IsActive;

        public DomainFactDelivery Commit()
        {
            inner.Cancel();
            return DomainFactDelivery.Reject(
                "injected-staff-transition-commit-rejection");
        }

        public void Cancel() => inner.Cancel();
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
