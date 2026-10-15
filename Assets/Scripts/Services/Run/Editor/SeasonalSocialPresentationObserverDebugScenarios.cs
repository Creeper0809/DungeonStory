#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;

public static class SeasonalSocialPresentationObserverDebugScenarios
{
    public static void RunForBatch()
    {
        Require(Application.isBatchMode, "B127 must use the isolated batch runner.");
        CultureInfo previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            VerifySeasonalProjection();
            VerifyFestivalProjectionAndLifetime();
            VerifyIndependentCompletion();
            FestivalCelebrationCausalProductionEntryDebugScenarios.RunPresentationOwnerForBatch();
            Debug.Log("B127_FESTIVAL_ACTUAL_OWNER_RESTORE=PASS");
            V20ContentResolutionRecoveryDebugScenarios.RunObserverBatch();
            Debug.Log("B127_CONTENT_DURABLE_REGRESSION=PASS");
            Debug.Log("B127_SEASONAL_SOCIAL_OBSERVERS=PASS");
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    private static void VerifySeasonalProjection()
    {
        var events = new GameEventBus();
        var output = new List<EventAlertRequest>();
        using var capture = events.Subscribe<EventAlertRequestedEvent>(value => output.Add(value.request));
        using var observer = new SeasonalSocialPresentationObserver(events);
        observer.Initialize(); observer.Initialize();
        var positions = new Vector2Int?[] { new Vector2Int(2, 3), null };
        var fact = new SeasonalWildlifeProgressedFact("occurrence:wildlife", "야생 방문", "동굴 개",
            SeasonalWildlifeArrivalQualification.Predatory, 3, 1, positions, 10, 8, "경로 대기");
        positions[0] = new Vector2Int(90, 90);
        events.Publish(fact);
        Require(output.Count == 1 && fact.Positions[0] == new Vector2Int(2, 3), "Wildlife positions were not frozen.");
        RequireAlert(output[0], "야생 방문", "V20 계절 야생동물", "occurrence:wildlife", EventAlertImportance.High, false);
        Require(output[0].Detail == "대상: 동굴 개 3마리\n도착 확정: 1마리 / 남은 생성: 2마리\n외부 위치: (2,3), ?\n기한: 10일 (남은 2일)\n현재 지연: 경로 대기",
            "Wildlife text changed.");
        events.Publish(new SeasonalWildlifeProgressedFact("occurrence:herd", "이동", "산양",
            SeasonalWildlifeArrivalQualification.NonHostile, 1, 1, Array.Empty<Vector2Int?>(), 10, 20, ""));
        Require(output.Last().Importance == EventAlertImportance.Medium
            && output.Last().Detail.Contains("외부 위치: 미확정") && output.Last().Detail.Contains("(남은 0일)"),
            "Non-hostile severity or missing-location projection changed.");
        events.Publish(new SeasonalWildlifeExpiredFact("occurrence:wildlife", "야생 방문",
            SeasonalWildlifeArrivalQualification.Predatory, 11));
        RequireAlert(output.Last(), "야생 방문", "V20 계절 야생동물", "occurrence:wildlife", EventAlertImportance.High, true);
        Require(output.Last().Detail == "계절 방문 기한이 11일에 종료되었습니다. 이미 도착한 개체는 유지되고 미생성 잔여만 취소됩니다."
            && output.Last().ResultSummary == "기한 종료: 미생성 잔여 취소, 기존 개체 유지", "Wildlife expiry text changed.");
        events.Publish(new SeasonalCargoProgressedFact("occurrence:cargo", "표류", "목재",
            6, new Vector2Int(4, 5), 4, 1250, 2, 3, 10, 8, "운반 대기"));
        RequireAlert(output.Last(), "표류", "V20 계절 표류 화물", "occurrence:cargo", EventAlertImportance.Medium, false);
        Require(output.Last().Detail == "대상: 목재 6개\n외곽 위치: (4,5)\n미회수: 4개 / 5kg\n실제 픽업 확보: 2개 / 현재 픽업 가능: 3개\n기한: 10일 (남은 2일)\n현재 지연: 운반 대기",
            "Cargo quantity, mass or pickup projection changed.");
        events.Publish(new SeasonalCargoProgressedFact("occurrence:cargo", "표류", "목재",
            6, null, 6, 1250, 0, 0, 10, 8, ""));
        Require(output.Last().Detail.Contains("외곽 위치: 미확정"), "Unfrozen cargo position changed.");
        events.Publish(new SeasonalCargoExpiredFact("occurrence:cargo", "표류", "목재", 2, 4, 5000, 11));
        RequireAlert(output.Last(), "표류", "V20 계절 표류 화물", "occurrence:cargo", EventAlertImportance.Medium, true);
        Require(output.Last().Detail == "회수 기한이 11일에 끝났습니다. 목재 확보 2개, 미픽업 원본 유실 4개 (5kg). 운반 중·회수 드롭·일반 재고는 유지됩니다."
            && output.Last().ResultSummary == "확보 2 / 원본 유실 4", "Cargo expiry text changed.");
        int count = output.Count;
        observer.Dispose(); observer.Dispose();
        events.Publish(fact);
        events.Publish(new SeasonalWildlifeExpiredFact("occurrence:wildlife", "야생 방문",
            SeasonalWildlifeArrivalQualification.Predatory, 11));
        events.Publish(new SeasonalCargoProgressedFact("occurrence:cargo", "표류", "목재", 6, null, 6, 1000, 0, 0, 10, 8, ""));
        events.Publish(new SeasonalCargoExpiredFact("occurrence:cargo", "표류", "목재", 2, 4, 5000, 11));
        Require(output.Count == count, "Disposed seasonal subscriptions still delivered.");
        observer.Initialize();
        events.Publish(fact);
        Require(output.Count == count + 1, "Reinitialized seasonal observer delivery changed.");
        Debug.Log("B127_SEASONAL_FOUR_PROJECTIONS=PASS");
    }

    private static void VerifyFestivalProjectionAndLifetime()
    {
        var events = new GameEventBus();
        var output = new List<EventAlertRequest>();
        using var capture = events.Subscribe<EventAlertRequestedEvent>(value => output.Add(value.request));
        using var observer = new SeasonalSocialPresentationObserver(events);
        observer.Initialize(); observer.Initialize();
        var state = new FestivalExecutionOccurrenceSaveData
        {
            occurrenceId = "festival-occurrence:b127", phase = FestivalExecutionPhase.Running,
            venueFacilityInstanceId = "venue:b127", venueCapacity = 2,
            completedPreparationWork = 2, requiredPreparationWork = 2, deadlineAbsoluteDay = 7, deadlineHour = 9,
            plannedDurationSeconds = 100, elapsedFestivalSeconds = 50, lastStatus = "개최 중",
            attendance = new List<FestivalAttendanceProgressSaveData>
            {
                new() { characterId = "character:a", attendedSeconds = 20 },
                new() { characterId = "character:b", attendedSeconds = 30 }
            }
        };
        var fact = new FestivalExecutionProgressedFact(state, "추모제", 0, FestivalExecutionChange.Advanced);
        state.attendance[0].attendedSeconds = 100;
        events.Publish(fact); events.Publish(fact);
        Require(output.Count == 1 && output[0].Detail.Contains("character:a 20%")
            && fact.Attendance[0].AttendedSeconds == 20, "Festival input freeze/dedup failed.");
        RequireAlert(output[0], "추모제", "V21 축제", state.occurrenceId, EventAlertImportance.High, false);
        state.attendance[0].attendedSeconds = 30; state.attendance[1].attendedSeconds = 20;
        events.Publish(new FestivalExecutionProgressedFact(state, "추모제", 0, FestivalExecutionChange.Advanced));
        Require(output.Count == 2 && output.Last().Detail.Contains("character:a 30%"),
            "Equal total attendance incorrectly suppressed a participant change.");
        events.Publish(new FestivalExecutionProgressedFact(state, "추모제", 1, FestivalExecutionChange.Advanced));
        Require(output.Count == 3, "Restore generation was ignored.");
        observer.Dispose(); observer.Initialize();
        var current = new FestivalExecutionProgressedFact(state, "추모제", 1, FestivalExecutionChange.Advanced);
        events.Publish(current); events.Publish(current);
        Require(output.Count == 4, "Observer recreation retained stale cache or duplicated subscriptions.");

        state.phase = FestivalExecutionPhase.Resolved; state.resultSummary = "개최 완료";
        var terminal = new FestivalExecutionProgressedFact(state, "추모제", 1, FestivalExecutionChange.Advanced);
        int attempts = 0;
        using (events.Subscribe<EventAlertRequestedEvent>(_ => { ++attempts; throw new InvalidOperationException("B127 expected presentation rejection"); }))
            events.Publish(terminal);
        Require(attempts == 1 && output.Count == 5, "Presentation failure path did not execute.");
        events.Publish(terminal); events.Publish(terminal);
        Require(output.Count == 6 && output.Last().IsResolved && output.Last().ResultSummary == "개최 완료"
            && output.Last().Importance == EventAlertImportance.Medium,
            "Failed presentation poisoned retry cache or terminal projection changed.");
        state.lastStatus = "다른 결과";
        var reentrant = new FestivalExecutionProgressedFact(state, "추모제", 1, FestivalExecutionChange.Advanced);
        using (events.Subscribe<EventAlertRequestedEvent>(_ => events.Publish(reentrant)))
            events.Publish(reentrant);
        Require(output.Count == 7, "Reentrant presentation was not fenced.");
        foreach (FestivalExecutionPhase phase in new[] { FestivalExecutionPhase.Cancelled, FestivalExecutionPhase.Stopped, FestivalExecutionPhase.Declined })
        {
            state.phase = phase; state.resultSummary = ""; state.lastStatus = "종료 사유";
            var projected = SeasonalSocialAlertProjection.Build(new FestivalExecutionProgressedFact(
                state, "추모제", 1, FestivalExecutionChange.Advanced));
            Require(projected.IsResolved && projected.ResultSummary == "종료 사유", "Festival terminal status fallback changed.");
        }
        Debug.Log("B127_FESTIVAL_CACHE_LIFETIME=PASS");
    }

    private static void VerifyIndependentCompletion()
    {
        var effects = new List<V20ContentEffect> { new() { kind = V20ContentEffectKind.Health, amount = 5, targetId = "character:a" } };
        var participants = new List<string> { "character:a" };
        var applications = new List<V20EffectApplicationEvidence> { new() { effectIndex = 0, requestedAmount = 5, appliedAmount = 5 } };
        var result = new V20ResolvedEventResult("life:b127", "completed", effects, participants,
            instanceId: "society:b127", displayName: "실제 도움", riskTier: ExperienceEventRiskTier.Recoverable,
            riskReason: "회복 가능", terminalActionLabel: "도움", terminalOutcomeNarrative: "건강 회복",
            terminalEffectKinds: new[] { V20ContentEffectKind.Health }, observedCause: "도움을 받았다",
            occurrenceInstanceId: "occurrence:b127", applications: applications);
        var fact = new V20ContentEffectsResolvedEvent(result, true);
        effects[0].amount = 90; participants[0] = "changed"; result.Applications[0].appliedAmount = 90;
        bool hasFirst = fact.TryGetResolution(out var first);
        Require(fact.Effects[0].amount == 5 && hasFirst
            && first.ParticipantCharacterIds[0] == "character:a" && first.Applications[0].appliedAmount == 5,
            "Content completion input was not deeply frozen.");
        first.Effects[0].amount = 100; first.Applications[0].appliedAmount = 100;
        Require(fact.TryGetResolution(out var second) && second.Effects[0].amount == 5
            && second.Applications[0].appliedAmount == 5, "Content snapshot accessor leaked mutable data.");

        var events = new GameEventBus();
        var output = new List<EventAlertRequest>();
        using var capture = events.Subscribe<EventAlertRequestedEvent>(value => output.Add(value.request));
        using var observer = new SeasonalSocialPresentationObserver(events);
        int completions = 0;
        using var mutate = events.Subscribe<V20ContentEffectsResolvedEvent>(value => value.Effects[0].amount = -1);
        observer.Initialize(); observer.Initialize();
        using var completed = events.Subscribe<V20ContentEffectsResolvedEvent>(value =>
        {
            Require(value.PhysicalEffectsApplied && value.Effects[0].amount == 5
                && value.OccurrenceInstanceId == "occurrence:b127", "Completion consumer got altered facts.");
            ++completions;
        });
        events.Publish(fact);
        Require(completions == 1 && output.Count == 1, "Society completion or presentation duplicated.");
        RequireAlert(output[0], "실제 도움", "V21 사회 사건", "society:b127", EventAlertImportance.Medium, true);
        Require(output[0].Detail == "사회 사건 처리가 확정되었습니다.\n확인된 발생 사실: 도움을 받았다\n예상 결과 위험: 회복 가능한 손실\n위험 근거: 회복 가능"
            && output[0].ResultSummary == "처리: 도움\n해결 상태: completed\n작성 결과 서술: 건강 회복\n적용 효과 유형: 건강",
            "Society terminal metadata projection changed.");
        observer.Dispose();
        events.Publish(fact);
        Require(completions == 2 && output.Count == 1, "Disabling presentation disabled domain completion.");
        observer.Initialize();
        using (events.Subscribe<EventAlertRequestedEvent>(_ => { throw new InvalidOperationException("B127 expected UI failure"); }))
            events.Publish(fact);
        Require(completions == 3 && output.Count == 2, "Presentation exception prevented later gameplay completion listener.");
        var plain = new V20ContentEffectsResolvedEvent("life:b127", "completed",
            new[] { new V20ContentEffect { kind = V20ContentEffectKind.Health, amount = 5 } }, true, "occurrence:b127");
        Require(!plain.TryGetResolution(out _), "Legacy domain notification invented society metadata.");
        events.Publish(plain);
        Require(completions == 4 && output.Count == 2, "Non-society notification emitted a terminal society alert.");
        Debug.Log("B127_COMPLETION_INDEPENDENT_IMMUTABLE=PASS");
    }

    private static void RequireAlert(EventAlertRequest request, string title, string category, string source,
        EventAlertImportance importance, bool resolved) =>
        Require(request.Title == title && request.Category == category && request.SourceId == source
            && request.Importance == importance && request.IsResolved == resolved && request.Choices.Count == 0,
            "Alert identity/category/severity/resolution mismatch.");
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); }
}
#endif
