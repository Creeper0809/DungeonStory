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
        int minimumPotency = 1 + Mathf.FloorToInt(
            Mathf.Max(0f, danger) / 25f);
        return TryDrop(minimumPotency, true, out itemId, out failureReason);
    }

    public bool TryGrantDefenseCatalyst(
        float threat,
        out string itemId,
        out string failureReason)
    {
        int minimumPotency = 1 + Mathf.FloorToInt(
            Mathf.Max(0f, threat) / 125f);
        return TryDrop(minimumPotency, false, out itemId, out failureReason);
    }

    private void OnOffenseRewardGranted(OffenseRewardGrantedEvent eventType)
    {
        if (eventType.expeditionResult == null
            || !eventType.expeditionResult.success)
        {
            return;
        }

        if (TryGrantOffenseCatalyst(
                eventType.expeditionResult.danger,
                out string itemId,
                out _))
        {
            events.RaiseAlert(
                "원정 촉매 회수",
                $"{GetDisplayName(itemId)}이 하차장에 도착했습니다.",
                EventAlertImportance.Medium,
                "진화");
        }
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

        if (TryGrantDefenseCatalyst(
                resolvedThreat,
                out string itemId,
                out _))
        {
            events.RaiseAlert(
                "침공 촉매 회수",
                $"{GetDisplayName(itemId)}이 하차장에 남았습니다.",
                EventAlertImportance.Medium,
                "진화");
        }
    }

    private bool TryDrop(
        int minimumPotency,
        bool expeditionDrop,
        out string itemId,
        out string failureReason)
    {
        itemId = string.Empty;
        failureReason = string.Empty;
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
        int potency = Mathf.Max(1, minimumPotency);
        itemId = EvolutionCatalystItemId.BuildCatalyst(family, potency);
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

        return true;
    }

    private static string GetDisplayName(string itemId)
    {
        return EvolutionCatalystItemDefinitions.TryGetDefinition(
            itemId,
            out DungeonItemDefinition definition)
            ? definition.DisplayName
            : "진화 촉매";
    }
}
