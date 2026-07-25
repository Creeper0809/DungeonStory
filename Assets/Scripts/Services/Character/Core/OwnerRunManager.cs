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

    public CharacterActor CurrentOwnerActor => currentOwnerActor;
    public bool IsRunEnded { get; private set; }
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
        IGameEventBus gameEventBus = null)
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

    public void HandleOwnerDeath(CharacterActor owner, string reason)
    {
        if (owner == null || owner != currentOwnerActor || IsRunEnded)
        {
            return;
        }

        CompleteRun(DungeonRunOutcome.Defeat, reason);
    }

    public bool CompleteRun(DungeonRunOutcome outcome, string reason)
    {
        if (outcome == DungeonRunOutcome.None || IsRunEnded || currentOwnerActor == null)
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
        IsRunEnded = value;
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
