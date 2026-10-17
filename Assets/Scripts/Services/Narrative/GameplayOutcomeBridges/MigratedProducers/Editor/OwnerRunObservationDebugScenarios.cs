#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public static class OwnerRunObservationDebugScenarios
{
    internal static void RunOrThrow()
    {
        VerifyMapping();
        VerifyPresentationLifetime();
        Debug.Log("B109_OWNER_RUN_OBSERVATION=PASS");
    }

    private static void VerifyMapping()
    {
        OwnerRunCompletionFactData data = new()
        {
            runSeed = 109,
            ownerId = "character:b109:owner",
            displayName = "검증 사장",
            outcome = DungeonRunOutcome.Victory,
            absoluteDay = 7,
            reason = "통제된 런 완료"
        };
        MigratedDomainFactCapture capture =
            OwnerRunCompletionFactMapping.Capture(
                new OwnerRunCompletionFact(data));
        MigratedProducerOutcomeDetails details = capture.Details;
        Require(
            capture.Kind == MigratedProducerOutcomeKind.OwnerRunEndedEvent
            && capture.OwnerIdentity ==
                "owner-run-ended:109:character:b109:owner"
            && capture.AbsoluteDay == 7
            && capture.Status == GameplayOutcomeStatus.Succeeded
            && !capture.ReservationOnly,
            "Owner-run completion mapping identity changed.");
        Require(
            details.Subjects.Length == 1
            && details.Subjects[0].EntityId.Kind.Equals(
                MigratedProducerOutcomeIds.CharacterKind)
            && details.Subjects[0].EntityId.Value
                == "character:b109:owner"
            && details.Subjects[0].DisplayName.DisplayText == "검증 사장"
            && details.Subjects[0].Role.Equals(
                MigratedProducerOutcomeIds.ActorRole)
            && details.Metrics.Length == 0
            && details.Facts.Length == 0
            && details.Summary ==
                "owner-run-ended@1|109|character:b109:owner|1|7|통제된 런 완료",
            "Owner-run completion native payload changed.");

        OwnerRunCompletionFactData defeat = data.Clone();
        defeat.outcome = DungeonRunOutcome.Defeat;
        defeat.reason = "사장 사망";
        MigratedDomainFactCapture failed =
            OwnerRunCompletionFactMapping.Capture(
                new OwnerRunCompletionFact(defeat));
        Require(
            failed.Status == GameplayOutcomeStatus.Failed
            && failed.Details.Summary ==
                "owner-run-ended@1|109|character:b109:owner|2|7|사장 사망",
            "Owner-run defeat mapping changed.");
    }

    private static void VerifyPresentationLifetime()
    {
        GameEventBus events = new();
        OwnerRunObservationObserver observer = new(events);
        List<NoticeFeedEvent> notices = new();
        using IDisposable capture =
            events.Subscribe<NoticeFeedEvent>(notices.Add);
        OwnerRunObservation[] values =
        {
            new(OwnerRunObservationKind.InvalidCandidateIndex),
            new(OwnerRunObservationKind.MissingOwnerData),
            new(
                OwnerRunObservationKind.NotOwnerCandidate,
                displayName: "방문자"),
            new(
                OwnerRunObservationKind.OwnerSelected,
                displayName: "행복 사장"),
            new(
                OwnerRunObservationKind.RunCompleted,
                reason: "진실을 밝혀습니다",
                outcome: DungeonRunOutcome.Victory),
            new(
                OwnerRunObservationKind.RunCompleted,
                reason: "사장 사망",
                outcome: DungeonRunOutcome.Defeat)
        };

        try
        {
            observer.Initialize();
            observer.Initialize();
            for (int index = 0; index < values.Length; index++)
            {
                events.Publish(values[index]);
            }

            Require(notices.Count == values.Length,
                "Owner-run observation subscription duplicated or lost output.");
            RequireNotice(
                notices[0],
                "선택할 사장 후보가 없습니다.",
                NoticeFeedEvent.Grade.WARNING);
            RequireNotice(
                notices[1],
                "사장 데이터가 없습니다.",
                NoticeFeedEvent.Grade.DANGER);
            RequireNotice(
                notices[2],
                "방문자은 사장 후보가 아닙니다.",
                NoticeFeedEvent.Grade.WARNING);
            RequireNotice(
                notices[3],
                "행복 사장으로 시작",
                NoticeFeedEvent.Grade.NONE);
            RequireNotice(
                notices[4],
                "런 승리: 진실을 밝혀습니다",
                NoticeFeedEvent.Grade.NONE);
            RequireNotice(
                notices[5],
                "런 패배: 사장 사망",
                NoticeFeedEvent.Grade.DANGER);

            observer.Dispose();
            observer.Dispose();
            events.Publish(values[0]);
            Require(notices.Count == values.Length,
                "Disposed owner-run observer remained subscribed.");

            observer.Initialize();
            observer.Initialize();
            events.Publish(new OwnerRunObservation(
                OwnerRunObservationKind.OwnerSelected,
                displayName: "검증자"));
            Require(notices.Count == values.Length + 1,
                "Reinitialized owner-run observer did not publish once.");
            RequireNotice(
                notices[notices.Count - 1],
                "검증자 사장으로 시작",
                NoticeFeedEvent.Grade.NONE);
        }
        finally
        {
            observer.Dispose();
        }
    }

    private static void RequireNotice(
        NoticeFeedEvent value,
        string notice,
        NoticeFeedEvent.Grade grade) =>
        Require(
            value.notice == notice && value.grade == grade,
            "Owner-run notice payload changed: " + notice);

    private static void Require(bool condition, string failure)
    {
        if (!condition)
        {
            throw new InvalidOperationException(failure);
        }
    }
}
#endif
