using System;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// Optional post-effect presentation. It never owns or retries gameplay state.
/// </summary>
public sealed class MilestonePressurePresentationObserver :
    IInitializable,
    IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public MilestonePressurePresentationObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize() => subscription ??=
        events.Subscribe<MilestonePressureAppliedFact>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    public static EventAlertRequest Build(MilestonePressureAppliedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));

        string title;
        string detail;
        switch (fact.Kind)
        {
            case MilestonePressureEffectKind.DefenseObligation:
                RequireIdentity(fact, "ending:monster-accord", "monster-accord");
                Require(fact.ObligationDelta == 1,
                    "Milestone defense obligation delta must be one.");
                title = "공동방위 의무 발생";
                detail = "대협약 회원 세력 모두에 공동방위 의무가 1 증가했습니다.";
                break;
            case MilestonePressureEffectKind.FarmingWorkDelay:
                RequireDelay(fact, "ending:sealed-paradise",
                    "sealed-paradise", "farm", 3);
                title = "폐쇄 생태 불균형";
                detail = "폐쇄 생태계의 균형 조정으로 농업 작업이 3일간 지연됩니다.";
                break;
            case MilestonePressureEffectKind.InheritanceWorkDelay:
                RequireDelay(fact, "ending:eternal-lineage",
                    "eternal-lineage", "global", 1);
                title = "계승권 분쟁";
                detail = "계승권 조정으로 전체 작업이 하루 동안 지연됩니다.";
                break;
            case MilestonePressureEffectKind.RepairWorkDelay:
                RequireDelay(fact, "ending:steel-apotheosis",
                    "steel-apotheosis", "repair", 2);
                title = "자동화 연쇄 고장";
                detail = "연쇄 고장과 내부 점검으로 수리 작업이 2일간 지연됩니다.";
                break;
            case MilestonePressureEffectKind.TemporalMaintenanceAcceleration:
                RequireIdentity(fact, "ending:timeless-sanctuary",
                    "timeless-sanctuary");
                Require(fact.AffectedCount > 0 && fact.MaintenanceLeadDays == 3,
                    "Milestone temporal maintenance fact is invalid.");
                title = "시간 이상 현상";
                detail = $"시간 고정 대상 {fact.AffectedCount}명의 다음 촉매 교체일이 "
                    + $"{fact.MaintenanceLeadDays}일 뒤로 앞당겨졌습니다.";
                break;
            case MilestonePressureEffectKind.InvasionScheduled:
                title = InvasionTitle(fact);
                Require(fact.InvasionOperationId.Length > 0
                        && fact.InvasionThreat > 0f,
                    "Milestone invasion fact is incomplete.");
                detail = $"위협도 {fact.InvasionThreat:0}의 침입 작전 "
                    + $"{fact.InvasionOperationId}이 예약되었습니다.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(fact.Kind));
        }

        return new EventAlertRequest(
            title,
            detail,
            EventAlertImportance.High,
            "V21 이정표 압력",
            sourceId:
                $"milestone-pressure:{fact.SourceId}:{fact.AbsoluteDay}");
    }

    private void Observe(MilestonePressureAppliedFact fact)
    {
        try
        {
            events.Publish(new EventAlertRequestedEvent(Build(fact)));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(
                "milestone-pressure-presentation-observer:"
                + exception.GetType().Name + ":" + exception.Message);
        }
    }

    private static string InvasionTitle(MilestonePressureAppliedFact fact)
    {
        string pressureId = "ending:" + fact.SourceId;
        Require(string.Equals(fact.PressureId, pressureId,
                StringComparison.Ordinal),
            "Milestone invasion pressure identity differs from its source.");
        return fact.SourceId switch
        {
            "truth-revealed" => "진실 수호자 보복",
            "surface-hegemony" => "인간 연합 반격",
            "dungeon-sovereignty" => "국가 규모 공성전",
            "arcane-ascension" => "비전 습격",
            _ => throw new InvalidOperationException(
                "Unknown milestone invasion source: " + fact.SourceId)
        };
    }

    private static void RequireDelay(
        MilestonePressureAppliedFact fact,
        string pressureId,
        string sourceId,
        string scopeId,
        int days)
    {
        RequireIdentity(fact, pressureId, sourceId);
        Require(string.Equals(fact.WorkScopeId, scopeId,
                    StringComparison.Ordinal)
                && fact.WorkDelayDays == days,
            "Milestone work-delay fact differs from its effect kind.");
    }

    private static void RequireIdentity(
        MilestonePressureAppliedFact fact,
        string pressureId,
        string sourceId) =>
        Require(string.Equals(fact.PressureId, pressureId,
                    StringComparison.Ordinal)
                && string.Equals(fact.SourceId, sourceId,
                    StringComparison.Ordinal),
            "Milestone pressure identity differs from its effect kind.");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
