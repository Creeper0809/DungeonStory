using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public interface IEvolutionCatalystDropRuntime
{
    bool TryGrantOffenseCatalyst(
        float danger,
        out string itemId,
        out string failureReason);
    bool TryGrantDefenseCatalyst(
        float threat,
        out string itemId,
        out string failureReason);
}

public sealed class EvolutionCatalystDropRuntime :
    IEvolutionCatalystDropRuntime,
    IStartable,
    IDisposable
{
    private static readonly string[] CatalystFamilies =
    {
        "offense",
        "defense",
        "industry",
        "survival",
        "arcane",
        "authority"
    };

    private readonly IGameEventBus events;
    private readonly IWorldItemStackRuntime items;
    private readonly IWorldDropZoneQuery dropZones;
    private readonly IRandomStream random;
    private IDisposable offenseRewardSubscription;
    private IDisposable invasionStartedSubscription;
    private IDisposable invasionResolvedSubscription;
    private float activeInvasionThreat;

    public EvolutionCatalystDropRuntime(
        IGameEventBus events,
        IWorldItemStackRuntime items,
        IWorldDropZoneQuery dropZones,
        IRandomStreamProvider randomStreams)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.dropZones = dropZones
            ?? throw new ArgumentNullException(nameof(dropZones));
        random = (randomStreams
            ?? throw new ArgumentNullException(nameof(randomStreams)))
            .Get("evolution.catalyst-drops");
    }

    public void Start()
    {
        offenseRewardSubscription ??=
            events.Subscribe<OffenseRewardGrantedEvent>(OnOffenseRewardGranted);
        invasionStartedSubscription ??=
            events.Subscribe<InvasionStartedEvent>(OnInvasionStarted);
        invasionResolvedSubscription ??=
            events.Subscribe<InvasionResolvedEvent>(OnInvasionResolved);
    }

    public void Dispose()
    {
        offenseRewardSubscription?.Dispose();
        offenseRewardSubscription = null;
        invasionStartedSubscription?.Dispose();
        invasionStartedSubscription = null;
        invasionResolvedSubscription?.Dispose();
        invasionResolvedSubscription = null;
    }

    public bool TryGrantOffenseCatalyst(
        float danger,
        out string itemId,
        out string failureReason)
    {
        int minimumProgressionLevel = 1 + Mathf.FloorToInt(
            Mathf.Max(0f, danger) / 25f);
        return TryDrop(
            minimumProgressionLevel,
            true,
            out itemId,
            out failureReason);
    }

    public bool TryGrantDefenseCatalyst(
        float threat,
        out string itemId,
        out string failureReason)
    {
        int minimumProgressionLevel = 1 + Mathf.FloorToInt(
            Mathf.Max(0f, threat) / 125f);
        return TryDrop(
            minimumProgressionLevel,
            false,
            out itemId,
            out failureReason);
    }

    private void OnOffenseRewardGranted(OffenseRewardGrantedEvent eventType)
    {
        if (eventType.expeditionResult == null
            || !eventType.expeditionResult.success)
        {
            return;
        }

        TryGrantOffenseCatalyst(eventType.expeditionResult.danger, out _, out _);
    }

    private void OnInvasionStarted(InvasionStartedEvent eventType)
    {
        activeInvasionThreat = Mathf.Max(0f, eventType.snapshot.threat);
    }

    private void OnInvasionResolved(InvasionResolvedEvent eventType)
    {
        float resolvedThreat = activeInvasionThreat;
        activeInvasionThreat = 0f;
        if (!eventType.defended)
        {
            return;
        }

        TryGrantDefenseCatalyst(resolvedThreat, out _, out _);
    }

    private bool TryDrop(
        int minimumProgressionLevel,
        bool expeditionDrop,
        out string itemId,
        out string failureReason)
    {
        itemId = string.Empty;
        failureReason = string.Empty;
        if (!EvolutionCatalystProgression.IsValid(minimumProgressionLevel))
        {
            failureReason = "획득 가능한 촉매 진행 단계 범위를 벗어났습니다.";
            return false;
        }

        bool hasDropoff = expeditionDrop
            ? dropZones.TryGetExpeditionLootDropoff(out Vector2Int dropoff)
            : dropZones.TryGetDeliveryDropoff(out dropoff);
        if (!hasDropoff)
        {
            failureReason = "촉매를 내려놓을 하차장을 찾을 수 없습니다.";
            return false;
        }

        string family = CatalystFamilies[
            random.NextInt(0, CatalystFamilies.Length)];
        itemId = EvolutionCatalystItemId.BuildCatalyst(
            family,
            minimumProgressionLevel);
        if (!items.SpawnItemAt(
                itemId,
                1,
                dropoff,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            || spawned != 1)
        {
            itemId = string.Empty;
            failureReason = "촉매 스택을 생성할 수 없습니다.";
            return false;
        }

        // The physical owner has committed. A display subscriber cannot turn
        // an actual drop into a failed grant or cause its caller to spawn again.
        try
        {
            events.Publish(new EvolutionCatalystDroppedEvent(itemId, spawned, dropoff,
                expeditionDrop ? EvolutionCatalystDropOrigin.Expedition : EvolutionCatalystDropOrigin.Defense));
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            Debug.LogWarning("catalyst-drop-observation:" + exception.GetType().Name + ":" + exception.Message);
        }
        return true;
    }
}
