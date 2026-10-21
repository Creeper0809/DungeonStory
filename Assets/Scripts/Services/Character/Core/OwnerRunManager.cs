using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;
using System.Linq;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;

public class OwnerRunManager : SerializedMonoBehaviour
{
    [SerializeField] private CharacterSO[] ownerCandidates = Array.Empty<CharacterSO>();
    [SerializeField] private CharacterSO defaultOwner;
    [SerializeField] private GameObject ownerPrefab;
    [SerializeField] private Transform ownerSpawnPoint;
    [SerializeField] private Vector2Int ownerSpawnGridPosition = Vector2Int.zero;
    [SerializeField] private bool autoSpawnDefaultOwner;

    public Data<CharacterSO> selectedOwnerData = new Data<CharacterSO>();

    private CharacterActor currentOwnerActor;
    private IOwnerCandidateCatalog ownerCandidateCatalog;
    private IOwnerCharacterFactory ownerCharacterFactory;
    private IGameEventBus gameEventBus;
    private IDomainFactPublisher factPublisher;
    private IDomainFactRecoveryPublisher factRecovery;
    private IRunCompletionPreparation completionPreparation;
    private IUiClock uiClock;
    private bool completionMutationActive;
    private float nextCompletionRetry;
    private string lastCompletionFailure;
    private IRunSeedProvider runSeedProvider;
    private DungeonRuntimeAggregateRootStore aggregateRootStore;
    private CoreSessionRulesDefinition coreSessionRules;
    private IDisposable deathSubscription;
    private IReadOnlyList<CharacterSO> ownerCandidatesView;
    private OwnerRestorePublication pendingRestorePublication;

    public CharacterActor CurrentOwnerActor => currentOwnerActor;
    public bool IsRunEnded { get; private set; }
    public bool HasPendingRestorePublication => pendingRestorePublication != null;
    public IReadOnlyList<CharacterSO> OwnerCandidates =>
        ownerCandidatesView ??= ReadOnlyView.List(ownerCandidates);

    public event Action<CharacterSO> OnOwnerSelected;
    public event Action<CharacterActor, string> OnRunEnded;

    private void Awake()
    {
        NormalizeOwnerCandidates();
        selectedOwnerData ??= new Data<CharacterSO>();
    }

    #if UNITY_EDITOR
    // Actor-only fixture setup; completion still requires explicit fact observers.
    public void ConstructOwnerRunManager(
        IUiClock uiClock,
        IOwnerCandidateCatalog ownerCandidateCatalog,
        IOwnerCharacterFactory ownerCharacterFactory,
        IGameEventBus gameEventBus) => ConstructOwnerRunManager(
        uiClock,
        ownerCandidateCatalog,
        ownerCharacterFactory,
        gameEventBus,
        null,
        null,
        null,
        null,
        null,
        null);
    #endif

    [Inject]
    public void ConstructOwnerRunManager(
        IUiClock uiClock,
        IOwnerCandidateCatalog ownerCandidateCatalog,
        IOwnerCharacterFactory ownerCharacterFactory,
        IGameEventBus gameEventBus,
        IDomainFactPublisher factPublisher,
        IRunSeedProvider runSeedProvider,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        ICoreSessionRulesProvider rulesProvider,
        IDomainFactRecoveryPublisher factRecovery,
        IRunCompletionPreparation completionPreparation)
    {
        this.uiClock = uiClock ?? throw new ArgumentNullException(nameof(uiClock));
        this.ownerCandidateCatalog = ownerCandidateCatalog
            ?? throw new ArgumentNullException(nameof(ownerCandidateCatalog));
        this.ownerCharacterFactory = ownerCharacterFactory
            ?? throw new ArgumentNullException(nameof(ownerCharacterFactory));
        this.gameEventBus = gameEventBus;
        this.factPublisher = factPublisher;
        this.factRecovery = factRecovery;
        this.completionPreparation = completionPreparation;
        this.runSeedProvider = runSeedProvider;
        this.aggregateRootStore = aggregateRootStore;
        coreSessionRules = rulesProvider?.CoreSessionRules;
        SubscribeToEvents();
        EnsureOwnerCandidates();
    }

    private void Start()
    {
        if (autoSpawnDefaultOwner && currentOwnerActor == null)
        {
            CharacterSO owner = defaultOwner != null ? defaultOwner : ownerCandidates.FirstOrDefault();
            if (owner != null)
            {
                SelectOwner(owner);
            }
        }
    }

    public void SelectOwnerByIndex(int index)
    {
        EnsureOwnerCandidates();
        if (index < 0 || index >= ownerCandidates.Length)
        {
            gameEventBus?.Publish(new OwnerRunObservation(OwnerRunObservationKind.InvalidCandidateIndex));
            return;
        }

        SelectOwner(ownerCandidates[index]);
    }

    public void SelectOwner(CharacterSO ownerData, string displayNameOverride = null)
    {
        RequireNoPendingRestorePublication();
        if (aggregateRootStore?.GetOrCreate(() => new DungeonRunFlowAggregateState()).PendingCompletion != null)
            throw new InvalidOperationException("Owner replacement is blocked by pending completion observation.");

        if (ownerData == null)
        {
            gameEventBus?.Publish(new OwnerRunObservation(OwnerRunObservationKind.MissingOwnerData));
            return;
        }

        if (!ownerData.IsOwnerCandidate)
        {
            gameEventBus?.Publish(new OwnerRunObservation(OwnerRunObservationKind.NotOwnerCandidate, ownerData.characterName));
            return;
        }

        if (currentOwnerActor != null && !currentOwnerActor.IsDead)
        {
            Destroy(currentOwnerActor.gameObject);
        }

        selectedOwnerData.Value = ownerData;
        currentOwnerActor = SpawnOwner(ownerData);
        OnOwnerSelected?.Invoke(ownerData);
        string displayName = string.IsNullOrWhiteSpace(displayNameOverride)
            ? ownerData.characterName
            : displayNameOverride.Trim();
        gameEventBus?.Publish(new OwnerRunObservation(OwnerRunObservationKind.OwnerSelected, displayName));
    }

    public CharacterActor RestoreOwner(CharacterSO ownerData)
    {
        RequireNoPendingRestorePublication();

        if (ownerData == null)
        {
            throw new ArgumentNullException(nameof(ownerData));
        }

        if (currentOwnerActor != null)
        {
            currentOwnerActor.gameObject.SetActive(false);
            Destroy(currentOwnerActor.gameObject);
        }

        selectedOwnerData ??= new Data<CharacterSO>();
        selectedOwnerData.Value = ownerData;
        IsRunEnded = false;
        currentOwnerActor = SpawnOwner(ownerData);
        OnOwnerSelected?.Invoke(ownerData);
        return currentOwnerActor;
    }

    public CharacterActor CreateRestoreCandidate(CharacterSO ownerData)
    {
        if (ownerData == null)
        {
            throw new ArgumentNullException(nameof(ownerData));
        }

        return ResolveOwnerCharacterFactory().CreateOwnerDetached(
            ownerData,
            ownerPrefab);
    }

    public void PublishRestoreCandidate(
        CharacterSO ownerData,
        CharacterActor candidate)
    {
        OwnerRestorePublication publication =
            BeginRestoreCandidatePublication(ownerData, candidate);
        CompleteRestoreCandidatePublication(publication);
    }

    public OwnerRestorePublication BeginRestoreCandidatePublication(
        CharacterSO ownerData,
        CharacterActor candidate)
    {
        RequireNoPendingRestorePublication();

        if (ownerData == null)
        {
            throw new ArgumentNullException(nameof(ownerData));
        }

        if (candidate == null || !candidate.IsDetachedRestoreCandidate)
        {
            throw new InvalidOperationException(
                "A detached owner candidate is required.");
        }
        if (candidate.gameObject.activeSelf || candidate.gameObject.activeInHierarchy)
        {
            throw new InvalidOperationException(
                "A detached owner candidate must remain inactive until publication.");
        }
        if (candidate == currentOwnerActor)
        {
            throw new InvalidOperationException(
                "The current owner cannot be used as its own restore candidate.");
        }

        candidate.RequireDetachedReadyForPublication();

        Data<CharacterSO> previousSelection = selectedOwnerData;
        OwnerRestorePublication publication = new OwnerRestorePublication(
            this,
            ownerData,
            candidate,
            currentOwnerActor,
            previousSelection,
            previousSelection != null ? previousSelection.Value : null,
            IsRunEnded,
            candidate.transform.parent,
            candidate.transform.GetSiblingIndex());
        pendingRestorePublication = publication;

        try
        {
            selectedOwnerData ??= new Data<CharacterSO>();
            selectedOwnerData.Value = ownerData;
            IsRunEnded = false;
            currentOwnerActor = candidate;
            DungeonRuntimeHierarchy.Parent(
                candidate.gameObject,
                DungeonRuntimeHierarchy.Characters);
            candidate.PublishDetachedRestore();
            publication.MarkCandidatePublished();
            return publication;
        }
        catch
        {
            RollbackRestoreCandidatePublicationCore(publication);
            throw;
        }
    }

    public void RollbackRestoreCandidatePublication(
        OwnerRestorePublication publication)
    {
        RequirePendingRestorePublication(publication);
        RollbackRestoreCandidatePublicationCore(publication);
    }

    public void CompleteRestoreCandidatePublication(
        OwnerRestorePublication publication)
    {
        RequirePendingRestorePublication(publication);

        CharacterActor previousOwner = publication.PreviousOwner;
        if (previousOwner != null)
        {
            previousOwner.gameObject.SetActive(false);
        }
        publication.Candidate.gameObject.SetActive(true);

        pendingRestorePublication = null;
        publication.MarkCompleted();

        if (previousOwner != null)
        {
            DestroyOwnerObject(previousOwner.gameObject);
        }

        OnOwnerSelected?.Invoke(publication.OwnerData);
    }

    public void HandleOwnerDeath(CharacterActor owner, string reason)
    {
        if (pendingRestorePublication != null
            || owner == null
            || owner != currentOwnerActor
            || IsRunEnded)
        {
            return;
        }

        CompleteRun(DungeonRunOutcome.Defeat, reason);
    }

    public bool CompleteRun(DungeonRunOutcome outcome, string reason)
    {
        if (completionMutationActive || pendingRestorePublication != null
            || outcome is not (DungeonRunOutcome.Victory or DungeonRunOutcome.Defeat)
            || IsRunEnded || currentOwnerActor == null) return false;
        if (factPublisher == null || factRecovery == null || gameEventBus == null || completionPreparation == null
            || runSeedProvider == null || runSeedProvider.RunSeed == 0
            || aggregateRootStore == null || coreSessionRules == null
            || !CharacterPersistentIdentity.TryGet(currentOwnerActor, out CharacterId ownerId))
        {
            Debug.LogError("Owner-run completion requires fact observers and run identity.");
            return false;
        }

        var previous = aggregateRootStore.GetOrCreate(() => new DungeonRunFlowAggregateState());
        if (previous.Outcome != DungeonRunOutcome.None || previous.PendingCompletion != null) return false;
        string resolvedReason = string.IsNullOrWhiteSpace(reason)
            ? outcome == DungeonRunOutcome.Victory ? "오펜스를 완수해 던전의 진실을 밝혔습니다" : "사장 사망"
            : reason.Trim();
        var raw = new OwnerRunCompletionFactData
        {
            runSeed = runSeedProvider.RunSeed, ownerId = ownerId.Value,
            displayName = currentOwnerActor.Identity?.DisplayName ?? ownerId.Value,
            outcome = outcome, absoluteDay = previous.CurrentDay, reason = resolvedReason
        };
        var fact = new OwnerRunCompletionFact(raw);
        completionMutationActive = true;
        try
        {
            if (!factPublisher.TryReserve(fact, out var lease, out string failure))
            {
                Debug.LogError("Owner-run completion observation reservation failed: " + failure);
                return false;
            }
            DungeonRunFlowAggregateState terminal;
            IRunCompletionPreparedState preparedState = null;
            try
            {
                preparedState = completionPreparation.Prepare(raw.Clone())
                    ?? throw new InvalidOperationException("Run completion preparation returned no state.");
                terminal = DungeonRunFlowReducer.Reduce(previous,
                    DungeonRunFlowEvent.OwnerRunEnded(outcome), coreSessionRules).State;
                terminal.PendingCompletion = new OwnerRunCompletionPendingData
                { publicationKey = lease.PublicationKey, fact = raw.Clone() };
                terminal.PendingCompletion.Validate(outcome, previous.CurrentDay);
                preparedState.Publish();
                aggregateRootStore.Replace(terminal);
                IsRunEnded = true;
            }
            catch
            {
                try { preparedState?.Cancel(); }
                finally
                {
                    factPublisher.Cancel(lease);
                    aggregateRootStore.Replace(previous);
                    IsRunEnded = false;
                }
                throw;
            }

            DomainFactDelivery delivery;
            try { delivery = factPublisher.Publish(lease, fact); }
            catch (Exception error) when (IsRecoverable(error))
            {
                factPublisher.Cancel(lease);
                delivery = DomainFactDelivery.Uncertain(error.GetType().Name);
            }
            if (delivery.Phase == DomainFactDeliveryPhase.Rejected)
            {
                preparedState.Cancel();
                aggregateRootStore.Replace(previous);
                IsRunEnded = false;
                Debug.LogError("Owner-run completion observation rejected: " + delivery.Detail);
                return false;
            }
            if (Acknowledged(terminal.PendingCompletion, delivery))
                FinishCompletion(terminal.PendingCompletion);
            else ReportPending(delivery.Detail);
            return true;
        }
        finally { completionMutationActive = false; }
    }

    // Replay only the saved fact. Never rerun the terminal reducer or award effects.
    public bool ResumePendingRunCompletion()
    {
        AssertCompletionIdle();
        if (pendingRestorePublication != null || aggregateRootStore == null) return false;
        var state = aggregateRootStore.GetOrCreate(() => new DungeonRunFlowAggregateState());
        var pending = state.PendingCompletion;
        if (pending == null) return true;
        pending.Validate(state.Outcome, state.CurrentDay);
        if (factRecovery == null || runSeedProvider?.RunSeed != pending.fact.runSeed
            || !CharacterPersistentIdentity.TryGet(currentOwnerActor, out CharacterId id)
            || id.Value != pending.fact.ownerId)
        {
            ReportPending("owner-or-recovery-binding-unavailable");
            return false;
        }
        completionMutationActive = true;
        try
        {
            var results = new DomainFactDelivery[1];
            bool accepted;
            string failure;
            try
            {
                accepted = factRecovery.ReplayBatch(new[] {
                    DomainFactReplay.Create(pending.publicationKey, new OwnerRunCompletionFact(pending.fact))
                }, results, out failure);
            }
            catch (Exception error) when (IsRecoverable(error))
            { accepted = false; failure = error.GetType().Name; }
            if (!accepted || !Acknowledged(pending, results[0]))
            { ReportPending(failure + ":" + results[0].Detail); return false; }
            IsRunEnded = true;
            FinishCompletion(pending);
            return true;
        }
        finally { completionMutationActive = false; }
    }

    public void AssertCompletionIdle()
    {
        if (completionMutationActive)
            throw new InvalidOperationException("Owner-run completion publication is active.");
    }

    private static bool Acknowledged(OwnerRunCompletionPendingData pending, DomainFactDelivery result) =>
        result.Phase is DomainFactDeliveryPhase.Delivered or DomainFactDeliveryPhase.AcceptedPendingDelivery
        && result.OperationKey == pending.publicationKey
        && !string.IsNullOrWhiteSpace(result.PublicationId)
        && !string.IsNullOrWhiteSpace(result.Fingerprint);

    private void FinishCompletion(OwnerRunCompletionPendingData pending)
    {
        var state = CloneRunFlow(aggregateRootStore.GetOrCreate(() => new DungeonRunFlowAggregateState()));
        state.PendingCompletion = null;
        aggregateRootStore.Replace(state);
        lastCompletionFailure = null;
        PublishRunEndedObservers(pending.fact.displayName, pending.fact.reason, pending.fact.outcome);
    }

    private void ReportPending(string detail)
    {
        if (lastCompletionFailure != detail)
            Debug.LogWarning("owner-run-completion-observation-pending:" + detail);
        lastCompletionFailure = detail;
    }

    private void Update()
    {
        if (completionMutationActive || uiClock.Time < nextCompletionRetry) return;
        nextCompletionRetry = uiClock.Time + 5f;
        ResumePendingRunCompletion();
    }

    private static bool IsRecoverable(Exception error) =>
        error is not OutOfMemoryException && error is not StackOverflowException
        && error is not AccessViolationException;

    private static DungeonRunFlowAggregateState CloneRunFlow(
        DungeonRunFlowAggregateState source) => new()
    {
        Phase = source.Phase,
        Outcome = source.Outcome,
        CurrentDay = source.CurrentDay,
        BossCycle = source.BossCycle,
        BossArmed = source.BossArmed,
        BossActive = source.BossActive,
        PendingCompletion = source.PendingCompletion?.Clone()
    };

    private void PublishRunEndedObservers(
        string ownerName,
        string resolvedReason,
        DungeonRunOutcome outcome)
    {
        PublishObserver(
            () => gameEventBus.Publish(new OwnerRunObservation(
                OwnerRunObservationKind.RunCompleted, reason: resolvedReason, outcome: outcome)),
            "owner-run-ended-notice");
        PublishObserver(
            () => OnRunEnded?.Invoke(currentOwnerActor, resolvedReason),
            "owner-run-ended-delegate");
        PublishObserver(
            () => gameEventBus.Publish(new OwnerRunEndedEvent(
                currentOwnerActor,
                resolvedReason,
                outcome,
                ownerName)),
            "owner-run-ended-event");
    }

    private static void PublishObserver(Action observer, string label)
    {
        try
        {
            observer?.Invoke();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(label + ":" + exception.GetType().Name);
        }
    }

    public void RestoreRunEnded(bool value)
    {
        RequireNoPendingRestorePublication();
        IsRunEnded = value;
    }

    private void RollbackRestoreCandidatePublicationCore(
        OwnerRestorePublication publication)
    {
        CharacterActor candidate = publication.Candidate;
        if (candidate != null)
        {
            candidate.gameObject.SetActive(false);
            if (publication.CandidateWasPublished)
            {
                candidate.RollbackDetachedRestorePublication();
            }

            candidate.transform.SetParent(
                publication.PreviousCandidateParent,
                true);
            if (publication.PreviousCandidateParent != null)
            {
                candidate.transform.SetSiblingIndex(
                    Mathf.Min(
                        publication.PreviousCandidateSiblingIndex,
                        publication.PreviousCandidateParent.childCount - 1));
            }
        }

        currentOwnerActor = publication.PreviousOwner;
        selectedOwnerData = publication.PreviousSelectionContainer;
        if (selectedOwnerData != null)
        {
            selectedOwnerData.Value = publication.PreviousSelectionValue;
        }
        IsRunEnded = publication.PreviousRunEnded;

        pendingRestorePublication = null;
        publication.MarkRolledBack();
    }

    private static void DestroyOwnerObject(GameObject ownerObject)
    {
        if (ownerObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(ownerObject);
        }
        else
        {
            DestroyImmediate(ownerObject);
        }
    }

    private void RequireNoPendingRestorePublication()
    {
        AssertCompletionIdle();
        if (pendingRestorePublication != null)
        {
            throw new InvalidOperationException(
                "Owner state cannot change while restore publication is pending.");
        }
    }

    private void RequirePendingRestorePublication(
        OwnerRestorePublication publication)
    {
        if (publication == null)
        {
            throw new ArgumentNullException(nameof(publication));
        }
        if (!ReferenceEquals(publication.Manager, this)
            || !ReferenceEquals(publication, pendingRestorePublication)
            || !publication.IsPending)
        {
            throw new InvalidOperationException(
                "The owner restore publication is not pending on this manager.");
        }
    }

    public CharacterSO GetDefaultOwner()
    {
        EnsureOwnerCandidates();
        return defaultOwner != null ? defaultOwner : ownerCandidates.FirstOrDefault();
    }

    private CharacterActor SpawnOwner(CharacterSO ownerData)
    {
        return ResolveOwnerCharacterFactory().CreateOwner(
            ownerData,
            ownerPrefab,
            ownerSpawnPoint,
            ownerSpawnGridPosition);
    }

    private void EnsureOwnerCandidates()
    {
        NormalizeOwnerCandidates();

        if (ownerCandidates.Length == 0)
        {
            IOwnerCandidateCatalog catalog = ownerCandidateCatalog
                ?? throw new InvalidOperationException($"{nameof(OwnerRunManager)} requires {nameof(IOwnerCandidateCatalog)} injection before loading owner candidates.");
            ownerCandidates = catalog.OwnerCandidates
                .Where((candidate) => candidate != null)
                .Distinct()
                .ToArray();
            ownerCandidatesView = null;
        }
    }

    private void NormalizeOwnerCandidates()
    {
        ownerCandidates = ownerCandidates?
            .Where((candidate) => candidate != null)
            .Distinct()
            .ToArray() ?? Array.Empty<CharacterSO>();
        ownerCandidatesView = null;
    }

    private IOwnerCharacterFactory ResolveOwnerCharacterFactory()
    {
        return ownerCharacterFactory
            ?? throw new InvalidOperationException($"{nameof(OwnerRunManager)} requires {nameof(IOwnerCharacterFactory)} injection.");
    }

    private void OnCharacterDeath(CharacterDeathEvent eventType)
    {
        if (currentOwnerActor != null
            && CharacterPersistentIdentity.TryGet(
                currentOwnerActor,
                out CharacterId ownerId)
            && ownerId.Equals(eventType.CharacterId))
        {
            HandleOwnerDeath(currentOwnerActor, eventType.Cause.ToString());
        }
    }

    private void OnEnable()
    {
        SubscribeToEvents();
    }

    private void OnDisable()
    {
        deathSubscription?.Dispose();
        deathSubscription = null;
    }

    private void SubscribeToEvents()
    {
        if (!isActiveAndEnabled || deathSubscription != null || gameEventBus == null)
        {
            return;
        }

        deathSubscription = gameEventBus.Subscribe<CharacterDeathEvent>(OnCharacterDeath);
    }
}

public sealed class OwnerRestorePublication
{
    internal OwnerRunManager Manager { get; }
    internal CharacterSO OwnerData { get; }
    internal CharacterActor Candidate { get; }
    internal CharacterActor PreviousOwner { get; }
    internal Data<CharacterSO> PreviousSelectionContainer { get; }
    internal CharacterSO PreviousSelectionValue { get; }
    internal bool PreviousRunEnded { get; }
    internal Transform PreviousCandidateParent { get; }
    internal int PreviousCandidateSiblingIndex { get; }
    internal bool CandidateWasPublished { get; private set; }

    public bool IsPending { get; private set; } = true;
    public bool IsCompleted { get; private set; }
    public bool IsRolledBack { get; private set; }

    internal OwnerRestorePublication(
        OwnerRunManager manager,
        CharacterSO ownerData,
        CharacterActor candidate,
        CharacterActor previousOwner,
        Data<CharacterSO> previousSelectionContainer,
        CharacterSO previousSelectionValue,
        bool previousRunEnded,
        Transform previousCandidateParent,
        int previousCandidateSiblingIndex)
    {
        Manager = manager ?? throw new ArgumentNullException(nameof(manager));
        OwnerData = ownerData ?? throw new ArgumentNullException(nameof(ownerData));
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        PreviousOwner = previousOwner;
        PreviousSelectionContainer = previousSelectionContainer;
        PreviousSelectionValue = previousSelectionValue;
        PreviousRunEnded = previousRunEnded;
        PreviousCandidateParent = previousCandidateParent;
        PreviousCandidateSiblingIndex = previousCandidateSiblingIndex;
    }

    internal void MarkCandidatePublished()
    {
        CandidateWasPublished = true;
    }

    internal void MarkCompleted()
    {
        IsPending = false;
        IsCompleted = true;
    }

    internal void MarkRolledBack()
    {
        IsPending = false;
        IsRolledBack = true;
    }
}

public readonly struct OwnerRunEndedEvent
{
    public CharacterActor OwnerActor { get; }
    public string OwnerName { get; }
    public string Reason { get; }
    public DungeonRunOutcome Outcome { get; }

    public OwnerRunEndedEvent(
        CharacterActor owner,
        string reason,
        DungeonRunOutcome outcome = DungeonRunOutcome.Defeat,
        string ownerName = null)
    {
        OwnerActor = owner;
        OwnerName = ownerName ?? owner?.Identity?.DisplayName ?? owner?.name ?? "사장";
        Reason = reason;
        Outcome = outcome == DungeonRunOutcome.None ? DungeonRunOutcome.Defeat : outcome;
    }
}
