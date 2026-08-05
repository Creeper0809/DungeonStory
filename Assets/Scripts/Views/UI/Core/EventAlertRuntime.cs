using System;
using DungeonStory.Operation;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using TMPro;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using VContainer;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public class EventAlertRuntime : MonoBehaviour
{
    [SerializeField] private Transform buttonRoot;
    [SerializeField] private GameObject detailPanel;
    [SerializeField] private TMP_Text detailText;

    private readonly EventAlertSelectionState selectionState = new EventAlertSelectionState();
    private IEventAlertViewPresenterFactory viewPresenterFactory;
    private IEventAlertViewPresenter viewPresenter;
    private IGameEventBus gameEventBus;
    private IDisposable requestedSubscription;
    private DungeonRuntimeAggregateRootStore aggregateRootStore;
    private int projectedRestoreRevision;

    public IReadOnlyList<EventAlertRecord> EventLog =>
        Array.AsReadOnly(CurrentState.Records.ToArray());
    public bool IsDetailVisible => viewPresenter != null
        ? viewPresenter.IsDetailVisible
        : detailPanel != null && detailPanel.activeSelf;
    public EventAlertRecord SelectedRecord => selectionState.SelectedRecord;

    [Inject]
    public void Construct(
        IEventAlertViewPresenterFactory viewPresenterFactory,
        IGameEventBus gameEventBus,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.viewPresenterFactory = viewPresenterFactory
            ?? throw new System.ArgumentNullException(nameof(viewPresenterFactory));
        this.gameEventBus = gameEventBus
            ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.aggregateRootStore = aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore));
        projectedRestoreRevision = this.aggregateRootStore.PublishedRestoreRevision;
        SubscribeToScopedEvents();
        RebuildPresentationFromState();
    }

    public void OnTriggerEvent(EventAlertRequestedEvent eventType)
    {
        if (eventType.request == null)
        {
            return;
        }

        EventAlertAggregateState state = WritableState;
        EventAlertRecord record = EventAlertMergePolicy.FindMergeTarget(
            state.Records,
            eventType.request);
        if (record == null)
        {
            record = new EventAlertRecord(state.NextId++, eventType.request);
            state.Records.Add(record);
            CreateButton(record);
        }
        else
        {
            record.Increment();
            if (state.DismissedRecordIds.Remove(record.Id))
            {
                CreateButton(record);
            }
            else
            {
                UpdateButton(record);
            }
        }

        gameEventBus.Publish(new EventAlertLoggedEvent(record));
    }

    public void Open(EventAlertRecord record)
    {
        EventAlertRecord current = FindCurrentRecord(record);
        if (current == null)
        {
            return;
        }

        selectionState.Select(current);
        if (TryResolveViewPresenter(out IEventAlertViewPresenter presenter))
        {
            presenter.OpenDetail(current);
        }
    }

    public void CloseDetail()
    {
        viewPresenter?.CloseDetail();
    }

    public bool Dismiss(EventAlertRecord record)
    {
        EventAlertAggregateState state = WritableState;
        EventAlertRecord current = FindRecordById(state, record?.Id ?? 0);
        if (current == null)
        {
            return false;
        }

        state.DismissedRecordIds.Add(current.Id);
        if (selectionState.SelectedRecord?.Id == current.Id)
        {
            selectionState.Clear();
            CloseDetail();
        }

        viewPresenter?.RemoveButton(current);
        return true;
    }

    public bool IsDismissed(EventAlertRecord record)
    {
        return record != null
            && CurrentState.DismissedRecordIds.Contains(record.Id);
    }

    public bool ExecuteChoice(int index)
    {
        if (!selectionState.ExecuteChoice(index))
        {
            return false;
        }

        CloseDetail();
        return true;
    }

    public EventAlertRestoreCandidate PrepareRestoreHistory(
        IEnumerable<EventAlertRecordSnapshot> records)
    {
        if (records == null)
        {
            throw new ArgumentNullException(nameof(records));
        }

        EventAlertAggregateState restored = new EventAlertAggregateState();
        HashSet<int> seenIds = new HashSet<int>();

        foreach (EventAlertRecordSnapshot snapshot in records)
        {
            if (snapshot == null)
            {
                throw new InvalidOperationException(
                    "Event-alert restore contains a null record.");
            }

            if (snapshot.Id <= 0
                || snapshot.Id == int.MaxValue
                || !seenIds.Add(snapshot.Id))
            {
                throw new InvalidOperationException(
                    $"Event-alert restore contains invalid or duplicate record ID {snapshot.Id}.");
            }

            if (string.IsNullOrWhiteSpace(snapshot.Title)
                || !Enum.IsDefined(typeof(EventAlertImportance), snapshot.Importance))
            {
                throw new InvalidOperationException(
                    $"Event-alert restore record {snapshot.Id} has invalid content.");
            }

            EventAlertRecord record = new EventAlertRecord(
                snapshot.Id,
                snapshot.Title,
                snapshot.Detail,
                snapshot.Importance,
                snapshot.Category,
                snapshot.Count,
                snapshot.Choices.Select(choice => new EventAlertChoice(choice.Label, choice.Description)));
            restored.Records.Add(record);
            restored.NextId = Math.Max(restored.NextId, record.Id + 1);
            if (snapshot.IsDismissed)
            {
                restored.DismissedRecordIds.Add(record.Id);
            }
        }

        return new EventAlertRestoreCandidate(restored);
    }

    public void PublishRestoreHistory(EventAlertRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }
        aggregateRootStore.Replace(candidate.State);
        if (!aggregateRootStore.IsRestoreStaging)
        {
            projectedRestoreRevision =
                aggregateRootStore.PublishedRestoreRevision;
            RebuildPresentationFromState();
        }
    }

    public void RestoreHistory(IEnumerable<EventAlertRecordSnapshot> records) =>
        PublishRestoreHistory(PrepareRestoreHistory(records));

    private void Update()
    {
        int revision = aggregateRootStore?.PublishedRestoreRevision ?? 0;
        if (projectedRestoreRevision == revision)
        {
            return;
        }

        projectedRestoreRevision = revision;
        RebuildPresentationFromState();
    }

    private void OnEnable()
    {
        SubscribeToScopedEvents();
    }

    private void OnDisable()
    {
        requestedSubscription?.Dispose();
        requestedSubscription = null;
    }

    private void OnDestroy()
    {
        viewPresenter?.DestroyRuntimeUI();
    }

    private void SubscribeToScopedEvents()
    {
        if (!isActiveAndEnabled || gameEventBus == null)
        {
            return;
        }

        requestedSubscription ??=
            gameEventBus.Subscribe<EventAlertRequestedEvent>(OnTriggerEvent);
    }

    private void CreateButton(EventAlertRecord record)
    {
        if (TryResolveViewPresenter(out IEventAlertViewPresenter presenter))
        {
            presenter.CreateButton(record);
        }
    }

    private void UpdateButton(EventAlertRecord record)
    {
        if (TryResolveViewPresenter(out IEventAlertViewPresenter presenter))
        {
            presenter.UpdateButton(record);
        }
    }

    private EventAlertAggregateState CurrentState =>
        RequireAggregateRoot().GetOrCreate(
            () => new EventAlertAggregateState());

    private EventAlertAggregateState WritableState =>
        RequireAggregateRoot().GetOrCreateWritable(
            () => new EventAlertAggregateState(),
            state => state.DeepClone());

    private DungeonRuntimeAggregateRootStore RequireAggregateRoot()
    {
        return aggregateRootStore
            ?? throw new InvalidOperationException(
                $"{nameof(EventAlertRuntime)} has not been constructed with its Aggregate root.");
    }

    private EventAlertRecord FindCurrentRecord(EventAlertRecord record)
    {
        return FindRecordById(CurrentState, record?.Id ?? 0);
    }

    private static EventAlertRecord FindRecordById(
        EventAlertAggregateState state,
        int recordId)
    {
        return recordId <= 0
            ? null
            : state.Records.FirstOrDefault(record => record.Id == recordId);
    }

    private void RebuildPresentationFromState()
    {
        viewPresenter?.DestroyRuntimeUI();
        viewPresenter = null;
        selectionState.Clear();
        if (!TryResolveViewPresenter(out IEventAlertViewPresenter presenter))
        {
            return;
        }

        presenter.EnsureRuntimeUI();
        EventAlertAggregateState state = CurrentState;
        foreach (EventAlertRecord record in state.Records)
        {
            if (!state.DismissedRecordIds.Contains(record.Id))
            {
                presenter.CreateButton(record);
            }
        }
    }

    private bool TryResolveViewPresenter(out IEventAlertViewPresenter presenter)
    {
        if (viewPresenter != null)
        {
            presenter = viewPresenter;
            return true;
        }

        if (viewPresenterFactory == null)
        {
            presenter = null;
            return false;
        }

        viewPresenter = viewPresenterFactory.Create(new EventAlertViewPresenterContext(
            buttonRoot,
            detailPanel,
            detailText,
            Open,
            Dismiss,
            ExecuteChoice,
            CloseDetail));
        presenter = viewPresenter;
        return presenter != null;
    }
}
