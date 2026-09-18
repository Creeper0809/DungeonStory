#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static UnityEngine.Object;

// Root-owned focused integration witness. Authored venue mappings are covered by
// Wim037041VenueAuthoringDebugScenarios; this runner exercises the production
// execution state machine against the registered main-world dependencies.
public sealed class Wim041FestivalExecutionLiveWitness
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-041-festival-execution-live.txt";

    private const string FestivalId = "festival:wim041-focused";
    private const string ItemId = "material:paper";
    private static bool running;
    private readonly List<string> lines = new();
    private readonly List<FestivalCelebratedEvent> celebrated = new();
    private DungeonRuntimeLifetimeScope scope;
    private IGameTimeScaleController timeScale;
    private FestivalDefinitionSO definition;
    private ToggleHazardQuery hazards;
    private FestivalExecutionRuntime runtime;
    private SocietyVenueCandidateSnapshot venue;
    private FixedVenueQuery venueQuery;
    private CharacterActor[] participants;
    private IDisposable celebratedSubscription;

    public static string StartFocused()
    {
        Require(Application.isPlaying && !running,
            "Start once in a fresh disposable main Play session.");
        DungeonRuntimeLifetimeScope current =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(current?.Container != null, "Main runtime is not initialized.");
        IDisposable persistence =
            current.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "Cannot protect user save files.");
        persistence.Dispose();
        current.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        GameManager host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Missing main coroutine host.");
        host.isPause = true;
        current.Container.Resolve<IGameTimeScaleController>().Scale = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        try
        {
            host.StartCoroutine(new Wim041FestivalExecutionLiveWitness().Observe());
        }
        catch
        {
            running = false;
            throw;
        }
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Observe()
    {
        Stack<IEnumerator> pending = new();
        pending.Push(Run());
        Exception failure = null;
        while (pending.Count > 0)
        {
            object current = null;
            bool moved;
            try
            {
                moved = pending.Peek().MoveNext();
                if (moved) current = pending.Peek().Current;
            }
            catch (Exception error)
            {
                failure = error;
                break;
            }
            if (!moved)
            {
                (pending.Pop() as IDisposable)?.Dispose();
                continue;
            }
            if (current is IEnumerator nested) pending.Push(nested);
            else yield return current;
        }

        while (pending.Count > 0)
            (pending.Pop() as IDisposable)?.Dispose();
        celebratedSubscription?.Dispose();
        celebratedSubscription = null;
        if (definition != null) Destroy(definition);
        Pause();
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add(
            "scope=production FestivalExecutionRuntime and FestivalVenueAlertApplicationAdapter with registered main character/grid/calendar/event/item/input-owner/sink/work/faction dependencies; controlled test-only festival and venue selector; announcement host/skip action IDs, exact submitted/preview/revalidated roster and capacity, actual host/cancel ownership, decline, 24.9/25/24.9 attendance grading, AI movement, EmergencyNeed preemption/rejoin, no-path release, field-fire stop, persistence transaction and physical-join rejection; authored live16 venue mappings are separate accepted evidence; not a free-running calendar occurrence or six-adult balance run");
        lines.Add(
            "cleanup=paused disposable protected Play; disk persistence disabled before owner selection; operator stops without scene/profile/save writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        running = false;
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null, "Main runtime disappeared during the witness.");
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        OwnerRunManager owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Owner preparation is unavailable.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                    .TryReconcileNewRunTierZero(out _, out string expansionFailure),
                "Tier-zero preparation failed: " + expansionFailure);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null,
                "Normal party UI did not publish an owner.");
        }

        ICharacterWorldQuery characters =
            scope.Container.Resolve<ICharacterWorldQuery>();
        participants = characters.Characters
            .Where(actor => actor != null
                && !actor.IsDead
                && !actor.IsOnExpedition
                && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                && actor.Brain != null
                && CharacterPersistentIdentity.TryGet(actor, out _))
            .OrderBy(actor => CharacterPersistentIdentity.Require(actor).Value,
                StringComparer.Ordinal)
            .Take(3)
            .ToArray();
        Require(participants.Length == 3,
            "The normal prepared party must expose three active persistent members.");
        foreach (CharacterActor participant in participants)
        {
            if (participant.TryGetAbility(out AbilityWork work))
                work.isWorking = false;
        }

        BuildableObject venueFacility = scope.Container.Resolve<IBuildingWorldQuery>()
            .Buildings
            .Where(value => value != null
                && !value.isDestroy
                && value.PersistentInstanceId.IsValid)
            .OrderBy(value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        Require(venueFacility != null,
            "The main world has no live facility for input-owner anchoring.");
        Vector2Int[] cells = participants.Select(value => value.GetNowXY()).ToArray();
        venue = CreateVenue(venueFacility, cells);
        venueQuery = new FixedVenueQuery(venue);
        definition = CreateDefinition();
        hazards = new ToggleHazardQuery();
        runtime = new FestivalExecutionRuntime(
            new SingleFestivalCatalog(definition),
            characters,
            venueQuery,
            scope.Container.Resolve<ICharacterNarrativeQuery>(),
            scope.Container.Resolve<ICharacterSettlementStandingQuery>(),
            scope.Container.Resolve<ICharacterCombatStanceQuery>(),
            hazards,
            scope.Container.Resolve<IGridSystemProvider>(),
            scope.Container.Resolve<IGameCalendar>(),
            scope.Container.Resolve<IGameClock>(),
            scope.Container.Resolve<IGameEventBus>(),
            scope.Container.Resolve<IPsychosocialPersistence>(),
            scope.Container.Resolve<IFactionCampaignQuery>(),
            scope.Container.Resolve<V20CampaignRuntime>(),
            scope.Container.Resolve<IEconomyProjectInputOwnerPort>(),
            scope.Container.Resolve<IWorldItemStackRuntime>(),
            scope.Container.Resolve<IPhysicalFacilityItemBatchSinkGateway>(),
            scope.Container.Resolve<IWorkAmountCalculator>());
        celebratedSubscription = scope.Container.Resolve<IGameEventBus>()
            .Subscribe<FestivalCelebratedEvent>(value => celebrated.Add(value));

        VerifyAnnouncementAlertPublication();
        VerifyHostThenDeadlineCancellation(venueFacility);
        VerifyDeclineHasNoCost();
        VerifyAttendanceBoundariesAndHazard(cells);
        VerifyPersistenceAndPhysicalJoin();
        yield return VerifyMovementDutyRejoinAndNoPath();
        yield return null;
    }

    private void VerifyAnnouncementAlertPublication()
    {
        const int year = 40;
        IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
        IGameEventBus events = scope.Container.Resolve<IGameEventBus>();
        calendar.SetDateTime(
            FestivalExecutionRules.AnnouncementAbsoluteDay(definition, year),
            8);
        List<EventAlertRequest> requested = new();
        using IDisposable subscription = events.Subscribe<EventAlertRequestedEvent>(
            value => requested.Add(value.request));
        using FestivalVenueAlertApplicationAdapter alerts = new(
            new SingleFestivalCatalog(definition),
            runtime,
            runtime,
            calendar,
            events);

        alerts.Start();

        string occurrenceId = FestivalExecutionRules.OccurrenceId(FestivalId, year);
        EventAlertRequest request = requested.Single(value => string.Equals(
            value.SourceId,
            occurrenceId,
            StringComparison.Ordinal));
        Require(request.Choices.Count == 2
            && request.Choices.Any(value => string.Equals(
                value.ActionId,
                V21ContentAlertActionIds.Festival(FestivalId, year),
                StringComparison.Ordinal))
            && request.Choices.Any(value => string.Equals(
                value.ActionId,
                V21ContentAlertActionIds.FestivalSkip(FestivalId, year),
                StringComparison.Ordinal))
            && request.Detail.Contains(venue.DisplayName, StringComparison.Ordinal)
            && request.Detail.Contains("준비 작업", StringComparison.Ordinal)
            && request.Detail.Contains("개최/마감", StringComparison.Ordinal)
            && !runtime.HasOccurrence(occurrenceId),
            "Announcement did not expose the real venue/preparation/deadline and host/skip actions without scheduling work.");
        lines.Add(
            "[PASS] announcement event publishes venue/preparation/deadline and host/skip action IDs without automatic scheduling");
    }

    private IEnumerator VerifyMovementDutyRejoinAndNoPath()
    {
        IGridSystemProvider gridProvider =
            scope.Container.Resolve<IGridSystemProvider>();
        Grid grid = gridProvider.Grid;
        CharacterActor subject = participants[0];
        Vector2Int start = subject.GetNowXY();
        Vector2Int target = grid.GetCells()
            .Select(value => value.Position)
            .Where(value => value != start
                && grid.IsWalkable(value)
                && grid.GetGridCell(value)?.GetOccupant(GridLayer.Character) == null
                && grid.GetGridCell(value)?.GetOccupant(GridLayer.DownedCharacter) == null)
            .Select(value => new
            {
                Position = value,
                Path = grid.GetMovePathTo(start, value)
            })
            .Where(value => value.Path != null
                && value.Path.Count > 0
                && value.Path.Count <= 12)
            .OrderBy(value => value.Path.Count)
            .ThenBy(value => value.Position.y)
            .ThenBy(value => value.Position.x)
            .Select(value => value.Position)
            .FirstOrDefault();
        Require(target != default || start != default,
            "No distinct reachable festival movement target was found.");
        Require(target != start && grid.GetMovePathTo(start, target)?.Count > 0,
            "The selected festival movement target is not reachable.");

        Vector2Int[] movementCells =
        {
            target,
            participants[1].GetNowXY(),
            participants[2].GetNowXY()
        };
        int year = 30;
        PublishOnly(BuildInProgressOccurrence(year, movementCells));
        scope.Container.Resolve<IGameCalendar>().SetDateTime(
            FestivalExecutionRules.FestivalAbsoluteDay(definition, year),
            FestivalExecutionRules.StartHour);

        GameManager host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Missing main host for the movement witness.");
        host.isPause = false;
        timeScale.Scale = 1f;
        string occurrenceId = FestivalExecutionRules.OccurrenceId(FestivalId, year);
        string festivalOwner = "festival:" + occurrenceId;
        int movementFrames = 0;
        while (movementFrames++ < 600)
        {
            runtime.Advance();
            if (subject.GetNowXY() == target
                && subject.Brain.IsExternallyDrivenActionActive
                && string.Equals(subject.Brain.ExternalIntentOwnerId,
                    festivalOwner, StringComparison.Ordinal))
                break;
            yield return null;
        }
        Require(subject.GetNowXY() == target
            && subject.Brain.IsExternallyDrivenActionActive
            && string.Equals(subject.Brain.ExternalIntentOwnerId,
                festivalOwner, StringComparison.Ordinal),
            "Festival execution did not acquire the real AI lease and move the actor to the assigned cell.");

        runtime.Advance();
        string subjectId = CharacterPersistentIdentity.Require(subject).Value;
        FestivalAttendanceProgressSaveData subjectAttendance = runtime.Occurrences
            .Single().attendance.Single(value => string.Equals(
                value.characterId,
                subjectId,
                StringComparison.Ordinal));
        float attendedBeforeDuty = subjectAttendance.attendedSeconds;
        Require(attendedBeforeDuty > 0f,
            "Attendance did not accrue after the actor reached the festival cell.");
        Require(subject.Brain.TryBeginExternallyDrivenAction(
                "wim041:emergency-duty",
                CharacterActionIntentKind.EmergencyNeed,
                "응급 업무",
                "축제 이탈 검증",
                "상위 우선순위",
                out CharacterActionIntentLease emergencyLease),
            "Emergency duty did not preempt the festival lease.");
        for (int i = 0; i < 3; i++)
        {
            runtime.Advance();
            yield return null;
        }
        FestivalAttendanceProgressSaveData duringDutyAttendance = runtime.Occurrences
            .Single().attendance.Single(value => string.Equals(
                value.characterId,
                subjectId,
                StringComparison.Ordinal));
        Require(subject.Brain.IsExternalIntentCurrent(emergencyLease)
            && Approximately(duringDutyAttendance.attendedSeconds, attendedBeforeDuty)
            && runtime.Occurrences.Single().lastStatus.Contains(
                "필수 업무 이탈 1명", StringComparison.Ordinal),
            "Emergency duty did not release festival ownership and pause personal attendance.");

        Require(subject.Brain.CancelExternallyDrivenAction(emergencyLease),
            "Emergency duty lease could not be released for rejoin.");
        runtime.Advance();
        FestivalAttendanceProgressSaveData rejoinedAttendance = runtime.Occurrences
            .Single().attendance.Single(value => string.Equals(
                value.characterId,
                subjectId,
                StringComparison.Ordinal));
        Require(subject.Brain.IsExternallyDrivenActionActive
            && string.Equals(subject.Brain.ExternalIntentOwnerId,
                festivalOwner, StringComparison.Ordinal)
            && rejoinedAttendance.attendedSeconds > attendedBeforeDuty,
            "The actor did not reacquire festival ownership and resume attendance after duty.");

        hazards.Fire = true;
        runtime.Advance();
        hazards.Fire = false;
        Require(runtime.Occurrences.Single().phase == FestivalExecutionPhase.Stopped
            && !subject.Brain.IsExternallyDrivenActionActive,
            "Movement occurrence cleanup leaked the festival AI lease.");

        Vector2Int invalid = new(-101, -101);
        Vector2Int[] invalidCells =
        {
            invalid,
            participants[1].GetNowXY(),
            participants[2].GetNowXY()
        };
        int invalidYear = 31;
        PublishOnly(BuildInProgressOccurrence(invalidYear, invalidCells));
        scope.Container.Resolve<IGameCalendar>().SetDateTime(
            FestivalExecutionRules.FestivalAbsoluteDay(definition, invalidYear),
            FestivalExecutionRules.StartHour);
        int noPathFrames = 0;
        while (noPathFrames++ < 120
            && !runtime.Occurrences.Single().lastStatus.Contains(
                "이동 불가", StringComparison.Ordinal))
        {
            runtime.Advance();
            yield return null;
        }
        Require(runtime.Occurrences.Single().lastStatus.Contains(
                "이동 불가", StringComparison.Ordinal)
            && (!subject.Brain.IsExternallyDrivenActionActive
                || !subject.Brain.ExternalIntentOwnerId.StartsWith(
                    "festival:", StringComparison.Ordinal)),
            "No-path attendance did not publish the typed failure and release its festival lease.");
        hazards.Fire = true;
        runtime.Advance();
        hazards.Fire = false;
        Pause();
        lines.Add(
            "[PASS] real AI festival lease and movement; EmergencyNeed preemption pauses personal attendance; release rejoins; no-path publishes failure and releases ownership");
    }

    private void VerifyHostThenDeadlineCancellation(BuildableObject venueFacility)
    {
        IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
        int year = 2;
        int festivalDay = FestivalExecutionRules.FestivalAbsoluteDay(definition, year);
        calendar.SetDateTime(
            FestivalExecutionRules.AnnouncementAbsoluteDay(definition, year),
            8);
        int stockBefore = CountItem(ItemId);
        int findCallsBefore = venueQuery.FindCalls.Count;
        CharacterId participant0 = CharacterPersistentIdentity.Require(participants[0]);
        CharacterId participant1 = CharacterPersistentIdentity.Require(participants[1]);
        CharacterId participant2 = CharacterPersistentIdentity.Require(participants[2]);
        bool scheduled = runtime.Schedule(new FestivalScheduleRequest
        {
            ActionId = V21ContentAlertActionIds.Festival(FestivalId, year),
            FestivalId = FestivalId,
            OccurrenceYear = year,
            ParticipantIds = new[]
            {
                participant2,
                participant0,
                participant0
            }
        }, out FestivalPreparedOrder order, out DomainFailure failure);
        Require(scheduled && !failure.IsFailure && order != null
            && order.ParticipantIds.Count == 2
            && order.ParticipantIds[0].Equals(participant0)
            && order.ParticipantIds[1].Equals(participant2)
            && string.Equals(order.FacilityInstanceId,
                venueFacility.RequirePersistentInstanceId().Value,
                StringComparison.Ordinal),
            "Actual host command replaced, duplicated, or reordered the submitted roster: "
            + failure);
        Require(venueQuery.FindCalls.Count == findCallsBefore + 2
            && IsExactVenueCall(
                venueQuery.FindCalls[findCallsBefore],
                2,
                participant0,
                participant1)
            && IsExactVenueCall(
                venueQuery.FindCalls[findCallsBefore + 1],
                2,
                participant0,
                participant2),
            "Preview or host scheduling did not query the venue with its exact canonical roster and capacity.");
        FestivalExecutionOccurrenceSaveData preparing = runtime.Occurrences.Single();
        Require(preparing.phase == FestivalExecutionPhase.Preparing
            && preparing.attendance.Count == 2
            && preparing.attendance.Select(value => value.characterId)
                .SequenceEqual(new[] { participant0.Value, participant2.Value })
            && preparing.inputOwnerActive
            && preparing.inputCapacityGrams > 0L,
            "Host command did not preserve the submitted roster with physical input ownership.");

        calendar.SetDateTime(festivalDay, FestivalExecutionRules.StartHour);
        int validateCallsBefore = venueQuery.ValidateCalls.Count;
        runtime.Advance();
        Require(venueQuery.ValidateCalls.Count == validateCallsBefore + 2
            && venueQuery.ValidateCalls
                .Skip(validateCallsBefore)
                .All(value => IsExactVenueCall(
                    value,
                    2,
                    participant0,
                    participant2)),
            "Deadline revalidation did not preserve the exact submitted roster and capacity.");
        FestivalExecutionOccurrenceSaveData cancelled = runtime.Occurrences.Single();
        Require(cancelled.phase == FestivalExecutionPhase.Cancelled
            && !cancelled.inputOwnerActive
            && !cancelled.materialReceiptPending
            && cancelled.cleanupComplete
            && CountItem(ItemId) == stockBefore,
            "Deadline cancellation consumed stock or leaked input ownership.");

        const int underMinimumYear = 4;
        calendar.SetDateTime(
            FestivalExecutionRules.AnnouncementAbsoluteDay(
                definition,
                underMinimumYear),
            8);
        bool underMinimumScheduled = runtime.Schedule(new FestivalScheduleRequest
        {
            ActionId = V21ContentAlertActionIds.Festival(
                FestivalId,
                underMinimumYear),
            FestivalId = FestivalId,
            OccurrenceYear = underMinimumYear,
            ParticipantIds = new[]
            {
                CharacterPersistentIdentity.Require(participants[1])
            }
        }, out _, out DomainFailure underMinimumFailure);
        Require(!underMinimumScheduled
            && underMinimumFailure.IsFailure
            && !runtime.HasOccurrence(FestivalExecutionRules.OccurrenceId(
                FestivalId,
                underMinimumYear)),
            "An explicit roster below the authored minimum was silently filled with other residents.");
        string stable = JsonUtility.ToJson(runtime.CaptureFestivalExecutions());
        runtime.Advance();
        Require(JsonUtility.ToJson(runtime.CaptureFestivalExecutions()) == stable,
            "Terminal cancellation changed on retry.");
        lines.Add("[PASS] explicit roster canonicalized without replacement; below-minimum roster rejected; host -> physical input owner -> 18:00 cancellation; stock preserved; retry stable");
    }

    private void VerifyDeclineHasNoCost()
    {
        int year = 3;
        IGameCalendar calendar = scope.Container.Resolve<IGameCalendar>();
        calendar.SetDateTime(
            FestivalExecutionRules.AnnouncementAbsoluteDay(definition, year),
            8);
        int stockBefore = CountItem(ItemId);
        Require(runtime.Decline(FestivalId, year, out DomainFailure failure)
            && !failure.IsFailure,
            "Decline command failed: " + failure);
        FestivalExecutionOccurrenceSaveData declined = runtime.Occurrences.Single(value =>
            value.occurrenceYear == year);
        Require(declined.phase == FestivalExecutionPhase.Declined
            && declined.cleanupComplete
            && !declined.inputOwnerActive
            && !declined.materialReceiptPending
            && CountItem(ItemId) == stockBefore,
            "Decline created a resource or ownership side effect.");
        lines.Add("[PASS] decline is cost-free, terminal and idempotent for the occurrence");
    }

    private void VerifyAttendanceBoundariesAndHazard(Vector2Int[] cells)
    {
        celebrated.Clear();
        PublishOnly(BuildRunningOccurrence(10, cells, 24.9f, 25f, 24.9f));
        hazards.Fire = false;
        runtime.Advance();
        FestivalCelebratedEvent partial = celebrated.Single();
        Require(partial.Grade == FestivalResolutionGrade.Partial
            && partial.ParticipantIds.Count == 1
            && partial.ParticipantIds.Any(id => id.Equals(
                CharacterPersistentIdentity.Require(participants[1])))
            && !partial.ParticipantIds.Any(id => id.Equals(
                CharacterPersistentIdentity.Require(participants[0])))
            && !partial.ParticipantIds.Any(id => id.Equals(
                CharacterPersistentIdentity.Require(participants[2]))),
            "24.9/25/24.9 attendance boundary did not produce the exact single valid participant required for partial success.");
        Require(Approximately(FestivalExecutionRules.ScalePersonalNumericBenefit(
                8f, 0.25f), 2f)
            && Approximately(FestivalExecutionRules.ScalePersonalNumericBenefit(
                8f, 1f), 8f),
            "Personal numeric benefit scaling changed.");

        celebrated.Clear();
        PublishOnly(BuildRunningOccurrence(11, cells, 100f, 100f, 100f));
        runtime.Advance();
        Require(celebrated.Single().Grade == FestivalResolutionGrade.Success
            && celebrated[0].ParticipantIds.Count == 3,
            "All valid participants did not resolve success.");

        celebrated.Clear();
        PublishOnly(BuildRunningOccurrence(12, cells, 0f, 0f, 0f));
        runtime.Advance();
        Require(celebrated.Single().Grade == FestivalResolutionGrade.Failure
            && celebrated[0].ParticipantIds.Count == 0,
            "No valid participants did not resolve failure.");

        celebrated.Clear();
        PublishOnly(BuildRunningOccurrence(13, cells, 100f, 100f, 100f));
        hazards.Fire = true;
        runtime.Advance();
        FestivalCelebratedEvent stopped = celebrated.Single();
        FestivalExecutionOccurrenceSaveData terminal = runtime.Occurrences.Single();
        Require(stopped.StoppedByVenueHazard
            && terminal.phase == FestivalExecutionPhase.Stopped
            && terminal.effectsApplied
            && terminal.resultReason.Contains("화재", StringComparison.Ordinal),
            "Direct venue fire did not stop and evacuate the occurrence.");
        int eventCount = celebrated.Count;
        runtime.Advance();
        Require(celebrated.Count == eventCount,
            "Terminal fire stop published duplicate festival effects.");
        hazards.Fire = false;
        lines.Add("[PASS] exact 24.9/25/24.9 attendance boundary, success/partial/failure and direct fire stop; terminal retry no duplicate");
    }

    private void VerifyPersistenceAndPhysicalJoin()
    {
        FestivalExecutionWorldSaveData before = runtime.CaptureFestivalExecutions();
        string beforeJson = JsonUtility.ToJson(before);
        FestivalExecutionWorldSaveData replacement = new()
        {
            occurrences = new List<FestivalExecutionOccurrenceSaveData>
            {
                BuildDeclinedOccurrence(20)
            }
        };
        FestivalExecutionAggregateState replacementCandidate =
            runtime.PrepareFestivalExecutionRestore(replacement);
        runtime.BeginRestoreCandidate();
        runtime.PublishFestivalExecutionRestore(replacementCandidate);
        runtime.PublishRestoreCandidate();
        Require(runtime.Occurrences.Count == 1
            && runtime.Occurrences[0].occurrenceYear == 20,
            "Staged restore did not publish its detached candidate.");
        runtime.RollbackPublishedRestoreCandidate();
        Require(JsonUtility.ToJson(runtime.CaptureFestivalExecutions()) == beforeJson,
            "Published restore rollback did not recover exact prior authority.");

        FestivalExecutionSaveSection.ValidatePhysicalRestoreJoin(
            before,
            new FixedPhysicalCandidate(Array.Empty<
                PhysicalItemRestoreCandidateDispositionSnapshot>()));
        bool orphanRejected = false;
        try
        {
            FestivalExecutionSaveSection.ValidatePhysicalRestoreJoin(
                before,
                new FixedPhysicalCandidate(new[]
                {
                    new PhysicalItemRestoreCandidateDispositionSnapshot(
                        PhysicalItemDispositionKind.Sink,
                        FestivalExecutionRules.MaterialOperationId(
                            FestivalExecutionRules.OccurrenceId(FestivalId, 99)),
                        FestivalExecutionRules.MaterialReasonCode,
                        "fingerprint:wim041",
                        new[] { "stack:wim041" },
                        1,
                        1,
                        "commit:wim041")
                }));
        }
        catch (InvalidOperationException)
        {
            orphanRejected = true;
        }
        Require(orphanRejected,
            "Physical festival sink without an execution owner was accepted.");
        lines.Add("[PASS] detached current-format capture, staged publication rollback, exact physical join and orphan receipt rejection");
    }

    private FestivalExecutionOccurrenceSaveData BuildRunningOccurrence(
        int year,
        IReadOnlyList<Vector2Int> cells,
        params float[] attendancePercent)
    {
        Require(attendancePercent.Length == participants.Length,
            "Attendance fixture length changed.");
        float duration = 100f;
        int day = FestivalExecutionRules.FestivalAbsoluteDay(definition, year);
        return new FestivalExecutionOccurrenceSaveData
        {
            occurrenceId = FestivalExecutionRules.OccurrenceId(FestivalId, year),
            actionId = "festival-action:wim041:" + year,
            festivalId = FestivalId,
            occurrenceYear = year,
            phase = FestivalExecutionPhase.Running,
            festivalAbsoluteDay = day,
            startHour = FestivalExecutionRules.StartHour,
            deadlineAbsoluteDay = day,
            deadlineHour = FestivalExecutionRules.StartHour,
            requiredPreparationWork = 1f,
            completedPreparationWork = 1f,
            plannedDurationSeconds = duration,
            elapsedFestivalSeconds = duration,
            venueFacilityInstanceId = venue.FacilityInstanceId,
            venueCapacity = participants.Length,
            venueCells = cells.Select(cell => new FestivalExecutionCellSaveData
                { x = cell.x, y = cell.y }).ToList(),
            itemCosts = new List<FestivalExecutionItemSaveData>
            {
                new() { itemId = ItemId, quantity = 1 }
            },
            attendance = participants.Select((actor, index) =>
                new FestivalAttendanceProgressSaveData
                {
                    characterId = CharacterPersistentIdentity.Require(actor).Value,
                    assignedCellX = cells[index].x,
                    assignedCellY = cells[index].y,
                    attendedSeconds = duration * attendancePercent[index] / 100f
                }).ToList()
        };
    }

    private FestivalExecutionOccurrenceSaveData BuildInProgressOccurrence(
        int year,
        IReadOnlyList<Vector2Int> cells)
    {
        FestivalExecutionOccurrenceSaveData value =
            BuildRunningOccurrence(year, cells, 0f, 0f, 0f);
        value.plannedDurationSeconds = 120f;
        value.elapsedFestivalSeconds = 0f;
        return value;
    }

    private FestivalExecutionOccurrenceSaveData BuildDeclinedOccurrence(int year)
    {
        int day = FestivalExecutionRules.FestivalAbsoluteDay(definition, year);
        return new FestivalExecutionOccurrenceSaveData
        {
            occurrenceId = FestivalExecutionRules.OccurrenceId(FestivalId, year),
            festivalId = FestivalId,
            occurrenceYear = year,
            phase = FestivalExecutionPhase.Declined,
            festivalAbsoluteDay = day,
            startHour = FestivalExecutionRules.StartHour,
            deadlineAbsoluteDay = day,
            deadlineHour = FestivalExecutionRules.StartHour,
            cleanupComplete = true,
            resultReason = "player-declined",
            resultSummary = "fixture"
        };
    }

    private void PublishOnly(FestivalExecutionOccurrenceSaveData occurrence)
    {
        FestivalExecutionWorldSaveData payload = new()
        {
            occurrences = new List<FestivalExecutionOccurrenceSaveData>
                { occurrence }
        };
        runtime.PublishFestivalExecutionRestore(
            runtime.PrepareFestivalExecutionRestore(payload));
    }

    private int CountItem(string itemId) => scope.Container
        .Resolve<IWorldItemStackRuntime>()
        .GetAllStacks()
        .Where(value => value != null
            && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
        .Sum(value => value.TotalQuantity);

    private static FestivalDefinitionSO CreateDefinition()
    {
        FestivalDefinitionSO value =
            ScriptableObject.CreateInstance<FestivalDefinitionSO>();
        value.festivalId = FestivalId;
        value.displayName = "WIM-041 실행 검증제";
        value.description = "축제 실행 상태 경계를 검증하는 일회성 테스트 정의";
        value.authoringRevision = 1;
        value.season = Season.Spring;
        value.dayOfSeason = 10;
        value.cultureId = string.Empty;
        value.minimumParticipants = 2;
        value.venueRequirements = new DungeonStory.Buildings.FacilityVenueRequirements
        {
            minimumEventCells = 3
        };
        value.requiredItems = new List<FestivalItemRequirement>
        {
            new() { itemDefinitionId = ItemId, amount = 1 }
        };
        value.successOutcome = new FestivalOutcomeDefinition
            { moodDelta = 8f, moodDurationDays = 2 };
        value.partialOutcome = new FestivalOutcomeDefinition
            { moodDelta = 4f, moodDurationDays = 1 };
        value.failureOutcome = new FestivalOutcomeDefinition
            { moodDelta = -2f, moodDurationDays = 1 };
        return value;
    }

    private static SocietyVenueCandidateSnapshot CreateVenue(
        BuildableObject facility,
        IReadOnlyList<Vector2Int> cells)
    {
        SocietyVenueCandidateSnapshot value = new();
        SetProperty(value, nameof(value.Facility), facility);
        SetProperty(value, nameof(value.FacilityInstanceId),
            facility.RequirePersistentInstanceId().Value);
        SetProperty(value, nameof(value.DisplayName), "WIM-041 기존 시설");
        SetProperty(value, nameof(value.AnchorCenter), facility.centerPos);
        SetProperty(value, nameof(value.AccessCell), facility.centerPos);
        SetProperty(value, nameof(value.EventCells), cells.ToArray());
        return value;
    }

    private static void SetProperty<T>(object target, string name, T value)
    {
        PropertyInfo property = target.GetType().GetProperty(
            name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Require(property?.SetMethod != null,
            "Missing venue snapshot property: " + name);
        property.SetValue(target, value);
    }

    private static void Click(string objectName)
    {
        Button button = Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(value => value != null
                && value.gameObject.scene.IsValid()
                && value.gameObject.activeInHierarchy
                && string.Equals(value.name, objectName, StringComparison.Ordinal));
        Require(button != null && button.interactable,
            "Missing UI button: " + objectName);
        button.onClick.Invoke();
    }

    private void Pause()
    {
        GameManager manager = FindFirstObjectByType<GameManager>();
        if (manager != null) manager.isPause = true;
        if (timeScale != null) timeScale.Scale = 0;
    }

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.0001f;

    private static bool IsExactVenueCall(
        FestivalVenueCall call,
        int requiredEventCapacity,
        params CharacterId[] participantIds) =>
        call != null
        && call.RequiredEventCapacity == requiredEventCapacity
        && call.ParticipantIds.SequenceEqual(participantIds);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class SingleFestivalCatalog : IFestivalDefinitionCatalog
    {
        private readonly FestivalDefinitionSO definition;
        public SingleFestivalCatalog(FestivalDefinitionSO definition) =>
            this.definition = definition;
        public IReadOnlyList<FestivalDefinitionSO> All => new[] { definition };
        public FestivalDefinitionSO Require(string festivalId) =>
            string.Equals(festivalId?.Trim(), definition.StableId,
                StringComparison.Ordinal)
                ? definition
                : throw new KeyNotFoundException(festivalId);
    }

    private sealed class FixedVenueQuery : ISocietyVenueQuery
    {
        private readonly SocietyVenueCandidateSnapshot venue;
        private readonly List<FestivalVenueCall> findCalls = new();
        private readonly List<FestivalVenueCall> validateCalls = new();

        public IReadOnlyList<FestivalVenueCall> FindCalls => findCalls;
        public IReadOnlyList<FestivalVenueCall> ValidateCalls => validateCalls;

        public FixedVenueQuery(SocietyVenueCandidateSnapshot venue) =>
            this.venue = venue;

        public bool TryFindDeliveryVenues(
            DungeonStory.Buildings.FacilityVenueRequirements requirements,
            Vector2Int supplyOrigin,
            string excludedClaimOwnerId,
            out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
            out string failureReason)
        {
            candidates = Array.Empty<SocietyVenueCandidateSnapshot>();
            failureReason = "not-used";
            return false;
        }

        public bool TryFindFestivalVenues(
            DungeonStory.Buildings.FacilityVenueRequirements requirements,
            IReadOnlyCollection<CharacterId> participantIds,
            int requiredEventCapacity,
            out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
            out string failureReason)
        {
            findCalls.Add(new FestivalVenueCall(
                participantIds,
                requiredEventCapacity));
            bool accepted = participantIds != null
                && participantIds.Count > 0
                && requiredEventCapacity <= venue.EventCells.Count;
            candidates = accepted
                ? new[] { venue }
                : Array.Empty<SocietyVenueCandidateSnapshot>();
            failureReason = accepted ? string.Empty : "controlled venue capacity";
            return accepted;
        }

        public bool TryValidateDeliveryVenue(
            DungeonStory.Buildings.FacilityVenueRequirements requirements,
            Vector2Int supplyOrigin,
            string claimOwnerId,
            string facilityInstanceId,
            Vector2Int anchorCenter,
            out SocietyVenueCandidateSnapshot candidate,
            out string failureReason)
        {
            candidate = null;
            failureReason = "not-used";
            return false;
        }

        public bool TryValidateFestivalVenue(
            DungeonStory.Buildings.FacilityVenueRequirements requirements,
            IReadOnlyCollection<CharacterId> participantIds,
            int requiredEventCapacity,
            string facilityInstanceId,
            out SocietyVenueCandidateSnapshot candidate,
            out string failureReason)
        {
            validateCalls.Add(new FestivalVenueCall(
                participantIds,
                requiredEventCapacity));
            bool accepted = participantIds != null
                && participantIds.Count > 0
                && requiredEventCapacity <= venue.EventCells.Count
                && string.Equals(facilityInstanceId,
                    venue.FacilityInstanceId, StringComparison.Ordinal);
            candidate = accepted ? venue : null;
            failureReason = accepted ? string.Empty : "controlled venue invalid";
            return accepted;
        }
    }

    private sealed class FestivalVenueCall
    {
        public FestivalVenueCall(
            IReadOnlyCollection<CharacterId> participantIds,
            int requiredEventCapacity)
        {
            ParticipantIds = (participantIds ?? Array.Empty<CharacterId>())
                .ToArray();
            RequiredEventCapacity = requiredEventCapacity;
        }

        public IReadOnlyList<CharacterId> ParticipantIds { get; }
        public int RequiredEventCapacity { get; }
    }

    private sealed class ToggleHazardQuery : IWorldHazardZoneQuery
    {
        public bool Fire { get; set; }
        public int Version => Fire ? 1 : 0;
        public WorldHazardSnapshot GetHazard(
            CharacterId characterId,
            Vector2Int position) => new(
            position,
            Fire ? WorldHazardLevel.Forbidden : WorldHazardLevel.Safe,
            Fire ? WorldHazardFlags.Fire : WorldHazardFlags.None);
    }

    private sealed class FixedPhysicalCandidate : IPhysicalItemRestoreCandidateQuery
    {
        private readonly IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            values;
        public FixedPhysicalCandidate(
            IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot> values) =>
            this.values = values;
        public bool IsCandidateAvailable => true;
        public IReadOnlyList<PhysicalItemRestoreCandidateDispositionSnapshot>
            PendingBatchDispositions => values;
        public bool TryGetPendingBatchDisposition(
            string operationId,
            out PhysicalItemRestoreCandidateDispositionSnapshot disposition)
        {
            disposition = values.FirstOrDefault(value => string.Equals(
                value.OperationId, operationId, StringComparison.Ordinal));
            return disposition != null;
        }
    }
}
#endif
