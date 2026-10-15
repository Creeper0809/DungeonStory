using System;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects a committed mercenary contract termination into the alert feed.
/// Employment and finance state remain owned by the settlement transaction.
/// </summary>
public sealed class EmploymentContractAlertObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public EmploymentContractAlertObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize() => subscription ??=
        events.Subscribe<MercenaryContractEndedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void Observe(MercenaryContractEndedEvent value)
    {
        events.RaiseAlert(
            "용병 계약 종료",
            $"{value.CharacterDisplayName}에게 일급을 지급하지 못해 용병 계약이 종료되었습니다.",
            EventAlertImportance.Medium,
            "경영");
    }
}
