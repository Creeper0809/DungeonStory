using System;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Presents frozen expedition activity facts without changing expedition state.
/// Listener failures retain the event bus's existing propagation behavior.
/// </summary>
public sealed class OffenseExpeditionActivityObserver :
    IInitializable,
    IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public OffenseExpeditionActivityObserver(IGameEventBus events)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Initialize()
    {
        if (subscription != null)
            return;
        subscription = events.Subscribe<OffenseExpeditionActivityFact>(Observe);
    }

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void Observe(OffenseExpeditionActivityFact fact)
    {
        OffenseExpeditionActivityFact value = fact
            ?? throw new ArgumentNullException(nameof(fact));
        switch (value.Activity)
        {
            case OffenseExpeditionActivity.RegionBossSealAwarded:
                events.RaiseAlert(
                    "기억 소거 인장 획득",
                    "이 권역의 첫 보스를 격파해 기억 소거 인장 1개가 전리품 투하 지점에 생성되었습니다.",
                    EventAlertImportance.High,
                    "오펜스");
                break;
            case OffenseExpeditionActivity.PartyStranded:
                events.RaiseAlert(
                    "원정대 조난",
                    value.Message,
                    EventAlertImportance.High,
                    "오펜스");
                break;
            case OffenseExpeditionActivity.ObjectiveDestroyed:
                events.RaiseAlert(
                    "목표 파괴",
                    value.TargetTitle + " 격파. 안전 이동 " + value.Count
                    + "칸이 지급되었습니다. 다음 목적지를 선택하세요.",
                    EventAlertImportance.High,
                    "오펜스");
                break;
            case OffenseExpeditionActivity.RouteBattleCompleted:
                events.RaiseAlert(
                    "원정 교전 승리",
                    value.NodeTitle
                    + "을 돌파했습니다. 다음 경로를 선택하세요.",
                    EventAlertImportance.Low,
                    "오펜스");
                break;
            case OffenseExpeditionActivity.DepartureRestorationFailed:
                events.RaiseAlert(
                    "출정 복원 실패",
                    "출정 집결 서비스를 찾을 수 없어 원정대가 대기합니다.",
                    EventAlertImportance.High,
                    "expedition");
                break;
            case OffenseExpeditionActivity.PackingResumptionFailed:
                events.RaiseAlert(
                    "출정 보급 오류",
                    value.Message,
                    EventAlertImportance.High,
                    "expedition");
                break;
            case OffenseExpeditionActivity.DepartureResumptionFailed:
                events.RaiseAlert(
                    "출정 복원 실패",
                    value.Message,
                    EventAlertImportance.High,
                    "expedition");
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(fact),
                    value.Activity,
                    "Unsupported offense expedition activity.");
        }
    }
}
