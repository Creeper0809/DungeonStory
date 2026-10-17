using System;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using VContainer.Unity;

public sealed class CaptivityInterrogationPresentationObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;
    public CaptivityInterrogationPresentationObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    public void Initialize() => subscription ??=
        events.Subscribe<CaptivityInterrogationInformationResolvedFact>(Observe);
    public void Dispose() { subscription?.Dispose(); subscription = null; }

    // This fact's existing required delivery boundary deliberately propagates failure.
    // Its owner retains the frozen attempt and retries only delivery, never interrogation effects.
    private void Observe(CaptivityInterrogationInformationResolvedFact fact) =>
        events.Publish(new EventAlertRequestedEvent(Build(fact)));

    public static EventAlertRequest Build(
        CaptivityInterrogationInformationResolvedFact fact)
    {
        if (fact == null) throw new ArgumentNullException(nameof(fact));
        string title = fact.HasInformation ? "심문 성공" : "심문 종료 — 추가 정보 없음";
        string detail;
        if (fact.HasInformation)
        {
            detail =
                $"대상 포로: {fact.SubjectDisplayName}\n"
                + $"정보 대상: {fact.EnemyDisplayName} "
                + $"({fact.OriginEnemyArchetypeId})\n"
                + $"소속 세력: {fact.OriginFactionId}\n"
                + "주제: 출신 부대의 전열·전술\n"
                + $"획득 내용: 전열·전술 표식 "
                + $"'{fact.FormationTag}'\n"
                + "검증 상태: 미확인 진술";
            if (fact.HighFearCaution)
            {
                detail +=
                    "\n주의: 공포도가 높은 상태의 진술이며, "
                    + "진실 또는 거짓으로 판정되지 않았습니다.";
            }
        }
        else
        {
            detail =
                $"대상 포로: {fact.SubjectDisplayName}\n"
                + "추가로 기록할 수 있는 실제 출신 부대의 "
                + "전열·전술 진술이 없습니다.";
        }

        return new EventAlertRequest(
            title,
            detail,
            EventAlertImportance.Medium,
            "포로",
            sourceId: CaptivityInterrogationAttemptIdentity
                .FormatNoticeSourceId(fact.CaptiveId, fact.AttemptId));
    }
}
