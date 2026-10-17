using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEngine;
using VContainer.Unity;

// Optional presentation for maintenance facts already owned by the life runtime.
public sealed class TemporalStasisMaintenanceWarningObserver :
    IInitializable,
    IDisposable
{
    private readonly ICharacterLifeQuery life;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly IGameEventBus events;
    private IDisposable dayStartedSubscription;

    public TemporalStasisMaintenanceWarningObserver(
        ICharacterLifeQuery life,
        IMilestoneGameplayModifierQuery milestoneModifiers,
        IGameEventBus events)
    {
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.milestoneModifiers = milestoneModifiers
            ?? throw new ArgumentNullException(nameof(milestoneModifiers));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public void Initialize() => dayStartedSubscription ??=
        events.Subscribe<OperatingDayStartedEvent>(OnDayStarted);

    public void Dispose()
    {
        dayStartedSubscription?.Dispose();
        dayStartedSubscription = null;
    }

    private void OnDayStarted(OperatingDayStartedEvent started)
    {
        try { PublishWarnings(started.day); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError("temporal-stasis-maintenance-presentation-observer:"
                + exception.GetType().Name);
        }
    }

    private void PublishWarnings(int absoluteDay)
    {
        int warningDays = Math.Max(
            0,
            milestoneModifiers.TemporalStasisWarningDays);
        if (warningDays == 0) return;

        foreach (CharacterLifeRecord record in life.Records
                     .Where(value => value != null
                         && value.RequestedAgingCareMode
                             == AgingCareMode.TemporalStasis)
                     .OrderBy(value => value.CharacterId.Value,
                         StringComparer.Ordinal))
        {
            int remaining = record.TemporalStasisNextMaintenanceAbsoluteDay
                - absoluteDay;
            if (remaining <= 0 || remaining > warningDays) continue;

            events.Publish(new EventAlertRequestedEvent(new EventAlertRequest(
                "시간 고정 유지보수 예고",
                $"{record.CharacterId.Value}의 시간 고정 촉매 교체까지 {remaining}일 남았습니다. "
                    + $"{PhysicalAgeTreatmentRuntime.RuneConductorItemId}와 "
                    + $"{PhysicalAgeTreatmentRuntime.ManaCrystalItemId}을 시설 버퍼에 준비해야 합니다.",
                EventAlertImportance.High,
                "V21 시간 고정",
                sourceId: $"temporal-stasis-maintenance-warning:{record.CharacterId.Value}")));
        }
    }
}
