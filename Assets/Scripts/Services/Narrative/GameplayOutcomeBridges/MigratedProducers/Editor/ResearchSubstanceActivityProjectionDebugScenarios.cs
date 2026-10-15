#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ResearchSubstanceActivityProjectionDebugScenarios
{
    private const string TargetId = "facility:research-activity-projection";
    private const string TargetName = "연구 작업대";
    private const int TargetDefinitionId = 4101;

    public static void Run()
    {
        VerifyResearchProjections();
        VerifySubstanceProjectionAndLifetime();
    }

    private static void VerifyResearchProjections()
    {
        RequireResearch(
            WorkExecutionActivityObserver.Project(Research(
                WorkExecutionTransition.ResearchRejected,
                "시약 부족",
                progress: 0.25f)),
            CharacterActivityOutcomes.Failed,
            "연구 실패: 시약 부족",
            "시약 부족",
            0f,
            -0.7f,
            bubbleEligible: true);
        RequireResearch(
            WorkExecutionActivityObserver.Project(Research(
                WorkExecutionTransition.ResearchProgressed,
                detail: "진척 적용",
                progress: 0.375f)),
            CharacterActivityOutcomes.Progress,
            "연구 진행: 연금술 연구",
            "research-progress",
            0.375f,
            0f,
            bubbleEligible: false);
        RequireResearch(
            WorkExecutionActivityObserver.Project(Research(
                WorkExecutionTransition.ResearchCompleted,
                detail: "완료",
                progress: 1f)),
            CharacterActivityOutcomes.Completed,
            "연구 완료: 연금술 연구",
            "blueprint-completed",
            1f,
            0.45f,
            bubbleEligible: false);
    }

    private static void VerifySubstanceProjectionAndLifetime()
    {
        var events = new GameEventBus();
        var host = new GameObject("Research Substance Activity Projection Fixture");
        try
        {
            CharacterActor actor = host.AddComponent<CharacterActor>();
            CharacterAiEditorTestDependencies.InjectWithDomainEvents(host, events);
            actor.EnsureRuntimeState();
            Require(actor.LogComponent != null,
                "Substance activity fixture did not initialize the character log authority.");

            var fact = new SubstanceUseCompletedEvent(
                actor,
                new ConsumableOperationId(
                    "consumable-operation:research-substance-activity"),
                new ItemDefinitionId("item:qa:calming-tonic"),
                new ItemStackId("stack:qa:calming-tonic"),
                "진정제");
            CharacterActivityEvent projected =
                CharacterActionActivityObserver.Project(fact);
            Require(
                projected.KindId == CharacterActivityKinds.FreeText
                && projected.OutcomeId == CharacterActivityOutcomes.Observed
                && projected.FactText == "진정제을 복용했다."
                && !projected.NarrativeEligible
                && !projected.BubbleEligible
                && projected.ActionId.Length == 0
                && projected.ReasonCode.Length == 0
                && Approximately(projected.Value, 0f)
                && Approximately(projected.Sentiment, 0f),
                "Substance activity projection changed its display-only FreeText contract.");

            var observed = new List<CharacterActivityEvent>();
            void Capture(CharacterLogEntry entry) => observed.Add(entry.Activity);
            actor.LogComponent.OnLogAdded += Capture;
            try
            {
                using var observer = new CharacterActionActivityObserver(events);
                observer.Initialize();
                observer.Initialize();
                events.Publish(fact);
                RequireSubstanceObservation(observed, 1,
                    "Substance observer subscribed more than once or lost its first event.");

                observer.Dispose();
                events.Publish(fact);
                Require(observed.Count == 1,
                    "Disposed substance observer still wrote a character activity.");

                observer.Initialize();
                events.Publish(fact);
                RequireSubstanceObservation(observed, 2,
                    "Reinitialized substance observer did not write exactly one activity.");
            }
            finally
            {
                actor.LogComponent.OnLogAdded -= Capture;
            }
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    private static WorkExecutionChangedEvent Research(
        WorkExecutionTransition transition,
        string detail,
        float progress) => new(
        transition,
        new CharacterId("character:research-activity-projection"),
        BuiltInWorkTypeIds.Research,
        targetId: TargetId,
        targetName: TargetName,
        targetDefinitionId: TargetDefinitionId,
        workLabel: "연금술 연구",
        detail: detail,
        progress: progress);

    private static void RequireResearch(
        CharacterActivityEvent value,
        string outcome,
        string text,
        string reason,
        float progress,
        float sentiment,
        bool bubbleEligible)
    {
        Require(
            value != null
            && value.KindId == CharacterActivityKinds.Work
            && value.ActionId == BuiltInWorkTypeIds.Research.Value
            && value.OutcomeId == outcome
            && value.FactText == text
            && value.ReasonCode == reason
            && value.TargetId == TargetId
            && value.TargetName == TargetName
            && value.FacilityId == TargetDefinitionId
            && Approximately(value.Value, progress)
            && Approximately(value.Sentiment, sentiment)
            && value.NarrativeEligible
            && value.BubbleEligible == bubbleEligible,
            "Research work activity projection changed its text, reason, value, target, or presentation policy: "
            + TransitionReport(value));
    }

    private static void RequireSubstanceObservation(
        IReadOnlyList<CharacterActivityEvent> observed,
        int expectedCount,
        string failure)
    {
        CharacterActivityEvent latest = observed.Count > 0
            ? observed[observed.Count - 1]
            : null;
        Require(
            observed.Count == expectedCount
            && latest != null
            && latest.KindId == CharacterActivityKinds.FreeText
            && latest.FactText == "진정제을 복용했다."
            && !latest.NarrativeEligible,
            failure);
    }

    private static string TransitionReport(CharacterActivityEvent value) =>
        value == null
            ? "null"
            : $"kind={value.KindId};outcome={value.OutcomeId};text={value.FactText};"
              + $"reason={value.ReasonCode};value={value.Value};sentiment={value.Sentiment};"
              + $"target={value.TargetId}/{value.TargetName}/{value.FacilityId};"
              + $"narrative={value.NarrativeEligible};bubble={value.BubbleEligible}";

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.0001f;

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
