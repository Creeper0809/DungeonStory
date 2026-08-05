using System;
using System.Collections.Generic;
using System.Linq;
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

    [Inject]
    public void ConstructOwnerRunManager(
        IOwnerCandidateCatalog ownerCandidateCatalog,
        IOwnerCharacterFactory ownerCharacterFactory,
        IGameEventBus gameEventBus)
    {
        this.ownerCandidateCatalog = ownerCandidateCatalog
            ?? throw new ArgumentNullException(nameof(ownerCandidateCatalog));
        this.ownerCharacterFactory = ownerCharacterFactory
            ?? throw new ArgumentNullException(nameof(ownerCharacterFactory));
        this.gameEventBus = gameEventBus;
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
            gameEventBus?.ShowNotice("선택할 사장 후보가 없습니다.", NoticeFeedEvent.Grade.WARNING);
            return;
        }

        SelectOwner(ownerCandidates[index]);
    }

    public void SelectOwner(CharacterSO ownerData, string displayNameOverride = null)
    {
        RequireNoPendingRestorePublication();

        if (ownerData == null)
        {
            gameEventBus?.ShowNotice("사장 데이터가 없습니다.", NoticeFeedEvent.Grade.DANGER);
            return;
        }

        if (!ownerData.IsOwnerCandidate)
        {
            gameEventBus?.ShowNotice($"{ownerData.characterName}은 사장 후보가 아닙니다.", NoticeFeedEvent.Grade.WARNING);
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
        string notice = displayName.EndsWith("사장", StringComparison.Ordinal)
            ? $"{displayName}으로 시작"
            : $"{displayName} 사장으로 시작";
        gameEventBus?.ShowNotice(notice, NoticeFeedEvent.Grade.NONE);
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
        if (pendingRestorePublication != null
            || outcome == DungeonRunOutcome.None
            || IsRunEnded
            || currentOwnerActor == null)
        {
            return false;
        }

        IsRunEnded = true;
        string resolvedReason = string.IsNullOrWhiteSpace(reason)
            ? outcome == DungeonRunOutcome.Victory ? "오펜스를 완수해 던전의 진실을 밝혔습니다" : "사장 사망"
            : reason.Trim();
        gameEventBus?.ShowNotice(
            outcome == DungeonRunOutcome.Victory
                ? $"런 승리: {resolvedReason}"
                : $"런 패배: {resolvedReason}",
            outcome == DungeonRunOutcome.Victory
                ? NoticeFeedEvent.Grade.NONE
                : NoticeFeedEvent.Grade.DANGER);
        OnRunEnded?.Invoke(currentOwnerActor, resolvedReason);
        (gameEventBus
            ?? throw new InvalidOperationException($"{nameof(OwnerRunManager)} requires {nameof(IGameEventBus)} injection."))
            .Publish(new OwnerRunEndedEvent(currentOwnerActor, resolvedReason, outcome));
        return true;
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
        if (eventType.Actor != null && eventType.Actor.IsOwner)
        {
            HandleOwnerDeath(eventType.Actor, eventType.Reason);
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
    public string Reason { get; }
    public DungeonRunOutcome Outcome { get; }

    public OwnerRunEndedEvent(
        CharacterActor owner,
        string reason,
        DungeonRunOutcome outcome = DungeonRunOutcome.Defeat)
    {
        OwnerActor = owner;
        Reason = reason;
        Outcome = outcome == DungeonRunOutcome.None ? DungeonRunOutcome.Defeat : outcome;
    }
}
