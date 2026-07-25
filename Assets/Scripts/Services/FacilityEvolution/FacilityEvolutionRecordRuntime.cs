using System;
using UnityEngine;
using VContainer;

public class FacilityEvolutionRecordRuntime : MonoBehaviour
{
    [SerializeField, Min(1)] private int highTurnoverVisitStep = 5;
    [SerializeField, Min(1)] private int cleanServiceMinVisits = 3;
    [SerializeField, Min(1)] private int highValueRevenueThreshold = 30;

    private IFacilityEvolutionRecordEventRecorder recordEventRecorder;
    private DungeonStory.Foundation.IGameEventBus gameEventBus;
    private IDisposable defenseFacilityTriggeredSubscription;
    private IDisposable invasionFacilityDamagedSubscription;
    private IDisposable facilityVisitSubscription;
    private IDisposable facilityRevenueSubscription;
    private IDisposable facilityStockConsumedSubscription;
    private IDisposable facilityCrimeSubscription;
    private IDisposable facilityRestockSubscription;
    private IDisposable operatingDayEndedSubscription;

    [Inject]
    public void Construct(
        IFacilityEvolutionRecordEventRecorder recordEventRecorder,
        DungeonStory.Foundation.IGameEventBus gameEventBus)
    {
        this.recordEventRecorder = recordEventRecorder
            ?? throw new ArgumentNullException(nameof(recordEventRecorder));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        SubscribeToScopedEvents();
    }

    public void OnTriggerEvent(FacilityVisitEvent eventType)
    {
        if (TryResolveRecordEventRecorder(out IFacilityEvolutionRecordEventRecorder recorder))
        {
            recorder.RecordVisit(eventType, highTurnoverVisitStep);
        }
    }

    public void OnTriggerEvent(FacilityRevenueEvent eventType)
    {
        if (TryResolveRecordEventRecorder(out IFacilityEvolutionRecordEventRecorder recorder))
        {
            recorder.RecordRevenue(eventType, highValueRevenueThreshold);
        }
    }

    public void OnTriggerEvent(FacilityStockConsumedEvent eventType)
    {
        if (TryResolveRecordEventRecorder(out IFacilityEvolutionRecordEventRecorder recorder))
        {
            recorder.RecordStockConsumed(eventType);
        }
    }

    public void OnTriggerEvent(FacilityCrimeEvent eventType)
    {
        if (TryResolveRecordEventRecorder(out IFacilityEvolutionRecordEventRecorder recorder))
        {
            recorder.RecordCrime(eventType);
        }
    }

    public void OnTriggerEvent(FacilityRestockEvent eventType)
    {
        if (TryResolveRecordEventRecorder(out IFacilityEvolutionRecordEventRecorder recorder))
        {
            recorder.RecordRestock(eventType);
        }
    }

    public void OnTriggerEvent(DefenseFacilityTriggeredEvent eventType)
    {
        if (TryResolveRecordEventRecorder(out IFacilityEvolutionRecordEventRecorder recorder))
        {
            recorder.RecordDefenseTriggered(eventType);
        }
    }

    public void OnTriggerEvent(InvasionFacilityDamagedEvent eventType)
    {
        if (TryResolveRecordEventRecorder(out IFacilityEvolutionRecordEventRecorder recorder))
        {
            recorder.RecordInvasionDamage(eventType);
        }
    }

    public void OnTriggerEvent(OperatingDayEndedEvent eventType)
    {
        if (TryResolveRecordEventRecorder(out IFacilityEvolutionRecordEventRecorder recorder))
        {
            recorder.CompleteOperatingDay(cleanServiceMinVisits);
        }
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        defenseFacilityTriggeredSubscription?.Dispose();
        defenseFacilityTriggeredSubscription = null;
        invasionFacilityDamagedSubscription?.Dispose();
        invasionFacilityDamagedSubscription = null;
        facilityVisitSubscription?.Dispose();
        facilityVisitSubscription = null;
        facilityRevenueSubscription?.Dispose();
        facilityRevenueSubscription = null;
        facilityStockConsumedSubscription?.Dispose();
        facilityStockConsumedSubscription = null;
        facilityCrimeSubscription?.Dispose();
        facilityCrimeSubscription = null;
        facilityRestockSubscription?.Dispose();
        facilityRestockSubscription = null;
        operatingDayEndedSubscription?.Dispose();
        operatingDayEndedSubscription = null;
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        defenseFacilityTriggeredSubscription ??=
            gameEventBus.Subscribe<DefenseFacilityTriggeredEvent>(OnTriggerEvent);
        invasionFacilityDamagedSubscription ??=
            gameEventBus.Subscribe<InvasionFacilityDamagedEvent>(OnTriggerEvent);
        facilityVisitSubscription ??=
            gameEventBus.Subscribe<FacilityVisitEvent>(OnTriggerEvent);
        facilityRevenueSubscription ??=
            gameEventBus.Subscribe<FacilityRevenueEvent>(OnTriggerEvent);
        facilityStockConsumedSubscription ??=
            gameEventBus.Subscribe<FacilityStockConsumedEvent>(OnTriggerEvent);
        facilityCrimeSubscription ??=
            gameEventBus.Subscribe<FacilityCrimeEvent>(OnTriggerEvent);
        facilityRestockSubscription ??=
            gameEventBus.Subscribe<FacilityRestockEvent>(OnTriggerEvent);
        operatingDayEndedSubscription ??=
            gameEventBus.Subscribe<OperatingDayEndedEvent>(OnTriggerEvent);
    }

    private bool TryResolveRecordEventRecorder(out IFacilityEvolutionRecordEventRecorder recorder)
    {
        recorder = recordEventRecorder;
        return recorder != null;
    }
}
