#if UNITY_EDITOR
using System;
using DungeonStory.ServiceRooms;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Aggregate-only regression coverage for the persisted service-session clock.
/// This intentionally constructs no authored assets or runtime/ledger adapters.
/// </summary>
public static class ServiceSessionClockDebugScenarios
{
    private const float Epsilon = 0.0001f;

    [MenuItem("DungeonStory/QA/Service Rooms/Run Session Clock Scenarios")]
    private static void RunFromMenu() => RunAll();

    public static bool RunAll(bool log = true)
    {
        VerifySaveFormatAndInitialRestoreMapping();
        VerifyUnadvancedJsonRecapturesExactTimeline();
        VerifyProjectedTransitionsAndSecondRestore();
        VerifyTimelineAndClockRejections();
        VerifyProvisionRestoreRejections();
        VerifyInvalidStageTransitionIsAtomic();
        VerifyBackdatedTransitionCommandsAreAtomic();
        VerifySecondBindPreservesOriginalMapping();

        if (log)
        {
            Debug.Log(
                "Service session clock scenarios PASS: saved logical time, "
                + "cold-restore epoch binding, monotonic transitions, and "
                + "clock rejection/atomicity.");
        }

        return true;
    }

    private static void VerifySaveFormatAndInitialRestoreMapping()
    {
        Require(ServiceRoomsSaveData.CurrentVersion == 7,
            "Service-room save payload must be version 7.");

        ServiceSessionAggregate source = CreateStartedAggregate(
            100f,
            out ServiceSessionSnapshot started);
        ServiceRoomsSaveData saved = source.Capture(110f);
        RequireNear(saved.timelineTime, 110f,
            "Capture did not persist its exact logical timeline.");
        Require(saved.sessions.Count == 1
                && saved.sessions[0].sessionId == started.SessionId
                && Near(saved.sessions[0].startedAt, 100f)
                && Near(saved.sessions[0].stageStartedAt, 100f),
            "Initial service timestamps changed while capturing a logical clock.");

        ServiceSessionAggregate restored = ServiceSessionAggregate.CreateRestored(
            RoundTrip(saved),
            source.Version + 1);
        ExpectThrows(() => restored.GetTimelineTime(2f),
            "An unbound restored service aggregate exposed a timeline.");

        restored.BindRuntimeClock(2f);
        RequireNear(restored.GetTimelineTime(2f), 110f,
            "Restored raw epoch did not map to its saved logical time.");
        RequireNear(restored.GetTimelineTime(5f), 113f,
            "Restored raw clock did not advance from the saved logical time.");
        RequireNear(restored.Capture().timelineTime, 110f,
            "A timeline read advanced the parameterless capture watermark.");
        RequireNear(
            restored.Capture(restored.GetTimelineTime(5f)).timelineTime,
            113f,
            "Explicit capture did not persist the projected logical time.");
    }

    private static void VerifyUnadvancedJsonRecapturesExactTimeline()
    {
        ServiceSessionAggregate source = CreateStartedAggregate(
            100f,
            out _);
        ServiceRoomsSaveData serializedSave = RoundTrip(source.Capture(110f));
        ServiceSessionAggregate restored = ServiceSessionAggregate.CreateRestored(
            serializedSave,
            source.Version + 1);

        restored.BindRuntimeClock(2f);
        ServiceRoomsSaveData recaptured = RoundTrip(restored.Capture());
        RequireNear(recaptured.timelineTime, 110f,
            "A same-JSON restore recapture changed the timeline before advancement.");
        Require(recaptured.sessions.Count == 1
                && Near(recaptured.sessions[0].startedAt, 100f)
                && Near(recaptured.sessions[0].stageStartedAt, 100f),
            "A same-JSON restore recapture rewrote stored session timestamps.");
    }

    private static void VerifyProjectedTransitionsAndSecondRestore()
    {
        ServiceSessionAggregate source = CreateStartedAggregate(
            100f,
            out ServiceSessionSnapshot initial);
        ServiceSessionAggregate restored = ServiceSessionAggregate.CreateRestored(
            RoundTrip(source.Capture(110f)),
            source.Version + 1);
        restored.BindRuntimeClock(2f);

        Require(restored.TrySetStage(
                    initial.SessionId,
                    ServiceSessionStage.Payment,
                    restored.GetTimelineTime(3f),
                    out DomainFailure stageFailure)
                && !stageFailure.IsFailure,
            "A forward projected service stage was rejected.");
        Require(restored.CancelSession(
                    initial.SessionId,
                    "qa.clock.cancel",
                    restored.GetTimelineTime(4f)),
            "A projected service cancellation was rejected.");

        Require(restored.TryGetSession(
                    initial.SessionId,
                    out ServiceSessionSnapshot cancelled)
                && cancelled.Stage == ServiceSessionStage.Cancelled
                && Near(cancelled.StartedAt, 100f)
                && Near(cancelled.StageStartedAt, 112f)
                && Near(cancelled.ProvidedServiceSeconds, 1f),
            "Cancellation rewrote or reversed the original projected timeline.");

        Require(restored.TryBegin(
                    CreateBeginCommand(
                        "actor:clock-complete",
                        restored.GetTimelineTime(4f)),
                    out ServiceSessionSnapshot completing,
                    out DomainFailure beginFailure)
                && !beginFailure.IsFailure,
            "A projected service begin was rejected.");
        Require(restored.TrySetStage(
                    completing.SessionId,
                    ServiceSessionStage.Payment,
                    restored.GetTimelineTime(4f),
                    out stageFailure)
                && !stageFailure.IsFailure,
            "A projected completion stage was rejected.");
        Require(restored.TryComplete(
                    completing.SessionId,
                    restored.GetTimelineTime(5f),
                    out ServiceSessionCompletionTransition completion,
                    out DomainFailure completionFailure)
                && !completionFailure.IsFailure
                && completion?.Completed?.Stage == ServiceSessionStage.Completed,
            "A projected service completion was rejected.");

        Require(restored.TryGetSession(
                completing.SessionId,
                out ServiceSessionSnapshot completed)
                && Near(completed.StartedAt, 112f)
                && Near(completed.StageStartedAt, 113f)
                && Near(completed.ProvidedServiceSeconds, 1f),
            "Completion rewrote or reversed projected session timestamps.");

        ServiceRoomsSaveData recaptured = RoundTrip(restored.Capture());
        RequireNear(recaptured.timelineTime, 113f,
            "Projected transitions did not retain the latest logical time.");

        ServiceSessionAggregate secondRestore =
            ServiceSessionAggregate.CreateRestored(
                recaptured,
                restored.Version + 1);
        secondRestore.BindRuntimeClock(1f);
        RequireNear(secondRestore.GetTimelineTime(1f), 113f,
            "Second restore did not reset to the saved logical timeline.");
        RequireNear(secondRestore.GetTimelineTime(4f), 116f,
            "Second restore did not advance from its new raw epoch.");
    }

    private static void VerifyProvisionRestoreRejections()
    {
        ServiceSessionAggregate partial = CreateStartedAggregate(
            100f,
            out ServiceSessionSnapshot partialSession);
        Require(partial.CancelSession(
                partialSession.SessionId,
                "qa.clock.partial",
                100.5f),
            "Partial-provision restore fixture could not cancel.");
        ServiceRoomsSaveData validPartial = RoundTrip(partial.Capture());
        Require(validPartial.sessions.Count == 1
                && Near(validPartial.sessions[0].providedServiceSeconds, 0.5f),
            "Partial-provision restore fixture did not freeze its actual duration.");
        _ = ServiceSessionAggregate.CreateRestored(
            RoundTrip(validPartial),
            partial.Version + 1);

        ServiceRoomsSaveData negative = RoundTrip(validPartial);
        negative.sessions[0].providedServiceSeconds = -0.01f;
        ExpectThrows(
            () => ServiceSessionAggregate.CreateRestored(
                negative,
                partial.Version + 1),
            "Restore accepted a negative provided-service duration.");

        ServiceRoomsSaveData overContract = RoundTrip(validPartial);
        overContract.sessions[0].providedServiceSeconds = 1.01f;
        ExpectThrows(
            () => ServiceSessionAggregate.CreateRestored(
                overContract,
                partial.Version + 1),
            "Restore accepted provided service beyond the frozen contract.");

        ServiceSessionAggregate completed = CreateStartedAggregate(
            200f,
            out ServiceSessionSnapshot completedSession);
        Require(completed.TryComplete(
                completedSession.SessionId,
                201f,
                out _,
                out DomainFailure completedFailure)
                && !completedFailure.IsFailure,
            "Completed-provision restore fixture could not complete.");
        ServiceRoomsSaveData incompleteCompleted = RoundTrip(
            completed.Capture());
        incompleteCompleted.sessions[0].providedServiceSeconds = 0.5f;
        ExpectThrows(
            () => ServiceSessionAggregate.CreateRestored(
                incompleteCompleted,
                completed.Version + 1),
            "Restore accepted a completed session with partial provision.");

        ServiceSessionAggregate preService = new();
        Require(preService.TryBegin(
                CreateBeginCommand(
                    "actor:clock-pre-service",
                    300f,
                    ServiceProcessStageMask.Reception
                    | ServiceProcessStageMask.Service),
                out ServiceSessionSnapshot preServiceSession,
                out DomainFailure preServiceFailure)
                && !preServiceFailure.IsFailure
                && preService.CancelSession(
                    preServiceSession.SessionId,
                    "qa.clock.pre-service",
                    300.25f),
            "Pre-service provision fixture could not cancel.");
        ServiceRoomsSaveData falseProvision = RoundTrip(
            preService.Capture());
        Require(falseProvision.sessions.Count == 1
                && falseProvision.sessions[0].providedServiceSeconds == 0f,
            "Pre-service cancellation invented provided service.");
        falseProvision.sessions[0].providedServiceSeconds = 0.25f;
        ExpectThrows(
            () => ServiceSessionAggregate.CreateRestored(
                falseProvision,
                preService.Version + 1),
            "Restore accepted service provision before the service stage.");
    }

    private static void VerifyTimelineAndClockRejections()
    {
        ServiceSessionAggregate aggregate = CreateStartedAggregate(
            100f,
            out ServiceSessionSnapshot session);
        Require(aggregate.TrySetStage(
                    session.SessionId,
                    ServiceSessionStage.Payment,
                    105f,
                    out DomainFailure stageFailure)
                && !stageFailure.IsFailure,
            "Timeline rejection fixture could not reach payment.");
        ServiceRoomsSaveData valid = aggregate.Capture(110f);

        AssertRejectedCapture(aggregate, -1f, "negative timeline");
        AssertRejectedCapture(aggregate, float.NaN, "NaN timeline");
        AssertRejectedCapture(
            aggregate,
            float.PositiveInfinity,
            "infinite timeline");
        RequireNear(aggregate.Capture(109f).timelineTime, 109f,
            "A read-only capture rejected a timeline after the last stage.");
        RequireNear(aggregate.Capture().timelineTime, 105f,
            "Read-only explicit captures changed the committed timeline watermark.");
        AssertRejectedCapture(aggregate, 104f, "timeline before the last stage");

        ServiceRoomsSaveData earlierThanStage = RoundTrip(valid);
        earlierThanStage.timelineTime = 104f;
        ExpectThrows(
            () => ServiceSessionAggregate.CreateRestored(
                earlierThanStage,
                aggregate.Version + 1),
            "Restore accepted a timeline before a saved session stage.");

        ServiceRoomsSaveData legacy = RoundTrip(valid);
        legacy.version = 3;
        ExpectThrows(
            () => ServiceSessionAggregate.CreateRestored(
                legacy,
                aggregate.Version + 1),
            "Restore accepted legacy service-room version 3.");

        ServiceSessionAggregate restored = ServiceSessionAggregate.CreateRestored(
            RoundTrip(valid),
            aggregate.Version + 1);
        ExpectThrows(() => restored.BindRuntimeClock(-1f),
            "Clock binding accepted a negative raw time.");
        ExpectThrows(() => restored.BindRuntimeClock(float.NaN),
            "Clock binding accepted NaN.");
        ExpectThrows(() => restored.BindRuntimeClock(float.PositiveInfinity),
            "Clock binding accepted infinity.");
        ExpectThrows(() => restored.GetTimelineTime(2f),
            "An unbound restored clock was readable after rejected binds.");

        restored.BindRuntimeClock(2f);
        ExpectThrows(() => restored.GetTimelineTime(float.NaN),
            "Clock read accepted NaN.");
        ExpectThrows(() => restored.GetTimelineTime(float.PositiveInfinity),
            "Clock read accepted infinity.");
        ExpectThrows(() => restored.GetTimelineTime(-1f),
            "Clock read accepted a negative raw time.");
        ExpectThrows(() => restored.GetTimelineTime(1f),
            "Clock read accepted a backwards raw time.");
        RequireNear(restored.GetTimelineTime(3f), 111f,
            "Rejected source clocks changed the restored timeline mapping.");
    }

    private static void VerifyInvalidStageTransitionIsAtomic()
    {
        ServiceSessionAggregate aggregate = CreateStartedAggregate(
            100f,
            out ServiceSessionSnapshot session);
        Require(aggregate.TrySetStage(
                    session.SessionId,
                    ServiceSessionStage.Payment,
                    110f,
                    out DomainFailure stageFailure)
                && !stageFailure.IsFailure,
            "Invalid-transition fixture could not reach payment.");
        ServiceRoomsSaveData beforeCapture = aggregate.Capture(110f);
        string beforeJson = JsonUtility.ToJson(beforeCapture);
        int beforeVersion = aggregate.Version;

        Require(!aggregate.TrySetStage(
                    session.SessionId,
                    ServiceSessionStage.Waiting,
                    111f,
                    out DomainFailure rejectedFailure)
                && rejectedFailure.IsFailure,
            "Service aggregate accepted a stage outside its contract mask.");
        Require(aggregate.Version == beforeVersion,
            "Rejected contract-mask stage changed aggregate version.");
        Require(JsonUtility.ToJson(aggregate.Capture()) == beforeJson,
            "Rejected contract-mask stage changed captured aggregate state.");
    }

    private static void VerifyBackdatedTransitionCommandsAreAtomic()
    {
        ServiceSessionAggregate stageAggregate =
            CreateAggregateAtCommittedServiceStage(
                out ServiceSessionSnapshot stageSession);
        AssertBackdatedTransitionIsAtomic(
            stageAggregate,
            () =>
            {
                stageAggregate.TrySetStage(
                    stageSession.SessionId,
                    ServiceSessionStage.Payment,
                    104f,
                    out _);
            },
            "stage");

        ServiceSessionAggregate completionAggregate =
            CreateAggregateAtCommittedServiceStage(
                out ServiceSessionSnapshot completionSession);
        AssertBackdatedTransitionIsAtomic(
            completionAggregate,
            () =>
            {
                completionAggregate.TryComplete(
                    completionSession.SessionId,
                    104f,
                    out _,
                    out _);
            },
            "completion");

        ServiceSessionAggregate cancellationAggregate =
            CreateAggregateAtCommittedServiceStage(
                out ServiceSessionSnapshot cancellationSession);
        AssertBackdatedTransitionIsAtomic(
            cancellationAggregate,
            () =>
            {
                cancellationAggregate.CancelSession(
                    cancellationSession.SessionId,
                    "qa.clock.backdated-cancel",
                    104f);
            },
            "session cancellation");

        ServiceSessionAggregate hubCancellationAggregate =
            CreateAggregateAtCommittedServiceStage(out _);
        AssertBackdatedTransitionIsAtomic(
            hubCancellationAggregate,
            () =>
            {
                hubCancellationAggregate.CancelHubSessions(
                    "hub:clock",
                    "qa.clock.backdated-hub-cancel",
                    104f);
            },
            "hub cancellation");
    }

    private static void VerifySecondBindPreservesOriginalMapping()
    {
        ServiceSessionAggregate source = CreateStartedAggregate(
            100f,
            out _);
        ServiceSessionAggregate restored = ServiceSessionAggregate.CreateRestored(
            RoundTrip(source.Capture(110f)),
            source.Version + 1);
        restored.BindRuntimeClock(2f);
        RequireNear(restored.GetTimelineTime(2f), 110f,
            "Initial bind did not establish the expected mapping.");

        ExpectThrows(() => restored.BindRuntimeClock(30f),
            "Service aggregate accepted a second runtime clock bind.");
        RequireNear(restored.GetTimelineTime(5f), 113f,
            "Rejected second bind changed the original clock mapping.");
    }

    private static ServiceSessionAggregate CreateStartedAggregate(
        float startedAt,
        out ServiceSessionSnapshot session)
    {
        ServiceSessionAggregate aggregate = new();
        Require(aggregate.TryBegin(
                    CreateBeginCommand("actor:clock-initial", startedAt),
                    out session,
                    out DomainFailure failure)
                && !failure.IsFailure,
            "Clock fixture begin command was rejected.");
        return aggregate;
    }

    private static ServiceSessionAggregate CreateAggregateAtCommittedServiceStage(
        out ServiceSessionSnapshot session)
    {
        ServiceSessionAggregate aggregate = new();
        Require(aggregate.TryBegin(
                    CreateBeginCommand(
                        "actor:clock-backdated",
                        100f,
                        ServiceProcessStageMask.Reception
                        | ServiceProcessStageMask.Service
                        | ServiceProcessStageMask.Payment),
                    out session,
                    out DomainFailure beginFailure)
                && !beginFailure.IsFailure,
            "Backdated-transition fixture begin command was rejected.");
        Require(aggregate.TrySetStage(
                    session.SessionId,
                    ServiceSessionStage.Service,
                    105f,
                    out DomainFailure stageFailure)
                && !stageFailure.IsFailure,
            "Backdated-transition fixture could not reach service at 105.");
        return aggregate;
    }

    private static ServiceSessionBeginCommand CreateBeginCommand(
        string actorId,
        float startedAt,
        ServiceProcessStageMask activeStages =
            ServiceProcessStageMask.Service
            | ServiceProcessStageMask.Payment) => new()
        {
            HubId = "hub:clock",
            ActorId = actorId,
            ProcessId = "service:clock",
            Category = ServiceCategory.Dining,
            Capacity = 2,
            StartedAt = startedAt,
            Contract = new ServiceSessionContractSnapshot
            {
                mode = ServiceOperationMode.Direct,
                activeStages = activeStages,
                serviceSeconds = 1f,
                paymentSeconds = 1f,
                paymentRequired = false,
                supportIds = Array.Empty<string>()
            }
        };

    private static void AssertRejectedCapture(
        ServiceSessionAggregate aggregate,
        float timelineTime,
        string description)
    {
        string beforeJson = JsonUtility.ToJson(aggregate.Capture());
        int beforeVersion = aggregate.Version;
        ExpectThrows(() => aggregate.Capture(timelineTime),
            "Capture accepted " + description + ".");
        Require(aggregate.Version == beforeVersion,
            "Rejected " + description + " changed aggregate version.");
        Require(JsonUtility.ToJson(aggregate.Capture()) == beforeJson,
            "Rejected " + description + " changed captured aggregate state.");
    }

    private static void AssertBackdatedTransitionIsAtomic(
        ServiceSessionAggregate aggregate,
        Action action,
        string description)
    {
        string beforeJson = JsonUtility.ToJson(aggregate.Capture());
        int beforeVersion = aggregate.Version;
        ExpectThrows(action,
            "Backdated " + description + " did not reject its timestamp.");
        Require(aggregate.Version == beforeVersion,
            "Rejected backdated " + description + " changed aggregate version.");
        Require(JsonUtility.ToJson(aggregate.Capture()) == beforeJson,
            "Rejected backdated " + description + " changed captured aggregate state.");
    }

    private static ServiceRoomsSaveData RoundTrip(ServiceRoomsSaveData save) =>
        JsonUtility.FromJson<ServiceRoomsSaveData>(JsonUtility.ToJson(save));

    private static void ExpectThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (ArgumentException)
        {
            return;
        }
        catch (InvalidOperationException)
        {
            return;
        }
        catch (OverflowException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static bool Near(float actual, float expected) =>
        !float.IsNaN(actual)
        && !float.IsInfinity(actual)
        && Math.Abs(actual - expected) < Epsilon;

    private static void RequireNear(float actual, float expected, string message)
    {
        if (!Near(actual, expected))
        {
            throw new InvalidOperationException(
                message + " Expected=" + expected + ", actual=" + actual + ".");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
